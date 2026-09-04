$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $projectRoot 'src\AorusControl.Diagnostics\bin\Release\net10.0-windows\AorusControl.Diagnostics.exe'

dotnet build (Join-Path $projectRoot 'src\AorusControl.Diagnostics\AorusControl.Diagnostics.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw 'Der aktuelle Verbrauchsmonitor konnte nicht gebaut werden; alte EXE wird nicht gestartet.'
}

& $executable --monitor-power-draw
exit $LASTEXITCODE
