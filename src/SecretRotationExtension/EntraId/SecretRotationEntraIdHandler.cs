using Bicep.Local.Extension.Host.Handlers;
using Microsoft.Extensions.Logging;

namespace SecretRotationExtension.EntraId;

/// <summary>
///     Secret Rotation for Entra Id
/// </summary>
public class SecretRotationEntraIdHandler(ILogger<SecretRotationEntraIdHandler> logger, ISecretClient client)
    : TypedResourceHandler<SecretRotationSourceEntraId, SecretRotationSourceIdentifier>
{
    /// <summary>
    ///     Verifies if there are any expiring secrets. If yes lists those in apps.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override async Task<ResourceResponse> Preview(
        ResourceRequest request,
        CancellationToken cancellationToken)
    {
        var existing =
            await GetAppRegistrationWithSecretsExpiringWithin(
                request.Properties.RotateSecretsExpiringWithinDays);

        if (existing.Any())
            request.Properties.AppsWithExpiringSecrets = Map(existing);

        return GetResponse(request);
    }

    /// <summary>
    ///     CreateOrUpdate operation - creates a new secret or rotates an existing one if it's expiring within defined
    ///     threshold.
    ///     If DeleteAfterRenew is set to true it will delete expiring secret after renew.
    /// </summary>
    protected override async Task<ResourceResponse> CreateOrUpdate(
        ResourceRequest request,
        CancellationToken cancellationToken)
    {
        var props = request.Properties;

        var appRegistrationWithRotatedSecrets = await RotateSecretsExpiringWithin(
            props.RotateSecretsExpiringWithinDays,
            props.SecretsToRotate,
            props.ExpiresInDays,
            props.DeleteAfterRenew);

        request.Properties.AppsWithExpiringSecrets = Map(appRegistrationWithRotatedSecrets);

        return GetResponse(request);
    }

    /// <summary>
    ///     GetIdentifiers - extracts the identifier properties from the resource.
    ///     These identifiers are used to locate and identify the resource.
    /// </summary>
    protected override SecretRotationSourceIdentifier GetIdentifiers(SecretRotationSourceEntraId properties)
    {
        return new SecretRotationSourceIdentifier { Id = properties.Id };
    }

    private async Task<List<AppRegistration>> GetAppRegistrationWithSecretsExpiringWithin(int days)
    {
        var appRegistrations = await client.GetAppRegistrationWithExpiringDates();

        return appRegistrations
            .Where(appRegistration => appRegistration.ExpiringSecrets.Any())
            .Select(appRegistration =>
            appRegistration with
            {
                ExpiringSecrets = appRegistration.ExpiringSecrets.Select(secret =>
                    secret with
                    {
                        IsExpiringSoon = secret.EndDateTime.UtcDateTime <= DateTimeOffset.UtcNow.AddDays(days)
                    })
            }).ToList();
    }

    private static AppRegistrationWithSecret[] Map(List<AppRegistration> existing)
    {
        return existing.SelectMany(e =>
                e.ExpiringSecrets.Select(p => new AppRegistrationWithSecret
                {
                    AppRegistrationName = e.DisplayName,
                    SecretName = p.DisplayName,
                    SecretExpiresOn = p.EndDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    SecretValue = p.Value ?? "No Secret Changed",
                    IsExpiringSoon = p.IsExpiringSoon,
                    IsRenewed = p.IsRenewed
                }).ToArray())
            .ToArray();
    }

    private async Task<List<AppRegistration>> RotateSecretsExpiringWithin(
        int rotateSecretsExpiringWithinDays,
        SecretsToRotate[] secretsToRotate,
        int expiresInDays,
        bool deleteAfterRenew)
    {
        logger.LogInformation(
            "Starting rotation of secrets expiring within {days} days",
            rotateSecretsExpiringWithinDays);

        var appRegistrationsWithExpiringSecrets = await GetAppRegistrationWithSecretsExpiringWithin(rotateSecretsExpiringWithinDays);

        logger.LogInformation(
            "Found {appRegs} app registrations with secrets expiring within {days} days",
            appRegistrationsWithExpiringSecrets.Count,
            rotateSecretsExpiringWithinDays);

        List<AppRegistration> appRegWithNewSecrets = [];
        foreach (var appRegistration in appRegistrationsWithExpiringSecrets)
        {
            List<Secret> secrets = [];
            foreach (var secret in appRegistration.ExpiringSecrets)
            {
                if (!secretsToRotate.Any(secretToRotate =>
                        secretToRotate.AppRegistrationName == appRegistration.DisplayName &&
                        secretToRotate.SecretName == secret.DisplayName))
                {
                    logger.LogInformation(
                        "Skipping {appReg} with secret {secretName} as it is not in the list of apps to rotate",
                        appRegistration.DisplayName,
                        secret.DisplayName);
                    secrets.Add(secret);
                    continue;
                }

                if (!secret.IsExpiringSoon)
                {
                    logger.LogInformation(
                        "Skipping {appReg} with secret {secretName}. Not expiring soon",
                        appRegistration.DisplayName,
                        secret.DisplayName);
                    secrets.Add(secret);
                    continue;
                }

                logger.LogInformation("Rotating app registration {appReg} with secret {secret}",
                    appRegistration.DisplayName,
                    secret.DisplayName);

                var (secretRenewed, newExpireDate) = await client.RecreateSecret(
                    appRegistration.Id,
                    secret.DisplayName,
                    expiresInDays);

                secrets.Add(
                    secret with
                    {
                        EndDateTime = newExpireDate,
                        IsRenewed = true,
                        Value = secretRenewed
                    });

                logger.LogInformation("Rotated secret {secret}", secret.DisplayName);

                if (!deleteAfterRenew) continue;
                await client.DeleteSecret(appRegistration.Id, secret.KeyId);
                logger.LogInformation("Deleted secret {secret}", secret.DisplayName);
            }

            if (secrets.Any())
                appRegWithNewSecrets.Add(appRegistration with { ExpiringSecrets = secrets });
            else
                appRegWithNewSecrets.Add(appRegistration);
        }

        return appRegWithNewSecrets;
    }
}
