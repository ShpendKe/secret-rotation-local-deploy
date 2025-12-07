namespace SecretRotationExtension;

public interface ISecretClient
{
    Task<IEnumerable<AppRegistration>> GetAppRegistrationWithExpiringDates();
    Task<(string Secret, DateTimeOffset NewExpireDate)> RecreateSecret(string appRegistrationId, string secretName, int expiresInDays);
    Task DeleteSecret(string appRegistrationId, Guid secretKeyId);
}

public record AppRegistration(
    string DisplayName,
    string Id,
    IEnumerable<Secret> Secrets);

public record Secret(
    string DisplayName,
    Guid KeyId,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    bool IsExpiringSoon = false,
    bool IsRenewed = false,
    bool IsNew = false,
    string? Value = null);
