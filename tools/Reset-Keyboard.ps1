<#
.SYNOPSIS
    Versucht, die interne Tastatur ohne hartes Ausschalten zurückzuholen.

.DESCRIPTION
    Für den Moment, in dem die Tastatur wieder tot ist. Die Schritte gehen von harmlos nach
    einschneidend, nach jedem wird geprüft, ob das Gerät zurück ist, und beim ersten Erfolg
    ist Schluss. Jede Zeile geht zusätzlich in einen Bericht unter research/runs.

    Entscheidend ist, WAS zurückgesetzt wird. Am 2026-09-17 sah der Gerätebaum im Ausfall so
    aus:

        USB\VID_1044&PID_7A41\AP0000000003   Present=False  CM_PROB_PHANTOM
        USB\VID_0000&PID_0002\5&7230AE1&0&7  Present=True   CM_PROB_FAILED_POST_START

    Die Tastatur selbst ist also gar nicht mehr da - ihr Eintrag ist eine Karteileiche. Am
    Anschluss hängt stattdessen der Platzhalter, den Windows anlegt, wenn ein Gerät nicht
    einmal seine Beschreibung beantwortet. Die erste Fassung dieses Skripts setzte die
    Karteileiche zurück und ließ den Platzhalter unangetastet; das konnte nicht wirken.

    Jetzt wird beides angefasst, und Stufe 3 macht per Software genau das, was bisher von Hand
    im Gerätemanager passierte: den Geräteeintrag entfernen und den Bus neu einlesen lassen.
    Entfernt wird nur der Eintrag, keine Treiberdatei - Windows legt ihn beim nächsten
    Erkennen neu an.

    Was NICHT geht: die 5 Volt am Anschluss abschalten. Dafür gäbe es IOCTL_USB_HUB_CYCLE_PORT,
    das aber nur der USB-2-Hubtreiber kennt; diese Tastatur hängt an einem USB-3-Root-Hub. Und
    intern verlötete Anschlüsse haben ohnehin meist keine schaltbare Stromversorgung. Hilft
    keine Stufe, bleibt nur echtes Stromlosmachen: Ruhezustand oder Herunterfahren - nicht
    Neustart, der lässt die Anschlüsse unter Spannung.

    Braucht Administratorrechte und fordert sie selbst an. Es wird keine Treiberdatei
    deinstalliert und nichts am EC geschrieben.
#>
[CmdletBinding()]
param([switch] $SkipController, [switch] $KeepOpen, [string] $OutputDirectory)

$ErrorActionPreference = 'Continue'
$keyboardPattern = '1044.*7A41'

# Die Arbeit passiert im Fenster mit Administratorrechten. Dieses hier hat dann nichts mehr
# zu sagen und geht sofort zu - ein zweites, stehengebliebenes Fenster lässt nur raten,
# welches gerade etwas tut.
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    # Der Pfad MUSS in Anfuehrungszeichen: Start-Process setzt die Argumentliste ohne eigenes
    # Quoting zusammen, und dieser Ordner heisst "Gigabyte Control Center". Ohne sie bekam das
    # zweite Fenster nur "-File C:\Users\...\chatgpt\Gigabyte" zu sehen, brach sofort ab und war
    # wieder zu, bevor man es lesen konnte - so ist das Skript nie ein einziges Mal gelaufen.
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-KeepOpen')
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

# --------------------------------------------------------------- Bericht -----------------
# Weil sonst hinterher niemand sagen kann, was passiert ist. Beim ersten Versuch am
# 2026-09-17 war genau das der Fall: Das Fenster war zu, und es gab nichts zu lesen.
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'research\runs' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$report = Join-Path $OutputDirectory ("keyboard-reset-{0:yyyyMMdd-HHmmss}.md" -f (Get-Date))

function Say([string] $text, [string] $colour = 'Gray') {
    if ($text) { Write-Host $text -ForegroundColor $colour } else { Write-Host "" }
    Add-Content -Path $script:report -Encoding utf8 -Value $text
}

