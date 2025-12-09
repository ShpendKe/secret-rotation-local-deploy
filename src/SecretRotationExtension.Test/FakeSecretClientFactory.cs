using SecretRotationExtension.EntraId;

namespace SecretRotationExtension.Test;

public class FakeSecretClient : ISecretClient
{
    private static readonly IEnumerable<AppRegistration> SampleAppRegistrations =
    [
        new(
            "AppRegWithoutSecret",
            Guid.NewGuid().ToString(),
            []),
        new(
            "AppRegWithSecret",
            Guid.NewGuid().ToString(),
            [
                new Secret("Secret", Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(180)),
                new Secret("Secret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(10), DateTimeOffset.UtcNow.AddDays(190)),
                new Secret("ExpiringSecret", Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10)),
                new Secret("ExpiringSecret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(10)),
                new Secret("AnotherExpiringSecret", Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10))
            ]),
        new(
            "AnotherAppRegWithSecret",
            Guid.NewGuid().ToString(),
            [
                new Secret("Secret", Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(180)),
                new Secret("SomeSecret", Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(180)),
                new Secret("SomeSecret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(180)),
                new Secret("SomeExpiringSecret", Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10))
            ])
    ];

    public List<(string AppRegistrationId, string SecretName)> RotatedOrCreatedSecrets = [];

    public Task<IEnumerable<AppRegistration>> GetAppRegistrationWithExpiringDates()
    {
        return Task.FromResult(SampleAppRegistrations);
    }

    public Task<(string Secret, DateTimeOffset NewExpireDate)> RecreateSecret(string appRegistrationId, string secretName,
        int expiresInDays)
    {
        RotatedOrCreatedSecrets.Add((appRegistrationId, secretName));
        return Task.FromResult((displayName: secretName, DateTimeOffset.UtcNow.AddDays(expiresInDays)));
    }

    public Task DeleteSecret(string appRegistrationId, Guid secretKeyId)
    {
        return Task.CompletedTask;
    }
}
