[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$diagnosticExecutable = Join-Path $repositoryRoot 'src\AorusControl.Diagnostics\bin\Release\net10.0-windows\AorusControl.Diagnostics.exe'

if (-not (Test-Path -LiteralPath $diagnosticExecutable)) {
    throw 'The Release diagnostic executable is missing. Build AorusControl.slnx first.'
}

Start-Process -FilePath $diagnosticExecutable -ArgumentList '--inspect-battery' -Verb RunAs -WorkingDirectory $repositoryRoot
