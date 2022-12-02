param (
    [hashtable] $DeploymentOutputs
    # [string] $TenantId,
    # [string] $TestApplicationId,
    # [string] $TestApplicationSecret
)

Write-Host $DeploymentOutputs['IDENTITY_WEBAPP_USER_DEFINED_IDENTITY']
dotnet publish ./sdk/identity/Azure.Identity/tests/WebApp/WebApp.csproj -o ./sdk/identity/Azure.Identity/tests/WebApp/Pub
Compress-Archive -Path ./sdk/identity/Azure.Identity/tests/WebApp/Pub/* -DestinationPath ./sdk/identity/Azure.Identity/tests/WebApp/Pub/package.zip  -Force
az webapp deploy --resource-group $DeploymentOutputs['IDENTITY_RESOURCE_GROUP'] --name $DeploymentOutputs['IDENTITY_WEBAPP_NAME'] --src-path ./sdk/identity/Azure.Identity/tests/WebApp/Pub/package.zip
