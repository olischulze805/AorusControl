[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$workerExecutable = Join-Path $repositoryRoot 'src\AorusControl.Worker\bin\Release\net10.0-windows\AorusControl.Worker.exe'

if (-not (Test-Path -LiteralPath $workerExecutable)) {
    dotnet build (Join-Path $repositoryRoot 'AorusControl.slnx') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Der Release-Build ist fehlgeschlagen.' }
}

$existing = Get-Process -Name 'AorusControl.Worker' -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host 'Es laeuft bereits ein AorusControl.Worker-Prozess:' -ForegroundColor Red
    $existing | Select-Object Id, StartTime | Format-Table -AutoSize | Out-String | Write-Host
    Write-Host 'Wegen FirstPipeInstance kann kein zweiter Worker denselben Pipenamen'
    Write-Host 'belegen. Bitte diesen Prozess zuerst im Task-Manager beenden.'
    exit 1
}

Write-Host 'Abnahmetest: Absturzsicherheit des Fixed-Luefter-Workers' -ForegroundColor Yellow
Write-Host ''
Write-Host 'Dieser Test:'
Write-Host '  1. startet den Hardware-Worker elevated (UAC-Abfrage folgt),'
Write-Host '  2. laesst einen Client Fixed 114 anfordern und sich SOFORT beenden'
Write-Host '     (simuliert einen Client, der direkt nach dem Zugriff abstuerzt),'
Write-Host '  3. wartet 15 Sekunden, OHNE den Worker erneut zu kontaktieren,'
Write-Host '  4. prueft, ob der Worker die Luefter von sich aus auf Normal'
Write-Host '     zurueckgestellt hat.'
Write-Host ''
Write-Host 'Am Ende wird der Worker wieder beendet. Es wird nichts dauerhaft veraendert.'
Write-Host ''

$answer = Read-Host 'Zum Fortfahren JA eingeben'
if ($answer -ne 'JA') {
    Write-Host 'Abgebrochen.'
    exit 1
}

Write-Host ''
Write-Host 'Starte Worker elevated (bitte die UAC-Abfrage bestaetigen) ...'
$worker = Start-Process -FilePath $workerExecutable -ArgumentList '--serve' -Verb RunAs -PassThru
Start-Sleep -Seconds 2

Write-Host ''
Write-Host '--- Ausgangszustand ---'
& $workerExecutable --fan-status

Write-Host ''
Write-Host '--- Simulierter Absturz: Fixed 114 anfordern, Client beendet sich sofort ---'
& $workerExecutable --acquire-fixed 114

Write-Host ''
Write-Host '--- Sofort danach ---'
& $workerExecutable --fan-status

Write-Host ''
Write-Host 'Warte 15 Sekunden, ohne den Worker zu kontaktieren ...'
Start-Sleep -Seconds 15

Write-Host ''
Write-Host '--- Nach 15 Sekunden, ohne jede Nachhilfe ---'
& $workerExecutable --fan-status

Write-Host ''
Write-Host 'Beende den Worker ...'
if ($worker -and -not $worker.HasExited) {
    try { Stop-Process -Id $worker.Id -Force -ErrorAction Stop }
    catch { Write-Host "Worker konnte nicht automatisch beendet werden (PID $($worker.Id)); bitte manuell schliessen." }
}

Write-Host ''
Write-Host 'Erwartung: Der erste fan-status nach dem simulierten Absturz zeigt' -ForegroundColor Cyan
Write-Host '"FanRequiresRestoration":true. Der letzte, 15 Sekunden spaeter, zeigt' -ForegroundColor Cyan
Write-Host '"FanRequiresRestoration":false - ohne dass irgendjemand den Worker' -ForegroundColor Cyan
Write-Host 'in der Zwischenzeit erneut kontaktiert hat.' -ForegroundColor Cyan
