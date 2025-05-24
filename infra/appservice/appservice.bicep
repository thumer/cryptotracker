@description('Name of the app service')
param name string
param location string = resourceGroup().location
param tags object = {}
param appServicePlanId string

param kind string = 'app,linux'
param alwaysOn bool = true

param linuxFxVersion string = 'DOTNETCORE|9.0'

param applicationInsightsName string

@secure()
param azureDbConnectionString string

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
  scope: resourceGroup()
}

resource appService 'Microsoft.Web/sites@2022-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  kind: kind
  properties: {
    serverFarmId: appServicePlanId
    siteConfig: {
        appSettings: [
          {
            name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
            value: applicationInsights.properties.InstrumentationKey
          }
          {
            name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
            value: applicationInsights.properties.ConnectionString
          }
        ]
        connectionStrings: [
          {
            connectionString: azureDbConnectionString
            name: 'DefaultConnection'
            type: 'SQLAzure'
          }
        ]
      alwaysOn: alwaysOn
      linuxFxVersion: linuxFxVersion
      appCommandLine: 'dotnet CryptoTracker.dll'
    }
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
  }
}

var monitoringMetricsPublisherRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'
resource monitoringMetricsPublisher 'Microsoft.Authorization/roleDefinitions@2018-01-01-preview' existing = {
  scope: resourceGroup()
  name: monitoringMetricsPublisherRoleId
}

resource roleAssignmentMonitoringMetricsPublisher 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(name, monitoringMetricsPublisherRoleId)
  scope: applicationInsights
  properties: {
    roleDefinitionId: monitoringMetricsPublisher.id
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output name string = appService.name
output uri string = 'https://${appService.properties.defaultHostName}'

