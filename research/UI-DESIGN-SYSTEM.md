# UI design system

## Framework choice

The app now uses [WPF-UI](https://github.com/lepoco/wpfui) (`WPF-UI` NuGet, v4.3.0) instead
of the earlier hand-rolled dark-theme XAML (custom `Button`/`ComboBox`/`CheckBox`
`ControlTemplate`s in `App.xaml`, which had already produced real bugs earlier in this
project — e.g. unreadable button text from an implicit `TextBlock.Foreground` style, and a
missing `SelectionBoxItemTemplate` on a `ComboBox`).

Why WPF-UI over the alternatives:
- It targets `net10.0-windows` directly (confirmed by building against it) and needs no
  extra native runtime.
- It gives a Windows 11 / Fluent look (Mica backdrop, rounded window corners, themed
  `Button`/`ComboBox`/`ToggleSwitch`/`DataGrid`/`NavigationView`) for free, which is exactly
  the "looks professional" ask, without hand-maintaining brushes and templates ourselves.
- MIT-licensed, actively maintained, no telemetry.
- It is a control *library*, not a framework that owns app startup or navigation — it plugs
  into the existing hand-rolled MVVM (`ObservableObject`, `RelayCommand`,
  `AsyncRelayCommand[<T>]`) without requiring a rewrite of that layer.

`App.xaml` merges `ui:ThemesDictionary Theme="Dark"` and `ui:ControlsDictionary`, then
**overrides both its own palette and WPF-UI's theme keys in the same dictionary** (later
definitions win), so the framework's controls - ComboBox, ToggleSwitch, NavigationView,
DataGrid, TitleBar - are retinted rather than retemplated. `App.xaml.cs` calls
`ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica)` on startup.

The palette is declared once, as `Color` resources with brushes derived from them, and
everything else references those keys - so a different accent or a future light theme is
one block to edit, not a search across pages:

| Role | Value |
|---|---|
| Window / nav | `#17181B` |
| Content area | `#1B1D21` |
| Card | `#212429`, 1px border `#2C3038`, radius 12, padding 22 |
| Accent (+ hover / pressed) | `#35C7E6` / `#54D3EE` / `#1FA7C4` |
| Text on accent | `#062024` (dark, not white - better contrast on a bright cyan) |
| Text primary / secondary / tertiary | `#F1F3F5` / `#9AA3AE` / `#6C7480` |
| Success / warning / danger | `#4CD787` / `#F2C94C` / `#F2555A` (unchanged) |

Dark is fixed for now to match the existing AORUS-panel aesthetic.

Shared styles (`Card`, `InnerPanel`, `CardTitle`, `Hint`, `SubLabel`, `StatusText`,
`PrimaryButton`, `SecondaryButton`, `DangerButton`, `Chip`, `IconTile`, `ValueSlider`,
`SliderReadout`) live in `App.xaml` and are used by all three windows, so the main window,
the profile editor and the colour picker cannot drift apart.

## Window shell

Both windows (`MainWindow`, `ProfileWindow`) are `ui:FluentWindow` with
`ExtendsContentIntoTitleBar="True"` and a `ui:TitleBar`, giving a native-feeling title bar
that still matches the Mica/dark theme instead of the default white Win32 chrome.

`MainWindow` uses `ui:NavigationView` for the left navigation rail (Dashboard, Kühlung,
Tastatur, Leistung & Akku, Info & Updates). Its built-in *page* navigation (Frame +
`TargetPageType`) was deliberately not used — that would mean one `Page` class per section
wired through a `INavigationViewPageProvider`/DI container, which is a lot of extra
machinery for five sections that all read from the same `MainWindowViewModel` and its live
device state. Instead, `NavigationView.ContentOverlay` (an overlay slot the control renders
regardless of Frame navigation state) hosts a single `Grid` containing all five sections,
each an `IValueConverter`-driven `Visibility` binding
(`StringEqualsVisibilityConverter`, keyed by `MainWindowViewModel.SelectedSection`) — so
`NavigationView` is used purely for its pane chrome (icons, selection highlight, collapse
button), while the actual section switching stays a plain property on the existing
ViewModel. `NavigationView.SelectionChanged` in `MainWindow.xaml.cs` just copies the
clicked item's `TargetPageTag` into `SelectedSection`.

## Per-feature control choices

