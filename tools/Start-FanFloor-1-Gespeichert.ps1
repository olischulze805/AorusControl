<#
.SYNOPSIS
    Prüft, wie tief die EC in der Lüfterkurve überhaupt geht.

.DESCRIPTION
    Schreibt der Reihe nach die Rohwerte 50, 40, 30, 20, 10 und 0 in die beiden untersten
    Kurvenpunkte und liest nach jedem Schreibvorgang alle 15 Punkte zurück. Damit steht fest,
    ob die Firmware Werte unterhalb der bisher bestätigten 57 speichert oder hochklemmt.

    Der Lüftermodus wird dabei nie auf Dynamic geschaltet: die Probekurve regelt die Lüfter
    also zu keinem Zeitpunkt, sie liegt nur in der Tabelle. Am Ende werden alle 15
    Originalpunkte zurückgeschrieben und überprüft.

    Braucht Administratorrechte (UAC-Abfrage) und den verifizierten Normalzustand.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
# Nur das Diagnoseprojekt bauen, nicht die ganze Projektmappe: läuft die App gerade aus dem
# Build-Ordner, hält sie ihre eigenen DLLs gesperrt und ein Mappenbau scheitert daran - obwohl
# dieser Test die App überhaupt nicht braucht.
$project = Join-Path $repositoryRoot 'src\AorusControl.Diagnostics\AorusControl.Diagnostics.csproj'
$diagnosticExecutable = Join-Path $repositoryRoot 'src\AorusControl.Diagnostics\bin\Release\net10.0-windows\AorusControl.Diagnostics.exe'

dotnet build $project --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw 'Der Release-Build ist fehlgeschlagen.'
}

$process = Start-Process -FilePath $diagnosticExecutable `
    -ArgumentList '--probe-fan-curve-floor --confirm-fan-curve-write' `
    -Verb RunAs `
    -WorkingDirectory $repositoryRoot `
    -Wait `
    -PassThru

exit $process.ExitCode
