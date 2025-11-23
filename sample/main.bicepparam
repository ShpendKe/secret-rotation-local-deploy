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
