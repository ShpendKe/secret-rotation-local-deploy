# Secret Rotation Bicep Local Extension (Experimental)

Custom Bicep Local Extension to rotate secrets from source (e.g. Entra Id) and save it in target (e.g. KeyVault) using Bicep.

> [!WARNING]
> Local-Deploy is an experimental Bicep feature and is subject to change.  
> Do not use it in production.

## Capabilities

![Secret Rotation Concept](./secret-rotation.png)

1. Create/Rotate secrets in **source**:
   1. Entra Id (App Registration)
2. Save/Update rotated secrets at **target**:
   1. Azure Key Vault with [Key Vault Data Plane Extension](https://github.com/anthony-c-martin/bicep-ext-keyvault)

## Prerequisites

- .NET 9 SDK
- Bicep CLI v0.37.4+ (for `local-deploy`)

### For development

- Github CLI
- Bash + jq

## How to use it locally

Here are the steps to run it either locally or using an ACR.

### Local Development

Run the scripts from root folder.

1. Publish and run secret rotation extension locally:  
    ```powershell
    .\infra\scripts\publish.ps1 -Target ./secret-rotation-extension
    ```
    This will publish the extension and make it available in your bicep file
2. Update `sample\main.bicepparam` with your params (e.g. TenantId)
    ```bicep
    using 'main.bicep'

    param secretRotations = [
      {
        source: {
          tenantId: '{YOUR-TENANT-ID}'
        }
        target: {
          keyVault: 'secretrotationkv'
        }
        secretTransfers: [
          {
            sourceSecretKey: {
              appRegistrationName: 'expiredSecretApp'
              secretName: 'expiredSecretNameInEntraId'
            }
            targetSecretKey: 'secretNameInKeyVault'
          }
        ]
      }
    ]
    ```
3. Run your local bicep deployment
    ```powershell
    ~/.azure/bin/bicep local-deploy .\sample\main.bicepparam
    ``` 

#### Debugging

If you get any errors, enable Tracing to understand errors by setting environment variable:
```powershell
$env:BICEP_TRACING_ENABLED = "true"
```

## Disclaimer

Sample only – not an official Microsoft supported extension. Use at your own risk.