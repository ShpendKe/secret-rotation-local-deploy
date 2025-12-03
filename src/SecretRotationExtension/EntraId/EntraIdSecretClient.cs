using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Applications.Item.AddPassword;
using Microsoft.Graph.Applications.Item.RemovePassword;
using Microsoft.Graph.Models;

namespace SecretRotationExtension.EntraId;

public class EntraIdSecretClient : ISecretClient
{
    private readonly GraphServiceClient _graphClient;

    public EntraIdSecretClient(string tenantId)
    {
        DefaultAzureCredential defaultAzureCredential = new(new DefaultAzureCredentialOptions
        {
            TenantId = tenantId
        });

        _graphClient = new GraphServiceClient(defaultAzureCredential);
    }

    public async Task<IEnumerable<AppRegistration>> GetAppRegistrationWithExpiringDates()
    {
        var appRegistrations = await _graphClient.Applications.GetAsync();

        return appRegistrations!.Value!.Select(app => new AppRegistration(
            app.DisplayName!,
            app.Id!,
            app.PasswordCredentials!.Select(p =>
                new Secret(
                    p.DisplayName!,
                    p.KeyId!.Value,
                    p.StartDateTime!.Value,
                    p.EndDateTime!.Value))
        ));
    }

    public async Task<(string Secret, DateTimeOffset NewExpireDate)> RecreateSecret(string appRegistrationId, string displayName,
        int expiresInDays)
    {
        var newSecret = new PasswordCredential
        {
            DisplayName = displayName,
            EndDateTime = DateTime.UtcNow.AddDays(expiresInDays)
        };

        var created = await _graphClient.Applications[appRegistrationId]
            .AddPassword
            .PostAsync(new AddPasswordPostRequestBody
            {
                PasswordCredential = newSecret
            });

        return (created?.SecretText!, created!.EndDateTime!.Value);
    }

    public async Task DeleteSecret(string appRegistrationId, Guid secretKeyId)
    {
        await _graphClient.Applications[appRegistrationId].RemovePassword.PostAsync(
            new RemovePasswordPostRequestBody
            {
                KeyId = secretKeyId
            });
    }
}

public record AppRegistration(
    string DisplayName,
    string Id,
    IEnumerable<Secret> ExpiringSecrets);

public record Secret(
    string DisplayName,
    Guid KeyId,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    bool IsExpiringSoon = false,
    bool IsRenewed = false,
    string? Value = null);
