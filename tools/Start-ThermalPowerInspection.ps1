[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'AorusControl.slnx'
$diagnosticExecutable = Join-Path $repositoryRoot 'src\AorusControl.Diagnostics\bin\Release\net10.0-windows\AorusControl.Diagnostics.exe'

dotnet build $solution --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw 'Der Release-Build ist fehlgeschlagen.'
}

$process = Start-Process -FilePath $diagnosticExecutable `
    -ArgumentList '--inspect-thermal-power' `
    -Verb RunAs `
    -WorkingDirectory $repositoryRoot `
    -Wait `
    -PassThru

exit $process.ExitCode
