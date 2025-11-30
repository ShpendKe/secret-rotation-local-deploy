targetScope = 'subscription'

param acrName string
param resourceGroupName string
param location string = deployment().location
param kvName string

resource resourceGroup 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: resourceGroupName
  location: location
}

module acr 'br/public:avm/res/container-registry/registry:0.9.1' = {
  scope: resourceGroup
  params: {
    name: acrName
    location: resourceGroup.location
    acrSku: 'Standard'
    acrAdminUserEnabled: false
    anonymousPullEnabled: true
  }
}

module kv 'br/public:avm/res/key-vault/vault:0.13.3' = {
  name: kvName
  scope: resourceGroup
  params: {
    name: kvName
  }
}
