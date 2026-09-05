# App architecture: feature modules, and what the shell keeps

## Why this exists

`MainWindowViewModel` had grown to 1562 lines holding every feature at once: lighting timers,
fan leases, power modes, telemetry and startup state in one object, with a dozen busy flags
that each feature had to step around. Adding anything meant editing three methods and hoping.

It is now a shell that composes modules. Each feature is one folder under
`src/AorusControl.App/Features/`, one class, and one entry in a list.

## The contract

`IFeatureModule` is deliberately tiny:

```csharp
Task StartAsync();   // read your own device state; never throw
bool IsBusy { get; } // a write is in flight
void Dispose();      // release the hardware
```

The shell iterates the list rather than naming each feature:

```csharp
private IReadOnlyList<IFeatureModule> Modules => [Keyboard, Cooling, Battery];
```

Adding a module means writing the class, adding it to that list, and adding its section to
the window. Nothing else in the shell changes.

`StartAsync` must not throw. A feature whose hardware is missing says so in its own status
text and leaves the rest of the app working - a keyboard that failed to enumerate is not a
reason to lose fan control.

## What stays in the shell

Only what is genuinely shared or genuinely the window's:

- **The telemetry clock.** One reader, one timer, feeding the dashboard.
- **The close choreography.** Flush pending writes, stop listening, wait for every module to
  go idle, hand the hardware back, and - if a handback fails - undo all of that and keep the
  window open.
- **Section visibility**, so a module can stop animating for a view nobody is looking at.
- **The Windows power mode and autostart**, which are Windows state rather than device state.

## Why cooling's safety is not hidden inside cooling

`CoolingViewModel` owns the fan hardware, but the moments that keep a pinned fan safe are on
its public surface rather than behind a private timer:

| Moment | Called by | Why it is not internal |
|---|---|---|
| `RenewFixedLeaseAsync` | every telemetry tick | The lease must be renewed against the *same* readings the user sees; a second clock inside the module could drift from it or keep renewing while the first has stalled. |
| `AbandonFixedAsync` | telemetry failure, stopping monitoring | Losing telemetry is a shell-level event. The fans must be released because the supervision ended, not because the fan module noticed something. |
| `HandBackAsync` | window closing | Throws on failure, which is what keeps the window open instead of closing over a machine left pinned. |
| `RestoreFansToFirmware` | dispose, Windows shutdown | Synchronous and best-effort: `SessionEnding` gives a process seconds, enough for one EC write. |

The worker's own supervisor remains the real guarantee - it restores Normal even if this
process disappears entirely. These calls are the app being a good citizen on top of that,
never a replacement for it.

## Windows integration decisions

| Problem this class of app usually has | What is done here |
|---|---|
| A console window flashes or sits open while a helper runs | The worker is a `WinExe`, so no console is ever allocated; `ConsoleAttach` reattaches to a parent console for its CLI modes. Elevation needs ShellExecute, which cannot suppress a console - so there must not be one to suppress. |
| Blurry on a second monitor | `dpiAwareness: PerMonitorV2` in the manifest, where it is read before any code runs. |
| A UAC prompt at every login | Autostart is a scheduled task marked to run elevated, not the registry `Run` key. |
| Settings silently lost after sleep | Lighting is reapplied after resume, with a delay for the USB device to re-enumerate. |
| Fans left pinned after a shutdown | `SessionEnding` hands them back. |
| A pane that renders light when transparency effects are off | The navigation pane sets its background explicitly instead of relying on Mica. |
| Autostart that throws its window in your face at every login | The logon task passes `--background`; the app starts into the tray and stays there until asked for. |
| Having to open the window for one thing | The tray menu can put the fans back on Normal and toggle the lighting without it. |
| Two copies fighting over the device | A named single-instance gate; the second copy activates the first and exits. |
| No idea why something failed | Every error goes to `%LocalAppData%\AorusControl\logs`, app and worker alike, one file per day, 14-day retention, and the folder is one button away in the app. |

## Verifying the UI without running it

`tests/AorusControl.UiChecks` builds the **real** window against fakes and lays it out
offscreen at 720, 1000 and 1600 pixels for every section, saving a PNG of each. It needs no
OS window, no elevation and no hardware, and it catches what the compiler cannot: a missing
resource key, a style based on one defined later in the dictionary, a layout that collides
at a narrow width.

Two bugs were found by looking at those images rather than by reasoning about the XAML: the
Fixed slider's end labels overlapped below ~800 px, and the navigation pane was relying on
Mica for its background.
