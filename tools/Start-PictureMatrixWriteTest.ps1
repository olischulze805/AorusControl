$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $projectRoot 'src\AorusControl.Diagnostics\bin\Release\net10.0-windows\AorusControl.Diagnostics.exe'

if (-not (Test-Path -LiteralPath $executable)) {
    dotnet build (Join-Path $projectRoot 'AorusControl.slnx') --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw 'Der Build des Picture-Matrix-Schreibtests ist fehlgeschlagen.'
    }
}

Write-Host 'Picture-Matrix-Schreibtest (Kommandobyte 0x12)' -ForegroundColor Yellow
Write-Host ''
Write-Host 'Dieser Test schreibt erstmals in den LED-Profilspeicher der Tastatur.'
Write-Host 'Gesichert und danach verifiziert wiederhergestellt werden:'
Write-Host '  - die 512 Byte des Ziel-Slots'
Write-Host '  - alle drei Zonenfarben'
Write-Host ''
Write-Host 'Nicht beschrieben werden: Firmware-Code, Tastenmatrix, Makros, BIOS, EC,'
Write-Host 'Akku und der ITE-Flash-Report 0x5A.'
Write-Host ''

$answer = Read-Host 'Zum Fortfahren JA eingeben'
if ($answer -ne 'JA') {
    Write-Host 'Abgebrochen. Es wurde nichts an die Tastatur gesendet.'
    exit 1
}

& $executable --test-picture-matrix-write --confirm-picture-matrix-write
exit $LASTEXITCODE
