# AORUS 5 SE4 control research

This file is the durable research log for a replacement control application for the Gigabyte AORUS 5 SE4. Generated read-only probe reports are stored under `research/runs/`.

## Scope and safety boundary

- Target device: Gigabyte AORUS 5 SE / SE4, Windows 11.
- Observed BIOS: FB0F.
- BIOS package name indicates bundled EC firmware F00B.
- The project will not modify or flash BIOS or EC firmware.
- Initial probes are metadata-only or read-only.
- No firmware setter is called until the exact interface is identified, readback works, and a tested automatic-mode rollback exists.
- Device serial numbers, UUIDs, user names, and network identifiers are not recorded.

## Local files supplied

- `GIGABYTE Control Center_2026_Jul_release_All_Setup_26.08.03.01.exe`
- `GCC_26.08.03.01.zip`
- `X5MVE_BIOS_FB0F_EC_F00B_WEB_26042801.exe`
- `nb-bios-aorus5-ve-win11-64bit-fb0f-ec-f00b.zip`

The BIOS updater contains an AMI image named `RX5ME4FB0F.rom`. It is retained only as a research reference and will not be flashed or modified.

## Local observations

### Development environment

- .NET SDK 10.0.400 and .NET runtime 10.0.11 are installed.
- Visual Studio Code, Microsoft's C# Dev Kit, and Git are installed.

### Windows and ACPI

- Windows exposes an ACPI embedded controller (`PNP0C09`).
- Windows exposes multiple standard ACPI fan devices (`PNP0C0B`). They do not currently expose useful telemetry through an obvious public WMI class.
- Two vendor ACPI WMI devices are present: `ACPI\\PNP0C14\\DSARDEV` and `ACPI\\PNP0C14\\TESTDEV`.
- The Microsoft `WmiAcpi` driver is running.
- `MofImagePath` is not configured under the `WmiAcpi` service.
- `GB_WMIACPI_Get`, `GB_WMIACPI_Set`, and `CLEVO_GET` are not currently registered in `root\\WMI`.
- Windows reports processed binary MOF resources for the `DSarDev` and `TestDev` devices. Both reference `{05901221-D566-11D1-B2F0-00A0C9062910}`.

### Gigabyte package inspection

- The current notebook package contains a signed `acpimof.dll`.
- Inspected `acpimof.dll` properties:
  - Size: 33,328 bytes.
  - Architecture: x64 resource-only PE DLL.
  - File/product version: 10.0.10011.16384.
  - SHA-256: `27DC01AEF90D9AC7FBD460E292ED9DC85575B77D8225E14569FC8500A34E5AA2`.
  - Authenticode status: valid; signer is GIGA-BYTE TECHNOLOGY CO., LTD.
  - The signing certificate is valid from 2025-06-02 through 2028-06-02 and the file has a GlobalSign timestamp signature.
  - Embedded `MOFDATA/MOFRESOURCE` is compiled binary MOF (`FOMB` header), not plain-text MOF.
- It also contains `ucNotebook.dll`; strings include `SetFanNormal`, `SetFanGaming`, `SetFanMax`, `SetFanFixedSpeed`, and `SetFanDeep`.
- This supports the hypothesis that Gigabyte registers method metadata through `acpimof.dll` and calls firmware through the Windows ACPI WMI mapper.

### First local C# diagnostic run

- Project: `src/AorusControl.Diagnostics` targeting `net10.0-windows`.
- Build result: successful with zero warnings and zero errors.
- The program performs metadata and operating-system reads only and automatically writes a privacy-safe Markdown report.
- First report: `research/runs/diagnostic-20260901-182740.md`.
- Confirmed ACPI WMI devices: `DSARDEV`, `TESTDEV`, and `DCK`.
- Confirmed that `GB_WMIACPI_Get`, `GB_WMIACPI_Set`, and `CLEVO_GET` are not registered before provider setup.
- No readable standard `MSAcpi_ThermalZoneTemperature` instance was returned.

### FB0F ACPI/DSDT inspection

- Tooling: official ACPICA 20260408 release from <https://github.com/acpica/acpica/releases/tag/20260408>.
- Verified official tool hashes before execution:
  - `acpidump.exe`: `A0095A57521378C290D030DB7AD196A27DE2FCC770DD0347104FF49D19D792E0`.
  - `iasl.exe`: `739C597BCEE4563F18D13E73B2051BB661707713E6154CF5712A1B7EEF4A5DF9`.
- DSDT table: 485,893 bytes, OEM ID `GBT`, table ID `GBTUACPI`, OEM revision `01072009`, compiler ID `INTL 20200717`.
- DSDT SHA-256: `97B3C92A1FB5F9DF15342E510E5CB2111864814D5F9BEAD4B83CA65627236346`.
- The DSDT was dumped and decompiled read-only. Tables containing Windows activation/licensing data were not inspected or retained with the research material.

#### Confirmed Gigabyte WMI path

- ACPI device: `\\_SB.PC00.AMW0`.
- Hardware ID: `PNP0C14`.
- Unique ID: `DCK`.
- Its `_WDG` descriptors expose the classic Gigabyte/Microsoft WMI GUID family:
  - `ABBC0F6C-8EA1-11D1-00A0-C90629100000`, object ID `AC`.
  - `ABBC0F6F-8EA1-11D1-00A0-C90629100000`, object ID `BC`.
  - `ABBC0F75-8EA1-11D1-00A0-C90629100000`, object ID `BD`.
  - `ABBC0F72-8EA1-11D1-00A0-C90629100000`, event ID `D2`.
- ACPI method `WMBC` is the read dispatcher used by `GB_WMIACPI_Get`.
- ACPI method `WMBD` is the write dispatcher used by `GB_WMIACPI_Set`.
- This is direct machine-specific confirmation that the SE4-compatible ALFC interface is correct. `CLEVO_GET` is not the primary interface for this FB0F DSDT.

#### Confirmed read mappings

The numeric values below are WMI method IDs and match the IDs in ALFC's reconstructed MOF:

| ID | Public method | FB0F DSDT result |
| --- | --- | --- |
| `0x46` / 70 | `GetCPUFanDuty` | EC field `FDTY` |
| `0x47` / 71 | `GetGPUFanDuty` | EC field `GDTY` |
| `0x49` / 73 | `GetMaxCharge` | EC field `MAXC` |
| `0x64` / 100 | `GetChargePolicy` | EC field `BCPS` |
| `0x65` / 101 | `GetChargeStop` | EC field `BCPC` |
| `0x6A` / 106 | `GetFixedFanStatus` | EC field `ADJF` |
| `0x6B` / 107 | `GetFixedFanSpeed` | EC field `FAN1` |
| `0x70` / 112 | `GetFanAdjustStatus` | EC field `FAN1` |
| `0x71` / 113 | `GetAutoFanStatus` | EC field `FANB` |
| `0x7D` / 125 | `GetFanSpeed` | EC field `TFAN` |
| `0xE1` / 225 | `getCpuTemp` | EC field `TCPU` |
| `0xE2` / 226 | `getGpuTemp1` | EC field `TGP1` |
| `0xE3` / 227 | `getGpuTemp2` | EC field `TGP2` |
| `0xE4` / 228 | `getRpm1` | EC field `RPM1` |
| `0xE5` / 229 | `getRpm2` | EC field `RPM2` |

#### Embedded-controller layout relevant to monitoring

- `TCPU`: offset `0x60`, 8-bit.
- `TGP1`: offset `0x61`, 8-bit.
- `TGP2`: offset `0x64`, 8-bit.
- `FAN1`: offset `0xB0`, 8-bit.
- `FAN2`: offset `0xB1`, 8-bit.
- `FDTY`: offset `0xB3`, 8-bit.
- `GDTY`: offset `0xB4`, 8-bit.
- `RPM1`: offset `0xFC`, 16-bit.
- `RPM2`: offset `0xFE`, 16-bit.

The DSDT also confirms that the corresponding write dispatcher directly changes these EC fields. This validates the protocol but also reinforces the rule that no setter should be tested before automatic-mode rollback and readback are implemented.

## Open-source findings

All repositories below used the MIT license when inspected. They are source references, not automatically trusted binaries.

### Aorus Laptop Fan Control (`s-h-a-d-o-w/alfc`)

Source: <https://github.com/s-h-a-d-o-w/alfc>

- Explicitly lists AORUS 5 SE4 fan control on Windows 11 as compatible.
- Uses `root\\WMI` classes `GB_WMIACPI_Get` and `GB_WMIACPI_Set`, not `CLEVO_GET`.
- Its C# bridge uses `System.Management`, enumerates the first instance, builds named input parameters, and invokes a named method.
- Relevant read methods include `getCpuTemp`, `getGpuTemp1`, `getGpuTemp2`, `getRpm1`, `getRpm2`, `GetCPUFanDuty`, `GetGPUFanDuty`, `GetAutoFanStatus`, `GetFixedFanStatus`, `GetFixedFanSpeed`, `GetChargePolicy`, and `GetChargeStop`.
- Relevant setter names exist, but are not approved for testing: `SetAutoFanStatus`, `SetFixedFanStatus`, `SetFixedFanSpeed`, `SetStepFanStatus`, `SetGPUFanDuty`, `SetChargePolicy`, and `SetChargeStop`.
- Failed temperature reads are treated as an emergency/high-temperature condition. Curve logic ramps up faster than it ramps down.
- It documents copying Gigabyte's `acpimof.dll`, setting `WmiAcpi\\MofImagePath`, and rebooting. This reversible registration procedure has now been completed with the signed DLL extracted from GCC 26.08.