The user asked explicitly to reason about which control fits which feature rather than
using one widget type everywhere. The choices made, and why:

| Feature | Control | Why |
|---|---|---|
| Fan profile (Quiet/Normal/Gaming/Maximum/Dynamic) | Chips - `RadioButton`s with an icon and a pill template | A small, fixed set of *named, mutually exclusive* states, so the control carries that meaning: a RadioButton group makes exclusivity structural instead of a highlight tracked by hand. `IsChecked` binds **one-way** to `ActiveFanProfile`, which comes from the EC readback - so the highlight shows what is really set, and a failed write snaps back rather than lying. (A held fixed value reports its own `"Fixed"` key, so no profile chip lights up then.) |
| Fixed fan speed (8 firmware-tested steps) | `Slider` with `Ticks` + `IsSnapToTickEnabled`, shown in percent | Originally a ComboBox, on the reasoning that a slider implies a continuous range. A *snapping* slider gets both: it feels direct, and the tick marks sit on the percentages the tested raw steps really occupy (25/30/40/50/60/70/85/100) - hence visibly uneven, which is the honest picture of non-linear firmware steps. `FixedFanPercent`'s setter additionally snaps to the nearest tested raw value, so an unverified duty is unreachable even if the slider's own snapping were bypassed. |
| Windows power mode (Efficiency/Balanced/Performance) | Chips, same as fan profiles | Three named, exclusive states, not a spectrum - and again highlighted from Windows' own readback (`ActivePowerMode`). |
| Keyboard on/off | A single `ui:Button` whose `Content` text flips between "Einschalten"/"Ausschalten", not a `ToggleSwitch` | A `ToggleSwitch` implies the visible state *is* the truth and flips synchronously on click. Here the actual state only changes after a device write that can fail (and on failure the ViewModel re-reads real device state and may leave the switch not matching what the user just set). A button avoids a toggle that silently un-flips itself out from under the user. |
| "Link all three RGB zones" | `ui:ToggleSwitch` | Purely a local UI preference (`LinkKeyboardZones`) with no device write of its own and no failure mode — it only affects how the *next* color write is fanned out. A real toggle is correct here because flipping it can never fail or need to be rolled back. |
| Keyboard brightness / effect speed | `Slider` over the ordinal steps (0-3 and 0-4), with a **named** readout ("Hell", "Normal") | Both are small ordered sets fixed by the firmware (`KeyboardBrightnessLevels.All`, `KeyboardEffectSpeeds.All`), so the slider addresses them by index and snaps per step - no invalid value exists to land on. The readout names the step rather than showing a number, because "Mittel" means something to a reader and "2" does not. A property setter cannot be awaited, so the write it launches is published as `PendingSliderWrite` instead of being fire-and-forget. |
| RGB effect selection | A grid of icon tiles (`RadioButton`s), applied on click | Nine named, equal-weight options belong on screen, not behind a dropdown - and "manual zone colours" is the tenth tile rather than a separate button, since choosing it is just choosing no effect. Highlighting follows `ActiveKeyboardEffect` (what is *running*), not the last pick, and the active tile carries a pulsing dot so the running effect is readable without looking at the keyboard. |
| Battery charge limit (60-100%) | `Slider`, self-applying via `Debouncer` (700 ms) | A genuinely continuous range, so a slider is the right shape. The apply button is gone: the write fires once the slider comes to rest, so a drag across the range is still one verified, rollback-checked EC transaction, not one per pixel. The slider deliberately stays enabled during that write (`CanAdjust`, not `CanApply`) so it cannot go dead under the user's own hand. |
| Update check | Single button ("Jetzt prüfen") + status text + conditional `ui:HyperlinkButton` | It's a one-shot, infrequent action with a clear success/failure outcome, not a setting; a hyperlink (not an auto-download) is used deliberately since the app never downloads or installs anything itself (see below). |
| Custom fan curve (15 points) | A draggable point-and-line chart (temperature °C × fan speed %), not a grid of numbers | This is fundamentally a *shape* the user is designing, not 15 independent settings - a chart lets them see and feel that shape directly, the way the hardware vendor's own tool does, instead of cross-referencing 15 rows of raw numbers. Dragging is live-clamped against every neighbour so the curve can never even be dragged into an invalid shape; the write itself waits for an explicit button, because writing a curve takes seconds and switches the fan mode, and shaping one is a dozen small edits. |

