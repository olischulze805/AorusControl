# AORUS Control

Replacement control application research for the Gigabyte AORUS 5 SE / SE4 with BIOS FB0F.

## Projects

- `AorusControl.App`: Windows WPF application and the future user-facing control panel.
- `AorusControl.Diagnostics`: privacy-safe hardware research, reports, and console live monitor.
- `AorusControl.Core`: shared device compatibility checks, read-only Gigabyte WMI telemetry, guarded battery charge-limit logic, and exact-device three-zone keyboard RGB control.

The first two are executable applications. `Core` is deliberately separated so both applications use one reviewed hardware boundary.

## Safety status

- BIOS and EC firmware are never flashed or modified.
- Telemetry opens only `GB_WMIACPI_Get`. The separate battery controller may open `GB_WMIACPI_Set` only after its exact device, BIOS, method, value, and administrator gates pass.
- Live telemetry is allowed only for exact model `AORUS 5 SE` and BIOS `FB0F`.
- Battery writes are restricted to raw policy 0/4 and thresholds 60–100%, save the original pair, verify readback, and automatically restore plus verify the original pair on failure. No battery setter is exposed by the graphical application yet.
- Keyboard control is locked to USB device `1044:7A41`, interface `MI_03`, and a 9-byte feature report. The application exposes proven three-zone colors, the four verified brightness steps, and nine host-rendered effects. Static changes are verified by readback. An effect captures the three original zones and restores plus verifies them when it is stopped, replaced, or the application closes. No firmware-effect, firmware, key-matrix, or macro write is exposed.
- The keyboard's 17-byte feature report ID `0x5A` on `MI_02/COL_07` was identified as the ITE firmware flash channel via `SHFU.ini` (`REPORTID=90`). It is never written; a single earlier passive read remains the only contact.
- Research details and the verified ACPI method map are in `RESEARCH.md`.

## Repository layout

| Path | Contents |
|---|---|
| `src/` | The three projects above, plus `AorusControl.Worker` (the elevated hardware worker that keeps Fixed fan mode crash-safe). |
| `tests/` | `AorusControl.App.SmokeTests`, a self-contained console test suite - no test framework, no hardware. |
| `tools/` | Launchers (`.cmd` + `.ps1`) for the app and for each hardware experiment, and `RecoverDiagnostics`, a small Roslyn helper written for this project. |
| `research/` | The reverse-engineering record: findings, decisions, and the timestamped run reports they are based on. |
| `third-party/` | **Not in version control.** Vendor installers, everything extracted or decompiled from them, and downloaded analysis tools. See `third-party/README.md` for the expected layout. |

