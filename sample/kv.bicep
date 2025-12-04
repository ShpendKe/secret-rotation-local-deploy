targetScope = 'local'

param vaultUri string
param secrets array

extension keyvault with {
  vaultUri: vaultUri
}

resource secret 'Secret' = [
  for i in range(0, length(secrets)): if (!empty(secrets[i].value) && secrets[i].value.isRenewed) {
    name: secrets[i].secretKey
    value: secrets[i].value.secretValue
  }
]
