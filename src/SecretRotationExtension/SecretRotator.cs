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

        return appRegistrations
            .Where(appRegistration => appRegistration.ExpiringSecrets.Any())
            .Select(appRegistration =>
                appRegistration with
                {
                    ExpiringSecrets = appRegistration.ExpiringSecrets.Select(secret =>
                        secret with
                        {
                            IsExpiringSoon = secret.EndDateTime.UtcDateTime <=
                                             DateTimeOffset.UtcNow.AddDays(_rotateSecretsExpiringWithinDays)
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

        List<AppRegistration> appRegWithNewSecrets = [];
        foreach (var appRegistration in appRegistrationsWithSecrets)
        {
            List<Secret> secrets = [];
            foreach (var secret in appRegistration.ExpiringSecrets)
            {
                if (!secretsToRotate.Any(secretToRotate =>
                        secretToRotate.AppRegistrationName == appRegistration.DisplayName &&
                        secretToRotate.SecretName == secret.DisplayName))
                {
                    _logger.LogInformation(
                        "Skipping {appReg} with secret {secretName} as it is not in the list of apps to rotate",
                        appRegistration.DisplayName,
                        secret.DisplayName);
                    secrets.Add(secret);
                    continue;
                }

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
                appRegWithNewSecrets.Add(appRegistration with { ExpiringSecrets = secrets });
            }
            else
            {
                appRegWithNewSecrets.Add(appRegistration);
            }
        }

        return appRegWithNewSecrets;
    }
}
