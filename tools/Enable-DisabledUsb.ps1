<#
.SYNOPSIS
    Schaltet abgeschaltete USB-Geräte wieder ein - Rettung, wenn Maus und Tastatur weg sind.

.DESCRIPTION
    Ein deaktiviertes Gerät bleibt deaktiviert. Auch über einen Neustart, auch über ein
    Herunterfahren: Windows merkt sich das im Gerätebaum, nicht im Arbeitsspeicher. Wer sich
    auf diese Weise den Root-Hub abschaltet, an dem Maus und Tastatur hängen, kommt durch
    bloßes Neustarten nicht mehr heraus.

    Genau das ist am 2026-09-17 passiert: Reset-Keyboard.ps1 rief Disable-PnpDevice auf den
    Root-Hub auf. Windows meldete "Nicht unterstützt" - und setzte das Deaktiviert-Kennzeichen
    trotzdem. Der Fehler wurde geglaubt, das Wiedereinschalten unterblieb.

    Dieses Skript sucht jedes USB-Gerät mit CM_PROB_DISABLED und schaltet es wieder ein. Es
    fordert Administratorrechte selbst an; bedienbar bleibt es allein mit dem Touchpad.
#>
[CmdletBinding()]
param([switch] $KeepOpen)

$ErrorActionPreference = 'Continue'

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    # Der Pfad in Anführungszeichen: Start-Process quotet nicht selbst, und dieser Ordner
    # heißt "Gigabyte Control Center".
    Start-Process powershell.exe -Verb RunAs -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-KeepOpen')
    Write-Host ""
    Write-Host "Es geht im neuen Fenster mit Administratorrechten weiter." -ForegroundColor Cyan
    Start-Sleep -Seconds 2
    return
}

Write-Host ""
Write-Host "Abgeschaltete USB-Geraete wieder einschalten" -ForegroundColor Cyan
Write-Host ""

$disabled = @(Get-PnpDevice -ErrorAction SilentlyContinue |
    Where-Object { $_.Problem -eq 'CM_PROB_DISABLED' -and ($_.Class -eq 'USB' -or $_.InstanceId -like 'USB\*') })

if (-not $disabled) {
    Write-Host "Kein abgeschaltetes USB-Geraet gefunden." -ForegroundColor Green
}
foreach ($device in $disabled) {
    Write-Host ("  {0}" -f $device.FriendlyName) -ForegroundColor Yellow
    Write-Host ("  {0}" -f $device.InstanceId) -ForegroundColor DarkGray
    try {
        Enable-PnpDevice -InstanceId $device.InstanceId -Confirm:$false -ErrorAction Stop
        Write-Host "  eingeschaltet." -ForegroundColor Green
    }
    catch {
        # Auch hier gilt: Der gemeldete Fehler sagt nicht, was wirklich passiert ist. Also
        # nachsehen statt glauben.
        Write-Host ("  Meldung: {0}" -f $_.Exception.Message.Trim()) -ForegroundColor DarkGray
    }
    Start-Sleep -Seconds 2
    $now = Get-PnpDevice -InstanceId $device.InstanceId -ErrorAction SilentlyContinue
    Write-Host ("  jetzt: Status={0} Problem={1}" -f $now.Status, $now.Problem) -ForegroundColor $(
        if ($now.Problem -eq 'CM_PROB_DISABLED') { 'Red' } else { 'Green' })
}

pnputil /scan-devices | Out-Null
Write-Host ""
$stillDisabled = @(Get-PnpDevice -ErrorAction SilentlyContinue |
    Where-Object { $_.Problem -eq 'CM_PROB_DISABLED' -and ($_.Class -eq 'USB' -or $_.InstanceId -like 'USB\*') })
if ($stillDisabled) {
    Write-Host "Noch immer abgeschaltet - bitte im Geraete-Manager von Hand aktivieren:" -ForegroundColor Red
    $stillDisabled | ForEach-Object { Write-Host ("  {0}" -f $_.FriendlyName) -ForegroundColor Red }
    Write-Host "  devmgmt.msc -> USB-Controller -> Rechtsklick -> Geraet aktivieren" -ForegroundColor Red
} else {
    Write-Host "Alle USB-Geraete sind eingeschaltet." -ForegroundColor Green
}

if ($KeepOpen) {
    Write-Host ""
    for ($left = 60; $left -gt 0; $left--) {
        Write-Host ("`r  Dieses Fenster schliesst sich in {0,2} s. " -f $left) -NoNewline -ForegroundColor DarkGray
        Start-Sleep -Seconds 1
    }
}