### Which colours an effect actually uses

Not every lighting mode reads the stored zone colours, so the colour controls must not
pretend otherwise - picking a colour for the rainbow marquee changes nothing on the
keyboard. `KeyboardEffectFrames.ColorUsage` declares this per mode, right next to the
frame function it describes:

| Mode | Reads |
|---|---|
| Manual (no effect) | all three stored zone colours |
| Atmen, Pulsieren | zone 1's colour only - modulated in brightness across all three zones |
| Farbwechsel, Regenbogen, Welle, Lauflicht, Pendel, Regentropfen, Ausblendende Welle | nothing; each carries its own palette |

The declaration is **verified, not trusted**: `KeyboardEffectFrameTests` renders every
effect twice with two very different base colours across a range of timestamps, and
asserts that it reacts exactly when `ColorUsage` says it does. A hardcoded palette added
to `Create` without updating the table fails the suite instead of quietly making the UI
lie.

In the Tastatur section this drives the zone card: a swatch the running mode does not read
is dimmed and labelled "wirkt bei diesem Effekt nicht", zone 1 is labelled
"Zone 1 · Basisfarbe" while an effect is built from it, and the "link all three zones"
toggle is disabled when only one zone is read. The swatches stay **clickable** throughout -
the colours are still stored and still worth setting up for manual mode, they simply have
no effect at this moment, and disabling them would block a legitimate action to make a
point the label already makes.

### The live keyboard preview

The Tastatur section shows this laptop's keyboard - full layout, numeric pad, real key
widths - with the keys lit per RGB zone. Three things about it are deliberate:

- **It is one keyboard, not three pads.** The zones are vertical bands across the same key
  field, and the boundary falls on a different key in each row, because that is where the
  hardware puts it (zone 1 reaches ~F6/T/G/V, zone 2 ~F7-F9/Y-O/H-L/B-M, zone 3 the rest
  including the numeric pad). `Controls/KeyboardLayout.cs` is the single transcription of
  that; `Controls/KeyboardPreview.cs` turns it into a 76-column grid, so a 1.25u or 2.25u
  key is still whole columns and every row's right edge stays flush.
- **The keycaps stay black.** Only the legend and a rim of spill light carry the colour,
  which is what the device actually looks like; flat-filled keys read as illuminated pads.
- **The animation is the real frame, not a lookalike.** `KeyboardEffectFrames` (extracted
  from `GigabyteHidKeyboardRgbController`, which now delegates to it) is the pure function
  `(effect, elapsed, base colour) -> three zone colours` whose output is written to the
  device. The preview calls that same function, with the same speed time scale, on a clock
  started when the effect started - so it shows the frame the keyboard is being sent rather
  than an imitation. Two honest limits: the preview samples at 20 Hz where the renderer
  writes at 30, and brightness is rendered as opacity (the frames carry no brightness - the
  device applies that separately), so *shape and colour* are exact while perceived intensity
  is an approximation. The timer runs only while the section is on screen, the window is
  visible and an effect is actually playing; a manual selection is painted once.

