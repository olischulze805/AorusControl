# AORUS 5 SE4 keyboard capability map

Date: 2026-09-01  
Target: GIGABYTE AORUS 5 SE / SE4, BIOS FB0F, keyboard USB `VID 1044 / PID 7A41`

## Evidence levels

- **Live verified**: read successfully from this laptop.
- **Exact official path**: Gigabyte's signed `GBT_Keyboard 25.07.25.01` explicitly selects `1044:7A41` and contains the operation in the same ITE implementation.
- **Shared-module only**: code exists for other keyboards but must not be assumed to work on this three-zone model.

The map began read-only. Later, explicitly requested guarded RGB/effect tests were added; every temporary test captured and restored the prior visible zone state. Each section labels whether it is read-only, live-write verified, or static evidence.

## Device classification

The official `GenericKeyBoard` implementation recognizes `1044:7A41` when the HID collection exposes a 9-byte feature report. It then sets all of these flags:

- ITE keyboard present
- zone RGB present
- `3a4041` keyboard-family layout

This selects `FusionController.YEKeyboard.ZoneRgb`. The locally observed matching endpoint is `MI_03`, vendor usage page `0xFF01`.

## Lighting controls

### Three independent zones — read and write live verified

Each of the three zones has:

- red, green, and blue channels, each stored as one byte (`0–255`);
- a byte labeled brightness by Gigabyte; it stores values `0–50`, but live tests on firmware `19.0.4` show visible output only at `50`;
- its own packet selector (`1`, `2`, or `3`).

The packet can represent a separate RGB color and nominal brightness byte for every zone. Despite the field name and stored range, it behaves as an enable gate on this firmware, not as visible PWM brightness.

Current live state:

| Zone | RGB | Hex | Raw brightness | UI estimate |
|---|---:|---:|---:|---:|
| 1 | 0, 255, 0 | `#00FF00` | 50 | 100% |
| 2 | 0, 255, 0 | `#00FF00` | 50 | 100% |
| 3 | 0, 255, 0 | `#00FF00` | 50 | 100% |

A guarded live write test changed only zone 1 temporarily from `#3E0066` to `#66003E` at the same brightness. Readback confirmed the temporary value. The `finally` path then restored `#3E0066`, and a second readback confirmed the restoration. This verifies direct RGB setting and readback on the actual `7A41` firmware. Report: `research/runs/keyboard-zone-write-test-20260901-192237.md`.

At the user's request, all three zones were subsequently set persistently to green `#00FF00` without restoration. Independent readback verified every zone and preserved raw brightness `50`. Report: `research/runs/keyboard-set-green-20260901-192437.md`.

### Fn+Space hardware brightness observation

After the user stepped the hardware keyboard brightness down with `Fn+Space`, the zone query still returned RGB-zone brightness raw `50` for all three zones. The passive feature report contained no second brightness field. The separate DSDT getter `GetKeyBoardBackLight` maps to EC field `KBLL@0xD7` but had previously returned `0`. This favored an internal keyboard-controller brightness level with four steps (including off), rather than a value exposed through the RGB-zone protocol.

That correlation was completed with a 25-second elevated monitor. While the user cycled the four levels, `KBLL` remained `0` and neither vendor-defined input interface (`MI_01`, `MI_03`) emitted a report. The hardware brightness level is therefore not observable through any currently identified host-facing channel. It should be modeled as **unknown/external state**, not inferred from the stored RGB brightness byte.

The nominal software brightness byte was live-tested afterward. All three zones accepted and returned raw values `0`, `17`, `33`, and `50`, but the user visibly observed only off and full brightness. Colors remained `#00FF00`; the final state was left at raw `50` / on.

During the initial two-second cycle, the user visually distinguished only off and full brightness. The zones also changed slightly from left to right because the protocol has no discovered all-zones transaction: it sends one packet per zone, and Gigabyte's reference code waits 65 ms after each. A five-second-per-step repeat produced the same result despite reading back every requested value.

A final boundary test used raw `0`, `1`, `25`, `49`, and `50`, each for five seconds. The keyboard remained off through `49` and became fully bright only at `50`.

