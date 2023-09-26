@description('The client OID to grant access to test resources.')
param testApplicationOid string

@minLength(6)
@maxLength(50)
@description('The base resource name.')
param baseName string = resourceGroup().name

@description('The location of the resource. By default, this is the same as the resource group.')
param location string = resourceGroup().location

param sshPubKey string

param adminUserName string = 'azureuser'

param latestAksVersion string

//See https://docs.microsoft.com/en-us/azure/role-based-access-control/built-in-roles
var blobContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') //Storage Blob Data Contributor
var websiteContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'de139f84-1756-47ae-9be6-808fbbe84772') //Website Contributor
// cluster parameters
var kubernetesVersion = latestAksVersion

resource usermgdid 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: baseName
  location: location
}

resource blobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sa
  name: guid(resourceGroup().id, blobContributor)
  properties: {
    principalId: web.identity.principalId
    roleDefinitionId: blobContributor
    principalType: 'ServicePrincipal'
  }
}

resource blobRoleFunc 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sa
  name: guid(resourceGroup().id, blobContributor, 'azfunc')
  properties: {
    principalId: azfunc.identity.principalId
    roleDefinitionId: blobContributor
    principalType: 'ServicePrincipal'
  }
}

resource blobRole2 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sa2
  name: guid(resourceGroup().id, blobContributor, usermgdid.id)
  properties: {
    principalId: usermgdid.properties.principalId
    roleDefinitionId: blobContributor
    principalType: 'ServicePrincipal'
  }
}

resource webRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: web
  name: guid(resourceGroup().id, websiteContributor)
  properties: {
    principalId: testApplicationOid
    roleDefinitionId: websiteContributor
    principalType: 'ServicePrincipal'
  }
}

resource sa 'Microsoft.Storage/storageAccounts@2021-08-01' = {
  name: baseName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
  }
}

resource sa2 'Microsoft.Storage/storageAccounts@2021-08-01' = {
  name: '${baseName}2'
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
  }
}

resource farm 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: '${baseName}_asp'
  location: location
  sku: {
    name: 'B1'
    tier: 'Bassic'
    size: 'B1'
    family: 'B'
    capacity: 1
  }
  properties: { }
  kind: 'app'
}

resource azfunc 'Microsoft.Web/sites@2021-03-01' = {
  name: '${baseName}func'
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${usermgdid.id}' : { }
    }
  }
  properties: {
    enabled: true
    serverFarmId: farm.id
    httpsOnly: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      alwaysOn: true
      netFrameworkVersion: 'v6.0'
      http20Enabled: true
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'IDENTITY_STORAGE_NAME_1'
          value: sa.name
        }
        {
          name: 'IDENTITY_STORAGE_NAME_2'
          value: sa2.name
        }
        {
          name: 'IDENTITY_USER_DEFINED_IDENTITY'
          value: usermgdid.id
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${sa.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${sa.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${sa.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${sa.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower('${baseName}-func')
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }
      ]
    }
  }
}

resource web 'Microsoft.Web/sites@2021-03-01' = {
  name: '${baseName}webapp'
  location: location
  kind: 'app'
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${usermgdid.id}' : { }
    }
  }
  properties: {
    enabled: true
    serverFarmId: farm.id
    httpsOnly: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      netFrameworkVersion: 'v6.0'
      http20Enabled: true
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AZURE_REGIONAL_AUTHORITY_NAME'
          value: 'eastus'
        }
        {
          name: 'IDENTITY_STORAGE_NAME_1'
          value: sa.name
        }
        {
          name: 'IDENTITY_STORAGE_NAME_2'
          value: sa2.name
        }
        {
          name: 'IDENTITY_USER_DEFINED_IDENTITY'
          value: usermgdid.id
        }
      ]
    }
  }
}

resource scmweb 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2022-09-01' = {
  kind: 'app'
  parent: web
  name: 'scm'
  properties: {
    allow: true
  }
}

