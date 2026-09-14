<#
.SYNOPSIS
    Versucht, die interne Tastatur ohne hartes Ausschalten zurückzuholen.

.DESCRIPTION
    Für den Moment, in dem die Tastatur wieder tot ist. Die Schritte werden von harmlos nach
    einschneidend abgearbeitet und nach jedem wird geprüft, ob das Gerät zurück ist; beim
    ersten Erfolg ist Schluss.

        1. Bus neu einlesen          - nichts wird abgeschaltet
        2. Tastatur aus und wieder an - USB-Reset des Geräts, falls es überhaupt noch da ist
        3. Root-Hub neu starten       - nimmt die Maus für ein paar Sekunden mit
        4. USB-Controller neu starten - das Nächste, was Software an einem Stromstoß hat

    Was hier NICHT geht, und warum: Die 5 Volt am Anschluss lassen sich per Software nicht
    abschalten. Dafür gäbe es IOCTL_USB_HUB_CYCLE_PORT, das aber nur der USB-2-Hubtreiber
    kennt; die Tastatur hängt an einem USB-3-Root-Hub. Und intern verlötete Anschlüsse haben
    ohnehin meist keine schaltbare Stromversorgung. Bleibt Schritt 4: Der Controller geht
    kurz in den PCI-Schlafzustand und kommt zurück - ob die Anschlüsse dabei stromlos werden,
    entscheidet die Hardware, nicht Windows.

    Hilft nichts davon, ist wirklich nur noch echtes Stromlosmachen übrig: Ruhezustand oder
    Herunterfahren (nicht Neustart - der lässt die Anschlüsse unter Spannung).

    Braucht Administratorrechte und fordert sie selbst an. Es wird nichts installiert, nichts
    deinstalliert und keine Treiberdatei angefasst.
#>
[CmdletBinding()]
param([switch] $SkipController, [switch] $KeepOpen)

$ErrorActionPreference = 'Continue'
$keyboardPattern = '1044.*7A41'

# Die Arbeit passiert im Fenster mit Administratorrechten. Dieses hier hat dann nichts mehr
# zu sagen und geht sofort zu - ein zweites, stehengebliebenes Fenster lässt nur raten,
# welches gerade etwas tut.
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-KeepOpen')
    if ($SkipController) { $arguments += '-SkipController' }
    try {
        Start-Process powershell.exe -ArgumentList $arguments -Verb RunAs -ErrorAction Stop
        Write-Host ""
        Write-Host "Es geht im neuen Fenster mit Administratorrechten weiter." -ForegroundColor Cyan
        Write-Host "Dieses hier wird nicht mehr gebraucht." -ForegroundColor Cyan
        Start-Sleep -Seconds 2
    } catch {
        Write-Host ""
        Write-Host "Ohne Administratorrechte geht es nicht: $($_.Exception.Message)" -ForegroundColor Red
        Start-Sleep -Seconds 20
    }
    return
}

# Alle Ausgänge laufen hier durch. Beim Start per Doppelklick würde das Fenster sonst im
# selben Moment zugehen, in dem das Ergebnis darin steht - und ausgerechnet hier kann man
# nicht davon ausgehen, dass jemand schnell genug eine Taste findet.
function Complete {
    if (-not $KeepOpen) { return }
    Write-Host ""
    for ($left = 90; $left -gt 0; $left--) {
        Write-Host ("`r  Dieses Fenster schließt sich in {0,2} s von selbst. " -f $left) -NoNewline -ForegroundColor DarkGray
        Start-Sleep -Seconds 1
    }
}

# --------------------------------------------------------------- Zustand -----------------
function Test-Keyboard {
    # Zurück ist die Tastatur erst, wenn die Tastenschnittstelle MI_00 wieder auf OK steht.
    # Das Verbundgerät allein genügt nicht: Es taucht auch dann auf, wenn die Schnittstellen
    # darunter nie starten.
    $keys = Get-PnpDevice -ErrorAction SilentlyContinue |
        Where-Object { $_.InstanceId -match "HID.*$keyboardPattern&MI_00" }
    return [bool]($keys | Where-Object Status -eq 'OK')
}

function Show-State([string] $label) {
    $devices = @(Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object InstanceId -match $keyboardPattern)
    $ok = @($devices | Where-Object Status -eq 'OK').Count
    Write-Host ("  {0,-22} {1} von {2} Schnittstellen auf OK, Tasten: {3}" -f $label, $ok, $devices.Count,
        $(if (Test-Keyboard) { 'DA' } else { 'weg' }))
}

function Wait-Bus([int] $seconds = 5) {
    for ($left = $seconds; $left -gt 0; $left--) {
        Write-Host ("`r  warte {0,2} s " -f $left) -NoNewline
        Start-Sleep -Seconds 1
    }
    Write-Host "`r             `r" -NoNewline
}

Write-Host ""
Write-Host "Tastatur zurückholen - ohne hartes Ausschalten" -ForegroundColor Cyan
Write-Host "Administratorrechte liegen vor. Es wird nichts installiert und kein Treiber angefasst." -ForegroundColor DarkGray
Write-Host ""
Show-State 'Ausgangslage:'

