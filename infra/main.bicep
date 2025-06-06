
targetScope = 'resourceGroup'

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string
param location string = resourceGroup().location
param appServicePlanName string
param appServicePlanNameResourceGroupName string
param appServiceName string = environmentName
param applicationInsightsName string = 'appi-${environmentName}'
param logAnalyticsWorkspaceName string = 'log-${environmentName}'

@secure()
param azureDbConnectionString string

@description('Key Vault reference to the CoinMarketCap API key')
param coinmarketcapApiKeyKVUri string

@description('Name of the Azure AD application used for authentication')
param identityProviderName string

@description('Azure AD application (client) ID for authentication')
param identityProviderClientId string

var tags = { 'azd-env-name': environmentName }

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' existing = {
  name: appServicePlanName
  scope: az.resourceGroup(appServicePlanNameResourceGroupName)
}

module applicationInsights 'monitor/applicationinsights.bicep' = {
  name: 'applicationinsights'
  params: {
    name: applicationInsightsName
    logAnalyticsWorkspaceName: logAnalyticsWorkspaceName
    location: location
    tags: tags
  }
}

module appService 'appservice/appservice.bicep' = {
  name: 'web'
  params: {
    name: appServiceName
    location: location
    tags: union(tags, { 'azd-service-name': 'appService' })
    appServicePlanId: appServicePlan.id
    applicationInsightsName: applicationInsights.outputs.name
    azureDbConnectionString: azureDbConnectionString
    coinmarketcapApiKeyKVUri: coinmarketcapApiKeyKVUri
    tenantId: tenant().tenantId
    identityProviderName: identityProviderName
    identityProviderClientId: identityProviderClientId
  }
}

output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output APP_SERVICE_URI string = appService.outputs.uri

