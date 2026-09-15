<#
.SYNOPSIS
    Misst, was der Bildschirm kostet - durch Vergleich statt durch Schätzung.

.DESCRIPTION
    Die Aufschlüsselung aus Start-PowerBreakdown lässt einen Rest übrig: alles, was weder CPU
    noch Arbeitsspeicher ist. Der grösste Posten darin ist erfahrungsgemäss das Display, aber
    „erfahrungsgemäss" ist keine Messung.

    Dieses Skript stellt die Helligkeit nacheinander auf mehrere Stufen, wartet jeweils, bis
    sich der Verbrauch beruhigt hat, und misst dann. Die Differenz zwischen zwei Stufen ist
    der Bildschirm - alles andere bleibt in der Zeit gleich.

    Am Ende wird die ursprüngliche Helligkeit wiederhergestellt, auch bei Abbruch.

    Nur im Akkubetrieb sinnvoll: Am Netz gibt es keinen Gesamtwert, gegen den man rechnen
    könnte.
#>
[CmdletBinding()]
param([int[]] $Levels = @(0, 25, 50, 75, 100), [int] $SettleSeconds = 8, [int] $SamplesPerLevel = 6)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'research\runs'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$report = Join-Path $output ("brightness-power-{0:yyyyMMdd-HHmmss}.md" -f (Get-Date))

$lines = [System.Collections.Generic.List[string]]::new()
function Say([string] $text) { Write-Host $text; $lines.Add($text) }

$energy = New-Object System.Diagnostics.PerformanceCounterCategory 'Energy Meter'
$previous = $null
function Read-PackageWatts {
    $sample = $script:energy.ReadCategory()['Power']['RAPL_Package0_PKG'].Sample
    $watts = if ($script:previous) { [System.Diagnostics.CounterSample]::Calculate($script:previous, $sample) / 1000 } else { $null }
    $script:previous = $sample
    return $watts
}

function Read-TotalWatts {
    $row = Get-CimInstance -Namespace root\WMI -ClassName BatteryStatus -ErrorAction SilentlyContinue |
        Where-Object Active | Select-Object -First 1
    if (-not $row -or $row.PowerOnline) { return $null }
    $rate = [int] $row.DischargeRate
    if ($rate -gt 0) { $rate / 1000 } else { $null }
}

function Get-Brightness { (Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorBrightness -ErrorAction SilentlyContinue).CurrentBrightness }
function Set-Brightness([int] $level) {
    $methods = Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorBrightnessMethods -ErrorAction SilentlyContinue
    if (-not $methods) { throw 'WmiMonitorBrightnessMethods nicht verfügbar - der Bildschirm lässt sich so nicht steuern.' }
    Invoke-CimMethod -InputObject $methods -MethodName WmiSetBrightness -Arguments @{ Timeout = 1; Brightness = [byte] $level } | Out-Null
}

if ((Read-TotalWatts) -eq $null) {
    Write-Host ""
    Write-Host "Kein Akkuwert: Bitte das Netzteil abziehen. Am Netz gibt es keinen Gesamtverbrauch," -ForegroundColor Yellow
    Write-Host "gegen den sich die Helligkeit rechnen liesse." -ForegroundColor Yellow
    return
}

$original = Get-Brightness
Write-Host ""
Write-Host ("Ausgangshelligkeit: {0} %. Sie wird am Ende wiederhergestellt." -f $original) -ForegroundColor Cyan
Write-Host "Der Bildschirm wird jetzt mehrmals heller und dunkler - das gehoert zur Messung." -ForegroundColor Cyan
Write-Host ""

$results = [System.Collections.Generic.List[object]]::new()
try {
    foreach ($level in $Levels) {
        Set-Brightness $level
        Start-Sleep -Seconds $SettleSeconds
        Read-PackageWatts | Out-Null
        $totals = @(); $packages = @()
        for ($index = 0; $index -lt $SamplesPerLevel; $index++) {
            Start-Sleep -Seconds 2
            $package = Read-PackageWatts
            $total = Read-TotalWatts
            if ($total) { $totals += $total }
            if ($package) { $packages += $package }
        }
        if (-not $totals) { continue }
        $result = [pscustomobject]@{
            Level   = $level
            Total   = ($totals | Measure-Object -Average).Average
            Package = if ($packages) { ($packages | Measure-Object -Average).Average } else { $null }
        }
        $results.Add($result)
        Write-Host ("  {0,3} %  gesamt {1,5:N1} W   CPU-Paket {2,5:N1} W" -f $result.Level, $result.Total, $result.Package)
    }
}
finally {
    if ($original -ne $null) { try { Set-Brightness $original } catch { } }
    Write-Host ("Helligkeit zurueckgestellt auf {0} %." -f $original) -ForegroundColor Cyan
}

Say "# Was der Bildschirm kostet"
Say ""
Say ("- Gemessen: {0:yyyy-MM-dd HH:mm:ss}, Akkubetrieb" -f (Get-Date))
Say ("- Je Stufe {0} s Beruhigung, dann {1} Proben" -f $SettleSeconds, $SamplesPerLevel)
Say ""
Say "| Helligkeit | Gesamt | CPU-Paket | Rest ohne CPU |"
Say "| ---: | ---: | ---: | ---: |"
foreach ($result in $results) {
    Say ("| {0} % | {1:N1} W | {2:N1} W | {3:N1} W |" -f $result.Level, $result.Total, $result.Package, ($result.Total - $result.Package))
}
Say ""
if ($results.Count -ge 2) {
    $darkest = $results[0]; $brightest = $results[-1]
    Say ("**Der Bildschirm von {0} % auf {1} %: {2:N1} W.** Das ist die Differenz der Gesamtwerte;" -f
        $darkest.Level, $brightest.Level, ($brightest.Total - $darkest.Total))
    Say ("die CPU hat sich dabei um {0:N1} W veraendert und ist entsprechend herausgerechnet." -f
        ($brightest.Package - $darkest.Package))
}

Set-Content -Path $report -Value $lines -Encoding utf8
Write-Host ""
Write-Host "Bericht: $report" -ForegroundColor Green
