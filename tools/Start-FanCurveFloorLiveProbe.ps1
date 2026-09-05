<#
.SYNOPSIS
    Prüft, ob die EC niedrige Kurvenwerte auch wirklich fährt.

.DESCRIPTION
    Der erste Test hat gezeigt, dass die Firmware Werte unter 57 speichert. Dieser hier
    schaltet Dynamic tatsächlich ein und misst, ob die Lüfter dann auch so langsam laufen -
    oder ob die Firmware intern doch hochregelt.

    Abgesenkt werden nur die Kurvenpunkte unterhalb 60 °C. Alles darüber bleibt unverändert,
    die Lüfter drehen also normal hoch, sobald die Kiste warm wird. Der Test startet nur im
    Leerlauf unter 60 °C, misst sechsmal im Abstand von zwei Sekunden, bricht bei über 65 °C
    sofort ab und stellt danach die Originalkurve und Normal wieder her.

    Braucht Administratorrechte (UAC-Abfrage). Bitte währenddessen am Gerät bleiben.
#>
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
    -ArgumentList '--probe-fan-curve-floor-live --confirm-fan-curve-write' `
    -Verb RunAs `
    -WorkingDirectory $repositoryRoot `
    -Wait `
    -PassThru

exit $process.ExitCode