**Extended on 2026-09-03 above Gigabyte's range.** The UI never sends more than `50` because it halves a 0-100 percentage, so `51`-`255` had never been tried. A sweep over `0`, `25`, `50`, `51`, `60`, `75`, `100`, `150`, `200`, and `255` stored every value exactly and showed `0` and `25` off, `50` on, and every value from `51` to `255` indistinguishable from `50`. This was read as a `>= 50` threshold, meaning a switch rather than a control. **That conclusion is withdrawn:** the sweep list contains none of the accepted intermediate values, so it could not see them. The field is a full four-level brightness control; see the resolution below. Report: `research/runs/keyboard-zone-brightness-sweep-20260903-115057.md`.

**The EC path is also closed.** `GB_WMIACPI_Set.SetKeyBoardBackLight`, WMI method ID `0xF6`, was invoked for the first time under the same gates used for battery writes. Values `0` through `4` were each accepted and read back exactly, which proves that `WMBD` implements case `0xF6` and that `EC.KBLL@0xD7` is a real writable storage byte rather than a MOF-only entry. None of the values changed the lighting in any way, so nothing on this model consumes the field. This also explains the earlier constant `0`: that was simply the stored value. `KBLL` is an orphaned register. Rollback to `0` verified. Report: `research/runs/keyboard-backlight-level-20260903-115048.md`.

All conceivable host paths to *setting* the four physical steps are exhaustively excluded: the zone brightness byte, `EC.KBLL`, and a dedicated command in the keyboard protocol, which the fully decompiled modules do not contain.

**Reading the step, however, works, and the earlier "not host-readable" conclusion is withdrawn.** It rested on a monitor that listened only to `MI_01` and `MI_03`. The collection `MI_02/COL_04`, which declares no usages at all and had never been listened to, emits a 4-byte input report on every `Fn+Space`: byte 0 is report ID `0x04`, byte 1 is a constant `0x01`, and **byte 2 carries the step**. Observed values are `0` for off, `24`, and `32`. The brightest step was not captured in that run, and the spacing suggests `40` without proving it. `MI_02/COL_04` exposes only an input report, no output or feature report, so this is a notification channel and not a control. Report: `research/runs/keyboard-brightness-signal-hunt-20260903-123301.md`; full status in `research/KEYBOARD-BRIGHTNESS.md`.

Also newly established: the official getter `0x88` was queried for selectors `4` through `15` for the first time. Each response echoes the command and selector correctly but carries only zeros, so beyond the three zones and the global slot there is no further readable state.

### Global effects — exact official path, first setter live-tested

Gigabyte's ITE effect packet contains effect, speed, brightness, palette color, and direction in one operation. The service exposes this complete firmware enum:

| ID | Official enum | Friendly name |
|---:|---|---|
| 1 | `Static` | Static |
| 2 | `Breathing` | Pulse/breathing |
| 3 | `Wave` | Wave |
| 4 | `Fadeonkeypress` | Reactive fade |
| 5 | `Marquee` | Marquee |
| 6 | `Ripple` | Ripple |
| 7 | `Flashonkeypress` | Reactive flash |
| 8 | `Neon` | Cycle/neon |
| 9 | `Rainbowmarquee` | Rainbow marquee |
| 10 | `Raindrop` | Droplet/raindrop |
| 11 | `Circlemarquee` | Circle marquee |
| 12 | `Hedge` | Hedge |
| 13 | `Rotate` | Spiral/rotate |
| 51–55 | `Custom1` … `Custom5` | Five custom slots |

A compact Gigabyte view model presents ten choices and maps them to IDs `1, 2, 3, 4, 5, 6, 8, 10, 12, 13`. The wider service can encode all IDs above. This proves what the official software can request, but not that every effect is implemented by the `7A41` firmware. A controlled test with readback and immediate restore is still required.

The global getter currently returns zero for every field. Effect `0` is not a defined effect, so this response must be treated as “global effect state unavailable in the current zone-RGB mode,” not as Static.

### First guarded live effect test