Every button above also carries a `SymbolIcon` (WPF-UI's Fluent icon set) matching its action - e.g. `LeafOne24` for Quiet, `Rocket24` for Gaming/best performance, `Save24` for anything that persists a value, `ArrowClockwise24` for anything that re-reads live state. This was chosen over decorative icons: each icon is a second, redundant cue for what the button *does*, useful for quickly scanning a WrapPanel of buttons, not just visual polish.

## Custom color picker

The keyboard zone color buttons used to open `System.Windows.Forms.ColorDialog` - functional,
but a jarring Win32-era dialog inside an otherwise Fluent/dark-themed app. It's replaced by
`ColorPickerWindow` (`src/AorusControl.App/ColorPickerWindow.xaml[.cs]`): a themed
`FluentWindow` with a classic layered-gradient saturation/value square (hue color, overlaid
with a white→transparent horizontal gradient, overlaid again with a transparent→black
vertical gradient - three `Rectangle`s, no third-party control), a vertical rainbow hue bar,
a live hex textbox, and a row of recently-used swatches.

- `AorusControl.Core.Models.HsvColor` holds the pure HSV↔RGB math (`FromRgb`/`ToRgb`),
  tested independently of any UI (`HsvColorTests`) - primaries, black/white edge cases,
  round-trip tolerance, and hue wraparound/clamping for out-of-range input.
- `AorusControl.Core.Features.Keyboard.RecentColorsStore` persists the last-used colors to
  `%LocalAppData%\AorusControl\recent-colors-v1.json`. Unlike every other store in this
  project, it is deliberately **fail-soft**: a missing or corrupt file just means an empty
  recent-colors list, never a thrown exception, because this is picker convenience, not a
  device setting that must be right or refused.
- `ColorPickerViewModel` keeps Hue/Saturation/Value, RGB, and hex text in sync from
  whichever the user just touched (drag the square, drag the hue bar, or type a hex code)
  without a feedback loop, since each setter recomputes the other two directly instead of
  reacting to its own property-changed notification.
- The picker is modal (`ShowDialog`) and only commits to the device (and to the recent-colors
  list) when "Übernehmen" is clicked - dragging around the square never writes anything.
  This one keeps its explicit apply where the fan curve and charge limit dropped theirs: a
  modal dialog already *is* the confirmation gesture, and a picker that wrote continuously
  would repaint the keyboard for every pixel crossed on the way to the colour wanted.

## The dashboard: what is in force, not just what is measured

The first version showed CPU and GPU temperature, fan RPM and the raw duty byte, in two
fixed columns capped at 880 px. It answered "how hot is it" and nothing else, and the layout
was squeezed on a narrow window and half empty on a wide one.

It is six tiles now, and the four new ones all answer the same question in different words -
*what is actually set right now*: the fan mode with a sentence naming what regulates the fans,
the Windows power mode with the power source next to it, the charge limit, and whether the
lighting is on. Duty leads with a percentage; the raw byte stays as the footnote, since
"Rohwert 66 / 229" is a fact about the firmware rather than an answer.

`TilePanel` lays them out in as many equal columns as fit at a minimum width, so they reflow
from four across to one without ragged gaps. A `WrapPanel` with a fixed `ItemWidth` wraps
correctly but leaves a gap on the right; computing that width from the panel's own
`ActualWidth` feeds the layout back into itself and settles on one column.

## One cooling card: the profile on top, what it does below

Fan profiles and the curve editor used to be two cards, which asked the reader to connect them
themselves - and the connection is the whole point, since the profile decides whether the
curve means anything at all. They are one card now: chips on top, and under them the chart of
what the *running* profile does.

The chart is editable exactly when the stored curve is the thing regulating the fans, which is
only under Dynamic. Under everything else it is disabled, along with the two buttons that
write to it. Points that can be dragged but change nothing are a lie told with a cursor.

What it draws depends on what is actually known, which is not the same for every profile:

| Profile | Shown | Why |
|---|---|---|
| Dynamic | the stored fifteen points, draggable | this is the curve in force |
| Maximum | a flat line at 100% | it pins the fans there, whatever the temperature |
| Fixed | a flat line at the chosen step | same, at the user's value |
| Leise / Normal / Gaming | nothing but the grid and a sentence | the firmware regulates internally and publishes no curve |

The last row is the uncomfortable one and the reason the others are drawn at all: an empty
chart with an explanation is worth more than a plausible line nobody can stand behind.
Gigabyte's own curve stays dashed underneath in every mode, as the one piece of vendor data
that does exist for this model.

## Showing what a vendor profile does, without inventing it

The obvious feature request is "show me the curve behind Leise / Normal / Gaming / Maximal".
It cannot be answered as asked. Those profiles write no curve: they set four status flags and
the firmware regulates internally, and the EC's fifteen curve points are provably untouched by
the switch - the write tests in `research/FAN-POWER-GPU-CONTROL.md` confirmed the curve came
back identical after every profile change.

The first attempt was to measure it: every telemetry tick contributed one temperature/duty
pair, accumulated per profile. It was honest and it did not work. Fan duty has hysteresis and
lags temperature, so at two samples a second the picture came out as a jagged scatter that
told the user less than the flat statement "these modes do not publish a curve" would have.
It is gone; the code it needed is gone with it.

What replaced it is the curve Gigabyte itself draws, taken from its own software rather than
guessed: `GigabyteReferenceCurve`, lifted from the decompiled notebook module
(`ucNotebook.Views/FanControlNb.cs`). GCC hardcodes one curve per model family - the 16-inch H
models and the Aorus 15/17 B/9/SF have one per fan mode, and everything else in the AORUS
family, this laptop included, gets a single default. So GCC never showed a per-mode curve on
this machine either, and that single curve is the whole of what it had.

It is drawn dashed and amber beside the edited curve, with a legend, and can be loaded into
the editor. Two of its values cannot be used as stated, and both are adjusted in the open
rather than quietly: GCC starts at 0 % below 55 °C where this firmware's lowest verified duty
is 25 %, and it ends at 99 % at 92 °C where the firmware requires full speed by 90 °C.

## Saying what a setting actually changes

Under the Windows power modes there is a panel naming what the running mode does - and, just
as deliberately, what it does not. Vendor tools routinely imply that a "performance mode"
also drives the fans. On this laptop it does not: the curve is ours, on the EC. So the panel
says that in words and shows the curve that IS in force right beside it, plus the cooling
state currently running.

Both texts follow the device readback, like every chip in the app, so a mode that failed to
apply cannot describe itself as if it had.

That is also why `FanCurveChart` became a control: the same implementation draws the editable
chart under Kühlung and the read-only one here, so the picture next to the explanation can
never drift from the editor. Read-only omits the point markers - fifteen dots on a small
chart read as a dotted line rather than as points.

## Sliders: WPF-UI's thumb, our track, one scale

Four settings are sliders (fan speed, charge limit, keyboard brightness, effect speed), and
all four were subtly wrong in the same ways.

The thumb is WPF-UI's now, referenced by key (`UiSliderThumbStyle`) rather than rebuilt. The
hand-written one had a focus halo drawn larger than the thumb itself, so it was clipped to the
thumb's bounds and appeared as a small square behind the round handle. A library that is
already a dependency had a correct one.

The track stays ours, because WPF-UI's is a single flat line: it never shows how far along the
value is. Ours fills the part the value has passed, which is the decrease button's own region -
no second value-to-width calculation to keep in sync.

`SliderScale` draws the ticks and their labels. Both of the obvious approaches are wrong:
WPF's `TickBar` spreads its marks across the full width unless told the thumb width, so they
drift away from the thumb towards the ends; and labels in equal grid columns sit at the centre
of each column (12.5%, 37.5%, …) rather than at the values they name (0%, 33%, …). One formula
for both, in `SliderGeometry`, means they cannot disagree - and it is a pure function, so the
smoke tests pin it down.

On a narrow window the scale drops the labels that would collide and keeps the ends; the ticks
stay, and the readout beside the slider names the current step in full anyway.

Two WPF traps cost an hour here and are worth naming, because both look like the style simply
having no effect:

- A style that is `BasedOn` WPF-UI's slider style cannot replace its template: that style sets
  `Template` from an `Orientation` trigger, and a trigger setter outranks a plain setter in the
  derived style.
- WPF-UI's implicit `RepeatButton` style overrode the templates set directly on the track's
  repeat buttons, which is why the filled part of the track never appeared. `Style="{x:Null}"`
  on those buttons is what makes them ours.

## Debounced apply, and the one place it was wrong

An "Einstellung übernehmen" button is honest but tiring: the app usually knows when a gesture
has finished, and making the user say so again is friction that RGB and fan tools are rightly
criticised for. `AorusControl.App.Infrastructure.Debouncer` replaces those buttons for the
charge limit and the Fixed value: every `Schedule()` restarts the wait, so a drag becomes one
device write at the value the user settled on.

**The fan curve is the exception, and it was a mistake to include it.** Writing a curve is a
fifteen-point EC transaction plus a mode switch - seconds, not milliseconds - and shaping a
curve is a dozen small edits. Debouncing turned an editing session into a queue of slow
writes, each one switching the fan mode underneath the person still drawing. It has an
explicit "Kurve übernehmen" button again, enabled only while there is something unapplied, and
the state says so in words.

The rule that separates the two: debounce a *value*, not a *composition*. One number the user
drags to a place is finished when the drag stops. A shape built from several points is not
finished until the person says it is.

What the mechanism still has to get right where it is used:

- **Nothing is silently lost.** `PrepareToCloseAsync` flushes the Fixed value and the charge
  limit before closing. The curve is deliberately *not* flushed: an unapplied curve was never
  confirmed, and closing a window is not a way of confirming it.
- **Nothing writes back what was just read.** Populating a control from the device is wrapped
  (`_applyingDeviceState`), and reloading the curve from the device clears the unsaved flag
  with the edits.
- **Entering a mode stays explicit.** The Fixed slider follows the drag only when Fixed is
  *already* running; brushing it never pins the fans on its own.
- **The wait is injected**, not a `DispatcherTimer`, so `DebouncerTests` and `AutoApplyTests`
  drive it directly and the behaviour above is checked without sleeping on a real clock.

## The curve editor: handles, not fifteen dots

The firmware stores exactly fifteen points, and for a long time the editor showed exactly
fifteen draggable dots. That is the wrong unit: a fan curve is three or four decisions - idle
here, ramp there, full speed by then - and thirteen of those dots existed only because the EC
table has that many rows.

The editor works on handles now, two to fifteen of them, and `FanCurveShape` translates:
expanding a drawn shape into the fifteen points the device demands, and collapsing a curve
read back from the device by dropping every point that sits on the straight line between its
neighbours. A curve drawn with four handles comes back as four handles. Both directions are
pure functions, which is what makes the round trip testable without hardware.

Editing has four ways in, because each answers something the others cannot:

| Gesture | What it does | Why it exists |
|---|---|---|
| Drag a handle | moves it | the obvious one, and the imprecise one |
| Click empty plot | adds a handle there | without it the editor could only ever lose handles, which makes removing one a decision nobody dares take |
| Right-click, or Delete | removes the selected handle | simplifying a curve is as much a part of shaping it as adding to it |
| Arrow keys | move the selected handle by one, or five with Ctrl | the only way to place a value exactly; no mouse reliably hits one degree |

The selected handle wears a ring rather than growing - a dot that changes size when you touch
it is harder to place, not easier to see. The first handle and the last are anchors and cannot
be removed, and the last cannot be moved at all: the firmware requires full speed by 90 °C, so
that point is not the user's to choose.

## Autostart: a Scheduled Task, not the registry Run key

A very common complaint about RGB/OC tools is that they nag with a UAC prompt every single
time you log in, because they autostart via the classic `HKCU\...\Run` registry key while
also requiring administrator rights - Windows has to ask again at every trigger, since the
Run key carries no elevation of its own. This app's `app.manifest` requires admin (needed
for the same WMI/HID writes as manual use), so the same trap applies here too unless
avoided deliberately.

