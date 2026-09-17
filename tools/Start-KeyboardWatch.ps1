<#
.SYNOPSIS
    Zeigt live an, ob die interne Tastatur am USB-Bus hängt - mit Ton bei jeder Änderung.

.DESCRIPTION
    Gedacht für die Fehlersuche mit den Händen am Gerät: laufen lassen, auf das Gehäuse
    drücken, den Stecker neu setzen, den Deckel zuklappen - und hören, ob sich etwas ändert.
    Ein Piepton nach oben heißt angemeldet, einer nach unten verschwunden.

    Genau das trennt die beiden verbliebenen Erklärungen. Bringt Druck auf eine bestimmte
    Stelle das Gerät zurück oder zum Verschwinden, ist es die Verbindung. Passiert beim
    Drücken nichts und es kommt später von allein wieder, spricht das eher für den Controller.

    Nach dem Zusammenbau ist es die Abnahme: erscheint die Tastatur sofort und bleibt sie über
    Minuten stabil, sitzt der Stecker.

    Jede Zeile geht auch in eine Datei unter research/runs. Das Fenster allein war zu wenig:
    Beim Ausfall am 2026-09-17 lief der Beobachter mit, und hinterher gab es nichts zu lesen -
    genau dann, wenn man es gebraucht hätte. Die Datei wird nach jeder Zeile geschrieben, damit
    auch ein hartes Ausschalten sie nicht leert.

    Nur lesend, keine Adminrechte. Beenden mit Strg+C.
#>
[CmdletBinding()]
param([int] $IntervalMilliseconds = 400, [string] $OutputDirectory)

$ErrorActionPreference = 'Continue'
$device = 'USB\VID_1044&PID_7A41\AP0000000003'

$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'research\runs' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$report = Join-Path $OutputDirectory ("keyboard-watch-{0:yyyyMMdd-HHmmss}.md" -f (Get-Date))
Set-Content -Path $report -Encoding utf8 -Value @(
    "# Tastatur-Beobachtung",
    "",
    ("- Gestartet: {0:yyyy-MM-dd HH:mm:ss}" -f (Get-Date)),
    ("- Gerät: {0}" -f $device),
    ("- Abfrage alle {0} ms" -f $IntervalMilliseconds),
    "",
    "| Zeit | Ereignis | Dauer davor |",
    "| --- | --- | ---: |"
)

function Note([string] $event, [string] $held) {
    Add-Content -Path $script:report -Encoding utf8 -Value ("| {0:HH:mm:ss} | {1} | {2} |" -f (Get-Date), $event, $held)
}

Write-Host ""
Write-Host "Beobachte die interne Tastatur ($device)." -ForegroundColor Cyan
Write-Host "Jetzt drücken, biegen, stecken - jede Änderung wird gemeldet. Beenden mit Strg+C." -ForegroundColor Cyan
Write-Host "Mitschrift: $report" -ForegroundColor DarkGray
Write-Host ""

$previous = $null
$since = Get-Date
while ($true) {
    $status = (Get-PnpDevice -InstanceId $device -ErrorAction SilentlyContinue).Status
    $present = $status -eq 'OK'

    if ($previous -eq $null) {
        $state = if ($present) { 'angemeldet' } else { 'nicht vorhanden' }
        Write-Host ("{0:HH:mm:ss}  Ausgangslage: {1}" -f (Get-Date), $state)
        Note "Ausgangslage: $state" ""
    }
    elseif ($present -ne $previous) {
        $held = (New-TimeSpan -Start $since).TotalSeconds
        if ($present) {
            Write-Host ("{0:HH:mm:ss}  ANGEMELDET   (war {1:N0}s weg)" -f (Get-Date), $held) -ForegroundColor Green
            Note "**ANGEMELDET**" ("{0:N0} s weg" -f $held)
            [Console]::Beep(1200, 150)
        }
        else {
            Write-Host ("{0:HH:mm:ss}  VERSCHWUNDEN (war {1:N0}s da)" -f (Get-Date), $held) -ForegroundColor Red
            Note "**VERSCHWUNDEN**" ("{0:N0} s da" -f $held)
            [Console]::Beep(500, 300)
        }
        $since = Get-Date
    }

    $previous = $present
    Start-Sleep -Milliseconds $IntervalMilliseconds
}