Say "# Tastatur-Reset"
Say ""
Say ("- Gestartet: {0:yyyy-MM-dd HH:mm:ss}" -f (Get-Date))
Say ""

# --------------------------------------------------------------- Zustand -----------------
function Test-Keyboard {
    # Zurück ist die Tastatur erst, wenn die Tastenschnittstelle MI_00 wieder auf OK steht.
    # Das Verbundgerät allein genügt nicht: Es taucht auch dann auf, wenn die Schnittstellen
    # darunter nie starten - und als Karteileiche taucht es sogar ohne Gerät auf.
    $keys = Get-PnpDevice -ErrorAction SilentlyContinue |
        Where-Object { $_.InstanceId -match "HID.*$keyboardPattern&MI_00" }
    return [bool]($keys | Where-Object Status -eq 'OK')
}

function Show-State([string] $label) {
    $devices = @(Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object InstanceId -match $keyboardPattern)
    $ok = @($devices | Where-Object Status -eq 'OK').Count
    Say ("  {0,-16} {1} von {2} Schnittstellen auf OK, Tasten: {3}" -f $label, $ok, $devices.Count,
        $(if (Test-Keyboard) { 'DA' } else { 'weg' }))
}

function Wait-Bus([int] $seconds = 5) {
    for ($left = $seconds; $left -gt 0; $left--) {
        Write-Host ("`r  warte {0,2} s " -f $left) -NoNewline
        Start-Sleep -Seconds 1
    }
    Write-Host "`r             `r" -NoNewline
}

Say "Tastatur zurückholen - ohne hartes Ausschalten" 'Cyan'
Say "Administratorrechte liegen vor. Keine Treiberdatei wird deinstalliert." 'DarkGray'
Say ""
Show-State 'Ausgangslage:'

function Complete {
    Say ""
    Say "Bericht: $report" 'Green'
    if (-not $KeepOpen) { return }
    for ($left = 90; $left -gt 0; $left--) {
        Write-Host ("`r  Dieses Fenster schließt sich in {0,2} s von selbst. " -f $left) -NoNewline -ForegroundColor DarkGray
        Start-Sleep -Seconds 1
    }
}

if (Test-Keyboard) {
    Say ""
    Say "Die Tastatur ist angemeldet. Es gibt nichts zurückzusetzen." 'Green'
    Say "Reagiert sie trotzdem nicht, ist es nicht die USB-Anmeldung - dann sagt" 'Green'
    Say "Start-KeyboardHangDiagnose.cmd mehr als dieses Skript." 'Green'
    Complete
    return
}

# --------------------------------------------------------------- Bauplan -----------------
# Die Kette Gerät -> Root-Hub -> Controller wird über DEVPKEY_Device_Parent gelaufen statt
# fest eingetragen: Auf einem anderen Gerät sitzt sie woanders, und ein hart eingetragener
# Pfad wäre genau dann falsch, wenn man ihn braucht.
function Get-Parent([string] $instanceId) {
    try { (Get-PnpDeviceProperty -InstanceId $instanceId -KeyName 'DEVPKEY_Device_Parent' -ErrorAction Stop).Data }
    catch { $null }
}

function Find-Device([string] $pattern) {
    Get-PnpDevice -ErrorAction SilentlyContinue |
        Where-Object { $_.InstanceId -match $pattern -and $_.InstanceId -notmatch '&MI_' } |
        Select-Object -First 1
}

$composite = Find-Device '^USB\\VID_1044&PID_7A41'
$ghost = Find-Device '^USB\\VID_0000&PID_0002'
# Der Hub am liebsten über das Gerät, das wirklich am Bus hängt: Eine Karteileiche kennt ihren
# Elternteil zwar noch, aber der Platzhalter ist der, der gerade am Anschluss sitzt.
$hub = $null
foreach ($candidate in @($ghost, $composite)) {
    if ($candidate -and -not $hub) { $hub = Get-Parent $candidate.InstanceId }
}
$controller = if ($hub) { Get-Parent $hub } else { $null }

