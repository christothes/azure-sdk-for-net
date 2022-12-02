param (
    [hashtable] $DeploymentOutputs
    # [string] $TenantId,
    # [string] $TestApplicationId,
    # [string] $TestApplicationSecret
)

Write-Host $DeploymentOutputs['IDENTITY_WEBAPP_USER_DEFINED_IDENTITY']
Write-Host $DeploymentOutputs['IDENTITY_STORAGE_NAME_2']
dotnet publish ./sdk/identity/Azure.Identity/tests/WebApp/WebApp.csproj -o ./artifacts/bin/Azure.Identity.Tests/WebApp/Pub
Compress-Archive -Path ./artifacts/bin/Azure.Identity.Tests/WebApp/Pub* -DestinationPath ./artifacts/bin/Azure.Identity.Tests/WebApp/Pub/package.zip  -Force
az webapp deploy --resource-group $DeploymentOutputs['IDENTITY_RESOURCE_GROUP'] --name $DeploymentOutputs['IDENTITY_WEBAPP_NAME'] --src-path ./artifacts/bin/Azure.Identity.Tests/WebApp/Pub/package.zip
