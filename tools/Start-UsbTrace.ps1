<#
.SYNOPSIS
    Schneidet den USB-Verkehr mit und hält fest, was im Moment des Abrisses passiert.

.DESCRIPTION
    Bisher wissen wir nur, dass die Tastatur verschwindet - Windows meldet das erst, wenn der
    Hub den Anschluss das nächste Mal abfragt, also bis zu zwei Sekunden später. Was in diesen
    zwei Sekunden auf dem Bus passiert, steht nirgends.

    Windows kann es aber mitschreiben. Die USB-Treiber melden ihre Vorgänge an ETW:

        Microsoft-Windows-USB-USBXHCI    der Hostcontroller
        Microsoft-Windows-USB-USBHUB3    der Hub und seine Anschlüsse
        Microsoft-Windows-USB-UCX        die Schicht dazwischen
        Microsoft-Windows-Input-HIDCLASS die HID-Ebene darüber

    Damit lässt sich unterscheiden, was bisher nur vermutet ist: ob das Gerät auf Anfragen
    nicht mehr antwortet (Zeitüberschreitungen, NAKs), ob der Anschluss einen elektrischen
    Abfall meldet (Disconnect, Overcurrent), oder ob Übertragungsfehler vorausgehen (CRC,
    Babble) - also ob die Verbindung reisst oder der Controller verstummt.

    Mitgeschrieben wird in einen Ringpuffer: Es bleibt immer nur das Letzte stehen, und genau
    das ist gewollt. Die Aufzeichnung läuft, bis die Tastatur verschwindet - dann wird sofort
    gestoppt, damit der Abriss am Ende des Puffers liegt.

    Nur lesend. Es wird nichts an Geräten verändert, keine Treiber angefasst und nichts
    geschrieben ausser der Aufzeichnung selbst.
#>
[CmdletBinding()]
param([int] $MaxMinutes = 30, [int] $BufferMegabytes = 64, [switch] $KeepOpen, [string] $OutputDirectory)

$ErrorActionPreference = 'Continue'
$session = 'AorusUsbTrace'
$device = 'USB\VID_1044&PID_7A41\AP0000000003'

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    # Pfad in Anfuehrungszeichen: Start-Process quotet nicht selbst, und dieser Ordner heisst
    # "Gigabyte Control Center".
    Start-Process powershell.exe -Verb RunAs -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-KeepOpen',
        '-MaxMinutes', $MaxMinutes)
    Write-Host ""
    Write-Host "Es geht im neuen Fenster mit Administratorrechten weiter." -ForegroundColor Cyan
    Start-Sleep -Seconds 2
    return
}

$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'research\runs' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$stamp = '{0:yyyyMMdd-HHmmss}' -f (Get-Date)
$etl = Join-Path $OutputDirectory "usb-trace-$stamp.etl"
$report = Join-Path $OutputDirectory "usb-trace-$stamp.md"

$providers = @(
    '{30E1D284-5D88-459C-83FD-6345B39B19EC}'  # USB-USBXHCI
    '{AC52AD17-CC01-4F85-8DF5-4DCE4333C99B}'  # USB-USBHUB3
    '{36DA592D-E43A-4E28-AF6F-4BC57C5A11E8}'  # USB-UCX
    '{C88A4EF5-D048-4013-9408-E04B7DB2814A}'  # USB-USBPORT
    '{6465DA78-E7A0-4F39-B084-8F53C7C30DC6}'  # Input-HIDCLASS
)

function Stop-Trace { logman stop $session -ets 2>&1 | Out-Null }

Write-Host ""
Write-Host "USB-Mitschnitt" -ForegroundColor Cyan
Write-Host "Laeuft, bis die Tastatur verschwindet - hoechstens $MaxMinutes Minuten." -ForegroundColor Cyan
Write-Host "Ringpuffer $BufferMegabytes MB: es bleibt immer das Letzte stehen, also der Abriss." -ForegroundColor DarkGray
Write-Host ""

# Eine eventuell haengengebliebene Sitzung von vorher zuerst wegraeumen.
Stop-Trace
$create = logman create trace $session -ets -o $etl -f bincirc -max $BufferMegabytes -bs 64 -nb 32 320 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host ("Aufzeichnung konnte nicht gestartet werden: {0}" -f ($create -join ' ')) -ForegroundColor Red
    if ($KeepOpen) { Start-Sleep -Seconds 30 }
    return
}
foreach ($provider in $providers) {
    logman update trace $session -ets -p $provider 0xffffffffffffffff 0x4 2>&1 | Out-Null
}

$startedAt = Get-Date
$deadline = $startedAt.AddMinutes($MaxMinutes)
$dropped = $null
try {
    while ((Get-Date) -lt $deadline) {
        $present = (Get-PnpDevice -InstanceId $device -ErrorAction SilentlyContinue).Status -eq 'OK'
        if (-not $present) { $dropped = Get-Date; break }
        Write-Host ("`r  laeuft seit {0:hh\:mm\:ss} - Tastatur angemeldet " -f ((Get-Date) - $startedAt)) -NoNewline
        Start-Sleep -Milliseconds 500
    }
}
finally {
    Write-Host ""
    # Sofort stoppen: Jede weitere Sekunde Mitschnitt schiebt den Abriss aus dem Ringpuffer.
    Stop-Trace
}

$lines = [System.Collections.Generic.List[string]]::new()
function Say([string] $text) { Write-Host $text; $lines.Add($text) }

Say "# USB-Mitschnitt"
Say ""
Say ("- Start: {0:yyyy-MM-dd HH:mm:ss}" -f $startedAt)
Say ("- Ende:  {0:yyyy-MM-dd HH:mm:ss}" -f (Get-Date))
Say ("- Abriss der Tastatur: {0}" -f $(if ($dropped) { '{0:HH:mm:ss}' -f $dropped } else { 'keiner in dieser Zeit' }))
Say ("- Aufzeichnung: {0}" -f $etl)
Say ""

if (Test-Path $etl) {
    Say ("Groesse der Aufzeichnung: {0:N1} MB" -f ((Get-Item $etl).Length / 1MB))
    Say ""
    Say "## Zusammenfassung nach Ereignisart"
    Say ""
    $summary = Join-Path $env:TEMP "usb-trace-$stamp-summary.txt"
    tracerpt $etl -summary $summary -o (Join-Path $env:TEMP "usb-trace-$stamp.xml") -of XML -y 2>&1 | Out-Null
    if (Test-Path $summary) {
        Say '```'
        Get-Content $summary | Where-Object { $_ -match '\S' } | Select-Object -First 40 | ForEach-Object { Say $_ }
        Say '```'
    } else {
        Say "tracerpt lieferte keine Zusammenfassung; die .etl laesst sich mit Windows Performance Analyzer oeffnen."
    }
}

Say ""
Say "Die .etl bleibt liegen. Auswerten laesst sie sich mit:"
Say '```'
Say "tracerpt `"$etl`" -o auswertung.xml -of XML"
Say '```'

Set-Content -Path $report -Value $lines -Encoding utf8
Write-Host ""
Write-Host "Bericht: $report" -ForegroundColor Green
if ($KeepOpen) {
    for ($left = 60; $left -gt 0; $left--) {
        Write-Host ("`r  Dieses Fenster schliesst sich in {0,2} s. " -f $left) -NoNewline -ForegroundColor DarkGray
        Start-Sleep -Seconds 1
    }
}