resource scmfunc 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2022-09-01' = {
  kind: 'functionapp'
  parent: azfunc
  name: 'scm'
  properties: {
    allow: true
  }
}

resource newCluster 'Microsoft.ContainerService/managedClusters@2023-06-01' = {
  name: baseName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    kubernetesVersion: kubernetesVersion
    enableRBAC: true
    dnsPrefix: 'identitytest'
    agentPoolProfiles: [
      {
        name: 'agentpool'
        count: 1
        vmSize: 'Standard_D2s_v3'
        osDiskSizeGB: 128
        osDiskType: 'Managed'
        kubeletDiskType: 'OS'
        type: 'VirtualMachineScaleSets'
        enableAutoScaling: false
        orchestratorVersion: kubernetesVersion
        mode: 'System'
        osType: 'Linux'
        osSKU: 'Ubuntu'
      }
    ]
    linuxProfile: {
      adminUsername: adminUserName
      ssh: {
        publicKeys: [
          {
            keyData: sshPubKey
          }
        ]
      }
    }
    oidcIssuerProfile: {
      enabled: true
    }
    securityProfile: {
      workloadIdentity: {
        enabled: true
      }
    }
  }
}

// Servic Fabric resources
var tenantId = subscription().tenantId
var clusterName = baseName
var dnsName = clusterName
var vmName = 'vm'
var nodeTypeSize = 'Standard_D2_v3'
var virtualNetworkName = 'VNet'
var addressPrefix = '10.0.0.0/16'
var nicName = 'NIC'
var lbIPName = 'PublicIP-LB-FE'
var overProvision = false
var nt0applicationStartPort = 20000
var nt0applicationEndPort = 30000
var nt0ephemeralStartPort = 49152
var nt0ephemeralEndPort = 65534
var nt0fabricTcpGatewayPort = 19000
var nt0fabricHttpGatewayPort = 19080
var loadBalancedAppPort1 = 80
var loadBalancedAppPort2 = 8081
var subnet0Name = 'Subnet-0'
var subnet0Prefix = '10.0.0.0/24'
var subnet0Ref = resourceId('Microsoft.Network/virtualNetworks/subnets/', virtualNetworkName, subnet0Name)
var supportLogStorageAccountName = '${uniqueString(resourceGroup().id)}2'
var applicationDiagnosticsStorageAccountName = '${uniqueString(resourceGroup().id)}3'
var lbName = 'LB-${clusterName}-${vmNodeType0Name}'
var lbIPConfig0 = resourceId('Microsoft.Network/loadBalancers/frontendIPConfigurations/', lbName, 'LoadBalancerIPConfig')
var lbPoolID0 = resourceId('Microsoft.Network/loadBalancers/backendAddressPools', lbName, 'LoadBalancerBEAddressPool')
var lbProbeID0 = resourceId('Microsoft.Network/loadBalancers/probes', lbName, 'FabricGatewayProbe')
var lbHttpProbeID0 = resourceId('Microsoft.Network/loadBalancers/probes', lbName, 'FabricHttpGatewayProbe')
var lbNatPoolID0 = resourceId('Microsoft.Network/loadBalancers/inboundNatPools', lbName, 'LoadBalancerBEAddressNatPool')
var vmNodeType0Name = toLower('NT1${vmName}')
var vmNodeType0Size = nodeTypeSize
var nodeDataDrive = 'Temp'
var certificateStoreValue = 'My'

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2021-08-01' = {
  name: virtualNetworkName
  location: location
  tags: {
    resourceType: 'Service Fabric'
    clusterName: clusterName
  }
  properties: {
    addressSpace: {
      addressPrefixes: [
        addressPrefix
      ]
    }
    subnets: [
      {
        name: subnet0Name
        properties: {
          addressPrefix: subnet0Prefix
        }
      }
    ]
  }
}

