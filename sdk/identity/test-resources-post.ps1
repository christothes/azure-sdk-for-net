param (
  [hashtable] $DeploymentOutputs
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$webappRoot = "$PSScriptRoot/Azure.Identity/integration" | Resolve-Path
$workingFolder = $webappRoot;
if ($null -ne $Env:AGENT_WORKFOLDER) {
  $workingFolder = $Env:AGENT_WORKFOLDER
}
az login --service-principal -u $DeploymentOutputs['IDENTITY_CLIENT_ID'] -p $DeploymentOutputs['IDENTITY_CLIENT_SECRET'] --tenant $DeploymentOutputs['IDENTITY_TENANT_ID']
az account set --subscription $DeploymentOutputs['IDENTITY_SUBSCRIPTION_ID']

$baseName = $DeploymentOutputs['IDENTITY_BASE_NAME']
$resourceGroupName = $DeploymentOutputs['IDENTITY_RESOURCE_GROUP_NAME']
$location = az group show --name $resourceGroupName --query location --output tsv
$policy = az keyvault certificate get-default-policy -o json
Set-Content -Value $policy -Path $PSScriptRoot\kvpolicy.json
$cert = az keyvault certificate create --vault-name $BaseName -n cert1 -p "@$PSScriptRoot\kvpolicy.json"
Remove-Item -Path $PSScriptRoot\kvpolicy.json
# Wait for the cert to be created
Start-Sleep -Seconds 5
$thumbprint = az keyvault certificate list --vault-name $baseName --query "[].x509ThumbprintHex" --output tsv
Write-Host "thumbprint: $thumbprint"

az sf cluster client-certificate add -g $baseName -c $baseName --thumbprint $thumbprint
az sf managed-node-type create -g $baseName -c $baseName -n 'testNodes' --instance-count 5 --primary

# }
# else {
# Write-Host "Keyvault $BaseName already exists"
# }
# declare CertSubjectName="mylinux.westus.cloudapp.azure.com"
# declare vmpassword="Password!1"
# declare certpassword="Password!4321"
# declare vmuser="myadmin"
# declare vmOs="UbuntuServer1804"
# declare certOutputFolder="c:\certificates"

# az sf cluster create --resource-group $baseName --location $location--certificate-output-folder $workingFolder --certificate-password $DeploymentOutputs['IDENTITY_CLIENT_SECRET'] --vault-name $baseName --vault-resource-group $resourceGroupName --template-file $templateFilePath --parameter-file $parametersFilePath --vm-os $vmOs --vm-password $vmpassword --vm-user-name $vmuser

# Deploy the webapp
dotnet publish "$webappRoot/WebApp/Integration.Identity.WebApp.csproj" -o "$workingFolder/Pub" /p:EnableSourceLink=false
Compress-Archive -Path "$workingFolder/Pub/*" -DestinationPath "$workingFolder/Pub/package.zip" -Force
az webapp deploy --resource-group $DeploymentOutputs['IDENTITY_RESOURCE_GROUP'] --name $DeploymentOutputs['IDENTITY_WEBAPP_NAME'] --src-path "$workingFolder/Pub/package.zip"

# clean up
Remove-Item -Force -Recurse "$workingFolder/Pub"

# Deploy the function app
dotnet publish "$webappRoot/Integration.Identity.Func/Integration.Identity.Func.csproj" -o "$workingFolder/Pub" /p:EnableSourceLink=false
Compress-Archive -Path "$workingFolder/Pub/*" -DestinationPath "$workingFolder/Pub/package.zip" -Force
az functionapp deployment source config-zip -g $DeploymentOutputs['IDENTITY_RESOURCE_GROUP'] -n $DeploymentOutputs['IDENTITY_FUNCTION_NAME'] --src "$workingFolder/Pub/package.zip"

# clean up
Remove-Item -Force -Recurse "$workingFolder/Pub"

$containerImage = 'azsdkengsys.azurecr.io/dotnet/ubuntu_netcore_keyring:3080193'
$MIClientId = $DeploymentOutputs['IDENTITY_USER_DEFINED_IDENTITY_CLIENT_ID']
$MIName = $DeploymentOutputs['IDENTITY_USER_DEFINED_IDENTITY_NAME']
$SaAccountName = 'workload-identity-sa'
$PodName = $DeploymentOutputs['IDENTITY_AKS_POD_NAME']

if ($IsMacOS -eq $true) {
  # Not supported on MacOS agents
  az logout
  return
}
# Get the aks cluster credentials
Write-Host "Getting AKS credentials"
az aks get-credentials --resource-group $DeploymentOutputs['IDENTITY_RESOURCE_GROUP'] --name $DeploymentOutputs['IDENTITY_AKS_CLUSTER_NAME']

#Get the aks cluster OIDC issuer
Write-Host "Getting AKS OIDC issuer"
$AKS_OIDC_ISSUER = az aks show -n $DeploymentOutputs['IDENTITY_AKS_CLUSTER_NAME'] -g $DeploymentOutputs['IDENTITY_RESOURCE_GROUP'] --query "oidcIssuerProfile.issuerUrl" -otsv

# Create the federated identity
Write-Host "Creating federated identity"
az identity federated-credential create --name $MIName --identity-name $MIName --resource-group $DeploymentOutputs['IDENTITY_RESOURCE_GROUP'] --issuer $AKS_OIDC_ISSUER --subject system:serviceaccount:default:workload-identity-sa

# Build the kubernetes deployment yaml
$kubeConfig = @"
apiVersion: v1
kind: ServiceAccount
metadata:
  annotations:
    azure.workload.identity/client-id: $MIClientId
  name: $SaAccountName
  namespace: default
---
apiVersion: v1
kind: Pod
metadata:
  name: $PodName
  namespace: default
  labels:
    azure.workload.identity/use: "true"
spec:
  serviceAccountName: $SaAccountName
  containers:
  - name: $PodName
    image: $containerImage
    env:
    - name: AZURE_TEST_MODE
      value: "LIVE"
    - name: IS_RUNNING_IN_IDENTITY_CLUSTER
      value: "true"
    command: ["tail"]
    args: ["-f", "/dev/null"]
    ports:
    - containerPort: 80
  nodeSelector:
    kubernetes.io/os: linux
"@

Set-Content -Path "$workingFolder/kubeconfig.yaml" -Value $kubeConfig
Write-Host "Created kubeconfig.yaml with contents:"
Write-Host $kubeConfig

# Apply the config
kubectl apply -f "$workingFolder/kubeconfig.yaml" --overwrite=true
Write-Host "Applied kubeconfig.yaml"
az logout