[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$diagnosticExecutable = Join-Path $repositoryRoot 'src\AorusControl.Diagnostics\bin\Release\net10.0-windows\AorusControl.Diagnostics.exe'

if (-not (Test-Path -LiteralPath $diagnosticExecutable)) {
    throw 'The Release diagnostic executable is missing. Build AorusControl.slnx first.'
}

Write-Host 'Test der Tastatur-Hintergrundbeleuchtungsstufe (ACPI SetKeyBoardBackLight)' -ForegroundColor Yellow
Write-Host ''
Write-Host 'Dieser Test ruft erstmals den ACPI-Setter SetKeyBoardBackLight auf und'
Write-Host 'schreibt damit in das EC-Feld KBLL an Offset 0xD7.'
Write-Host ''
Write-Host 'Gesichert und danach verifiziert wiederhergestellt wird der Ausgangswert.'
Write-Host 'Nicht beschrieben werden: Akku, Luefter, Tastenmatrix, Makros, BIOS und Firmware.'
Write-Host ''
Write-Host 'Windows zeigt eine UAC-Abfrage, weil ACPI-Schreibzugriffe'
Write-Host 'Administratorrechte benoetigen.'
Write-Host ''

$answer = Read-Host 'Zum Fortfahren JA eingeben'
if ($answer -ne 'JA') {
    Write-Host 'Abgebrochen. Es wurde nichts an die Firmware gesendet.'
    exit 1
}

$command = "& '$diagnosticExecutable' --test-backlight-level --confirm-backlight-write"
Start-Process -FilePath 'powershell.exe' -Verb RunAs -WorkingDirectory $repositoryRoot -ArgumentList @(
    '-NoProfile',
    '-NoExit',
    '-ExecutionPolicy', 'Bypass',
    '-Command', $command
)