resource lbIP 'Microsoft.Network/publicIPAddresses@2021-08-01' = {
  name: lbIPName
  location: location
  tags: {
    resourceType: 'Service Fabric'
    clusterName: clusterName
  }
  properties: {
    dnsSettings: {
      domainNameLabel: dnsName
    }
    publicIPAllocationMethod: 'Dynamic'
  }
}

resource lb 'Microsoft.Network/loadBalancers@2021-08-01' = {
  name: lbName
  location: location
  tags: {
    resourceType: 'Service Fabric'
    clusterName: clusterName
  }
  properties: {
    frontendIPConfigurations: [
      {
        name: 'LoadBalancerIPConfig'
        properties: {
          publicIPAddress: {
            id: lbIP.id
          }
        }
      }
    ]
    backendAddressPools: [
      {
        name: 'LoadBalancerBEAddressPool'
        properties: {}
      }
    ]
    loadBalancingRules: [
      {
        name: 'LBRule'
        properties: {
          backendAddressPool: {
            id: lbPoolID0
          }
          backendPort: nt0fabricTcpGatewayPort
          enableFloatingIP: false
          frontendIPConfiguration: {
            id: lbIPConfig0
          }
          frontendPort: nt0fabricTcpGatewayPort
          idleTimeoutInMinutes: 5
          probe: {
            id: lbProbeID0
          }
          protocol: 'Tcp'
        }
      }
      {
        name: 'LBHttpRule'
        properties: {
          backendAddressPool: {
            id: lbPoolID0
          }
          backendPort: nt0fabricHttpGatewayPort
          enableFloatingIP: false
          frontendIPConfiguration: {
            id: lbIPConfig0
          }
          frontendPort: nt0fabricHttpGatewayPort
          idleTimeoutInMinutes: 5
          probe: {
            id: lbHttpProbeID0
          }
          protocol: 'Tcp'
        }
      }
      {
        name: 'AppPortLBRule1'
        properties: {
          backendAddressPool: {
            id: lbPoolID0
          }
          backendPort: loadBalancedAppPort1
          enableFloatingIP: false
          frontendIPConfiguration: {
            id: lbIPConfig0
          }
          frontendPort: loadBalancedAppPort1
          idleTimeoutInMinutes: 5
          probe: {
            id: resourceId('Microsoft.Network/loadBalancers/probes', lbName, 'AppPortProbe1')
          }
          protocol: 'Tcp'
        }
      }
      {
        name: 'AppPortLBRule2'
        properties: {
          backendAddressPool: {
            id: lbPoolID0
          }
          backendPort: loadBalancedAppPort2
          enableFloatingIP: false
          frontendIPConfiguration: {
            id: lbIPConfig0
          }
          frontendPort: loadBalancedAppPort2
          idleTimeoutInMinutes: 5
          probe: {
            id: resourceId('Microsoft.Network/loadBalancers/probes', lbName, 'AppPortProbe2')
          }
          protocol: 'Tcp'
        }
      }
    ]
    probes: [
      {
        name: 'FabricGatewayProbe'
        properties: {
          intervalInSeconds: 5
          numberOfProbes: 2
          port: nt0fabricTcpGatewayPort
          protocol: 'Tcp'
        }
      }
      {
        name: 'FabricHttpGatewayProbe'
        properties: {
          intervalInSeconds: 5
          numberOfProbes: 2
          port: nt0fabricHttpGatewayPort
          protocol: 'Tcp'
        }
      }
      {
        name: 'AppPortProbe1'
        properties: {
          intervalInSeconds: 5
          numberOfProbes: 2
          port: loadBalancedAppPort1
          protocol: 'Tcp'
        }
      }
      {
        name: 'AppPortProbe2'
        properties: {
          intervalInSeconds: 5
          numberOfProbes: 2
          port: loadBalancedAppPort2
          protocol: 'Tcp'
        }
      }
    ]
    inboundNatPools: [
      {
        name: 'LoadBalancerBEAddressNatPool'
        properties: {
          backendPort: 3389
          frontendIPConfiguration: {
            id: lbIPConfig0
          }
          frontendPortRangeEnd: 4500
          frontendPortRangeStart: 3389
          protocol: 'Tcp'
        }
      }
    ]
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: baseName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
  }
}

