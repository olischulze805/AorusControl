[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
dotnet build (Join-Path $projectRoot 'src\AorusControl.Worker\AorusControl.Worker.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Worker-Build fehlgeschlagen; kein Test gestartet.' }
$executable = Join-Path $projectRoot 'src\AorusControl.Worker\bin\Release\net10.0-windows\AorusControl.Worker.exe'
$diagnosticProcess = Start-Process -FilePath $executable -ArgumentList '--diagnose-report' -WorkingDirectory $projectRoot -Verb RunAs -WindowStyle Hidden -PassThru
Write-Output "Lesende erhöhte Diagnose gestartet, PID $($diagnosticProcess.Id)."
if (-not $diagnosticProcess.WaitForExit(30000)) {
    Write-Output "Diagnose läuft noch, PID $($diagnosticProcess.Id). Nicht erneut starten; Prozessstatus prüfen."
    exit 2
}
Write-Output "Diagnose beendet: ExitCode $($diagnosticProcess.ExitCode). Bericht unter research\runs\worker-access-*.md."
exit $diagnosticProcess.ExitCode
