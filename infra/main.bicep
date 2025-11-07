targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Your IP address for storage account access')
param allowedIpAddress string

@description('Current user object ID for role assignment')
param currentUserObjectId string

// Web app parameters
param webAppServicePlanName string = ''
param webAppServiceName string = ''

// Optional parameters to override the default azd resource naming conventions. Update the main.parameters.json file to provide values. e.g.,:
// "resourceGroupName": {
//      "value": "myGroupName"
// }
param resourceGroupName string = ''

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: tags
}

// Deploy Data Lake Storage Gen2
module dataLake './storage.bicep' = {
  name: 'dataLake'
  scope: rg
  params: {
    name: 'st${resourceToken}'
    location: location
    tags: tags
    allowedIpAddress: allowedIpAddress
    currentUserObjectId: currentUserObjectId
    storageAccountName: 'st${resourceToken}'
  }
}

// Deploy App Service Plan
module appServicePlan 'core/host/appserviceplan.bicep' = {
  name: 'appserviceplan'
  scope: rg
  params: {
    name: !empty(webAppServicePlanName) ? webAppServicePlanName : '${abbrs.webServerFarms}${resourceToken}'
    location: location
    tags: tags
    sku: {
      name: 'B1'
      capacity: 1
    }
  }
}

// Deploy Web App
module web 'core/host/appservice.bicep' = {
  name: 'web'
  scope: rg
  params: {
    name: !empty(webAppServiceName) ? webAppServiceName : '${abbrs.webSitesAppService}web-${resourceToken}'
    location: location
    tags: tags
    appServicePlanId: appServicePlan.outputs.id
    runtimeName: 'dotnet'
    runtimeVersion: '8.0'
  }
}

// Data outputs
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output STORAGE_ACCOUNT_NAME string = dataLake.outputs.storageAccountName
output STORAGE_ACCOUNT_ID string = dataLake.outputs.storageAccountId
output DATA_LAKE_ENDPOINT string = dataLake.outputs.dfsEndpoint

// Web app outputs
output WEB_URI string = web.outputs.uri