- A temporary Breathing request was sent with effect `2`, raw speed `5` (official UI 50/100), raw brightness `50`, palette Green `2`, and direction `1`.
- Request packet: `00 08 00 02 05 32 02 01 BB`.
- Before the test, all three zones were captured as `#00FF00`, raw brightness `50`.
- The global getter still returned all zeros after the write, so this firmware does not provide usable global-effect readback even after an effect request.
- The effect was held for ten seconds for direct user observation.
- All three captured green zones were restored afterward and independently verified as `#00FF00`, raw brightness `50`.
- Diagnostic exit code: `0`; report: `research/runs/keyboard-effect-breathing-test-20260901-200013.md`.
- Whether the animation was visibly implemented must be recorded from user observation; HID readback alone cannot establish visible motion.

### Visible effect batch 1

- Three official effects were requested for eight seconds each: Breathing `2`, Wave `3`, and Fade-on-keypress `4`.
- Shared parameters were raw speed `5`, brightness `50`, direction `1`; Breathing/Fade used Green `2`, Wave used Random `8`.
- The global getter returned zeros after every request, confirming again that visible support cannot be classified electronically on this firmware.
- All three original `#00FF00` zones were restored and verified after the batch; exit code `0`.
- User observation is pending for each effect. Report: `research/runs/keyboard-effect-batch1-20260901-201007.md`.

### Interactive visible-effect tester

- `tools/Start-KeyboardEffectTest.cmd` starts a guided test of Gigabyte's ten compact ITE effects.
- Every effect remains active until the user presses Enter or types an observation; typed text is stored beside the exact effect request in a timestamped Markdown report.
- `/stop` ends the sequence early.
- The program captures all three zone states before the first effect and restores plus verifies them in `finally` after normal completion, `/stop`, or a handled error.
- A `/stop` smoke test completed with exit code `0` and verified all three restored green zones: `research/runs/keyboard-effect-interactive-20260901-201354.md`.

### Final live effect classification for `7A41` firmware `19.0.4`

The user completed the full interactive ten-entry test. Results:

| Requested mode | Visible result |
|---|---|
| Static | Existing green remained; direct three-zone static RGB is independently verified |
| Breathing / Pulse | No visible change |
| Wave | No visible change |
| Fade-on-keypress / Reactive | No visible change while testing keys |
| Marquee | No visible change |
| Ripple | No visible change while testing keys |
| Neon / Cycle | No visible change |
| Raindrop / Droplet | No visible change |
| Hedge | No visible change |
| Rotate / Spiral | No visible change |

Every effect command was transmitted with valid checksum and exact official fields; the global getter returned zeros after every request. Gigabyte's ZoneRgb profile loader parses effect, brightness, color, speed, and direction but discards effect/speed/direction and applies only three `SetZoneColors` calls. Later recovery of the exact old `GBT_Keyboard 23.03.10.01` module changes the interpretation: its live RGB page explicitly recognizes `1044:7A41`, displays Pulse/Cycle and other effects, and directly invokes the same global setter. The profile loader is static-only, but the old live page was not. The global packet format is therefore confirmed for this exact device family even though firmware `19.0.4` currently ignores it. Full comparison: `research/OLD-KEYBOARD-MODULE-COMPARISON.md`.

Current safe application model for this exact keyboard, pending reconstruction of the previously working effect path:

- expose three independent static 24-bit RGB colors;
- expose lighting off/on, while treating Fn+Space's four physical levels as external/unreadable;
- keep animation, speed, palette, and direction controls experimental until the old working path is identified;
- investigate both an older GCC/service protocol and host-rendered three-zone animation. The owner directly confirms that Breathing, Flash/Pulse, and slow full-color transitions previously worked on this same laptop. The known direct-zone path has a ~5.1 full-frame/s ceiling and visible left-to-right stagger, but another historical command or service may have avoided this limitation.

Full user observations and requests: `research/runs/keyboard-effect-interactive-20260901-201738.md`.

The web cross-check independently supports the static-only result. Gigabyte advertises this generation as a three-zone RGB keyboard without animation claims, contemporary SE4 reviews explicitly describe constant/static lighting, and reports of effect menus changing with GCC versions or appearing after a Gigabyte mouse is connected point to shared RGB Fusion UI/profile detection rather than controller support. Detailed sources, conflicting reports, and confidence grading: `research/RGB-WEB-FINDINGS.md`.

### Effect engine re-tested with a sound design on 2026-09-03

