param name string
param location string = resourceGroup().location
param tags object = {}
param allowedIpAddress string

@description('Storage account name')
param storageAccountName string = name

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    isHnsEnabled: true // Enable hierarchical namespace for Data Lake Gen2
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    networkAcls: {
      defaultAction: 'Deny'
      ipRules: [
        {
          action: 'Allow'
          value: allowedIpAddress
        }
      ]
      bypass: 'AzureServices'
    }
  }
}

// Create the blob service
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    cors: {
      corsRules: []
    }
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

// Create bronze container
resource bronzeContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'bronze'
  properties: {
    publicAccess: 'None'
  }
}

// Create silver container
resource silverContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'silver'
  properties: {
    publicAccess: 'None'
  }
}

// Create gold container
resource goldContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'gold'
  properties: {
    publicAccess: 'None'
  }
}

// Get current user's object ID (this will be passed as a parameter)
param currentUserObjectId string

// Assign Storage Blob Data Owner role to current user
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, currentUserObjectId, 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Owner
    principalId: currentUserObjectId
    principalType: 'User'
  }
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
output primaryEndpoints object = storageAccount.properties.primaryEndpoints
output dfsEndpoint string = storageAccount.properties.primaryEndpoints.dfs