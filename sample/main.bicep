targetScope = 'local'
extension secretrotation

param secretRotations secretRotation[]

param rotateSecretsExpiringWithinDays int = 10
param newGeneratedSecretsWithNewExpiringDateOffsetInDays int = 10

resource entraIdApps 'SecretRotationSourceEntraId' = [
  for i in range(0, length(secretRotations)): {
    id: secretRotations[i].source.tenantId
    rotateSecretsExpiringWithinDays: rotateSecretsExpiringWithinDays
    expiresInDays: newGeneratedSecretsWithNewExpiringDateOffsetInDays
    deleteAfterRenew: true
    secretsToRotate: [
      for j in range(0, length(secretRotations[i].secretTransfers)): secretRotations[i].secretTransfers[j].sourceSecretKey
    ]
  }
]

module kv 'kv.bicep' = [
  for i in range(0, length(secretRotations)): {
    name: secretRotations[i].target.keyVault
    params: {
      #disable-next-line no-hardcoded-env-urls
      vaultUri: 'https://${secretRotations[i].target.keyVault}.vault.azure.net'
      secrets: [
        for j in range(0, length(secretRotations[i].secretTransfers)): {
          secretKey: secretRotations[i].secretTransfers[j].targetSecretKey
          value: first(filter(
            entraIdApps[i].appsWithExpiringSecrets,
            appReg =>
              appReg.appRegistrationName == secretRotations[i].secretTransfers[j].sourceSecretKey.appRegistrationName && appReg.secretName == secretRotations[i].secretTransfers[j].sourceSecretKey.secretName
          ))
        }
      ]
    }
  }
]

// Outputs
output entraIdAppsOutput array = [
  for ii in range(0, length(secretRotations)): toObject(
    entraIdApps[ii].appsWithExpiringSecrets,
    a => '${a.appRegistrationName}-${a.secretName}',
    c => {
      appRegistrationName: c.appRegistrationName
      secretName: c.secretName
      isRenewed: c.isRenewed
      isExpiringSoon: c.isExpiringSoon
    }
  )
]

type secretRotation = {
  source: secretSource
  target: secretTarget
  secretTransfers: secretTransfer[]
}

type secretSource = {
  tenantId: string
}

type secretTarget = {
  keyVault: string
}

type secretTransfer = {
  sourceSecretKey: secretKeySource
  targetSecretKey: string
}

type secretKeySource = {
  appRegistrationName: string
  secretName: string
}
