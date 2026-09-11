$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $projectRoot 'src\AorusControl.App\bin\Release\net10.0-windows\AorusControl.exe'

# Keep app and worker on the same source version. Incremental builds are quick
# when nothing changed; existence alone does not mean the executable is current.
dotnet build (Join-Path $projectRoot 'AorusControl.slnx') --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw 'Der Build von AORUS Control ist fehlgeschlagen. Eine laufende App bitte über das Tray-Menü vollständig beenden und erneut starten.'
}

Start-Process -FilePath $executable
