using Bicep.Local.Extension.Host.Handlers;

namespace SecretRotationExtension.EntraId;

/// <summary>
///     Secret Rotation for Entra Id
/// </summary>
public class SecretRotationEntraIdHandler(SecretRotatorFactory secretRotatorFactory)
    : TypedResourceHandler<SecretRotationSourceEntraId, SecretRotationSourceEntraIdIdentifier>
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
        var entraIdSecretRotator = secretRotatorFactory.Create(
            request.Properties.Id,
            request.Properties.RotateSecretsExpiringWithinDays);

        var foundAppRegistrationsWithSecrets =
            await entraIdSecretRotator.GetAppRegistrationWithSecrets();

        if (foundAppRegistrationsWithSecrets.Any())
        {
            request.Properties.AppsWithExpiringSecrets = entraIdSecretRotator.Map(foundAppRegistrationsWithSecrets);
        }

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

        var entraIdSecretRotator = secretRotatorFactory.Create(props.Id, props.RotateSecretsExpiringWithinDays);

        var appRegistrationWithRotatedSecrets = await entraIdSecretRotator.RotateSecretsExpiringWithin(
            props.RotateSecretsExpiringWithinDays,
            props.SecretsToRotate,
            props.ExpiresInDays,
            props.DeleteAfterRenew);

        request.Properties.AppsWithExpiringSecrets = entraIdSecretRotator.Map(appRegistrationWithRotatedSecrets);

        return GetResponse(request);
    }

    /// <summary>
    ///     GetIdentifiers - extracts the identifier properties from the resource.
    ///     These identifiers are used to locate and identify the resource.
    /// </summary>
    protected override SecretRotationSourceEntraIdIdentifier GetIdentifiers(SecretRotationSourceEntraId properties)
    {
        return new SecretRotationSourceEntraIdIdentifier { Id = properties.Id };
    }
}