Say ""
foreach ($pair in @(@{ N = 'Verbundgerät'; D = $composite }, @{ N = 'Platzhalter '; D = $ghost })) {
    Say ("  {0}: {1}" -f $pair.N, $(if ($pair.D) {
        "{0}  Present={1}  {2}" -f $pair.D.InstanceId, $pair.D.Present, $pair.D.Problem
    } else { 'nicht vorhanden' }))
}
Say ("  Root-Hub:     {0}" -f $(if ($hub) { $hub } else { 'unbekannt' }))
Say ("  Controller:   {0}" -f $(if ($controller) { $controller } else { 'unbekannt' }))

# --------------------------------------------------------------- Schritte ----------------
function Invoke-Step([string] $name, [scriptblock] $action) {
    Say ""
    Say $name 'Cyan'
    try { & $action }
    catch { Say ("  Fehlgeschlagen: {0}" -f $_.Exception.Message) 'Red' }
    Wait-Bus 5
    Show-State 'danach:'
    return (Test-Keyboard)
}

function Restart-Device([string] $instanceId, [string] $what) {
    Say ("  $what wird neu gestartet ...") 'Yellow'
    try { Disable-PnpDevice -InstanceId $instanceId -Confirm:$false -ErrorAction Stop }
    catch { Say ("  Abschalten meldete: {0}" -f $_.Exception.Message.Trim()) 'DarkGray' }
    try { Start-Sleep -Seconds 3 }
    finally { Restore-Device $instanceId }
}

<#
.SYNOPSIS
    Schaltet den Root-Hub aus und wieder ein - mit einem Wächter, der ihn auch dann
    zurückholt, wenn dieses Skript stirbt.

.DESCRIPTION
    Das ist die Stufe, die am 2026-09-17 als einzige gewirkt hat: Danach meldete sich die
    Tastatur mit frischen Instanz-IDs neu an, ohne hartes Ausschalten.

    Sie ist zugleich die gefährlichste, und am selben Tag ist genau das schiefgegangen. Zwei
    Dinge waren falsch:

    Erstens wurde der Fehlermeldung geglaubt. Windows antwortete "Nicht unterstützt" und setzte
    das Deaktiviert-Kennzeichen trotzdem. Der Code sah einen Fehlschlag, sparte sich das
    Wiedereinschalten - und Maus und Tastatur waren weg. Deshalb wird jetzt nachgesehen statt
    geglaubt: Was zählt, ist der zurückgelesene Zustand.

    Zweitens schützt finally nur gegen eine Ausnahme, nicht gegen ein geschlossenes Fenster
    oder einen beendeten Prozess. Ein deaktiviertes Gerät bleibt deaktiviert, auch über einen
    Neustart hinweg - wer sich den Hub abschaltet, an dem die Eingabegeräte hängen, kommt ohne
    fremde Hilfe nicht mehr heraus.

    Also ein eigener Prozess als Wächter, nach demselben Muster wie die Lüfterleine der App:
    Er wird vorher gestartet, schläft, und schaltet dann ein - egal was hier passiert.
#>
function Restart-Hub([string] $instanceId, [string] $what) {
    Say ("  $what wird aus- und wieder eingeschaltet.") 'Yellow'
    Say "  Maus und Tastatur sind dabei fuer ein paar Sekunden weg - das gehoert dazu." 'DarkGray'

    $guard = "Start-Sleep -Seconds 30; Enable-PnpDevice -InstanceId '$instanceId' -Confirm:`$false -ErrorAction SilentlyContinue"
    try {
        Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $guard) -ErrorAction Stop | Out-Null
        Say "  Waechter laeuft: schaltet spaetestens in 30 s wieder ein, auch wenn dieses Fenster stirbt." 'DarkGray'
    }
    catch {
        Say ("  Waechter konnte nicht gestartet werden ({0}) - Schritt wird uebersprungen." -f $_.Exception.Message.Trim()) 'Red'
        return
    }

    try { Disable-PnpDevice -InstanceId $instanceId -Confirm:$false -ErrorAction Stop }
    catch { Say ("  Abschalten meldete: {0}" -f $_.Exception.Message.Trim()) 'DarkGray' }
    # Nachsehen statt glauben: Genau hier ging es schief.
    Say ("  zurueckgelesen: {0}" -f (Get-PnpDevice -InstanceId $instanceId -ErrorAction SilentlyContinue).Problem)
    Start-Sleep -Seconds 5
    Restore-Device $instanceId
}