`AorusControl.Core.Features.Startup.StartupManager` avoids it by using a **Scheduled Task**
(via `schtasks.exe`) with an "At log on" trigger and "Run with highest privileges" already
set on the task itself - Windows takes the elevation decision from the task's own
configuration at trigger time, not by asking again, so autostart is silent. Creating or
removing the task needs no extra prompt either, since the app process creating it is
already elevated. The "Info & Updates" section exposes this as a plain button (not a
`ToggleSwitch`) whose text flips between "Autostart aktivieren"/"deaktivieren", following
the same rule as the keyboard on/off button: the underlying action is a real Windows call
that can fail, so the control must be able to show a failure rather than silently
un-flipping itself.

## Reapplying RGB after sleep/resume

Another common weak spot in RGB software: the keyboard's USB HID lighting controller
often power-cycles when the laptop sleeps, silently reverting to its own firmware default
the moment it wakes - and most tools never notice, because they only ever write in
response to a user action, never on their own initiative. `MainWindowViewModel` now
subscribes to `Microsoft.Win32.SystemEvents.PowerModeChanged` and, on `PowerModes.Resume`,
waits briefly (the device needs a moment to re-enumerate on USB before it will accept a
write) and then reapplies the last known lighting state via the same `ReapplyAsync` path
the "Auswahl erneut senden" button already uses - so a real quirk is fixed with existing,
already-tested machinery rather than new bespoke logic. The subscription is added in the
constructor and removed in `Dispose()`, since `SystemEvents` is a process-wide static event
that would otherwise leak the ViewModel for the app's entire lifetime.