### LiquidControl (`srikrishnadeveloper/LiquidControl`)

Source: <https://github.com/srikrishnadeveloper/LiquidControl>

- Targets newer Gigabyte G5/Clevo-style machines using `CLEVO_GET`.
- Documents `Fan1Info`, `Fan2Info`, `GetFan12RPM`, `SetFanDuty`, `SetFanAutoDuty`, and `SetKBLED`.
- This is likely a related but different interface. The SE4-specific evidence for `GB_WMIACPI_*` is stronger, so `CLEVO_GET` is only a secondary capability probe.

### clevo-thermald (`samoylenkodmitry/clevo-thermald`)

Source: <https://github.com/samoylenkodmitry/clevo-thermald>

- Documents the newer `CLEVO_GET` command map in depth.
- Maps command 104 (`0x68`) to direct fan duty and command 105 (`0x69`) to returning selected channels to automatic control.
- Recommends confirming commands against the machine's own ACPI tables.
- Architectural lesson: automatic EC control must be the safe state on exit, crash, invalid telemetry, or timeout.

### AeroControl (`lavann/AeroControl`)

Source: <https://github.com/lavann/AeroControl>

- A recent C# implementation around `GB_WMIACPI_Get` and `GB_WMIACPI_Set`.
- Discovers methods at runtime and disables writes unless manufacturer, model, board/SKU, and BIOS match an explicit verified configuration.
- Reads control state back after every write and attempts automatic-mode rollback after partial failure.
- Ideas to retain: capability detection, exact firmware allowlist, serialized operations, readback verification, and rollback-required state tracking.

### Gigabyte Fan Battery Center (`Ixmoon/Gigabyte-Fan-Battery-Center`)

Source: <https://github.com/Ixmoon/Gigabyte-Fan-Battery-Center>

- Uses `GB_WMIACPI_Get` and `GB_WMIACPI_Set` for fan and charging controls.
- Includes a separate emergency helper intended to place fans at a safe high speed after a crash.
- Confirms `GetChargePolicy`, `GetChargeStop`, `SetChargePolicy`, and `SetChargeStop`.

## Current working hypothesis

The AORUS 5 SE4 uses the older Gigabyte `GB_WMIACPI_Get` / `GB_WMIACPI_Set` interface exposed by the signed Gigabyte `acpimof.dll`; the FB0F DSDT confirms the matching dispatchers and method IDs. The ACPI devices already existed, while Windows initially lacked the vendor method schema because `MofImagePath` was empty. Registration and the required restart succeeded, and the exact expected schema and live instance are now verified.

## Provider registration status

- The exact verified DLL was installed to `C:\Windows\System32\acpimof.dll` using the location recommended by Microsoft's WMI ACPI sample.
- `HKLM\SYSTEM\CurrentControlSet\Services\WmiAcpi\MofImagePath` is now the expandable string `\SystemRoot\System32\acpimof.dll`.
- Post-install SHA-256 and Authenticode signature verification succeeded.
- The previous registry/file state was saved under ignored local directory `research/state/`.
- Reversible scripts:
  - `tools/Register-GigabyteWmiProvider.ps1`
  - `tools/Unregister-GigabyteWmiProvider.ps1`
- As expected, the WMI classes did not appear immediately. After the restart, Plug and Play/WmiAcpi imported the schema successfully.
- Pre-restart verification report: `research/runs/diagnostic-20260901-183729.md`.
- First post-restart getter report: `research/runs/diagnostic-20260901-184404.md`.
- Updated getter report with decoded RPM: `research/runs/diagnostic-20260901-184535.md`.
- No setter was invoked during registration or verification.

## First post-restart telemetry result

- Verified live instance: `GB_WMIACPI_Get.InstanceName="ACPI\\PNP0C14\\DCK_0"`.
- The installed MOF exposes 92 getter methods and 92 setter methods. The safe probe invoked only the 14 DSDT-reviewed getter methods in its explicit allowlist.
- CPU temperature: `55 °C`.
- GPU temperature 1: `47 °C`; independently matched `nvidia-smi` at `47 °C` immediately afterward.
- GPU temperature 2: `0`; likely an unused sensor channel on this configuration.
- CPU/GPU fan duty: `66` / `66`, plausibly percent values.
- Raw RPM words: `15879` (`0x3E07`) and `58887` (`0xE607`). The WMI MOF exposes these as `UInt16`, but the bytes arrive in EC order; swapping them yields `0x073E = 1854 RPM` and `0x07E6 = 2022 RPM`.
- A second sample returned raw `14087` / `56071`, decoded to `1847` / `2011 RPM`. The small, physically plausible change strengthens the byte-swap interpretation.
- Charge policy: `0`; charge-stop threshold: `97`.
- Fixed-fan status: `0`; auto-fan status: `0`; fixed/adjust value: `57`.
- Standard Windows ACPI thermal zone `TZ00` read `27.9 °C`; this is not the CPU package temperature.
- A non-elevated telemetry call is denied by Windows. Metadata enumeration works without elevation, but direct ACPI method invocation must be performed by an administrator.

## Read-only live monitor

- The diagnostic executable now supports `--monitor` for continuous read-only sampling.
- It displays CPU/GPU temperatures, decoded CPU/GPU fan RPM, and both fan-duty values.
- Default sampling interval: two seconds; stop with `Ctrl+C`.
- Safety gates require administrator rights, exact model `AORUS 5 SE`, exact BIOS `FB0F`, the expected Gigabyte class, and all six approved getter methods.
- It never opens or invokes `GB_WMIACPI_Set`.
- Convenient elevated launcher: `tools/Start-AorusMonitor.ps1`.
- One-click launcher: `tools/Start-AorusMonitor.cmd` (it calls the local PowerShell launcher and triggers the normal Windows UAC prompt).
- Test-only options: `--samples N`, `--interval-ms N`, and `--plain` permit a finite, capture-friendly run.
- A three-sample elevated test completed successfully with exit code `0`: CPU `51–55 °C`, GPU `46 °C`, CPU fan `1844–1858 RPM`, GPU fan `1981–1998 RPM`, and both duty values `66%`.
- Test log: `research/runs/live-monitor-test-20260901-184846.log`.

## Application structure

- `src/AorusControl.App`: administrator-elevated WPF desktop application using MVVM-style separation. It currently provides a read-only dashboard and a start/stop control for telemetry refresh.
- `src/AorusControl.Diagnostics`: console diagnostics, durable Markdown reports, WMI metadata inspection, and a finite/infinite live-monitor mode.
- `src/AorusControl.Core`: the shared hardware boundary used by both executable projects. It owns the exact model/BIOS allowlist, getter/setter allowlists, WMI connection, RPM byte decoding, operation serialization, timeouts, telemetry plausibility checks, and guarded battery rollback logic.
- `Directory.Build.props`: shared nullable, deterministic-build, language-version, and warning-as-error rules.
- `global.json`: pins the known-working .NET SDK feature band (`10.0.400`).
- `.editorconfig`: shared formatting rules.
- `README.md`: build, run, project-layout, and safety overview.
- The WPF manifest requests administrator elevation because Windows rejects direct ACPI WMI method invocation for a standard token.
- The GUI still has no battery control. Only the dedicated core battery controller references `GB_WMIACPI_Set`, behind the documented gates; no live battery write has been made.

### Structure verification

- Full Release build of all three projects: successful, zero warnings, zero errors.
- Shared-core elevated telemetry test: three samples completed with exit code `0`; log `research/runs/shared-core-test-20260901-185621.log`.
- WPF smoke test: application started, remained alive and responsive for six seconds while reading through the shared core, and was then closed by the test harness; log `research/runs/wpf-smoke-test-20260901-185642.log`.

## Keyboard and lighting capability

- The AORUS 5 SE specification describes a three-zone RGB keyboard.
- The local internal keyboard is a USB composite HID device with vendor/product ID `1044:7A41`. Its interfaces include two HID keyboard collections, multiple vendor-defined HID collections, a system controller, mouse collections, and consumer-control collections.
- Public driver listings independently associate `HID\\VID_1044&PID_7A41` with the AORUS 5 SE keyboard.
- Gigabyte's current GCC package contains a dedicated `GBT_Keyboard_25.07.25.01.exe` module (46 MB), separate from `GBT_Notebook`. Its Authenticode signature is valid and names `GIGA-BYTE TECHNOLOGY CO., LTD.`. SHA-256: `4E4986CDB5B23A7CC55C8C438AE2D7CE7B565BEFB51F807B044F2A0F618B56A1`.
- With explicit user approval, only this official keyboard module was installed silently; the installer returned exit code `0`. Windows registers `GBT_Keyboard 25.07.25.01`; its reversible uninstaller is `C:\Program Files\GIGABYTE\Control Center\Lib\GBT_Keyboard\uninst.exe`. The installation added no Windows service.
- The installed Gigabyte WMI schema nevertheless exposes keyboard/light-related getters: `GetKeyBoardBackLight`, `GetKeyboardMatrix`, `GetLightBar`, `GetBrightness`, and `getFnKeyLockStatus`.
- Related setters exist in the schema but remain untested: `SetKeyBoardBackLight`, `SetKeyboardMatrix`, `SetLightBar`, `SetRGBLed`, `SetBrightness`, and `SetFnKeyLockStatus`.
- Verified architecture: the official module explicitly recognizes HID `1044:7A41` with a 9-byte feature report as an ITE three-zone RGB keyboard. WMI/EC still appears to cover only a coarse/compatibility backlight field on this model.
- The open-source `keyboard-fusion-rgb` project documents a similar Gigabyte/Chu Yuen HID protocol for AORUS keyboards with nearby product IDs, including effects and per-key RGB. Its packet format must not be assumed compatible with `7A41` without capturing or statically recovering the exact GCC protocol.
- No exact public `1044:7A41` RGB protocol implementation was found in the initial search. The exact getter protocol was instead recovered from Gigabyte's signed module.