resource vmNodeType0 'Microsoft.Compute/virtualMachineScaleSets@2021-11-01' = {
  name: vmNodeType0Name
  location: location
  sku: {
    name: vmNodeType0Size
    capacity: 2
    tier: 'Standard'
  }
  tags: {
    resourceType: 'Service Fabric'
    clusterName: clusterName
  }
  properties: {
    overprovision: overProvision
    upgradePolicy: {
      mode: 'Automatic'
    }
    virtualMachineProfile: {
      extensionProfile: {
        extensions: [
          {
            name: 'ServiceFabricNodeVmExt_vmNodeType0Name'
            properties: {
              type: 'ServiceFabricNode'
              autoUpgradeMinorVersion: true
              protectedSettings: {
                StorageAccountKey1:  sa.listKeys().keys[0].value
                StorageAccountKey2: sa.listkeys().keys[1].value
              }
              publisher: 'Microsoft.Azure.ServiceFabric'
              settings: {
                clusterEndpoint: cluster.properties.clusterEndpoint
                nodeTypeRef: vmNodeType0Name
                dataPath: '${((nodeDataDrive == 'OS') ? 'C' : 'D')}:\\\\SvcFab'
                durabilityLevel: 'Silver'
                nicPrefixOverride: subnet0Prefix
                certificate: {
                  thumbprint: certificateThumbprint
                  x509StoreName: certificateStoreValue
                }
              }
              typeHandlerVersion: '1.0'
            }
          }
          {
            name: 'VMDiagnosticsVmExt_vmNodeType0Name'
            properties: {
              type: 'IaaSDiagnostics'
              autoUpgradeMinorVersion: true
              protectedSettings: {
                storageAccountName: applicationDiagnosticsStorageAccountName
                storageAccountKey: listKeys(sa2.id, '2021-01-01').keys[0].value
                storageAccountEndPoint: 'https://${environment().suffixes.storage}'
              }
              publisher: 'Microsoft.Azure.Diagnostics'
              settings: {
                WadCfg: {
                  DiagnosticMonitorConfiguration: {
                    overallQuotaInMB: '50000'
                    EtwProviders: {
                      EtwEventSourceProviderConfiguration: [
                        {
                          provider: 'Microsoft-ServiceFabric-Actors'
                          scheduledTransferKeywordFilter: '1'
                          scheduledTransferPeriod: 'PT5M'
                          DefaultEvents: {
                            eventDestination: 'ServiceFabricReliableActorEventTable'
                          }
                        }
                        {
                          provider: 'Microsoft-ServiceFabric-Services'
                          scheduledTransferPeriod: 'PT5M'
                          DefaultEvents: {
                            eventDestination: 'ServiceFabricReliableServiceEventTable'
                          }
                        }
                      ]
                      EtwManifestProviderConfiguration: [
                        {
                          provider: 'cbd93bc2-71e5-4566-b3a7-595d8eeca6e8'
                          scheduledTransferLogLevelFilter: 'Information'
                          scheduledTransferKeywordFilter: '4611686018427387904'
                          scheduledTransferPeriod: 'PT5M'
                          DefaultEvents: {
                            eventDestination: 'ServiceFabricSystemEventTable'
                          }
                        }
                      ]
                    }
                  }
                }
                StorageAccount: applicationDiagnosticsStorageAccountName
              }
              typeHandlerVersion: '1.5'
            }
          }
        ]
      }
      networkProfile: {
        networkInterfaceConfigurations: [
          {
            name: '${nicName}-0'
            properties: {
              ipConfigurations: [
                {
                  name: '${nicName}-0'
                  properties: {
                    loadBalancerBackendAddressPools: [
                      {
                        id: lbPoolID0
                      }
                    ]
                    loadBalancerInboundNatPools: [
                      {
                        id: lbNatPoolID0
                      }
                    ]
                    subnet: {
                      id: subnet0Ref
                    }
                  }
                }
              ]
              primary: true
            }
          }
        ]
      }
      osProfile: {
        adminPassword: adminPassword
        adminUsername: adminUsername
        computerNamePrefix: vmNodeType0Name
        secrets: [
          {
            sourceVault: {
              id: keyVault.id
            }
            vaultCertificates: [
              {
                certificateStore: certificateStoreValue
                certificateUrl: certificateUrlValue
              }
            ]
          }
        ]
      }
      storageProfile: {
        imageReference: {
          publisher: vmImagePublisher
          offer: vmImageOffer
          sku: vmImageSku
          version: vmImageVersion
        }
        osDisk: {
          managedDisk: {
            storageAccountType: 'StandardSSD_LRS'
          }
          caching: 'ReadOnly'
          createOption: 'FromImage'
        }
      }
    }
  }
  dependsOn: [
    virtualNetwork
    lb
  ]
}

