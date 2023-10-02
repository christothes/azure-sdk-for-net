param testApplicationOid string

@secure()
param testApplicationSecret string

@minLength(6)
@maxLength(50)
@description('The base resource name.')
param baseName string = resourceGroup().name

@description('The location of the resource. By default, this is the same as the resource group.')
param location string = resourceGroup().location

// param sfCertThunbprint string

param adminUserName string

var ManagedIdentityOperator = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'f1a07417-d97a-45cb-824c-7a7467783830')
var Owner = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8e3af657-a8ff-443c-a75c-2fe8c4bcb635')

resource usermgdid 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: baseName
}

resource SfRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: usermgdid
  name: guid(resourceGroup().id, ManagedIdentityOperator, usermgdid.id)
  properties: {
    principalId: usermgdid.properties.principalId
    roleDefinitionId: ManagedIdentityOperator
    principalType: 'ServicePrincipal'
  }
}

resource ownerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sf
  name: guid(resourceGroup().id, Owner)
  properties: {
    principalId: testApplicationOid
    roleDefinitionId: Owner
    principalType: 'ServicePrincipal'
  }
}

resource sf 'Microsoft.ServiceFabric/managedClusters@2023-07-01-preview' = {
  name: baseName
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    adminUserName: adminUserName
    adminPassword: testApplicationSecret
    azureActiveDirectory: {
      tenantId: subscription().tenantId
      clusterApplication: testApplicationOid
      clientApplication: testApplicationOid
    }
    dnsName: baseName
    clientConnectionPort: 19000
    httpGatewayConnectionPort: 19080
    // clients: [
    //   {
    //     isAdmin: true
    //     thumbprint: sfCertThunbprint
    //   }
    // ]
    addonFeatures: [
      'DnsService'
    ]
    enableAutoOSUpgrade: false
    zonalResiliency: false
    useCustomVnet: false
    zonalUpdateMode: 'Standard'
  }
}

// resource nodeType 'Microsoft.ServiceFabric/managedClusters/nodeTypes@2023-07-01-preview' = {
//   name: 'default'
//   parent: sf
//   properties: {
//     isPrimary: true
//     vmImagePublisher: 'MicrosoftWindowsServer'
//     vmImageOffer: 'WindowsServer'
//     vmImageSku: '2022-Datacenter'
//     vmImageVersion: 'latest'
//     vmSize: 'Standard_D2_v2'
//     vmInstanceCount: 5
//     dataDiskSizeGB: 256
//     dataDiskType: 'Standard_LRS'
//     dataDiskLetter: 'S'
//     applicationPorts: {
//       startPort: 20000
//       endPort: 30000
//     }
//     ephemeralPorts: {
//       startPort: 49152
//       endPort: 65534
//     }
//     isStateless: false
//     multiplePlacementGroups: false
//     enableOverProvisioning: false
//     enableEncryptionAtHost: false
//     enableAcceleratedNetworking: false
//     useTempDataDisk: false
//     enableNodePublicIP: false
//     vmManagedIdentity: {
//       userAssignedIdentities: [
//         usermgdid.id
//       ]
//     }
//   }
// }

output IDENTITY_SERVICE_FABRIC_FQDN string = sf.properties.fqdn
