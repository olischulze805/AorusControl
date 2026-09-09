<#
.SYNOPSIS
    Hält fest, in welchem Zustand die interne Tastatur ist - besonders dann, wenn sie nicht
    mehr reagiert.

.DESCRIPTION
    Vor dem harten Ausschalten ausführen, per Doppelklick auf die .cmd. Danach ist der Zustand
    verloren: Das Stromabschalten setzt den EC zurück, und Windows protokolliert bei diesem
    Fehlerbild von sich aus gar nichts - das Gerät bleibt angemeldet und verstummt nur.

    Es wird nichts eingetippt und nichts bestätigt. Das Skript misst den Energiezustand
    zweimal: einmal so, wie es ihn vorfindet, und einmal nach einem Fenster von zehn Sekunden,
    in dem du auf der internen Tastatur herumtippen sollst - auch wenn nichts passiert.

    Genau dieser Unterschied ist die Messung. Im Leerlauf liegen alle Schnittstellen in D2
    oder D3; ein Tastendruck weckt eine gesunde Tastatur, und die Tastenschnittstelle MI_00
    steht danach auf D0 (am 2026-09-09 an dieser Maschine so nachgemessen, im Netz- wie im
    Akkubetrieb). Bleibt sie unter Tastendruck auf D2/D3, wacht das Gerät nicht mehr auf;
    steht sie auf D0 und reagiert trotzdem nichts, hängt der ITE-Controller im wachen Zustand.

    Nur lesend: keine Treiberänderung, kein Reset, kein Schreibzugriff auf Gerät oder EC.
    Braucht keine Administratorrechte.
#>
[CmdletBinding()]
param([int] $TypingSeconds = 10, [string] $OutputDirectory)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'research\runs' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$report = Join-Path $OutputDirectory ("keyboard-hang-{0:yyyyMMdd-HHmmss}.md" -f (Get-Date))

$lines = [System.Collections.Generic.List[string]]::new()
function Say([string] $text) { Write-Host $text; $lines.Add($text) }

# ---------------------------------------------------------------- Energiezustand ---------
# Dieselbe Windows-Struktur, aus der das Dashboard den NVIDIA-Zustand liest: CM_POWER_DATA,
# Feld PD_MostRecentPowerState. Einsbasiert, 1 = D0 wach, 4 = D3 schlafend.
function Get-PowerState([string] $instanceId) {
    try {
        $data = (Get-PnpDeviceProperty -InstanceId $instanceId -KeyName 'DEVPKEY_Device_PowerData' -ErrorAction Stop).Data
        if (-not $data -or $data.Length -lt 8) { return 'unbekannt' }
        switch ([BitConverter]::ToUInt32($data, 4)) {
            1 { 'D0 wach' } 2 { 'D1' } 3 { 'D2' } 4 { 'D3 schläft' } default { 'unbekannt' }
        }
    } catch { 'unbekannt' }
}

function Measure-Keyboard {
    $result = [ordered]@{}
    foreach ($device in (Get-PnpDevice | Where-Object InstanceId -match '1044.*7A41' | Sort-Object InstanceId)) {
        $result[$device.InstanceId] = Get-PowerState $device.InstanceId
    }
    return $result
}

$before = Measure-Keyboard
Write-Host ""
Write-Host "Jetzt bitte auf der internen Tastatur tippen - egal welche Tasten, auch wenn" -ForegroundColor Cyan
Write-Host "nichts passiert. Es ist nichts zu bestätigen, das Fenster wartet von selbst." -ForegroundColor Cyan
for ($second = $TypingSeconds; $second -gt 0; $second--) {
    Write-Host ("`r  noch {0,2} Sekunden " -f $second) -NoNewline -ForegroundColor Cyan
    Start-Sleep -Seconds 1
}
Write-Host "`r  fertig gemessen.        " -ForegroundColor Cyan
Write-Host ""
$after = Measure-Keyboard

# ---------------------------------------------------------------- Bericht ---------------
Say "# Tastatur-Diagnose"
Say ""
Say ("- Erstellt: {0:yyyy-MM-dd HH:mm:ss zzz}" -f (Get-Date))
Say ("- Letzter Systemstart: {0}" -f (Get-CimInstance Win32_OperatingSystem).LastBootUpTime)
$battery = Get-CimInstance Win32_Battery
Say ("- Stromquelle: {0}" -f $(if ($battery.BatteryStatus -eq 2) { 'Netz' } elseif ($battery) { 'Akku' } else { 'unbekannt' }))
Say ("- Tippfenster: {0} Sekunden" -f $TypingSeconds)
Say "- Tastatur reagiert nach deinem Eindruck: (hier von Hand ergänzen)"
Say ""

