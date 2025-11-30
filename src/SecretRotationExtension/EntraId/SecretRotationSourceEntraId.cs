using Azure.Bicep.Types.Concrete;

namespace SecretRotationExtension.EntraId;

[BicepFrontMatter("category", "Sample")]
[BicepDocHeading(
    "SecretRotationSourceEntraId",
    "Represents a entra id secret rotation resource that demonstrates all available documentation attributes."
)]
[BicepDocExample(
    "Rotate secrets for a basic example resource",
    "This example shows how to create a simple example resource with all properties.",
    @"
@description('Tenant ID')
param tenantId string

resource entraIdApps 'SecretRotationSourceEntraId' = {
  id: tenantId
  rotateSecretsExpiringWithinDays: 30
  expiresInDays: 180
  deleteAfterRenew: true
  apps: [
    'expiredSecretApp'
  ]
}
"
)]
[ResourceType("SecretRotationSourceEntraId")]
public class SecretRotationSourceEntraId : SecretRotationSourceIdentifier
{
    [TypeProperty("Rotate secrets expiring within this many days (default 30).")]
    public int RotateSecretsExpiringWithinDays { get; set; } = 30;

    [TypeProperty("The number of days until the secret expires (default 365).")]
    public int ExpiresInDays { get; set; } = 180;

    [TypeProperty("App registration with secret name which should be considered for rotation. Others are not rotated.")]
    public SecretsToRotate[] SecretsToRotate { get; set; } = null!;
    
    [TypeProperty("If true, deletes the old secret after renewal.")]
    public bool DeleteAfterRenew { get; set; } = false;
    
    // Output
    [TypeProperty("App registrations which should be considered for rotation.", ObjectTypePropertyFlags.ReadOnly)]
    public AppRegistrationWithSecret[] AppsWithExpiringSecrets { get; set; } = null!;
}

public class SecretsToRotate
{
    public string AppRegistrationName { get; set; } = null!;
    public string SecretName { get; set; } = null!;
}

public class AppRegistrationWithSecret
{
    public string AppRegistrationName { get; set; } = null!;
    public string SecretName { get; set; } = null!;
    public string SecretExpiresOn { get; set; } = null!;
    public string SecretValue { get; set; } = null!;
    
    public bool IsExpiringSoon { get; set; } = false;
    public bool IsRenewed { get; set; } = false;
}

public class SecretRotationSourceIdentifier
{
    // Required identifier property
    [TypeProperty("The unique id of the resource.",
        ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string Id { get; set; }
}