## The fan curve chart

`MainWindow.FanCurveChart.cs` is a partial-class file kept separate from the window's
general lifecycle code, since it is a self-contained visual component: a `Canvas` drawn
and driven entirely in code (dashed gridlines, an accent-colored area fill under the curve,
a softly blurred duplicate line underneath the crisp one for a subtle glow, and draggable
point markers with a drop-shadow), matching the reference screenshot's look while fitting
the app's own dark/accent palette rather than copying its exact colors.

Two things are handled deliberately, not left as loose ends:

- **Percent, not raw duty bytes.** The Dashboard already reports fan duty as "Rohwert X /
  229", so `AorusControl.Core.Features.Cooling.FanSpeedPercent` treats that same 229 as
  100% for every percent shown anywhere in the app (`ToPercent`/`ToRaw`, tested for
  clamping and round-trip tolerance) - including the Fixed-fan slider, which now shows
  "Aus"/"25%"/"30%"/.../"100%" for its tested raw steps instead of the raw byte value.
  Raw 0 really is fans off and is offered as the left end of that slider: it was measured
  on the device (both fans at 0 RPM, research/runs/fan-floor-rpm-test-20260905-135015.md)
  and the vendor's Quiet profile does the same. What makes it safe is not a floor on the
  value but the hardware worker's lease, which refuses to hold any fixed value at 65 °C and
  restores Normal by itself.