Hardware-facing settings the app writes are stored per user under
`%LocalAppData%\AorusControl\` (keyboard selection, fan curve, recent colours), and logs
land in `%LocalAppData%\AorusControl\logs`. Nothing machine-specific lives in the
repository.

## Build

Everything needed to build and test is in the repository; the ignored `third-party/`
folder is only required to reproduce the reverse-engineering documented under `research/`.

```powershell
dotnet build AorusControl.slnx --configuration Release
dotnet run --project tests/AorusControl.App.SmokeTests
```

Requires the .NET SDK pinned in `global.json`. The application targets
`net10.0-windows` and is Windows-only.

## Run

- Graphical application: double-click `tools\Start-AorusControl.cmd`.
- Console live monitor: double-click `tools\Start-AorusMonitor.cmd`.
- Stop the console monitor with `Ctrl+C`.

Direct ACPI telemetry requires administrator rights. Windows will show its normal UAC prompt.

The graphical application now reads and controls the three keyboard RGB zones. Click a zone color to open the app's own themed color picker (gradient square, hue bar, hex box, recent colors - see `research/UI-DESIGN-SYSTEM.md`), or enable `Alle drei Zonen gemeinsam ändern` before choosing a color. The Tastatur section shows a live preview of this exact keyboard - full layout including the numeric pad, keys lit per zone - rendered from `KeyboardEffectFrames`, the same function whose output is written to the device, so it shows the actual frame rather than an imitation. Effects are picked from a grid of icon tiles (the running one pulses); brightness and tempo are sliders over the firmware's own steps. The brightness selector offers all four hardware steps — off, low, medium, and high — which map to the only raw values the firmware accepts, `0`, `24`, `32`, and `50`. Writing one of them overrides the step last chosen with `Fn+Space`. No slider is offered, because every other value is either off or full brightness. The effect selector offers Breathing, Pulse, Colour cycle, Rainbow marquee, Wave, Marquee, Rotate, Raindrop, and Fade sweep, with a `Tempo` selector offering five speeds. Speed is a host-side time scale, not Gigabyte's firmware speed byte, which belongs to the global effect command that renders nothing on this firmware. These effects are rendered by the application through the proven zone protocol; `Stoppen`, another RGB change, or closing the application restores the state captured before the effect.

Read the current three-zone keyboard colors without administrator rights:

```powershell
dotnet run --project src/AorusControl.Diagnostics -- --query-keyboard-rgb
```

The result is also saved as a timestamped Markdown file under `research/runs/`.

Read the current battery policy and stored stop value with the elevated launcher
`tools\Start-AorusBatteryInspection.cmd`, or run its script directly:

```powershell
powershell -ExecutionPolicy Bypass -File tools\Start-AorusBatteryInspection.ps1
```

### Cooling, power and GPU

The WPF app now includes simple fan-profile buttons, a fixed raw-value selector and
the three Windows performance modes (AC only). Fixed mode is crash-safe: setting it
acquires a lease from an independent `AorusControl.Worker.exe` background process
(started, elevated, on first use), whose own `FanSafetySupervisor` restores Normal
by itself — on overheat, stale telemetry, or the main app crashing outright — even if
the WPF app is no longer running. App-set Maximum/Dynamic are restored on normal
close of the app itself. See `research/WORKER-ARCHITECTURE.md` for the crash-safety
design and `research/FAN-POWER-APP-INTEGRATION.md` for verification limits.

The Cooling section also has a live fan curve editor - a draggable point-and-line chart
(temperature °C × fan speed %), not a table of numbers. It starts out showing whatever
curve is currently on the EC (or your last saved one); drag any point (the last one is
fixed - the firmware requires 100% by 90 °C at the latest) and "Kurve übernehmen" writes it
to the EC, switches into Dynamic mode to activate it, and saves it to
`%LocalAppData%\AorusControl\fan-curve-v1.json` for next time. Every drag is already
clamped to a valid shape live (25–100% / 57–229 raw range, non-decreasing), so nothing
gets rejected as a surprise at "Übernehmen" time. Fan speed is shown as a percentage
everywhere in the app now (including the Fixed-fan dropdown), on the same "raw 229 = 100%"
basis the Dashboard's duty readout already used.

The app's UI uses [WPF-UI](https://github.com/lepoco/wpfui) (Fluent/Windows 11 style
controls, Mica backdrop, dark theme) with per-feature control choices explained in
`research/UI-DESIGN-SYSTEM.md`. The Info & Updates section can check a static JSON
manifest for a newer release (never downloads or auto-installs — see the same doc for
why); no real release feed exists yet, so it will report "check failed" until one does.
The same section also has an Autostart toggle, backed by a Scheduled Task rather than the
registry Run key specifically so it never shows a fresh UAC prompt at every login (see
`research/UI-DESIGN-SYSTEM.md` for why that distinction matters for an admin-required app).

Build the app independently with `dotnet build src/AorusControl.App -c Release`.
The diagnostics build has been restored (see `research/DIAGNOSTICS-RECOVERY.md`);
its power monitor now uses batched interval sampling (see `research/POWER-MONITOR-V2.md`
for measured overhead and remaining validation). The fan rescue
launcher uses the app's dedicated `--restore-fan-normal` path and does not
depend on diagnostics. Unlike the diagnostic launchers below, this recovery path
reports its result in a dialog, not a timestamped Markdown report.

Every launcher below has both a `.cmd` for double-clicking and a `.ps1`. All of
them elevate themselves, so Windows shows its normal UAC prompt, and each writes a
timestamped report under `research/runs/`.

| Launcher | Purpose |
|---|---|
| `Start-ThermalPowerInspection.cmd` | read-only capability report: temperatures, RPM, duty, all fan mode getters, the fifteen curve points, the five-point `GetDeepFan` interface, thermal sensors, Windows power state, displays, and NVIDIA runtime |
| `Start-FanNormalRestore.cmd` | rescue path: return both fans to firmware/normal control |
| `Start-FanQuietTest.cmd` | temporary Quiet profile, restored afterwards |
| `Start-FanGamingTest.cmd` | temporary Gaming/Power profile, restored afterwards |
| `Start-FanMaximumTest.cmd` | temporary Maximum profile, restored afterwards |
| `Start-FanFixedScaleTest.cmd` | measures the upper fixed-duty raw scale |
| `Start-FanFixedLowScaleTest.cmd` | measures the lower fixed-duty raw scale |
| `Start-FanDynamicTest.cmd` | temporary Dynamic profile with the unchanged original curve |
| `Start-FanCurveWriteTest.cmd` | writes a single curve point, verifies it, and restores all fifteen |
| `Start-WindowsPowerModeTest.cmd` | cycles the three Windows power overlays and restores the original |

Fan behaviour, the measured raw scale, and the safety rules are documented in
`research/FAN-POWER-GPU-CONTROL.md`. `Start-FanNormalRestore.cmd` works
independently of the graphical application and is the intended recovery step if a
fan test is interrupted.

### Crash-safe Fixed fan mode

Fixed mode is the only fan profile that does not correct itself, so the app
never writes it directly. It asks `AorusControl.Worker` for a time-limited
lease instead; the worker runs on its own, keeps a `FanSafetySupervisor` that
independently expires and restores Normal roughly every 10 seconds if nobody
renews it, and survives the graphical application being killed outright. Full
design, the two real bugs found while building it, and a known stray-process
caveat: `research/WORKER-ARCHITECTURE.md`.

`tools\Test-WorkerCrashSafety.cmd` proves this end to end: it starts the worker
elevated, has a throwaway client acquire Fixed and exit immediately (simulating
a crash right after acquiring), waits 15 seconds without contacting the worker
again, and shows that the fans were restored to Normal on the worker's own
initiative.

The battery write diagnostic deliberately requires an explicit target and a second confirmation token. It is not run automatically. Protocol evidence and the rollback design are recorded in `research/BATTERY-CHARGE-LIMIT.md`.

Probe the second lighting path found in Gigabyte's signed module without administrator rights. It sends only the official picture-matrix getter `0x92`; the setter `0x12` is not implemented:

```powershell
dotnet run --project src/AorusControl.Diagnostics -- --probe-keyboard-picture-matrix --slot 1
```

`--slot` selects custom slot 1 to 5. Findings and the current state of the effect investigation are in `research/RGB-EFFECT-INVESTIGATION.md`.

The picture-matrix write test deliberately requires a typed confirmation plus a second confirmation token, like the battery write diagnostic. It writes one 512-byte matrix into a custom slot with Gigabyte's official `0x12` command, activates the matching Custom effect, asks for an observation, and then rewrites plus verifies both the saved slot and the three zone colours. Start it with `tools\Start-PictureMatrixWriteTest.cmd`. Evidence for the path is in `research/RGB-EFFECT-INVESTIGATION.md`.

Run the interactive host-rendered effect test by double-clicking `tools\Start-HostEffectTest.cmd`. Ten host-rendered effects are offered; each keeps animating until Enter is pressed, entered text is saved as the observation for that effect, and `/stop` ends early. The original three-zone colours are captured before the test and restored plus verified afterwards.

Render lighting effects in host software using only the proven zone commands. It measures the fastest zone-write interval that still verifies, then plays Breathing, a colour cycle, and a three-zone wave, and finally restores plus verifies the original zone colours:

```powershell
dotnet run --project src/AorusControl.Diagnostics -- --test-keyboard-host-effects --seconds 8
```

For research, an elevated `--monitor-keyboard-brightness --seconds 25` mode correlates the DSDT-backed `KBLL` getter with vendor-only HID input while the user presses `Fn+Space`. It never captures standard keyboard interfaces and sends no state-changing command.

Run the interactive physical RGB-effect test by double-clicking `tools\Start-KeyboardEffectTest.cmd`. Each effect stays active until Enter is pressed; entered text is saved as the observation for that effect, and `/stop` ends early. The original three-zone colors are captured before the test and restored plus verified afterward.