$keyInterface = $after.Keys | Where-Object { $_ -match '^HID\\VID_1044&PID_7A41&MI_00' } | Select-Object -First 1
if ($keyInterface) {
    $verdict = if ($after[$keyInterface] -eq 'D0 wach') {
        '**Die Tastatur ist aufgewacht.** Reagiert sie trotzdem nicht, hängt der Controller im wachen Zustand - dann liegt es nicht am Energiesparen.'
    } else {
        ('**Die Tastatur wacht nicht auf** (MI_00 steht auf {0}). Das passt zum selektiven Energiesparen als Ursache.' -f $after[$keyInterface])
    }
    Say "## Befund"
    Say ""
    Say $verdict
    Say ""
    Say "Gilt nur, wenn im Tippfenster wirklich getippt wurde; sonst ist D2 auch bei gesunder Tastatur richtig."
    Say ""
}

Say "## Interne Tastatur 1044:7A41"
Say ""
Say '| Gerät | Status | Problemcode | vorher | nach Tippen |'
Say '| --- | --- | --- | --- | --- |'
$devices = Get-PnpDevice | Where-Object InstanceId -match '1044.*7A41' | Sort-Object InstanceId
foreach ($device in $devices) {
    $problem = try { (Get-PnpDeviceProperty -InstanceId $device.InstanceId -KeyName 'DEVPKEY_Device_ProblemCode' -ErrorAction Stop).Data } catch { '?' }
    Say ('| {0} | {1} | {2} | {3} | {4} |' -f $device.InstanceId, $device.Status, $problem,
        $before[$device.InstanceId], $after[$device.InstanceId])
}
if (-not $devices) { Say '| - | Kein Gerät 1044:7A41 gefunden - das wäre neu und wichtig | - | - | - |' }
Say ""

$composite = 'USB\VID_1044&PID_7A41\AP0000000003'
foreach ($key in 'DEVPKEY_Device_LastArrivalDate', 'DEVPKEY_Device_LastRemovalDate') {
    $value = try { (Get-PnpDeviceProperty -InstanceId $composite -KeyName $key -ErrorAction Stop).Data } catch { $null }
    if ($value) { Say ("- {0}: {1}" -f ($key -replace 'DEVPKEY_Device_', ''), $value) }
}
$parameters = Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Enum\$composite\Device Parameters" -ErrorAction SilentlyContinue
Say ("- Selektives Energiesparen erlaubt: {0}" -f $parameters.DeviceSelectiveSuspended)
$usb = powercfg /q SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 2>$null
Say ("- USB-Selektiv-Suspend im Schema: {0}" -f (($usb | Select-String 'aktuelle') -join ' · '))
Say ""

# ---------------------------------------------------------------- Umfeld ----------------
Say "## Systemprotokoll der letzten zwei Stunden (USB, HID, PnP, Energie)"
Say ""
$since = (Get-Date).AddHours(-2)
$events = Get-WinEvent -FilterHashtable @{ LogName = 'System'; StartTime = $since } -ErrorAction SilentlyContinue |
    Where-Object { $_.ProviderName -match 'USB|HID|PnP|Kernel-Power|kbd|i8042' -or $_.Message -match 'Tastatur|keyboard|HID' } |
    Sort-Object TimeCreated
foreach ($event in $events) {
    $text = ($event.Message -replace "`r`n", ' ' -replace '\s+', ' ')
    Say ('- {0:HH:mm:ss} L{1} {2} {3}' -f $event.TimeCreated, $event.Level, $event.ProviderName, $text.Substring(0, [Math]::Min(160, $text.Length)))
}
if (-not $events) { Say '- Keine passenden Ereignisse. Bei diesem Fehlerbild ist genau das der Normalfall.' }
Say ""

Say "## Letzte Zeilen des App-Protokolls"
Say ""
$log = Get-ChildItem (Join-Path $env:APPDATA 'AorusControl\logs') -Filter 'app-*.log' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1
if ($log) {
    Say ('Datei: {0}' -f $log.Name)
    Say '```'
    foreach ($line in (Get-Content $log.FullName -Tail 12)) { Say $line.Substring(0, [Math]::Min(200, $line.Length)) }
    Say '```'
} else {
    Say '- Kein App-Protokoll gefunden.'
}

Set-Content -Path $report -Value $lines -Encoding utf8
Write-Host ""
Write-Host "Bericht: $report"
Write-Host "Dieses Fenster schließt sich von selbst; es ist keine Taste nötig."
