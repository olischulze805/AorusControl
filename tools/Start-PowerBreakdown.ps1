<#
.SYNOPSIS
    Misst über einige Minuten, wohin die Watt im Akkubetrieb gehen.

.DESCRIPTION
    Der Akku ist das einzige Messgerät dieses Laptops, das den ganzen Verbrauch sieht. Die
    CPU misst ihre eigenen Anteile über RAPL. Die Differenz ist alles Übrige - Bildschirm,
    Board, Arbeitsspeicher, SSD, WLAN und die Verluste der Spannungswandler.

        Gesamt (Akku)
          - CPU-Paket (RAPL PKG, enthält Kerne, iGPU und Uncore)
          - Arbeitsspeicher (RAPL DRAM)
          = Rest

    Gemessen wird nur gelesen: Akku über ACPI, RAPL über die Windows-Leistungsindikatoren,
    der Zustand der NVIDIA-Karte über PnP. Die Grafikkarte wird nicht angesprochen und
    deshalb auch nicht geweckt; steht sie auf D3, kostet sie nichts und taucht im Rest nicht
    auf.

    Am Netz ist der Gesamtwert nicht messbar - dann bricht das Skript mit einer Erklärung ab.

.PARAMETER Minutes
    Messdauer. Voreinstellung drei Minuten, das reicht für einen belastbaren Median.

.PARAMETER IntervalSeconds
    Abstand zwischen zwei Proben.
#>
[CmdletBinding()]
param([double] $Minutes = 3, [int] $IntervalSeconds = 2)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'research\runs'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$report = Join-Path $output ("power-breakdown-{0:yyyyMMdd-HHmmss}.md" -f (Get-Date))

$lines = [System.Collections.Generic.List[string]]::new()
function Say([string] $text) { Write-Host $text; $lines.Add($text) }

# ------------------------------------------------------------------ Quellen ---------------
$energy = New-Object System.Diagnostics.PerformanceCounterCategory 'Energy Meter'
$cpuLoad = New-Object System.Diagnostics.PerformanceCounter 'Processor Information', '% Processor Time', '_Total'
$cpuClock = New-Object System.Diagnostics.PerformanceCounter 'Processor Information', '% Processor Performance', '_Total'
$cpuLoad.NextValue() | Out-Null
$cpuClock.NextValue() | Out-Null

# RAPL liefert Energiezähler, keine Momentanleistung: Watt entstehen erst aus zwei Proben.
$domains = 'RAPL_Package0_PKG', 'RAPL_Package0_PP0', 'RAPL_Package0_PP1', 'RAPL_Package0_DRAM'
$previous = @{}
function Read-Rapl {
    $category = $script:energy.ReadCategory()['Power']
    $watts = @{}
    foreach ($domain in $script:domains) {
        $sample = $category[$domain].Sample
        if ($script:previous.ContainsKey($domain)) {
            $value = [System.Diagnostics.CounterSample]::Calculate($script:previous[$domain], $sample) / 1000
            if ($value -ge 0 -and $value -lt 250) { $watts[$domain] = $value }
        }
        $script:previous[$domain] = $sample
    }
    return $watts
}

function Read-Battery {
    $row = Get-CimInstance -Namespace root\WMI -ClassName BatteryStatus -ErrorAction SilentlyContinue |
        Where-Object Active | Select-Object -First 1
    if (-not $row) { return $null }
    # SInt32 in WMI: dieselbe Falle, die die Dashboardkarte anfangs leer gelassen hat.
    [pscustomobject]@{
        Online  = [bool] $row.PowerOnline
        Watts   = if ([int] $row.DischargeRate -gt 0) { [int] $row.DischargeRate / 1000 } else { $null }
        Percent = $row.RemainingCapacity
    }
}

function Read-GpuState {
    $device = Get-PnpDevice -Class Display -ErrorAction SilentlyContinue | Where-Object InstanceId -match 'VEN_10DE' | Select-Object -First 1
    if (-not $device) { return 'keine NVIDIA' }
    try {
        $data = (Get-PnpDeviceProperty -InstanceId $device.InstanceId -KeyName 'DEVPKEY_Device_PowerData' -ErrorAction Stop).Data
        'D' + ([BitConverter]::ToUInt32($data, 4) - 1)
    } catch { '?' }
}

function Read-Brightness {
    try { (Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorBrightness -ErrorAction Stop).CurrentBrightness } catch { $null }
}

# ------------------------------------------------------------------ Messung ---------------
$battery = Read-Battery
if (-not $battery) { Write-Host "Kein Akku gefunden." -ForegroundColor Red; return }
if ($battery.Online) {
    Write-Host ""
    Write-Host "Der Laptop haengt am Netz. Der Gesamtverbrauch ist dann nicht messbar:" -ForegroundColor Yellow
    Write-Host "Dieses Geraet hat keinen Sensor fuer die Leistung aus dem Netzteil, nur den Akku." -ForegroundColor Yellow
    Write-Host "Bitte das Netzteil abziehen und erneut starten." -ForegroundColor Yellow
    return
}

