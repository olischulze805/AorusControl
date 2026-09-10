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

    Nur lesend, keine Adminrechte. Beenden mit Strg+C.
#>
[CmdletBinding()]
param([int] $IntervalMilliseconds = 400)

$ErrorActionPreference = 'Continue'
$device = 'USB\VID_1044&PID_7A41\AP0000000003'

Write-Host ""
Write-Host "Beobachte die interne Tastatur ($device)." -ForegroundColor Cyan
Write-Host "Jetzt drücken, biegen, stecken - jede Änderung wird gemeldet. Beenden mit Strg+C." -ForegroundColor Cyan
Write-Host ""

$previous = $null
$since = Get-Date
while ($true) {
    $status = (Get-PnpDevice -InstanceId $device -ErrorAction SilentlyContinue).Status
    $present = $status -eq 'OK'

    if ($previous -eq $null) {
        Write-Host ("{0:HH:mm:ss}  Ausgangslage: {1}" -f (Get-Date), $(if ($present) { 'angemeldet' } else { 'nicht vorhanden' }))
    }
    elseif ($present -ne $previous) {
        $held = (New-TimeSpan -Start $since).TotalSeconds
        if ($present) {
            Write-Host ("{0:HH:mm:ss}  ANGEMELDET   (war {1:N0}s weg)" -f (Get-Date), $held) -ForegroundColor Green
            [Console]::Beep(1200, 150)
        }
        else {
            Write-Host ("{0:HH:mm:ss}  VERSCHWUNDEN (war {1:N0}s da)" -f (Get-Date), $held) -ForegroundColor Red
            [Console]::Beep(500, 300)
        }
        $since = Get-Date
    }

    $previous = $present
    Start-Sleep -Milliseconds $IntervalMilliseconds
}
