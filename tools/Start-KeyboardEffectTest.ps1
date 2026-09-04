$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $projectRoot 'src\AorusControl.Diagnostics\bin\Release\net10.0-windows\AorusControl.Diagnostics.exe'

if (-not (Test-Path -LiteralPath $executable)) {
    dotnet build (Join-Path $projectRoot 'AorusControl.slnx') --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw 'Der Build des interaktiven RGB-Tests ist fehlgeschlagen.'
    }
}

& $executable --interactive-keyboard-effect-test
exit $LASTEXITCODE