<#
.SYNOPSIS
    Schaltet ein Gerät wieder ein und gibt nicht eher Ruhe, bis der Zustand es bestätigt.
#>
function Restore-Device([string] $instanceId) {
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try { Enable-PnpDevice -InstanceId $instanceId -Confirm:$false -ErrorAction Stop } catch { }
        Start-Sleep -Seconds 2
        $state = Get-PnpDevice -InstanceId $instanceId -ErrorAction SilentlyContinue
        if (-not $state -or $state.Problem -ne 'CM_PROB_DISABLED') {
            Say ("  wieder eingeschaltet (Versuch {0})." -f $attempt) 'Green'
            return
        }
    }
    Say "  EINSCHALTEN FEHLGESCHLAGEN - bitte Enable-DisabledUsb.cmd ausfuehren." 'Red'
}

function Remove-Node([string] $instanceId, [string] $what) {
    # Genau das, was bisher von Hand im Gerätemanager passierte: den Eintrag entfernen und neu
    # erkennen lassen. /remove-device löscht nur den Knoten, keine Treiberdatei.
    Say ("  Geräteeintrag $what wird entfernt ...") 'Yellow'
    $output = & pnputil /remove-device $instanceId 2>&1
    Say ("  {0}" -f (($output | Where-Object { $_ -match '\S' }) -join ' · '))
}

$steps = @(
    @{ Name = '1. Bus neu einlesen'; Action = { pnputil /scan-devices | Out-Null } }
)
if ($ghost) {
    $steps += @{ Name = '2. Platzhalter am Anschluss zurücksetzen'; Action = { Restart-Device $ghost.InstanceId 'Der Platzhalter' }.GetNewClosure() }
    $steps += @{ Name = '3. Geräteeinträge entfernen und neu erkennen lassen'; Action = {
        Remove-Node $ghost.InstanceId 'des Platzhalters'
        if ($composite) { Remove-Node $composite.InstanceId 'der Tastatur' }
        pnputil /scan-devices | Out-Null
    }.GetNewClosure() }
}
elseif ($composite) {
    $steps += @{ Name = '2. Geräteeintrag der Tastatur entfernen und neu erkennen lassen'; Action = {
        Remove-Node $composite.InstanceId 'der Tastatur'
        pnputil /scan-devices | Out-Null
    }.GetNewClosure() }
}
if ($hub) {
    $steps += @{ Name = '4. Root-Hub aus und wieder an - hat am 2026-09-17 als einzige Stufe gewirkt'; Action = { Restart-Hub $hub 'Der Root-Hub' }.GetNewClosure() }
}
if ($controller -and -not $SkipController) {
    $steps += @{ Name = '5. USB-Controller neu starten (alles USB blinkt kurz weg)'; Action = { Restart-Hub $controller 'Der USB-Controller' }.GetNewClosure() }
}

foreach ($step in $steps) {
    if (Invoke-Step $step.Name $step.Action) {
        Say ""
        Say "Die Tastatur ist zurück. Bitte gleich ausprobieren." 'Green'
        Say ("Geholfen hat: {0}" -f $step.Name) 'Green'
        Complete
        return
    }
}

Say ""
Say "Kein Schritt hat geholfen." 'Red'
Say ""
Say "Damit ist Software am Ende: Die 5 Volt am Anschluss kann sie nicht abschalten." 'Red'
Say "Was noch Strom wegnimmt, ohne den Einschaltknopf zu quaelen:" 'Red'
Say "  - Ruhezustand  (shutdown /h)   - kommt ohne Kaltstart zurueck" 'Red'
Say "  - Herunterfahren (shutdown /s /t 0) - NICHT Neustart, der laesst Strom drauf" 'Red'
Complete