The classification above rests on a confounded setup and is superseded. Those runs used palette `2` (Green) on a keyboard that was already displaying static green, so a correctly rendered green effect would have been visually identical to the previous state. They also treated the global getter's zero response as evidence, which is now disproven.

Three controlled runs replaced it. Every step writes all three zones to `#FFFFFF` at brightness `50` and verifies the readback, so the starting point is unambiguous, then sends exactly one global effect packet and nothing else.

| Run | Effect IDs | Palette | Visible result |
|---|---|---|---|
| `--isolate-effect-selection` | `51`, `1`, `52`, `2`, `8` | `0` Black | keyboard goes dark every time |
| `--test-effect-palette` | `1`, `1`, `2`, `3`, `8` | `1` Red, `4` Blue, `1` Red, `8` Random, `8` Random | stays white, no change at all |

Effect `1` appears in both runs with only the palette byte differing, and the outcomes differ. That isolates the mechanism:

- **Palette `0` (Black) is honoured and blanks the lighting.** The packet is parsed, so the firmware does receive and evaluate it.
- **Every other palette value is a no-op.** No colour is rendered and no animation starts, for static and animated effect IDs alike.
- During the blackout the zone registers keep their values; zone 1 still read `#FFFFFF` at brightness `50`. In the blanked state the LEDs no longer follow the zone registers, and any subsequent zone write restores lighting.
- The global getter returned `008800000000000000` in all ten steps, including the steps where the lighting visibly changed. The getter is therefore worthless as evidence of effect state and must not be cited as such.

Conclusion for `7A41` firmware `19.0.4`: the global effect command `0x08` selector 0 is reduced to a single working function, blanking via palette `0`. The effect engine renders neither colours nor animation. Because Gigabyte's host code is proven bit-identical to the 2023 release that the owner remembers working, a firmware behaviour change remains the leading explanation. Reports: `research/runs/keyboard-effect-isolation-20260902-193655.md` and `research/runs/keyboard-effect-palette-20260903-112527.md`.

The application does not depend on this. Ten host-rendered effects are confirmed working; see the frame-rate correction below.

### Effect parameters

- **Nominal brightness field:** Gigabyte encodes UI `0–100%` by integer division by two (`0–50`). On this `7A41` firmware, tested raw values `0`, `1`, `25`, and `49` were visibly off and raw `50` was fully on. Expose this as off/on, not as a continuous slider.
- **Speed:** normal UI scale appears to be `10–100` in steps of ten. It maps inversely to protocol values `9–1`; UI 100 maps to raw 1.
- **Palette color:** Black `0`, Red `1`, Green `2`, Yellow `3`, Blue `4`, Orange `5`, Purple `6`, White `7`, Random `8`. This is used by effects; zone colors use direct RGB bytes instead.
- **Direction:** the ITE family defines four linear directions, `Left→Right=1`, `Right→Left=2`, `Down→Up=3`, and `Up→Down=4`. Other shared-module enums also contain clockwise/anticlockwise values, but their applicability to `7A41` is not yet proven.

The UI mapping produces only nine distinct firmware speed bytes: UI 10–80 map to raw 9–2, while UI 90 and 100 both map to raw 1. Built-in effects animate inside the keyboard controller after one packet, so their frame rate is not constrained by USB update traffic; only these discrete speed settings are host-selectable.

Static zone colors are direct 8-bit red, green, and blue values, nominally 24-bit RGB or 16,777,216 combinations per zone. The laptop has three zones, not per-key RGB. Actual LED gamut, low-level quantization, and color accuracy remain hardware-dependent.

Gigabyte's implementation waits 65 ms after every zone write, which would cap host-driven movement at roughly 5.1 complete three-zone frames per second. **Corrected by direct measurement on 2026-09-02:** that delay is not a firmware requirement. Intervals of 65, 40, 25, 15, 10, and 5 ms were each written six times to zone 1 and verified by readback, and all six succeeded at every interval including 5 ms. A full interactive run of ten host-rendered effects then held a steady 21.2-21.4 three-zone frames per second, limited by Windows timer granularity rather than the device, and the owner confirmed every one of the ten as visibly working. Host-rendered animation is therefore the preferred path on this firmware, not a fallback.

## Keyboard assignment and macro controls