- **Every drag is clamped live**, not just validated at Apply time: dragging point *i*
  clamps its temperature and percent between its immediate left and right neighbors and
  against the floor for the temperature it lands on - 0% below `PassiveBelowCelsius`
  (60 °C), where the fans were measured to genuinely stop, and 25% from there upwards, so a
  curve can never be silent into the temperatures where silence stops being harmless. The
  chart therefore can never even display an invalid shape while the user is still dragging -
  `FanCurveValidation`'s own rules are mirrored directly into the drag math. The
  last point is excluded from hit-testing entirely (rendered smaller, in a muted color,
  with a "fest" tooltip) rather than merely being difficult to drag, since the firmware
  requires it fixed and a curve editor that lets you drag a point it's going to reject
  anyway is worse than one that never offers to.

## Update checking

`AorusControl.Core.Features.Updates` (`UpdateManifest`, `UpdateCheckResult`,
`UpdateChecker`) adds a minimal, explicitly scoped update check:

- Fetches a small static JSON manifest over **HTTPS only** (rejects `http://`), with a 16 KB
  size cap and strict JSON parsing (`UnmappedMemberHandling.Disallow`), matching this
  project's general "fail loud, never silently half-apply" convention.
- Compares `System.Version` against the running assembly's version
  (`AorusControl.App.csproj` now carries an explicit `<Version>`, bumped on every release).
- **Never downloads or installs anything.** It reports "up to date" / "update available"
  (with a `DownloadUrl` the user can open themselves via the `ui:HyperlinkButton` on the
  Info & Updates page) / "check failed", full stop.

This is deliberately narrow: an auto-installer needs a signed release pipeline (code
signing, a real hosting endpoint, a rollback story) that doesn't exist for this project yet.
`UpdateViewModel` currently points at a placeholder URL
(`https://example.invalid/aorus-control/update-manifest.json`) that will always fail
cleanly with a real error message — this is intentional (see the doc comment at the top of
`UpdateViewModel.cs`) rather than a bug; point it at a real manifest once one exists, using
the shape documented in `UpdateManifest.cs`'s own doc comment.

## Known environment limitation for this change

`AorusControl.App`'s `app.manifest` requests admin (`requireAdministrator`), so it always
shows a UAC prompt on launch — including from this automated environment, where no human is
present to approve it. That means this redesign was verified by:
- A clean build of `AorusControl.App`, `AorusControl.Core`, and the smoke test project.
- The full 29-test smoke suite (`tests/AorusControl.App.SmokeTests`), unaffected by this
  change since it exercises ViewModels/Core directly, not XAML.
- Manual review of every renamed/removed WPF-UI type and property against the installed
  package (via `third-party/ilspy/ilspycmd.exe` decompilation, since the NuGet package ships no
  separate XAML/doc reference for enum members like `SymbolRegular` icon names or
  `NavigationView`'s template-part-only content model).

It was **not** verified by actually running and looking at the app — that still needs a
human to click through the UAC prompt once, same limitation already documented in
`WORKER-ARCHITECTURE.md` for the crash-safety acceptance test. Please launch
`AorusControl.exe` yourself and sanity-check the five sections before considering this
done.
