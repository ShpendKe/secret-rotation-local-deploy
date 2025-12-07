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
            .Where(appRegistration => appRegistration.Secrets.Any())
            .Select(appRegistration =>
                appRegistration with
                {
                    Secrets = appRegistration.Secrets
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
                appRegistration.Secrets.Select(secret => new AppRegistrationWithSecret
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

        var appRegistrationsWithExpiringOrToCreateSecrets =
            FilterAppRegistrationWithSecretsToRotateOrCreate(appRegistrationsWithSecrets, secretsToRotate)
                .ToDictionary(a => a.DisplayName);

        Dictionary<string, AppRegistration> appRegWithNewSecrets = [];
        foreach (var appRegistration in appRegistrationsWithExpiringOrToCreateSecrets.Values)
        {
            List<Secret> rotatedOrCreatedSecrets = [];
            foreach (var secret in appRegistration.Secrets)
            {
                _logger.LogInformation("{Action} app registration {appReg} with secret {secret}",
                    secret.IsNew ? "Creating" : "Rotating",
                    appRegistration.DisplayName,
                    secret.DisplayName);

                var (secretRenewed, newExpireDate) = await _client.RecreateSecret(
                    appRegistration.Id,
                    secret.DisplayName,
                    newSecretsExpiresInDays);

                rotatedOrCreatedSecrets.Add(
                    secret with
                    {
                        EndDateTime = newExpireDate,
                        IsRenewed = true,
                        Value = secretRenewed
                    });

                _logger.LogInformation("{Action} secret {secret}",
                    secret.IsNew ? "Created" : "Rotated",
                    secret.DisplayName);

                if (!deleteAfterRenew || secret.IsNew)
                {
                    continue;
                }

                await _client.DeleteSecret(appRegistration.Id, secret.KeyId);
                _logger.LogInformation("Deleted secret {secret}", secret.DisplayName);
            }

            appRegWithNewSecrets[appRegistration.DisplayName] =
                appRegistration with { Secrets = rotatedOrCreatedSecrets };
        }

        return appRegWithNewSecrets.Values.ToList();
    }

    private List<AppRegistration> FilterAppRegistrationWithSecretsToRotateOrCreate(
        List<AppRegistration> appRegistrationsWithSecrets,
        SecretsToRotate[] secretsToRotate)
    {
        var appRegistrationsWithSecretsLookup = appRegistrationsWithSecrets.ToDictionary(a => a.DisplayName);

        _logger.LogInformation(
            "Found {appRegs} app registrations with secrets",
            appRegistrationsWithSecrets.Count);

        Dictionary<string, AppRegistration> appRegWithExpiringSecrets = [];

        var allSecrets = secretsToRotate.Select(s => (s.AppRegistrationName, s.SecretName)).ToHashSet();

        var secrets = appRegistrationsWithSecrets
            .SelectMany(ar => ar.Secrets.Select(s => (ar.DisplayName, s.DisplayName)))
            .ToHashSet();

        foreach (var secretToRotate in secretsToRotate.GroupBy(s => s.AppRegistrationName))
        {
            if (!appRegistrationsWithSecretsLookup.TryGetValue(
                    secretToRotate.Key,
                    out var appRegistration))
            {
                _logger.LogInformation(
                    "Skipping {appReg} as it is in the list of apps to rotate",
                    secretToRotate.Key);
                continue;
            }

            List<(string AppRegistration, string SecretName)> newSecrets = allSecrets.Except(secrets).ToList();

            var rotatedOrCreatedSecrets = new List<Secret>();

            foreach (var secret in appRegistration.Secrets)
            {
                if (allSecrets.Contains((appRegistration.DisplayName, secret.DisplayName)) && secret.IsExpiringSoon)
                {
                    rotatedOrCreatedSecrets.Add(secret);
                }
            }

            foreach (var secret in newSecrets)
            {
                rotatedOrCreatedSecrets.Add(
                    new Secret(
                        secret.SecretName,
                        Guid.NewGuid(),
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow.AddDays(180),
                        IsNew: true));
            }

            appRegWithExpiringSecrets[secretToRotate.Key] = appRegistration with
            {
                Secrets = rotatedOrCreatedSecrets
            };
        }

        return appRegWithExpiringSecrets.Values.ToList();
    }
}
