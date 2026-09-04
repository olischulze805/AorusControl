[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
$expectedHash = '27DC01AEF90D9AC7FBD460E292ED9DC85575B77D8225E14569FC8500A34E5AA2'
$sourcePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\research\vendor\acpimof.dll'))
$targetPath = Join-Path $env:windir 'System32\acpimof.dll'
$registryPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\WmiAcpi'
$registryValue = '\SystemRoot\System32\acpimof.dll'
$stateDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\research\state'))
$statePath = Join-Path $stateDirectory 'wmi-provider-backup.json'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell terminal.'
}

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Provider DLL not found: $sourcePath"
}

$actualHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
if ($actualHash -ne $expectedHash) {
    throw "Provider DLL hash mismatch. Expected $expectedHash, got $actualHash."
}

$signature = Get-AuthenticodeSignature -LiteralPath $sourcePath
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
    throw "Provider DLL signature is not valid: $($signature.Status)"
}

if ($signature.SignerCertificate.Subject -notmatch 'GIGA-BYTE TECHNOLOGY') {
    throw "Unexpected provider signer: $($signature.SignerCertificate.Subject)"
}

New-Item -ItemType Directory -Force -Path $stateDirectory | Out-Null
$existingProperty = Get-ItemProperty -LiteralPath $registryPath -Name MofImagePath -ErrorAction SilentlyContinue
$targetExisted = Test-Path -LiteralPath $targetPath -PathType Leaf
$backup = [ordered]@{
    CreatedAt = [DateTimeOffset]::Now.ToString('o')
    RegistryValueExisted = $null -ne $existingProperty
    RegistryValue = if ($null -ne $existingProperty) { $existingProperty.MofImagePath } else { $null }
    TargetFileExisted = $targetExisted
    TargetFileSha256 = if ($targetExisted) { (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash } else { $null }
}

if ($targetExisted) {
    Copy-Item -LiteralPath $targetPath -Destination (Join-Path $stateDirectory 'acpimof-before.dll') -Force
}

$backup | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8

if ($PSCmdlet.ShouldProcess($targetPath, 'Install verified Gigabyte MOF resource DLL')) {
    Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
}

if ($PSCmdlet.ShouldProcess("$registryPath\MofImagePath", "Set to $registryValue")) {
    New-ItemProperty `
        -LiteralPath $registryPath `
        -Name MofImagePath `
        -PropertyType ExpandString `
        -Value $registryValue `
        -Force | Out-Null
}

$installedHash = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
$installedValue = (Get-ItemProperty -LiteralPath $registryPath -Name MofImagePath).MofImagePath
if ($installedHash -ne $expectedHash -or $installedValue -ne $registryValue) {
    throw 'Post-install verification failed. Run Unregister-GigabyteWmiProvider.ps1 to restore the backup.'
}

Write-Host 'Gigabyte WMI MOF provider installed and verified.'
Write-Host 'Restart Windows, then rerun AorusControl.Diagnostics.'
