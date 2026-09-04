[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
$targetPath = Join-Path $env:windir 'System32\acpimof.dll'
$registryPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\WmiAcpi'
$stateDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\research\state'))
$statePath = Join-Path $stateDirectory 'wmi-provider-backup.json'
$fileBackupPath = Join-Path $stateDirectory 'acpimof-before.dll'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell terminal.'
}

if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
    throw "Backup state not found: $statePath"
}

$backup = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json

if ($backup.RegistryValueExisted) {
    if ($PSCmdlet.ShouldProcess("$registryPath\MofImagePath", 'Restore previous registry value')) {
        New-ItemProperty `
            -LiteralPath $registryPath `
            -Name MofImagePath `
            -PropertyType ExpandString `
            -Value $backup.RegistryValue `
            -Force | Out-Null
    }
}
elseif ($PSCmdlet.ShouldProcess("$registryPath\MofImagePath", 'Remove value created by this project')) {
    Remove-ItemProperty -LiteralPath $registryPath -Name MofImagePath -ErrorAction SilentlyContinue
}

if ($backup.TargetFileExisted) {
    if (-not (Test-Path -LiteralPath $fileBackupPath -PathType Leaf)) {
        throw "Original DLL backup is missing: $fileBackupPath"
    }

    if ($PSCmdlet.ShouldProcess($targetPath, 'Restore previous DLL')) {
        Copy-Item -LiteralPath $fileBackupPath -Destination $targetPath -Force
    }
}
elseif ((Test-Path -LiteralPath $targetPath) -and
        $PSCmdlet.ShouldProcess($targetPath, 'Move project-installed DLL to Recycle Bin')) {
    Add-Type -AssemblyName Microsoft.VisualBasic
    [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile(
        $targetPath,
        [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
        [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin)
}

Write-Host 'Previous WMI provider state restored.'
Write-Host 'Restart Windows to complete the rollback.'
