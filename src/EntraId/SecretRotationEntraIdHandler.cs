using Bicep.Local.Extension.Host.Handlers;
using Microsoft.Extensions.Logging;

namespace SecretRotationExtension.EntraId;

/// <summary>
///     Secret Rotation for Entra Id
/// </summary>
public class SecretRotationEntraIdHandler :
    TypedResourceHandler<SecretRotationSourceEntraId, SecretRotationSourceIdentifier>
{
    private readonly EntraIdSecretClient _client;
    private readonly ILogger<SecretRotationEntraIdHandler> _logger;

    public SecretRotationEntraIdHandler(ILogger<SecretRotationEntraIdHandler> logger)
    {
        _logger = logger;
        _client = new EntraIdSecretClient();
    }

    private async Task<List<AppRegistration>> GetAppRegistrationWithSecretsExpiringWithin(int days)
    {
        var existing = await _client.GetAppRegistrationWithExpiringDates();

        return existing.Select(appRegistration =>
            appRegistration with
            {
                ExpiringSecrets = appRegistration.ExpiringSecrets.Select(secret =>
                    secret with
                    {
                        IsExpiringSoon = secret.EndDateTime.UtcDateTime <= DateTimeOffset.UtcNow.AddDays(days)
                    })
            }).ToList();
    }

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
            props.Apps.ToHashSet(),
            props.ExpiresInDays,
            props.DeleteAfterRenew);

        request.Properties.AppsWithExpiringSecrets = Map(appRegistrationWithRotatedSecrets);

        return GetResponse(request);
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

    private async Task<List<AppRegistration>> RotateSecretsExpiringWithin(int rotateSecretsExpiringWithinDays,
        HashSet<string> appsToFilter,
        int expiresInDays,
        bool deleteAfterRenew)
    {
        _logger.LogInformation(
            "Starting rotation of secrets expiring within {days} days",
            rotateSecretsExpiringWithinDays);

        var apps = await GetAppRegistrationWithSecretsExpiringWithin(rotateSecretsExpiringWithinDays);

        _logger.LogInformation(
            "Found {count} app registrations with secrets expiring within {days} days",
            apps.Count,
            rotateSecretsExpiringWithinDays);

        List<AppRegistration> appRegWithNewSecrets = [];
        foreach (var app in apps)
        {
            if (!appsToFilter.Contains(app.DisplayName))
            {
                _logger.LogInformation(
                    "Skipping {app} with id {id} as it is not in the list of apps to rotate",
                    app.DisplayName,
                    app.Id);
                continue;
            }

            _logger.LogInformation("Rotating secrets for {app}", app.DisplayName);

            List<Secret> secrets = [];
            foreach (var secret in app.ExpiringSecrets)
            {
                if (!secret.IsExpiringSoon)
                {
                    secrets.Add(secret);
                    continue;
                }

                _logger.LogInformation("Rotating secret {secret}", secret.DisplayName);
                var (secretRenewed, newExpireDate) = await _client.RecreateSecret(
                    app.Id,
                    secret.DisplayName,
                    expiresInDays);

                secrets.Add(
                    secret with
                    {
                        EndDateTime = newExpireDate,
                        IsRenewed = true,
                        Value = secretRenewed
                    });

                _logger.LogInformation("Rotated secret {secret}", secret.DisplayName);

                if (!deleteAfterRenew) continue;
                await _client.DeleteSecret(app.Id, secret.KeyId);
                _logger.LogInformation("Deleted secret {secret}", secret.DisplayName);
            }

            if (secrets.Any())
                appRegWithNewSecrets.Add(app with { ExpiringSecrets = secrets });
            else
                appRegWithNewSecrets.Add(app);
        }

        return appRegWithNewSecrets;
    }

    /// <summary>
    ///     GetIdentifiers - extracts the identifier properties from the resource.
    ///     These identifiers are used to locate and identify the resource.
    /// </summary>
    protected override SecretRotationSourceIdentifier GetIdentifiers(SecretRotationSourceEntraId properties)
    {
        return new SecretRotationSourceIdentifier { Id = properties.Id };
    }
}