### First keyboard state investigation

- FB0F DSDT confirms `GetKeyBoardBackLight` as method ID `0xF6`, a side-effect-free direct read of EC byte `KBLL` at offset `0xD7`.
- The live `GetKeyBoardBackLight` result was `0`. This does not necessarily mean RGB is off; it may be an unused compatibility field for this HID-controlled keyboard.
- `GetKeyboardMatrix` (`0xF1`), `GetLightBar` (`0x59`), `GetBrightness` (`0xC0`), and `getFnKeyLockStatus` (`0xC9`) have no dedicated read cases in this FB0F dispatcher. They were not invoked because they would only return default/input data and cannot reveal the keyboard colors.
- WMI read report: `research/runs/keyboard-wmi-read-20260901-190422.txt`.
- A new diagnostics mode, `--inspect-keyboard`, inventories HID metadata for exact device `1044:7A41` without opening a communication stream or requesting/sending reports.
- HID inventory found 11 collections. The RGB-relevant candidates are:
  - `MI_01`: vendor usage page `0xFF00`, 65-byte input and 65-byte output reports, no report ID.
  - `MI_03`: vendor usage page `0xFF01`, 65-byte input/output and 9-byte feature reports, no report ID.
  - `MI_02/COL_07`: feature report ID `0x5A`, length 17 bytes.
- Detailed descriptor report: `research/runs/keyboard-hid-inventory-20260901-190640.md`.
- A second diagnostics mode, `--read-keyboard-state`, performs only USB HID `GET_REPORT (Feature)` on the two collections that expose feature reports. It contains no `Write` or `SetFeature` operation.
- Returned feature bytes:
  - `MI_02/COL_07`: `00 FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF 00`.
  - `MI_03`: nine zero bytes.
- These feature reports do not expose the active RGB colors in an obvious form. Report: `research/runs/keyboard-feature-read-20260901-190735.md`.
- The zero-only passive feature result was explained by the official implementation: the keyboard requires a proprietary query command before `GET_FEATURE` returns current state.

### Exact three-zone RGB protocol recovered from the signed module