These functions are on the same exact ITE/`3a4041` code path, but have not yet been invoked on the laptop.

### Live 512-byte matrix read

- Official getter command `0x8D` was executed on exact interface `MI_03`; it returned all eight expected 64-byte payload blocks, totaling 512 bytes.
- This confirms 128 firmware-side four-byte assignment slots on the physical `7A41` controller.
- Live matrix SHA-256: `F3AC2DB4BD9F98A0851ADF11B05CAACB1962A5C34D6CE85DF5FF281513251ADD`.
- Gigabyte signed `newKeyMatrix3a4041` default SHA-256: `92431FE3FAE62A5777FC124D73F090F00877BA7DAFA3080F496CB313F72EC78A`.
- Only five bytes/records differ. The live additions are special secondary codes in otherwise empty default slots: 79=`00008800`, 80=`00008700`, 88=`00008900`, 93=`00008900`, 117=`00008B00`.
- No record begins with Gigabyte's user-assignment markers `3`, `4`, or `10`; therefore no saved user macro or basic-hotkey reassignment is indicated.
- Exact physical labels for the five special codes are not returned by firmware and are not guessed.
- No macro record was requested and nothing was written. Full raw report: `research/runs/keyboard-matrix-read-20260901-200413.md`.

### Per-key assignment

- A dedicated 512-byte default key matrix named `newKeyMatrix3a4041` is selected for this family.
- The official layout contains 105 logical key entries and a model-specific matrix-index map.
- A key can be restored to its factory function.
- A key can point to a recorded macro with an execution mode.
- The UI code contains predefined hotkeys/actions, including close window, show desktop, next/previous tab, reset zoom, copy, paste, cut, undo, and redo.
- The matrix can be read from the keyboard, edited locally, written back, saved in profiles, imported, and exported.

### Macros

- Each firmware macro record is 192 bytes in the ITE implementation.
- The module can record keyboard and mouse actions plus delays.
- Macro data can be read from firmware, compared with the local XML representation, and written back when different.
- Import/export and multiple local profiles are implemented.

### Gaming mode

- Protocol command `0x09` accepts a mode byte.
- Gigabyte calls it with value `1` when entering its gaming/assignment page.
- The meaning and restore value should be confirmed before our software exposes this control.

## Protocol command map

The following map comes from the official ITE class. Query commands still use a HID `SET_FEATURE` request to select data before reading it back.

| Command | Meaning | Current status |
|---:|---|---|
| `0x80` | Query keyboard firmware version | live verified: `19.0.4` |
| `0x88` | Query lighting state or zone | live verified |
| `0x08` | Set global effect or zone RGB | zone-1 write/readback/restore live verified in isolated diagnostic; not exposed by core/app |
| `0x8D` | Query 512-byte key matrix | recovered, not tested |
| `0x0D` | Write 512-byte key matrix | recovered, not implemented |
| `0x91` | Query 192-byte macro record | recovered, not tested |
| corresponding macro writer | Write macro record | recovered, not implemented |
| `0x92` | Query custom 512-byte picture matrix | shared-module only for per-key lighting |
| `0x12` | Write custom picture matrix | shared-module only; not appropriate for three-zone RGB without further proof |
| `0x09` | Set standard/gaming keyboard mode | recovered, not implemented |

All 9-byte feature commands use byte 8 as `255 - sum(bytes 1..7)` modulo 256. Bulk matrices and macros additionally use 65-byte HID transfers.

## Explicit exclusions

- **Per-key RGB is not claimed for this laptop.** The shared ITE class contains a per-key picture matrix for other products, while the exact `7A41` classification is three-zone RGB.
- No BIOS or EC firmware modification is involved.
- No arbitrary HID packet interface should be exposed by the replacement application.
- Key-matrix and macro writes require backup, verification, and a tested factory-restore path before use.

## Recommended implementation order

1. Keep the existing exact-device read-only RGB and firmware queries.
2. Add a typed three-zone lighting model to the shared core.
3. Completed: verify one guarded zone-color write with mandatory before-state capture, readback, automatic restore, and final readback.
4. Validate the ten compact UI effects individually, restoring the original three-zone state after every test.
5. Read and archive the key matrix before implementing any assignment or macro write.
