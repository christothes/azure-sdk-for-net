[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param (
    # Captures any arguments from eng/New-TestResources.ps1 not declared here (no parameter errors).
    [Parameter(ValueFromRemainingArguments = $true)]
    $RemainingArguments,

    [Parameter()]
    $ResourceGroupName,

    [Parameter()]
    [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
    [string] $TestApplicationId,

    [Parameter()]
    [string] $TestApplicationSecret,

    [Parameter()]
    [string] $BaseName,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $TenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47"
)

Import-Module -Name $PSScriptRoot/../../eng/common/scripts/X509Certificate2 -Verbose

ssh-keygen -t rsa -b 4096 -f $PSScriptRoot/sshKey -N '' -C ''
$sshKey = Get-Content $PSScriptRoot/sshKey.pub

$templateFileParameters['sshPubKey'] = $sshKey

az login --service-principal -u $TestApplicationId -p $TestApplicationSecret --tenant $TenantId
# get the resource group location from the $ResourceGroupName
# $location = az group show --name $ResourceGroupName --query location --output tsv
# $cert = New-X509Certificate2 -SubjectName 'E=opensource@microsoft.com, CN=Azure SDK, OU=Azure SDK, O=Microsoft, L=Frisco, S=TX, C=US' -ValidDays 365
# $templateFileParameters['sfCertThunbprint'] = $cert.Thumbprint
# $CertFileFullPath = "$PSScriptRoot/cfCert.pfx"
# $SecurePassword = ConvertTo-SecureString -String $TestApplicationSecret -AsPlainText -Force
# Export-PfxCertificate -FilePath $CertFileFullPath -Password $SecurePassword -Cert $cert
# $Bytes = [System.IO.File]::ReadAllBytes($CertFileFullPath)
# $Base64 = [System.Convert]::ToBase64String($Bytes)
# # Cleanup the cert on disk
# Remove-Item -Path $CertFileFullPath

# $JSONBlob = @{
#     data = $Base64
#     dataType = 'pfx'
#     password = $Password
# } | ConvertTo-Json
# $ContentBytes = [System.Text.Encoding]::UTF8.GetBytes($JSONBlob)
# $Content = [System.Convert]::ToBase64String($ContentBytes)
# $templateFileParameters['sfCertSecretValue'] = $Content
# $kv = az keyvault show --name $BaseName --resource-group $BaseName
# if $kv contains 'not found within subscription' then create the keyvault
# if ($kv -like '*not found within subscription*') {
    # Write-Host "Creating keyvault $BaseName in resource group $BaseName in location $location"
    # az keyvault create --name $BaseName --resource-group $BaseName --location $location
    # $policy = az keyvault certificate get-default-policy -o json
    # Set-Content -Value $policy -Path $PSScriptRoot\kvpolicy.json
    # $cert = az keyvault certificate create --vault-name $BaseName -n cert1 -p "@$PSScriptRoot\kvpolicy.json"
    # Remove-Item -Path $PSScriptRoot\kvpolicy.json
    # # Wait for the cert to be created
    # Start-Sleep -Seconds 5
# }
# else {
    # Write-Host "Keyvault $BaseName already exists"
# }
# $thumbprint = az keyvault certificate list --vault-name t56c180479338b12b2 --query "[].x509ThumbprintHex" --output tsv
# $certId = az keyvault certificate list --vault-name t56c180479338b12b2 --query "[].x509ThumbprintHex" --output tsv
# $templateFileParameters['sfCertThumbprint'] = $thumbprint
# $templateFileParameters['sfCertUri'] = $certId

# Get the max version that is not preview and then get the name of the patch version with the max value
$versions = az aks get-versions -l westus -o json | ConvertFrom-Json
Write-Host "AKS versions: $($versions | ConvertTo-Json -Depth 100)"
$patchVersions = $versions.values | Where-Object { $_.isPreview -eq $null } | Select-Object -ExpandProperty patchVersions
Write-Host "AKS patch versions: $($patchVersions | ConvertTo-Json -Depth 100)"
$latestAksVersion = $patchVersions | Get-Member -MemberType NoteProperty | Select-Object -ExpandProperty Name | Sort-Object -Descending | Select-Object -First 1
Write-Host "Latest AKS version: $latestAksVersion"
$templateFileParameters['latestAksVersion'] = $latestAksVersion

if (!$CI) {
    # TODO: Remove this once auto-cloud config downloads are supported locally
    Write-Host "Skipping cert setup in local testing mode"
    return
}

if ($EnvironmentVariables -eq $null -or $EnvironmentVariables.Count -eq 0) {
    throw "EnvironmentVariables must be set in the calling script New-TestResources.ps1"
}

$tmp = $env:TEMP ? $env:TEMP : [System.IO.Path]::GetTempPath()
$pfxPath = Join-Path $tmp "test.pfx"
$pemPath = Join-Path $tmp "test.pem"
$sniPath = Join-Path $tmp "testsni.pfx"

Write-Host "Creating identity test files: $pfxPath $pemPath $sniPath"

[System.Convert]::FromBase64String($EnvironmentVariables['PFX_CONTENTS']) | Set-Content -Path $pfxPath -AsByteStream
Set-Content -Path $pemPath -Value $EnvironmentVariables['PEM_CONTENTS']
[System.Convert]::FromBase64String($EnvironmentVariables['SNI_CONTENTS']) | Set-Content -Path $sniPath -AsByteStream

# Set for pipeline
Write-Host "##vso[task.setvariable variable=IDENTITY_SP_CERT_PFX;]$pfxPath"
Write-Host "##vso[task.setvariable variable=IDENTITY_SP_CERT_PEM;]$pemPath"
Write-Host "##vso[task.setvariable variable=IDENTITY_SP_CERT_SNI;]$sniPath"
# Set for local
$env:IDENTITY_SP_CERT_PFX = $pfxPath
$env:IDENTITY_SP_CERT_PEM = $pemPath
$env:IDENTITY_SP_CERT_SNI = $sniPath