using SecretRotationExtension.EntraId;

namespace SecretRotationExtension;

public interface ISecretClient
{
    Task<IEnumerable<AppRegistration>> GetAppRegistrationWithExpiringDates();
    Task<(string Secret, DateTimeOffset NewExpireDate)> RecreateSecret(string appRegistrationId, string secretName, int expiresInDays);
    Task DeleteSecret(string appRegistrationId, Guid secretKeyId);
}
