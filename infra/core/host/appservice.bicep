param name string
param location string = resourceGroup().location
param tags object = {}
param appServicePlanId string
param runtimeName string
param runtimeVersion string

resource appService 'Microsoft.Web/sites@2022-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    serverFarmId: appServicePlanId
    siteConfig: {
      metadata: [
        {
          name: 'CURRENT_STACK'
          value: runtimeName
        }
      ]
      netFrameworkVersion: runtimeName == 'dotnet' ? 'v${runtimeVersion}' : null
    }
  }
}

output id string = appService.id
output name string = appService.name
output uri string = 'https://${appService.properties.defaultHostName}'