resource cluster 'Microsoft.ServiceFabric/clusters@2021-06-01' = {
  name: clusterName
  location: location
  tags: {
    resourceType: 'Service Fabric'
    clusterName: clusterName
  }
  properties: {
    azureActiveDirectory: {
      clientApplication: clientapplication
      clusterApplication: clusterApplication
      tenantId: tenantId
    }
    certificate: {
      thumbprint: certificateThumbprint
      x509StoreName: certificateStoreValue
    }
    diagnosticsStorageAccountConfig: {
      blobEndpoint: reference(sa.id, '2021-01-01').primaryEndpoints.blob
      protectedAccountKeyName: 'StorageAccountKey1'
      queueEndpoint: reference(sa.id, '2021-01-01').primaryEndpoints.queue
      storageAccountName: supportLogStorageAccountName
      tableEndpoint: reference(sa.id, '2021-01-01').primaryEndpoints.table
    }
    fabricSettings: [
      {
        parameters: [
          {
            name: 'ClusterProtectionLevel'
            value: 'EncryptAndSign'
          }
        ]
        name: 'Security'
      }
    ]
    managementEndpoint: 'https://${lbIP.properties.dnsSettings.fqdn}:${nt0fabricHttpGatewayPort}'
    nodeTypes: [
      {
        name: vmNodeType0Name
        applicationPorts: {
          endPort: nt0applicationEndPort
          startPort: nt0applicationStartPort
        }
        clientConnectionEndpointPort: nt0fabricTcpGatewayPort
        durabilityLevel: 'Bronze'
        ephemeralPorts: {
          endPort: nt0ephemeralEndPort
          startPort: nt0ephemeralStartPort
        }
        httpGatewayEndpointPort: nt0fabricHttpGatewayPort
        isPrimary: true
        vmInstanceCount: 1
      }
    ]
    reliabilityLevel: 'Bronze'
    upgradeMode: 'Automatic'
    vmImage: 'Windows'
  }
}


output IDENTITY_WEBAPP_NAME string = web.name
output IDENTITY_USER_DEFINED_IDENTITY string = usermgdid.id
output IDENTITY_USER_DEFINED_IDENTITY_CLIENT_ID string = usermgdid.properties.clientId
output IDENTITY_USER_DEFINED_IDENTITY_NAME string = usermgdid.name
output IDENTITY_AKS_CLUSTER_NAME string = newCluster.name
output IDENTITY_AKS_POD_NAME string = 'dotnet-test-app'
output IDENTITY_STORAGE_NAME_1 string = sa.name
output IDENTITY_STORAGE_NAME_2 string = sa2.name
output IDENTITY_FUNCTION_NAME string = azfunc.name