- Relevant official managed code is in `KeyboardModel.dll` and `KeyboardDomainLogic.dll`; the native stack includes `HidDriver.dll`, `GkLedLib.dll`, and `GLedApi.dll`.
- `GenericKeyBoard` explicitly matches a device path containing `1044` and `7a41`. When that collection has `FeatureReportByteLength == 9`, it marks the device as ITE, zone RGB, and the `3a4041` family.
- The exact collection observed locally is `MI_03`, vendor usage page `0xFF01`, with a 9-byte feature report.
- All feature packets are 9 bytes including report-ID byte `0x00`. Byte 8 is `255 - sum(bytes 1..7)`, truncated to one byte.
- Query command: byte 1 is `0x88`. The selector in byte 2 is `0` for global effect state or zone `1`, `2`, or `3` for zone state. Official code calls `HidD_SetFeature`, then `HidD_GetFeature`.
- State-changing command: byte 1 is `0x08`. It is deliberately absent from the current diagnostic implementation.
- Global response fields: byte 3 effect, byte 4 speed, byte 5 brightness, byte 6 color enum, byte 7 direction.
- Zone response fields: bytes 3/4/5 are red/green/blue and byte 6 is brightness. Gigabyte's helper converts its 0–50 brightness value to a UI percentage by multiplying by two.
- Official effect enum: Static `1`, Breathing `2`, Wave `3`, Fade-on-keypress `4`, Marquee `5`, Ripple `6`, Flash-on-keypress `7`, Neon `8`, Rainbow-marquee `9`, Raindrop `10`, Circle-marquee `11`, Hedge `12`, Rotate `13`, and Custom 1–5 as `51`–`55`.
- Official color enum: Black `0`, Red `1`, Green `2`, Yellow `3`, Blue `4`, Orange `5`, Purple `6`, White `7`, Random `8`.
- The new `--query-keyboard-rgb` diagnostic has an exact VID/PID/interface/report-length gate and can transmit only query command `0x88`; it has no arbitrary packet API and no setter command.
- First live query succeeded without elevation. All three zones returned RGB `(62, 0, 102)`, hex `#3E0066`, brightness raw `50` / estimated UI `100%`.
- The official firmware query was also added and returned keyboard firmware version `19.0.4` (raw `00 80 13 04 00 00 00 00 00`).
- The global response returned zeros for effect/speed/brightness/color/direction. Since effect `0` is not in Gigabyte's enum while zone state is valid, global effect state may be unused in the three-zone operating mode; it must not be labeled Static.
- Durable live report: `research/runs/keyboard-rgb-query-20260901-191409.md`.
- Updated RGB plus firmware report: `research/runs/keyboard-rgb-query-20260901-192115.md`.
- A guarded zone-1 setter test temporarily changed `#3E0066` to `#66003E`, verified it by readback, restored the original in `finally`, and verified the restoration. Report: `research/runs/keyboard-zone-write-test-20260901-192237.md`.
- This confirms arbitrary per-zone RGB writes and brightness preservation on the exact `7A41` firmware. The setter remains isolated in diagnostics and is not exposed by `AorusControl.Core` or the application.
- At the user's explicit request, all three zones were then changed from `#3E0066` to persistent green `#00FF00` with no restore. Readback verified zones 1–3 and preserved raw brightness `50` (100% UI estimate). Report: `research/runs/keyboard-set-green-20260901-192437.md`.
- After the user changed keyboard brightness with `Fn+Space` to the lowest non-off level, a new RGB query still returned all three zones at `#00FF00` with raw brightness `50`. The passive HID feature read exposed no independent brightness field; its `MI_03` value only reflected the most recently queried zone. The earlier DSDT-backed EC getter `GetKeyBoardBackLight` reads `KBLL@0xD7` but returned `0`. This indicates the four `Fn+Space` brightness steps are likely maintained internally by the keyboard controller and are not exposed as the RGB-zone brightness byte or a persistent Windows-readable field. Comparison report: `research/runs/keyboard-rgb-query-20260901-192611.md`; passive report: `research/runs/keyboard-feature-read-20260901-192818.md`.
- A 25-second elevated correlation monitor then sampled `EC.KBLL` every 250 ms while listening only to vendor HID interfaces `MI_01` and `MI_03` as the user cycled `Fn+Space`. `KBLL` remained `0`, and neither vendor channel emitted an input report. This strengthens the conclusion that the four-step dimmer and the Fn+Space chord are processed entirely inside the keyboard controller. Report: `research/runs/keyboard-brightness-monitor-20260901-193246.md`.
- The software-controlled zone brightness was then cycled through raw values `0`, `17`, `33`, and `50` (approximately 0%, 34%, 66%, and 100%) while preserving all three green colors. Readback verified every value on every zone. The final state was left at raw `50` / 100%. Report: `research/runs/keyboard-brightness-cycle-20260901-193519.md`.
- In the first two-second visual cycle, the user clearly observed only off and full brightness, plus a slight left-to-right delay as the three zones changed. This matches the protocol's separate per-zone packets and Gigabyte's own 65 ms delay after each write. A second test held every step for five seconds; the device again stored/read back `0`, `17`, `33`, and `50` on all zones. Report: `research/runs/keyboard-brightness-cycle-20260901-193719.md`. Readback proves storage, but visible intermediate PWM levels still depend on user observation and may be coarsely quantized by firmware.
- A boundary test then held raw `0`, `1`, `25`, `49`, and `50` for five seconds each. User observation confirmed that the keyboard stayed off through `49` and switched to full brightness only at `50`, even though every byte read back exactly. This was read at the time as an off/on gate rather than a usable control. **That conclusion was wrong, and the cause was the chosen values.** See the 2026-09-03 correction below: the field accepts exactly `0`, `24`, `32`, and `50`, and `1`, `17`, `25`, `33`, and `49` all miss those values and read as off. Report: `research/runs/keyboard-brightness-cycle-20260901-193917.md`.
- Application implication: expose zone lighting as off/on (`0` or `50`). Do not offer a misleading software brightness slider. The four physical levels are controlled by the internal Fn+Space state, which remains unreadable and has no identified host command.
- Full solution build after adding the query: successful, zero warnings and zero errors.
- A complete evidence-graded inventory of lighting, effects, zones, key assignment, macros, gaming mode, and the recovered HID command map is maintained in `research/KEYBOARD-CAPABILITIES.md`.
- First guarded global-effect write: temporary Breathing `2`, raw speed `5`, brightness `50`, Green palette `2`, direction `1`. The global getter still returned zeros; all three original green zones were restored and verified after ten seconds. Visible success awaits user observation. Report: `research/runs/keyboard-effect-breathing-test-20260901-200013.md`.
- Speed has nine distinct firmware bytes (`9` slowest through `1` fastest; Gigabyte's UI 90 and 100 both map to raw `1`). Built-in animations run inside the keyboard. Gigabyte's own module waits 65 ms after every zone write, which would cap host-driven three-zone animation at about 5.1 frames/s. **Correction after direct measurement:** that delay is not a firmware requirement. Intervals of 65, 40, 25, 15, 10, and 5 ms were each written six times to zone 1 and verified by readback; all six succeeded at every interval, including 5 ms. Host-rendered animation is therefore practical, not marginal.
- The exact official `0x8D` getter returned the complete live 512-byte `3a4041` assignment matrix in eight blocks. It contains 128 four-byte slots and no Gigabyte user-macro/basic-hotkey marker records. Five secondary special-key bytes differ from the generic signed default; they remain unlabeled rather than guessed. No macro data was requested and nothing was written. Report: `research/runs/keyboard-matrix-read-20260901-200413.md`.
- Visible RGB batch 1 requested Breathing `2`, Wave `3`, and Fade-on-keypress `4` for eight seconds each. The controller again returned zero-only global readback, so visible classification awaits the user's observation. All three green zones were restored and verified. Report: `research/runs/keyboard-effect-batch1-20260901-201007.md`.
- An interactive ten-effect tester is now available through `tools/Start-KeyboardEffectTest.cmd`. Enter advances, arbitrary text is saved as the effect observation, and `/stop` ends early; all original zones are restored and verified afterward. A `/stop` smoke test completed successfully: `research/runs/keyboard-effect-interactive-20260901-201354.md`.
- The user completed all ten entries. Only the already-active static green state remained visible; Breathing, Wave, Fade-on-keypress, Marquee, Ripple, Neon, Raindrop, Hedge, and Rotate caused no visible change. All requests had valid official packet fields, but global readback stayed zero. Report: `research/runs/keyboard-effect-interactive-20260901-201738.md`.
- This is corroborated for the **current discovered path** by Gigabyte's exact ZoneRgb profile loader: it reads effect/speed/direction from profiles but discards them and writes only three zone colors. Direct static three-zone RGB works on `1044:7A41` firmware `19.0.4`; the tested global effect path does not. The owner subsequently supplied decisive historical evidence from this exact laptop: Breathing, Flash/Pulse, and slow full-color transitions previously worked visibly. Therefore effects are not a pure hardware impossibility; an older GCC/service/profile path or host-rendered animation remains to be recovered before the replacement app's final capability model is fixed.
- A web cross-check found the same static-only description in Gigabyte's official three-zone specification and independent 2022 SE4 reviews. User reports show effect controls appearing or disappearing across GCC updates, and one exact-model report says connecting a Gigabyte mouse made extra modes appear. Combined with the shared RGB Fusion architecture, this strongly indicates UI/profile capability leakage rather than working keyboard effects. Full source-by-source analysis: `research/RGB-WEB-FINDINGS.md`.
- After the owner's historical confirmation that effects previously worked, a combined slow-cycle request was sent without restoring it: Neon/Cycle `8`, slowest raw speed `9`, brightness `50`, Random palette `8`, direction `1`. The exact packet was `0008000809320801AB`; immediate global readback again returned all zeros. Static green zone values were not overwritten, so any visible animation remains active for user observation if the controller accepted it. Report: `research/runs/keyboard-slow-color-cycle-20260901-205852.md`.
- Older GCC `23.03.02.01` was recovered from a mirror after its historic Gigabyte URL returned 404. The 89 MB archive contains a valid Authenticode-signed Gigabyte installer and signed RGB sync component; nothing was installed or executed. Its release notes explicitly add Zone RGB keyboard gen-1/gen-2 detection and mention Neon/off, brightness, and keyboard firmware fixes. Static contents include `GHidApi`, `RGBFI`, `UIEffect`, `sp.xml`, and managed symbols for `ZoneRgb`, `SetLightEffect`, Breathing, Pulse, Cycling, Neon, and speed control. Full provenance, hashes, signature results, and next analysis steps: `research/OLD-GCC-ARCHIVE.md`.
- The old update client exposed Gigabyte's still-live official module path, from which the valid signed `GBT_Keyboard_23.03.10.01` was recovered and statically decompiled. It explicitly recognizes exact USB device `1044:7A41` as ITE + ZoneRgb + `3a4041`, exposes Static/Pulse/Wave/Reactive/Marquee/Ripple/Cycle/Droplet/Hedge/Spiral, and its live page calls the global effect setter. Its 9-byte `0x08`, selector-0 packet and `0x88` getter are byte-for-byte the same protocol used by our tests. Thus the packet format is not the fault. Full comparison: `research/OLD-KEYBOARD-MODULE-COMPARISON.md`.
- Static extraction of the matching official keyboard updater confirms target `USB\\VID_1044&PID_7A41` and firmware string `Gigabyte Fusion_8298:1.9.0.4`, matching the live `19.0.4` result. The firmware image dates from September 2023, after the recovered old module's July 2023 core DLLs. A firmware behavior change is now the leading hypothesis, not a proven cause. Nothing was executed or flashed.
- The old RGB page's initialization was also traced. `InitKeyboard()` only enumerates/opens the HID device and classifies it; it sends no unlock or setup packet before the immediate effect write. The separate `0x09` gaming-mode call is not part of the RGB page. This makes an omitted initialization command less likely and strengthens, without proving, the firmware-behavior hypothesis.
- The exact old default Pulse packet was then sent and deliberately left active: `0008000205320501B8` = Breathing `2`, speed `5`, brightness `50`, Orange `5`, direction `1`. The write completed, but immediate global readback was again all zeros (`008800000000000000`); all three saved static zones remained green at raw brightness `50`. Visible outcome awaits owner observation. Report: `research/runs/keyboard-old-default-pulse-20260902-182252.md`.
- Owner observation: the exact old Orange Pulse request caused no visible change; the keyboard remained static green.
- The old keyboard package contains no `.inf`, `.sys`, or `.cat` and installs no separate keyboard device driver. Its `7A41` implementation uses ordinary Windows HID `CreateFile` + `HidD_SetFeature`; current `MI_03` is healthy on Microsoft's signed `input.inf` driver. Installing the old module would therefore replace application DLL/UI code, not provide an older keyboard driver, and is unlikely to change the already-replicated packet result. Avoid installation because of GCC version-conflict risk.
- Proven keyboard RGB control is now exposed through the shared core and simple WPF application: off/on, three independent 24-bit colors, an all-zones link option, and nine host-rendered moving effects. The controller hard-gates `1044:7A41 / MI_03 / 9 bytes`. Static changes use full readback and rollback; an effect captures the original three zones and restores plus verifies them when stopped, replaced, superseded by a static change, or when the application closes. Full implementation notes: `research/APP-KEYBOARD-RGB.md`.

- Decisive elimination of the software hypothesis: the installed `GBT_Keyboard 25.07.25.01` ships **bit-identical** keyboard binaries to the historic `23.03.10.01`. `KeyboardModel.dll`, `KeyboardDomainLogic.dll`, `GkCenters.dll`, and `ucKeyboard.dll` all match by SHA-256 and are dated 2023-07-28. The installed managed assemblies were decompiled for the first time; a full file-by-file and content comparison against the old decompilation found no added, removed, or changed file. Since Gigabyte's host code has not changed by a single byte between the period when effects were visible and today, the cause must lie on the device side.
- The getter-race hypothesis is also eliminated: our diagnostics already waits 500 ms between `SET_FEATURE` and `GET_FEATURE` on selector 0, while zone queries return valid data after 65 ms. Byte 1 echoes `0x88` correctly and only fields 3-7 stay zero, so the firmware maintains the global effect field but keeps it empty.
- The official ITE command set was read out exhaustively. Beyond the known `0x08`/`0x88`, `0x80`, `0x0D`/`0x8D`, `0x11`/`0x91`, and `0x09`, it contains one never-tested pair: `0x12`/`0x92`, a 960-byte `PictureMatrix` transferred in 64-byte blocks over 65-byte `ReadFile`/`WriteFile` reports on the same `MI_03` handle. Its slot index is `effect - 51`, matching the `Custom1`-`Custom5` enum values, and Gigabyte's own `SetPictureMatrix2DeviceSleepTime` plus `SynchPictureMatrixColor` used it for host-driven animation. This is a complete second lighting path that has never been probed.
- The firmware image in `third-party/vendor/keyboard-firmware-19.0.4-static/docking_b.bin` was confirmed as the live firmware: `Gigabyte Fusion_8298:1.9.0.4` sits unencrypted at offset `0x2010`, and `SHFU.ini` names `USB\VID_1044&PID_7A41` with chip ID `8298`. The occupied code region is `0x2000`-`0xB487`. No command dispatcher could be located; searches for the 8051 `CJNE A,#imm` pattern on every known command byte returned zero hits, and the tables at `0x000`/`0x400` match neither 8051, ARM Cortex-M, nor a clean LE32 pointer format. The ITE 8298 core is unidentified and a reliable disassembly would be a separate sub-project.
- Safety finding: `SHFU.ini` sets `REPORTID=90`, decimal 90 = `0x5A`, which is exactly the 17-byte feature report on `MI_02/COL_07` found by the HID inventory. That collection is the ITE flash-update channel. New hard exclusion rule: never write to report ID `0x5A`.
- Full elimination table, remaining hypotheses, and the next safe steps: `research/RGB-EFFECT-INVESTIGATION.md`.
- The effect question was then re-tested with a sound design, and the earlier classification is superseded. Every step writes all three zones to `#FFFFFF` at brightness `50`, verifies the readback, then sends exactly one global effect packet. Run one used palette `0` for effect IDs `51`, `1`, `52`, `2`, and `8`: the keyboard went dark every time. Palette `0` is `FusionLightColor.Black`, so that blackout was a correct rendering, not a failure, and it proves the packet is parsed. Run two used vivid palettes — Static/Red, Static/Blue, Breathing/Red, Wave/Random, Neon/Random — and nothing changed at all in any step. Effect `1` appears in both runs differing only in the palette byte, which isolates the mechanism: **palette `0` is honoured and blanks the lighting, while every other palette value is a no-op for both static and animated effect IDs.** During the blackout the zone registers keep their values and any zone write restores lighting. The global getter returned `008800000000000000` in all ten steps, including those where the lighting visibly changed, so it is worthless as evidence of effect state and the earlier conclusions that cited it are void. Reports: `research/runs/keyboard-effect-isolation-20260902-193655.md`, `research/runs/keyboard-effect-palette-20260903-112527.md`.
- Also invalidated: the 2026-09-01 interactive effect test used palette `2` (Green) while the keyboard was already static green, so a correctly rendered green effect would have looked identical to the previous state. That run could not distinguish effect from no effect.
- Standing conclusion for `7A41` firmware `19.0.4`: the global effect command `0x08` selector 0 has exactly one working function, blanking via palette `0`; the effect engine renders neither colour nor animation. Since Gigabyte's host code is proven bit-identical to the 2023 release the owner remembers working, a firmware behaviour change remains the leading explanation, provable only by disassembling the ITE 8298 core or by a downgrade, which is excluded. The application does not depend on the answer.
- Host-rendered lighting was then demonstrated end to end with the new `--test-keyboard-host-effects` mode, which uses only the two commands already proven on this device: zone setter `0x08` selector 1-3 and zone getter `0x88`. No global effect command, no new command byte, no picture matrix. Breathing, a full-spectrum colour cycle, and a travelling three-zone wave each ran for eight seconds at an achieved 8 to 21 three-zone frames/s, limited by Windows timer granularity rather than by the device. The original three zones were captured first and restored plus verified exactly in a `finally` block. This is the path that reaches the owner's actual goal of visible effects without depending on the unresolved firmware question. Report: `research/runs/keyboard-host-effects-20260902-192100.md`.
- **The owner confirmed that the host-rendered effects were visibly displayed.** This settles the practical question: visible multi-effect lighting on this exact laptop does not require the firmware effect engine at all. An interactive ten-effect tester was therefore added as `--interactive-host-effect-test`, reachable through `tools/Start-HostEffectTest.cmd`, following the same conventions as the existing firmware-effect tester: each effect animates until Enter, typed text becomes that effect's observation, and `/stop` ends early. Offered effects are Static, Breathing, Pulse, Colour cycle, Rainbow marquee, Wave, Marquee, Rotate, Raindrop, and Fade sweep. A `/stop` smoke test restored and verified all three zones exactly.
- Full interactive run completed by the owner: **all ten host-rendered effects were confirmed working** — Static, Breathing, Pulse, Colour cycle, Rainbow marquee, Wave, Marquee, Rotate, Raindrop, and Fade sweep. Measured throughput was a steady 21.2-21.4 three-zone frames/s, set by Windows timer granularity rather than the device, and all three original zones were restored and verified exactly. Report: `research/runs/keyboard-host-effect-interactive-20260902-192953.md`. The application's effect capability is therefore settled and no longer depends on the unresolved firmware question.
- The nine moving host-rendered variants from that confirmed run are now integrated into the WPF application with a minimal selector plus Start/Stop controls. They use only zone setter `0x08` selectors 1-3; the global firmware-effect selector, picture matrix, flash channel, WMI, and EC are not used. Release build of all three projects completed with 0 warnings and 0 errors on 2026-09-03. The UI integration still awaits the owner's visual click-through.
- Resolved observation: that run captured the zones as `#0000FF`, `#FF02FF`, and `#FF0006`, while the previous documented state on 2026-09-02 18:22 was uniform green `#00FF00`. The owner confirmed setting these colours by hand while testing this project's own WPF colour pickers. No firmware effect was involved, and repeated queries returned a stable state. Recorded because it is otherwise the only zone change not explained by a logged diagnostic.
- First live probe of that second path: the new read-only `--probe-keyboard-picture-matrix` mode sends only getter `0x92` and reads the eight 65-byte input reports; setter `0x12` is deliberately absent. Firmware 19.0.4 answers it. Read into a zeroed buffer so the response is provably device-sourced, the handshake returned `00 92 00 02 08 00 00 00 00`: command echo, the requested slot index mirrored back, and the block count supplied by the device. All eight blocks then arrived without timeout in every run, and `MI_03` is known not to emit input reports on its own. All five custom slots are empty: 512 bytes each, zero non-zero bytes. This is a sharp contrast to `0x88`/selector 0, which echoes the command but leaves its data fields blank, and it explains why effects `51`-`55` would show nothing. An empty payload does not fully exclude a generic block-transfer engine returning a zeroed buffer, but the correct slot mirroring argues for real packet evaluation. Reports: `research/runs/keyboard-picture-matrix-probe-20260902-19*.md`.

### Keyboard brightness: the four Fn+Space steps

- The WMI classes were searched exhaustively for brightness-related methods for the first time. `GB_WMIACPI_Set` exposes **`SetKeyBoardBackLight` with method ID `246` = `0xF6`**, the same ID as the getter that the FB0F DSDT maps to `EC.KBLL@0xD7`; only the dispatcher differs, `WMBC` for reads and `WMBD` for writes. Its signature is `SetKeyBoardBackLight(Data: UInt8)`. **It has never been invoked.** Since the getter returns a constant `0` while the lighting is on, a read can no longer distinguish an unused field from a write-only one, so the write path is the only remaining source of information. Presence in the shared MOF still does not prove that `WMBD` implements a case for `0xF6`; the test answers that empirically through its readback.
- Also present and never invoked: `SetRGBLed` `131`, plus `SetBrightness` `192`, `SetBrightnessOff` `196`, and `IncreaseBrightness` `205`, which most likely concern display brightness.
- Second untested region: the zone packet's brightness byte above `50`. The earlier boundary test covered only `0`, `1`, `25`, `49`, and `50`, and Gigabyte's UI never sends more than `50` because it halves a 0-100 percentage. Off/on behaviour at exactly `50` is compatible with an exact `== 50` comparison, a `>= 50` threshold, or a real scale with a high minimum, and only the third would be a usable control.
- Two guarded diagnostics were added for these. `--test-backlight-level` gates on exact model and BIOS, administrator rights, a typed `JA`, and the token `--confirm-backlight-write`; it saves the original value, writes `0` through `4` with a readback and owner observation after each, and rewrites plus verifies the original in `finally`. `--sweep-zone-brightness` needs no elevation, holds all three zones at `#FFFFFF`, and walks the brightness byte over `0`, `25`, `50`, `51`, `60`, `75`, `100`, `150`, `200`, `255` using only the proven zone setter and getter. Both refusal paths were verified.
- Still unbuilt third angle: the HID inventory found 11 collections including a system controller and consumer-control collections, while the earlier Fn+Space monitor listened only to `MI_01` and `MI_03`. Brightness hotkeys are commonly reported as consumer-control or system-control usages. These are not standard keyboard interfaces, so listening there captures no keystrokes and stays inside the existing privacy boundary. Even a read-only result would let the application display the current step.
- Full status, evidence, and exclusions: `research/KEYBOARD-BRIGHTNESS.md`.
- **The Fn+Space step is host-readable after all, and the earlier conclusion is withdrawn.** That conclusion rested on a monitor listening only to `MI_01` and `MI_03`. A new read-only hunt listened to all eight non-keyboard collections and queried the official getter `0x88` for selectors `0` through `15`. The collection `MI_02/COL_04`, which declares no usages at all, emits a 4-byte input report on every `Fn+Space`: byte 0 is report ID `0x04`, byte 1 is a constant `0x01` that presumably identifies the event type, and **byte 2 carries the step**. Captured values were `0` when the owner reported off, `24` for low, and `32` for medium. The brightest step was not captured because no report arrived in the first two rounds; the spacing suggests `40` = `0x28` but that is a guess, not a measurement. `MI_02/COL_04` exposes only an input report and no output or feature report, so it is a notification channel, not a control. Report: `research/runs/keyboard-brightness-signal-hunt-20260903-123301.md`.
- Side result from the same run: selectors `4` through `15` of getter `0x88` each echo the command and selector correctly but carry only zeros, so beyond the three zones and the global slot there is no further readable state. The firmware query stayed constant across all rounds. `MI_02/COL_01` and `COL_06` could not be opened in any round because Windows holds them through the mouse class driver; both declare mouse usages and are irrelevant here.
- Privacy defect found and fixed during that run: the first version of the collection filter checked only collection-level usages, which let both keyboard interfaces into the listen list. Keyboard collections declare only Generic Desktop `0x00010006` at that level and carry their `0x0007` key usages on the data items. Windows refused to open them, so nothing was captured, but the gate must not depend on that. It now tests key usages on data items and collection level, the `0x00010006` collection usage, and device paths ending in `\kbd`; `MI_00` and `MI_02/COL_05` are now provably skipped and logged as such.
- Follow-up built: `--monitor-brightness-events` listens continuously on `MI_02/COL_04` instead of in rounds, prints each event with a timestamp and decoded step, and states in its report whether all four steps were covered. This should complete the value table, including the brightest step.
- Application consequence: the graphical application can display the physical brightness step live and keep its own state in sync, even though it cannot set it.
- The continuous monitor completed the table with 32 captured events and all four steps. The predicted `40` was **wrong**: the brightest step reports `50` = `0x32`. The full mapping is off `0`, low `24`, medium `32`, bright `50`, and the Fn+Space cycle order is `0` to `24` to `32` to `50` and back to `0`. Byte 1 stayed `0x01` and byte 3 stayed `0x00` in every event, so only one event type appeared on this channel. Report: `research/runs/keyboard-brightness-events-20260903-123822.md`.
- **This weakens the earlier battery of brightness conclusions.** `0, 24, 32, 50` is Gigabyte's own 0-50 scale, the same one the zone brightness byte uses with `50` as its on-threshold — not a `0-3` index. The ACPI write test only ever wrote `0` through `4`, so it never tried a single real step value, and its "no effect" result is therefore **not conclusive**. `--test-backlight-level` gained a `--levels` option and now defaults to `0,24,32,50`.
- Note for interpretation: writing `24` or `32` into the *zone* brightness byte was already shown to leave the keyboard dark, so the shared scale does not by itself make the zone byte a dimmer. The two fields are distinct even though their value range matches.
- The retest closes the question. `SetKeyBoardBackLight` was invoked with the real step values `0`, `24`, `32`, and `50`; each was stored and read back exactly, and none had any visible effect. Rollback verified. `EC.KBLL@0xD7` is therefore conclusively an orphaned storage byte, and the qualification placed on the first run is lifted now that the genuine step values have been tried. Report: `research/runs/keyboard-backlight-level-20260903-124228.md`.
- **Superseded.** The claim that brightness is not settable was wrong; see the 2026-09-03 resolution below. It survived this long because every sweep value happened to miss the four accepted ones.
- Two paths were never touched, both with low expected value and outside the current command boundary, to be used only if displaying the step proves insufficient: `MI_01` with vendor usage page `0xFF00` and 65-byte reports, for which no protocol is known and which Gigabyte's own modules do not use, and `SetRGBLed` at WMI ID `0x83`, never invoked and of unclear purpose on this model.
- Interaction matrix completed, with the hardware step read live from `MI_02/COL_04` rather than assumed. Across all four steps the zone brightness byte behaved identically: `0` and `25` off, `50` on at full brightness. The two quantities therefore do not combine multiplicatively. The decisive finding is **precedence: writing zone byte `50` forces full brightness even when the hardware step is `0`, and any value below `50` switches the lighting off regardless of the step.** Setting brightness thus always works but offers only two states, and the owner's impression of intermittent behaviour came from three different fields being conflated, only two of which have any effect. Report: `research/runs/keyboard-brightness-interaction-20260903-124922.md`.
- Consequence for the application: after any zone write, the last step value reported on `MI_02/COL_04` no longer matches the visible brightness. A live display must treat its own zone write as an override and stop showing the stale step value.
- Defect in that run's design: the zone values were `0`, `25`, `50`, which carried `25` over from the pre-discovery sweep and omitted `32` entirely, so the exact step values were never paired against a matching hardware step. Neighbouring values are already covered — the 2026-09-01 boundary test used `17`, `33`, and `49`, all off — but `24` and `32` exactly are untested. The `--zone-values` default is now `0,24,32,50`.
- **RESOLVED, and several earlier conclusions are overturned: keyboard brightness is fully settable.** The rerun with the measured step values as zone values produced, at every one of the four hardware steps, the identical result: zone byte `0` off, `24` level 1, `32` level 2, `50` level 3. The zone brightness byte is a complete four-level control, and it overrides whatever step Fn+Space last set. Report: `research/runs/keyboard-brightness-interaction-20260903-125316.md`.
- Why this stayed hidden: the firmware appears to accept only those four exact values, treating anything else below `50` as off and anything above `50` as full. Every earlier value list missed all of them — the 2026-09-01 cycle used `0, 17, 33, 50`, the boundary test `0, 1, 25, 49, 50`, the 2026-09-03 sweep `0, 25, 50, 51 … 255`, and the first interaction matrix `0, 25, 50`. Each list was derived from the previous one instead of from the current state of knowledge, so the same blind spot was repeated three times. The owner named the flaw by asking why the discovered step values had not been used, and the rerun settled it immediately.
- Method rule adopted from this: when a measurement yields new discrete values, the value lists of all related tests must be realigned to them rather than carried forward.
- Still to pin down: `32` selects level 2 while `33` reads as off, which is a very sharp quantisation inferred from two separate runs rather than observed side by side. Writing only the four exact values is sufficient for the implementation; a neighbourhood sweep over `23, 24, 25, 31, 32, 33, 49, 50` would establish the firmware's actual comparison rule.
- Application consequence: the restriction to off and full brightness is obsolete. `AorusControl.Core` and the graphical application can offer four brightness levels through the same command already verified for zone colours, `0x08` selector 1-3 with byte 6 from `{0, 24, 32, 50}`.
- Effect speed needed no further research. Gigabyte's speed byte belongs to the global effect command `0x08` selector 0, which renders nothing on firmware `19.0.4`, so its nine discrete values are meaningless here. Because the animation is host-rendered, speed is a plain time scale: `KeyboardEffectSpeed` offers five steps with factors `0.25`, `0.5`, `1.0`, `2.0`, and `4.0`, `Normal` being exactly `1.0` so the timings the owner already confirmed stay unchanged. The renderer multiplies elapsed time by the factor, which scales every effect uniformly including those with modulo thresholds.
- Interface defect reported by the owner and fixed: buttons and combo boxes were barely legible. The cause was the implicit `TextBlock` style in `App.xaml` setting a light foreground — WPF renders button and combo-box item text through a `ContentPresenter`, which creates a `TextBlock`, and an implicit style beats inheritance, so light text landed on the light default Windows surfaces. The implicit style now sets only the font family, the window sets `Foreground` once, and `Button`, `ComboBox`, `ComboBoxItem`, and `CheckBox` have explicit dark templates with visible disabled states. The button template keeps `Background` as a `TemplateBinding` so the three zone colour swatches still show their bound colour, the two value pickers use explicit `ItemTemplate`s because `DisplayMemberPath` alone left `SelectionBoxItemTemplate` empty in a custom template, and the content area sits in a `ScrollViewer` so no control becomes unreachable on a short screen. Verified from screenshots of the running application rather than from source alone.
- Both tests were run and both angles are closed. `SetKeyBoardBackLight` accepted values `0` through `4` and every one read back exactly, which proves `WMBD` really does implement case `0xF6` and that `EC.KBLL@0xD7` is a working, writable storage byte rather than a MOF-only entry. None of the values had any visible effect, so nothing on this model consumes the field; it is an orphaned register. This also explains the earlier constant `0` reading — the stored value simply was `0`. Rollback to `0` verified. Report: `research/runs/keyboard-backlight-level-20260903-115048.md`.
- The zone brightness byte sweep over `0`, `25`, `50`, `51`, `60`, `75`, `100`, `150`, `200`, `255` stored every value exactly and showed `0` and `25` off, `50` on, and `51` through `255` indistinguishable from `50`. This was read as a `>= 50` threshold. **The reading was wrong:** the list contains none of the accepted intermediate values, so the two working levels between off and full were invisible to it. Report: `research/runs/keyboard-zone-brightness-sweep-20260903-115057.md`.
- Three of the four conceivable control paths for the four physical steps are now exhaustively excluded: the zone brightness byte, the EC field `KBLL`, and a dedicated command in the keyboard protocol. The standing interpretation is that the steps are managed entirely inside the ITE controller with no host access. Only reading the current step through the not-yet-monitored HID collections remains open, and setting it is not expected to follow.
- Process defect found and fixed while collecting these results: `FindRepositoryRoot()` walked up from `Environment.CurrentDirectory`, and the elevated launchers start in `C:\WINDOWS\system32`. Reports from elevated runs were therefore written to `C:\WINDOWS\system32\research\runs\` instead of the repository, silently and since 2026-09-01. Four reports were recovered from there, including three battery inspections that had never reached the project. The resolver now probes `AppContext.BaseDirectory` first and keeps the current directory only as a fallback, and both elevated launchers pass `-WorkingDirectory`.

### Other potentially controllable features exposed by the schema

- Battery charge threshold and charge policy.
- Automatic, fixed, and stepped fan modes plus fan curves.
- CPU/GPU temperatures, RPM, duty, and other thermal sensors.
- GPU mode/power configuration, Dynamic Boost, NVIDIA thermal target, and Whisper Mode.
- Fn-key lock and Windows-key blocking.
- Camera, touchpad, Wi-Fi, Bluetooth, LAN, and USB charging states.
- Power/indicator lights, light bar, display brightness, and post animation.

Clarification of these shared-schema labels:

- USB charging is exposed separately as `Get/SetSleepUSBCharge` and `Get/SetHibernationUSBCharge`. It most likely keeps a selected USB port powered so a phone/headset can charge while the laptop sleeps or hibernates. Full shutdown/S5 support is not established, so “charging while switched off” was too broad.
- `get/SetPostAnimate` most likely refers to the firmware-controlled RGB animation during POST/startup, not the on-screen GIGABYTE logo. This is supported by exact-generation GIGABYTE G5 owner reports describing a red/green/blue keyboard cycle at boot and a Control Center option to enable the RGB boot animation. The separate BIOS term for the on-screen splash is normally `Full Screen LOGO Show`. `PostAnimate` appears to be an enable/disable byte, not an image-upload or animation editor. Exact FB0F applicability and raw values remain unverified.
- Indicator-light methods include `get/SetPwrLightDisplay`, `get/SetHddLightDisplay`, `SetWiFiLED`, and `SetBluetoothLED`; these likely concern small power, drive-activity, Wi-Fi, and Bluetooth indicators, where physically present. `Get/SetLightBar` is a separate optional decorative chassis-light feature.
- None of these methods has a confirmed FB0F implementation or live applicability yet; do not expose them in the application based only on their presence in the shared MOF schema.

Presence in the shared MOF does not prove that a method applies to this exact model or is safe. Each feature needs the same DSDT/device-protocol verification and rollback design used for telemetry.

## Battery charge-limit capability

- FB0F maps `Get/SetChargePolicy` to method ID `0x64` and EC field `BCPS`; it maps `Get/SetChargeStop` to method ID `0x65` and EC field `BCPC`.
- Live state before any battery write: raw policy `0` (Standard/BIOS mode), stored stop byte `97`, Windows charge remaining `96%`.
- The signed current Gigabyte notebook module uses raw policy `0` for Standard and `4` for Custom. Its UI contains the exact pairs `0 + 100`, `4 + 80`, and `4 + 60`, writing policy first and threshold second.
- Two independent open-source implementations corroborate raw policies `0/4`, method IDs `0x64/0x65`, and a supported custom threshold range of 60–100%.
- Our control must use live firmware readback, not Gigabyte's registry-only UI state; it must preserve the exact original pair, verify every write, and restore that pair on failure.
- No battery setter has been invoked yet.
- Full evidence, signed-module hash, protocol table, safety gates, and rollback plan: `research/BATTERY-CHARGE-LIMIT.md`.
- The new elevated read-only inspection reconfirmed policy `0`, stored stop `97`, and 96% charge: `research/runs/battery-inspection-20260901-195109.md`.
- Shared-core charge control is now implemented but not executed. It allows only custom 60–100% or Standard `0 + 100`, verifies the exact setter signatures, serializes writes, saves the original pair, reads back the result, and automatically restores and verifies the original pair after any failure.
- A two-part command guard requires the typed target plus `--confirm-battery-write`. A no-confirmation 80% test was rejected before setter access: `research/runs/battery-change-20260901-195356.md`.
- At the user's explicit request, the first guarded live change set custom 80%: original `0 + 97`, verified `4 + 80`, exit code `0`. Report: `research/runs/battery-change-20260901-195616.md`.
- A separate elevated read-only process independently reconfirmed active custom policy `4` and stop threshold `80`. Report: `research/runs/battery-inspection-20260901-195627.md`.
- Current known firmware state: custom 80% enabled. Battery level at verification remained 96%; the firmware setting does not actively discharge the battery.

## Planned test ladder

### Lüfter, Leistung und GPU-Modi (Recherche 2026-09-03)

- Das aktuelle offizielle, gültig signierte Notebook-Modul aus GCC 26.08.03.01 wurde statisch analysiert. Es bestätigt sechs Lüftermodi: Game/Power, Eco/Quiet, Normal, Turbo/Max, Fixed und Dynamic; Dynamic verwendet 15 Temperatur-/Leistungs-Paare. Auf der älteren AORUS-5-Plattform kann der Lüfterrohwert bis 229 reichen und darf nicht ungeprüft als Prozent behandelt werden.
- Die vermeintlichen „Eco/Performance“-Funktionen bestehen aus vier unabhängigen Ebenen: Lüfterprofil, Windows-Power-Overlay, modellabhängige Gigabyte-CPU/GPU-Leistungsgrenzen und Grafikmodus. Die Anwendung muss sie getrennt darstellen und steuern.
- Gigabytes echter GPU-Eco-Ablauf prüft externe Monitore und NVIDIA-Prozesse, behandelt NVIDIA-Audio/Platform-Geräte und verwendet `SetNvPowerConfig(3)` zum Ausschalten sowie `(4)` zum Einschalten mit Geräte-Neusuche. Ein bloßes Deaktivieren der RTX im Geräte-Manager ist kein gleichwertiger Ersatz.
- Das interne Display wird aktuell von Intel Iris Xe betrieben; die RTX 3070 war dennoch in P5 bei ungefähr 16,8 W wach und trieb keine Anzeige. „iGPU-only“ bedeutet Intel-Grafik, nicht CPU-Rendering.
- Ein `SetPEG2orSG2`-Pfad für hybride/diskrete Display-Routen ist im gemeinsamen Gigabyte-Code vorhanden, aber ein MUX oder Advanced Optimus ist für das AORUS 5 SE4/FB0F noch nicht bestätigt. Die offizielle Modellspezifikation nennt nur Optimus.
- Empfohlene Reihenfolge: read-only Capability Report, sichere Hersteller-Lüfterprofile, Windows-Leistungsmodus, danach Fixed/Dynamic Fan; Gigabyte-Systemleistung und physisches GPU-Eco erst nach modellspezifischer Live-Bestätigung und vollständigem Rollback.
- Vollständige Funktionskarte, Sicherheitsregeln, Hashes und Quellen: `research/FAN-POWER-GPU-CONTROL.md`.
- Phase 1.1 ist ausgeführt: der neue erhöhte read-only Capability-Lauf bestätigte die vollständige Kurve bis Rohwert 229, etwa 1878/1994 RPM und Roh-Duty 66. `GetNvPowerConfig`, `GetPEG2orSG2` und `getAiPowerCtlCapability` werden auf FB0F trotz MOF-Präsenz als ungültiges Objekt abgewiesen; physisches GPU-Eco und MUX gelten deshalb vorerst als nicht unterstützt und werden nicht geschrieben. Bericht: `research/runs/thermal-power-inspection-20260903-134039.md`; Ablaufplan: `research/FAN-POWER-GPU-TEST-PLAN.md`.
- Phase 1.2 ist ebenfalls abgeschlossen. Drei 2-Sekunden-Proben zeigten eine plausible Kurvenreaktion: bei 59 °C Duty 93 und 2557/2698 RPM, später bei 51 °C Duty 84 und 2414/2545 RPM. CPU- und GPU-Duty waren stets identisch; die 15 Kurvenpunkte blieben zwischen den erhöhten Läufen bytegleich. Bericht: `research/runs/thermal-power-inspection-20260903-134200.md`.
- Lüfterphase 2.1/2.2: ein abgesicherter Controller und separater Normal-Rettungsstarter wurden gebaut. Der unbestätigte Test wurde vor Setterzugriff abgewiesen; der bestätigte Normaltest erhielt exakt `fixed=0, step=0, auto=0, thermal=0` und die komplette Kurve. Bericht: `research/runs/fan-normal-change-20260903-134611.md`.
- Lüfterphase 2.3: Quiet wurde vorübergehend bestätigt (`thermal=1`) und stoppte bei 51–52 °C beide Lüfter. Der erste GPU-RPM-Wert 7000 bei Duty 0 war ein unplausibler Umschaltwert; vier Folgeproben waren 0 RPM. Nach 12 Sekunden wurde Normal mit Duty 66 verifiziert wiederhergestellt. Bericht: `research/runs/fan-quiet-test-20260903-134826.md`.
- Lüfterphase 2.4: Gaming/Power setzte nur `auto=1`. Im Leerlauf bei 51/48 °C blieb Duty 66 mit etwa 1880/2000 RPM, also praktisch wie Normal; das Profil wählt eine Firmwarestrategie statt pauschal höherer RPM. Normal wurde verifiziert wiederhergestellt. Bericht: `research/runs/fan-gaming-test-20260903-135023.md`.
- Lüfterphase 2.5, erster Maximum-Lauf: `fixed=1`, `step=1`, Festwert/Duty 229 und schließlich 5208/5472 RPM wurden bestätigt. Eine vermeintlich kritische Rollback-Meldung war ein Verifierfehler: dynamischer GPU-Duty war fälschlich als persistenter Zustand verglichen worden. Der unabhängige Rettungsbefehl plus neue Inspektion bestätigte vollständig Normal, Festwert 57, Duty 66 und unveränderte Kurve; kein Hardwarefehler blieb zurück. Berichte: `research/runs/fan-maximum-test-20260903-135300.md`, `research/runs/fan-normal-change-20260903-135327.md`, `research/runs/thermal-power-inspection-20260903-135342.md`.
- Lüfterphase 2.5 abgeschlossen: der korrigierte Wiederholungslauf erreichte bei 229 bis zu 5220/5417 RPM und stellte danach Modus, gespeicherten Festwert 57 sowie den dynamischen Duty-Wert 66 erfolgreich wieder her. Damit sind Quiet, Normal, Gaming und Maximum samt Recovery bestätigt. Bericht: `research/runs/fan-maximum-test-20260903-135458.md`.
- Windows-Leistungsmodus Phase 3 abgeschlossen: Energieeffizienz, Ausbalanciert und Beste Leistung wurden über die Overlay-API gesetzt und jeweils exakt zurückgelesen; der ursprüngliche ausgeglichene AC-Zustand wurde wiederhergestellt. Kein Firmware-/EC-Befehl wurde aufgerufen. Bericht: `research/runs/windows-power-overlay-test-20260903-135645.md`.
- Fixed-Fan-Skala, obere Hälfte: Rohwert 160 ≈ 4000/4190 RPM, 194 ≈ 4600/4800 RPM, 229 ≈ 5220/5420 RPM. Beide Steuerwerte wurden jeweils exakt zurückgelesen, und Normal/Festwert 57/Duty 66 wurden danach vollständig wiederhergestellt. Bericht: `research/runs/fan-fixed-scale-test-20260903-135853.md`.
- Fixed-Fan-Skala vollständig: 57 ≈ 1640/1750 RPM, 68 ≈ 1925/2045, 91 ≈ 2510/2665, 114 ≈ 3040/3210, 137 ≈ 3520/3695; zusammen mit den hohen Punkten ist 57–229 monoton bestätigt. Der kurze Test beweist 57 nicht als sicheren Dauerwert unter Last; dafür bleibt ein Temperatur-Failsafe nötig. Bericht: `research/runs/fan-fixed-scale-test-20260903-140107.md`.
- Dynamic-Modus mit unveränderter Werkskurve: `step=1`, bei 48–49 °C stabil Rohwert 68 und etwa 1930/2045 RPM. Punkt `(0,57)` ist offenbar kein normaler Temperaturstützpunkt; knapp unter 50 °C gilt bereits `(50,68)`. Kurve und Normalzustand wurden erhalten. Bericht: `research/runs/fan-dynamic-test-20260903-140240.md`.
- Kurvenschreibpfad bestätigt: Punkt 1 wurde vorübergehend von `(50,68)` auf `(50,80)` erhöht, 14 andere Punkte blieben gleich, Dynamic verwendete Rohwert 80 bei etwa 2240/2350 RPM. Alle 15 Originalpunkte und Normal/Duty 66 wurden exakt wiederhergestellt. Bericht: `research/runs/fan-curve-write-test-20260903-140517.md`.
- Gigabyte-Systemleistung wird auf FB0F ausgelassen: der zentrale `getAiPowerCtlCapability` sowie mehrere Power-/Turbo-Getter werden als ungültig abgewiesen. Es werden keine fremden CPU-/GPU-Leistungswerte übernommen.
- GPU-Routing read-only: nur internes BOE-Panel aktiv, aber viele Programme halten die RTX wach. Gespeicherte `GpuPreference=1`-Einträge einiger Windows-Oberflächen verhindern nicht, dass deren bereits laufende Prozesse in NVIDIAs Liste erscheinen. App-Präferenz und physisches GPU-Aus sind getrennte Dinge. Bericht: `research/runs/gpu-routing-inspection-20260903-140600.md`.
- Physisches GPU-Eco und MUX werden auf FB0F nicht getestet oder implementiert: `GetNvPowerConfig`, `getAiPowerCtlCapability` und `GetPEG2orSG2` werden abgewiesen; `GetPEGorSG=66` ist kein plausibler boolescher Zustand. Kein entsprechender Setter wurde aufgerufen.
- Abschließender unabhängiger Read-only-Lauf nach allen Lüftertests: Normal vollständig aktiv, Festwert/FanAdjust 57, Duty 66, etwa 1890/2000 RPM und alle 15 Originalpunkte exakt wieder vorhanden. Release-Build weiterhin 0 Warnungen/0 Fehler. Bericht: `research/runs/thermal-power-inspection-20260903-140823.md`.

1. Completed: run the local metadata-only diagnostic and save its report.
2. Completed: inspect and hash the exact signed `acpimof.dll` from the matching GCC package.
3. Completed: create a reversible provider-registration procedure with backup and verification.
4. Completed: register only the signed MOF provider and restart.
5. Completed: re-run metadata-only diagnostics and verify expected classes/method signatures.
6. Completed: invoke only the DSDT-confirmed getter whitelist; validate GPU temperature independently and decode the RPM byte order.
7. Completed: add repeated read-only sampling and plausibility checks.
8. Completed for keyboard RGB reads: recover the exact official three-zone getter protocol and verify all three zones live.
9. Later: design and test automatic-mode rollback before any setter call.

## Unresolved questions

RGB software research: concrete lessons from SignalRGB support, OpenRGB issue reports, liquidctl and an official Gigabyte service advisory are mapped to implementation/test requirements in RGB-SOFTWARE-LESSONS.md. Implemented this step: tray-close behavior, per-user/session single-instance activation and hidden-dashboard telemetry suppression while retaining manual-fan checks. Build/tests pass; physical tray and cross-process lifecycle tests remain open.

Fan background-safety update: Core now contains a WPF-independent supervisor with 10-second manual leases, fresh-telemetry checks, normal restoration/retries and cancellation cleanup. Simulated tests pass. Not yet wired into the UI or an installed service; a separate process watchdog and real crash/hang tests remain required. See FAN-SUPERVISOR.md.

Battery UI update: explicit apply/readback controls are implemented in a separate BatteryViewModel and simulated tests cover successful/failed/overlapping operations. No battery writes performed during integration. See BATTERY-APP-INTEGRATION.md. Expanded background-service objective and crash-test gates are recorded in BACKGROUND-SERVICE-DESIGN.md; no service/autostart is installed and crash-safe firmware fallback is not yet proven.

RGB UI update: WPF now routes RGB changes through KeyboardLightingSession. On/off remembers current-session brightness and effect; brightness/color changes retain effect selection; Manual restores saved user colors. A simulated ViewModel integration test checks the path without hardware writes. Physical UI testing, continuous renderer updates and persistence remain open; details in RGB-SESSION-STATE.md.

RGB coordination update: a shared immutable intent model and serialized keyboard session are implemented and tested with a fake transport. Power/brightness/mode/manual-color transitions no longer need separate owners. WPF is not yet migrated; controlled renderer restarts remain an interim mechanism. See [RGB-SESSION-STATE.md](research/RGB-SESSION-STATE.md).

Power-monitor update: V2 is now a separate Core feature with a diagnostic command, explicit unavailable/error values and batched two-sample GPU counters. Two read-only AC runs completed; post-start sampling improved from 562–599 ms to 20–29 ms in these short runs. Eight calculation checks and the seven existing fan smoke tests pass. Battery operation, adapter names and broader validation remain open. See [POWER-MONITOR-V2.md](research/POWER-MONITOR-V2.md).

Recovery update: The full solution now builds again after recovering 43 missing diagnostic functions from the preserved compiled assembly, without replacing newer existing functions. See [DIAGNOSTICS-RECOVERY.md](research/DIAGNOSTICS-RECOVERY.md) for limitations and verification. The full product requirements, including unified RGB interaction rules, are tracked in [APP-ROADMAP.md](research/APP-ROADMAP.md). Earlier build-failure notes below are historical.

### App integration update — 2026-09-03

Simple fan profiles, fixed raw values and AC Windows power modes are now wired into the WPF app. Fixed has a conservative 65 °C software guard and normal-close restoration; it is not crash-safe. Seven simulated safety tests passed. App/Core Release build passes; the separate Diagnostics Program.cs currently fails with 159 compilation errors. Fan recovery now has a dedicated app startup path, independent of Diagnostics. Full implementation, limits and remaining live checks: [FAN-POWER-APP-INTEGRATION.md](research/FAN-POWER-APP-INTEGRATION.md).

- Confirm the RPM byte order over multiple samples and changing fan speeds; the first sample strongly supports a simple 16-bit byte swap.
- Determine the exact units/ranges for `FAN1`, `FDTY`, and `GDTY` over multiple samples.
- Optional: statically compare an older official signed GCC package with the current package to determine whether it once rendered three-zone animations in host software. Do not install or flash old packages for this analysis.
- Which exact firmware-identifying fields should form the write allowlist beyond model and BIOS?
