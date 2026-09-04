$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $projectRoot 'src\AorusControl.App\bin\Release\net10.0-windows\AorusControl.exe'

if (-not (Test-Path -LiteralPath $executable)) {
    dotnet build (Join-Path $projectRoot 'src\AorusControl.App\AorusControl.App.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw 'Der Build von AORUS Control ist fehlgeschlagen.'
    }
}

Start-Process -FilePath $executable
