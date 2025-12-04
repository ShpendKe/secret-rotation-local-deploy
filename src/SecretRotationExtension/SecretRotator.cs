using Microsoft.Extensions.Logging;
using SecretRotationExtension.EntraId;

namespace SecretRotationExtension;

public class SecretRotator
{
    private readonly ISecretClient _client;
    private readonly ILogger<SecretRotator> _logger;
    private readonly int _rotateSecretsExpiringWithinDays;

    public SecretRotator(
        ISecretClient client,
        int rotateSecretsExpiringWithinDays,
        ILogger<SecretRotator> logger)
    {
        _client = client;
        _rotateSecretsExpiringWithinDays = rotateSecretsExpiringWithinDays;
        _logger = logger;
    }

    public async Task<List<AppRegistration>> GetAppRegistrationWithSecrets()
    {
        var appRegistrations = await _client.GetAppRegistrationWithExpiringDates();

        var expireDateInUtc = DateTimeOffset.UtcNow.AddDays(_rotateSecretsExpiringWithinDays);
        return appRegistrations
            .Where(appRegistration => appRegistration.ExpiringSecrets.Any())
            .Select(appRegistration =>
                appRegistration with
                {
                    ExpiringSecrets = appRegistration.ExpiringSecrets
                        .GroupBy(s => s.DisplayName)
                        .Select(s => s.OrderByDescending(ss => ss.StartDateTime).First())
                        .Select(secret =>
                            secret with
                            {
                                IsExpiringSoon = secret.EndDateTime.UtcDateTime <= expireDateInUtc
                            })
                }).ToList();
    }

    public AppRegistrationWithSecret[] Map(List<AppRegistration> appRegistrations)
    {
        return appRegistrations.SelectMany(appRegistration =>
                appRegistration.ExpiringSecrets.Select(secret => new AppRegistrationWithSecret
                {
                    AppRegistrationName = appRegistration.DisplayName,
                    SecretName = secret.DisplayName,
                    SecretExpiresOn = secret.EndDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    SecretValue = secret.Value ?? "No Secret Changed",
                    IsExpiringSoon = secret.IsExpiringSoon,
                    IsRenewed = secret.IsRenewed
                }).ToArray())
            .ToArray();
    }

    public async Task<List<AppRegistration>> RotateSecretsExpiringWithin(
        int rotateSecretsExpiringWithinDays,
        SecretsToRotate[] secretsToRotate,
        int newSecretsExpiresInDays,
        bool deleteAfterRenew)
    {
        _logger.LogInformation(
            "Starting rotation of secrets expiring within {days} days",
            rotateSecretsExpiringWithinDays);

        var appRegistrationsWithSecrets = await GetAppRegistrationWithSecrets();

        _logger.LogInformation(
            "Found {appRegs} app registrations with secrets",
            appRegistrationsWithSecrets.Count);

        Dictionary<string, AppRegistration> appRegWithNewSecrets = [];
        var newSecretsToCreate = secretsToRotate.ToList();
        foreach (var appRegistration in appRegistrationsWithSecrets)
        {
            if (!secretsToRotate.Any(secretToRotate =>
                    secretToRotate.AppRegistrationName == appRegistration.DisplayName))
            {
                _logger.LogInformation(
                    "Skipping {appReg} as it is in the list of apps to rotate",
                    appRegistration.DisplayName);
                appRegWithNewSecrets[appRegistration.DisplayName] = appRegistration;
                continue;
            }

            List<Secret> secrets = [];
            foreach (var secret in appRegistration.ExpiringSecrets)
            {
                var foundSecretToRotate = secretsToRotate.FirstOrDefault(secretToRotate =>
                    secretToRotate.SecretName == secret.DisplayName);

                if (foundSecretToRotate is null)
                {
                    _logger.LogInformation(
                        "Skipping {appReg} with secret {secretName} as it is not in the list of apps to rotate",
                        appRegistration.DisplayName,
                        secret.DisplayName);
                    secrets.Add(secret);
                    continue;
                }

                newSecretsToCreate.Remove(foundSecretToRotate);

                if (!secret.IsExpiringSoon)
                {
                    _logger.LogInformation(
                        "Skipping {appReg} with secret {secretName}. Not expiring soon",
                        appRegistration.DisplayName,
                        secret.DisplayName);
                    secrets.Add(secret);
                    continue;
                }

                _logger.LogInformation("Rotating app registration {appReg} with secret {secret}",
                    appRegistration.DisplayName,
                    secret.DisplayName);

                var (secretRenewed, newExpireDate) = await _client.RecreateSecret(
                    appRegistration.Id,
                    secret.DisplayName,
                    newSecretsExpiresInDays);

                secrets.Add(
                    secret with
                    {
                        EndDateTime = newExpireDate,
                        IsRenewed = true,
                        Value = secretRenewed
                    });

                _logger.LogInformation("Rotated secret {secret}", secret.DisplayName);

                if (!deleteAfterRenew)
                {
                    continue;
                }

                await _client.DeleteSecret(appRegistration.Id, secret.KeyId);
                _logger.LogInformation("Deleted secret {secret}", secret.DisplayName);
            }

            if (secrets.Any())
            {
                appRegWithNewSecrets[appRegistration.DisplayName] = appRegistration with { ExpiringSecrets = secrets };
            }
            else
            {
                appRegWithNewSecrets[appRegistration.DisplayName] = appRegistration;
            }
        }

        var lookup = appRegistrationsWithSecrets.ToDictionary(appRegistration => appRegistration.DisplayName);
        foreach (var secretToCreate in newSecretsToCreate)
        {
            var (secretRenewed, newExpireDate) = await _client.RecreateSecret(
                lookup[secretToCreate.AppRegistrationName].Id,
                secretToCreate.SecretName,
                newSecretsExpiresInDays);

            appRegWithNewSecrets[secretToCreate.AppRegistrationName] =
                appRegWithNewSecrets[secretToCreate.AppRegistrationName] with
                {
                    ExpiringSecrets =
                    [
                        ..appRegWithNewSecrets[secretToCreate.AppRegistrationName].ExpiringSecrets,
                        new Secret(
                            secretToCreate.SecretName,
                            Guid.Empty,
                            DateTimeOffset.UtcNow,
                            newExpireDate,
                            false,
                            true,
                            secretRenewed)
                    ]
                };
        }

        return appRegWithNewSecrets.Values.ToList();
    }
}