$samples = [System.Collections.Generic.List[object]]::new()
$count = [Math]::Max(2, [int]($Minutes * 60 / $IntervalSeconds))
Write-Host ""
Write-Host ("Messung laeuft: {0} Proben im Abstand von {1} s (~{2:N1} Minuten)." -f $count, $IntervalSeconds, $Minutes) -ForegroundColor Cyan
Write-Host "Bitte den Laptop normal weiterbenutzen oder in Ruhe lassen - beides ist brauchbar." -ForegroundColor Cyan
Write-Host ""

Read-Rapl | Out-Null
for ($index = 1; $index -le $count; $index++) {
    Start-Sleep -Seconds $IntervalSeconds
    $rapl = Read-Rapl
    $now = Read-Battery
    if (-not $now -or $now.Online) {
        Write-Host "Stromquelle hat gewechselt - Messung abgebrochen." -ForegroundColor Yellow
        break
    }
    $sample = [pscustomobject]@{
        Time       = Get-Date
        Total      = $now.Watts
        Package    = $rapl['RAPL_Package0_PKG']
        Cores      = $rapl['RAPL_Package0_PP0']
        Igpu       = $rapl['RAPL_Package0_PP1']
        Dram       = $rapl['RAPL_Package0_DRAM']
        CpuPercent = $cpuLoad.NextValue()
        CpuClock   = $cpuClock.NextValue()
        Gpu        = Read-GpuState
        Brightness = Read-Brightness
    }
    $samples.Add($sample)
    Write-Host ("`r  {0,3}/{1}  gesamt {2,5:N1} W  CPU {3,5:N1} W  Rest {4,5:N1} W    " -f $index, $count,
        $sample.Total, $sample.Package, ($sample.Total - $sample.Package - $sample.Dram)) -NoNewline
}
Write-Host ""

# ------------------------------------------------------------------ Auswertung -------------
function Stat([string] $name, [scriptblock] $select) {
    $values = @($samples | ForEach-Object $select | Where-Object { $_ -ne $null })
    if (-not $values) { return $null }
    $sorted = $values | Sort-Object
    [pscustomobject]@{
        Name   = $name
        Min    = $sorted[0]
        Median = $sorted[[int]($sorted.Count / 2)]
        Max    = $sorted[-1]
    }
}

$rows = @(
    Stat 'Gesamt (Akku)'        { $_.Total }
    Stat 'CPU-Paket (PKG)'      { $_.Package }
    Stat '  davon Kerne (PP0)'  { $_.Cores }
    Stat '  davon iGPU (PP1)'   { $_.Igpu }
    Stat 'Arbeitsspeicher'      { $_.Dram }
    Stat 'Rest'                 { if ($_.Total -and $_.Package) { $_.Total - $_.Package - $_.Dram } }
    Stat 'CPU-Last %'           { $_.CpuPercent }
    Stat 'Takt % der Nennfrequenz' { $_.CpuClock }
)

Say "# Woher die Watt kommen"
Say ""
Say ("- Gemessen: {0:yyyy-MM-dd HH:mm:ss}, {1} Proben im Abstand von {2} s" -f (Get-Date), $samples.Count, $IntervalSeconds)
Say ("- Stromquelle: Akku")
Say ("- NVIDIA-Zustand: {0}" -f (($samples.Gpu | Sort-Object -Unique) -join ', '))
Say ("- Bildschirmhelligkeit: {0} %" -f (($samples.Brightness | Sort-Object -Unique) -join ', '))
Say ("- Windows-Leistungsmodus (Akku): {0}" -f (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes').ActiveOverlayDcPowerScheme)
Say ""
Say "| Posten | Minimum | Median | Maximum |"
Say "| --- | ---: | ---: | ---: |"
foreach ($row in $rows) {
    if ($row) { Say ("| {0} | {1:N1} | {2:N1} | {3:N1} |" -f $row.Name, $row.Min, $row.Median, $row.Max) }
}
Say ""
Say "Der Rest ist Bildschirm, Board, SSD, WLAN, Tastaturbeleuchtung und die Verluste der"
Say "Spannungswandler. Er ist eine Differenz, keine Messung: Jeder Fehler der beiden"
Say "gemessenen Groessen landet in ihm."
Say ""
Say "## Alle Proben"
Say ""
Say "| Zeit | Gesamt | PKG | PP0 | PP1 | DRAM | Rest | CPU % | Takt % | GPU |"
Say "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |"
foreach ($sample in $samples) {
    Say ("| {0:HH:mm:ss} | {1:N1} | {2:N1} | {3:N1} | {4:N1} | {5:N1} | {6:N1} | {7:N0} | {8:N0} | {9} |" -f
        $sample.Time, $sample.Total, $sample.Package, $sample.Cores, $sample.Igpu, $sample.Dram,
        ($sample.Total - $sample.Package - $sample.Dram), $sample.CpuPercent, $sample.CpuClock, $sample.Gpu)
}

Set-Content -Path $report -Value $lines -Encoding utf8
Write-Host ""
Write-Host "Bericht: $report" -ForegroundColor Green
