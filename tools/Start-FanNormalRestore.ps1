[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'src\AorusControl.App\AorusControl.App.csproj'
$diagnosticExecutable = Join-Path $repositoryRoot 'src\AorusControl.App\bin\Release\net10.0-windows\AorusControl.exe'

if (-not (Test-Path -LiteralPath $diagnosticExecutable)) {
    dotnet build $project --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw 'Der Release-Build ist fehlgeschlagen.'
    }
}

$process = Start-Process -FilePath $diagnosticExecutable `
    -ArgumentList '--restore-fan-normal' `
    -Verb RunAs `
    -WorkingDirectory $repositoryRoot `
    -Wait `
    -PassThru

exit $process.ExitCode