if (Test-Keyboard) {
    Write-Host ""
    Write-Host "Die Tastatur ist angemeldet. Es gibt nichts zurückzusetzen." -ForegroundColor Green
    Write-Host "Reagiert sie trotzdem nicht, ist es nicht die USB-Anmeldung - dann sagt" -ForegroundColor Green
    Write-Host "Start-KeyboardHangDiagnose.cmd mehr als dieses Skript." -ForegroundColor Green
    Complete
    return
}

# --------------------------------------------------------------- Bauplan -----------------
# Die Kette Tastatur -> Root-Hub -> Controller wird nicht fest eingetragen, sondern über
# DEVPKEY_Device_Parent gelaufen: Auf einem anderen Gerät sitzt sie woanders, und ein hart
# eingetragener Pfad wäre genau dann falsch, wenn man ihn braucht.
function Get-Parent([string] $instanceId) {
    try { (Get-PnpDeviceProperty -InstanceId $instanceId -KeyName 'DEVPKEY_Device_Parent' -ErrorAction Stop).Data }
    catch { $null }
}

# Das Verbundgerät, nicht eine seiner Schnittstellen: Es ist der Elternteil aller vier
# und damit das, was ein USB-Reset zurücksetzen muss.
$composite = (Get-PnpDevice -ErrorAction SilentlyContinue |
    Where-Object { $_.InstanceId -match '^USB\\VID_1044&PID_7A41' -and $_.InstanceId -notmatch '&MI_' } |
    Select-Object -First 1).InstanceId

# Der Hub ist der Elternteil der Tastatur; ist die Tastatur ganz verschwunden, hilft der
# Platzhalter am selben Anschluss oder - als letzter Ausweg - der Hub der Maus.
$hub = if ($composite) { Get-Parent $composite } else { $null }
if (-not $hub) {
    $ghost = (Get-PnpDevice -ErrorAction SilentlyContinue |
        Where-Object { $_.InstanceId -match '^USB\\VID_0000&PID_0002' } | Select-Object -First 1).InstanceId
    if ($ghost) { $hub = Get-Parent $ghost }
}
$controller = if ($hub) { Get-Parent $hub } else { $null }

Write-Host ""
Write-Host "  Verbundgerät: $(if ($composite) { $composite } else { 'nicht vorhanden' })"
Write-Host "  Root-Hub:     $(if ($hub) { $hub } else { 'unbekannt' })"
Write-Host "  Controller:   $(if ($controller) { $controller } else { 'unbekannt' })"
Write-Host ""

# --------------------------------------------------------------- Schritte ----------------
function Restart-Device([string] $instanceId, [string] $what) {
    Write-Host "  $what wird neu gestartet ..." -ForegroundColor Yellow
    try {
        Disable-PnpDevice -InstanceId $instanceId -Confirm:$false -ErrorAction Stop
    } catch {
        Write-Host "  Abschalten fehlgeschlagen: $($_.Exception.Message)" -ForegroundColor Red
        return
    }
    # Das Wiedereinschalten steht in finally, weil ein abgeschaltet zurückbleibender
    # Controller auch die Maus mitnimmt - und dann gäbe es gar keine Eingabe mehr.
    try { Start-Sleep -Seconds 3 }
    finally {
        try { Enable-PnpDevice -InstanceId $instanceId -Confirm:$false -ErrorAction Stop }
        catch { Write-Host "  EINSCHALTEN FEHLGESCHLAGEN: $($_.Exception.Message)" -ForegroundColor Red }
    }
}

$steps = @(
    @{ Name = '1. Bus neu einlesen'; Action = { pnputil /scan-devices | Out-Null } }
)
if ($composite) {
    $steps += @{ Name = '2. Tastatur aus und wieder an'; Action = { Restart-Device $composite 'Die Tastatur' }.GetNewClosure() }
}
if ($hub) {
    $steps += @{ Name = '3. Root-Hub neu starten (die Maus blinkt kurz weg)'; Action = { Restart-Device $hub 'Der Root-Hub' }.GetNewClosure() }
}
if ($controller -and -not $SkipController) {
    $steps += @{ Name = '4. USB-Controller neu starten (alles USB blinkt kurz weg)'; Action = { Restart-Device $controller 'Der USB-Controller' }.GetNewClosure() }
}

foreach ($step in $steps) {
    Write-Host ""
    Write-Host $step.Name -ForegroundColor Cyan
    & $step.Action
    Wait-Bus 5
    Show-State 'danach:'
    if (Test-Keyboard) {
        Write-Host ""
        Write-Host "Die Tastatur ist zurück. Bitte gleich ausprobieren." -ForegroundColor Green
        Write-Host "Welcher Schritt geholfen hat, gehört in research/KEYBOARD-HANG.md." -ForegroundColor Green
        Complete
        return
    }
}

Write-Host ""
Write-Host "Kein Schritt hat geholfen." -ForegroundColor Red
Write-Host ""
Write-Host "Damit ist Software am Ende: Die 5 Volt am Anschluss kann sie nicht abschalten." -ForegroundColor Red
Write-Host "Was noch Strom wegnimmt, ohne den Einschaltknopf zu quaelen:" -ForegroundColor Red
Write-Host "  - Ruhezustand  (shutdown /h)   - kommt ohne Kaltstart zurueck" -ForegroundColor Red
Write-Host "  - Herunterfahren (shutdown /s /t 0) - NICHT Neustart, der laesst Strom drauf" -ForegroundColor Red
Complete
