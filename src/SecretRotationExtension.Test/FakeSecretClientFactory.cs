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
                new Secret("Secret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(180)),
                new Secret("ExpiringSecret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(10)),
                new Secret("AnotherExpiringSecret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(10))
            ]),
        new(
            "AnotherAppRegWithSecret",
            Guid.NewGuid().ToString(),
            [
                new Secret("Secret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(180)),
                new Secret("SomeSecret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(180)),
                new Secret("SomeExpiringSecret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(10))
            ])
    ];

    public Task<IEnumerable<AppRegistration>> GetAppRegistrationWithExpiringDates()
    {
        return Task.FromResult(SampleAppRegistrations);
    }

    public Task<(string Secret, DateTimeOffset NewExpireDate)> RecreateSecret(string appRegistrationId, string displayName,
        int expiresInDays)
    {
        return Task.FromResult((displayName, DateTimeOffset.UtcNow.AddDays(expiresInDays)));
    }

    public Task DeleteSecret(string appRegistrationId, Guid secretKeyId)
    {
        return Task.CompletedTask;
    }
}
