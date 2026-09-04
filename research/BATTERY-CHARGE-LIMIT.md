# Battery charge-limit research — AORUS 5 SE / FB0F

## Scope

This file records the evidence needed to implement a narrowly guarded battery charge-limit control. It does not describe BIOS flashing and does not authorize arbitrary ACPI/WMI writes.

## Current live state

- Device gate: `AORUS 5 SE`
- BIOS gate: `FB0F`
- Battery reported by Windows: `Aorus 15`
- Charge remaining during the read: `96%`
- `GetChargePolicy`: raw `0`
- `GetChargeStop`: raw `97`
- Source report: `research/runs/diagnostic-20260901-194523.md`
- No battery setter has been invoked during this investigation.

`Policy = 0` is Standard/BIOS mode. In that mode the stored stop byte is not the active custom limit, so the raw value `97` must not be presented as an enabled 97% limit. It is still important rollback state and must be preserved.

## Confirmed firmware interface

The local FB0F DSDT maps these Gigabyte ACPI-WMI methods directly to embedded-controller fields:

| Purpose | WMI method | Method ID | EC field | Valid app values |
| --- | --- | ---: | --- | --- |
| Read policy | `GetChargePolicy` | `0x64` / 100 | `BCPS` | raw `0` or `4` |
| Read stop threshold | `GetChargeStop` | `0x65` / 101 | `BCPC` | `60`–`100` |
| Write policy | `SetChargePolicy` | `0x64` / 100 | `BCPS` | raw `0` or `4` only |
| Write stop threshold | `SetChargeStop` | `0x65` / 101 | `BCPC` | `60`–`100` only |

Live WMI metadata confirms that both setters accept one `UInt8` input named `Data` and expose a `UInt8` output named `DataOut`.

Policy interpretation:

- raw `0`: Standard/BIOS-controlled charging
- raw `4`: Custom charge limit enabled

The Linux driver represents the user-facing mode as 0/1 and shifts it left by two bits before calling method `0x64`; therefore firmware raw values are 0/4.

## Exact behavior of Gigabyte's signed notebook module

Static inspection was performed on the exact `ucNotebook.dll` extracted from the current Gigabyte Control Center package:

- SHA-256: `24DE360044E03E1D52592350606D5BD644B5AF2ABE43920E7BA52123C68C65C9`
- Authenticode: valid
- Signer: `GIGA-BYTE TECHNOLOGY CO., LTD.`

The official module's `ChargeMode_Change` handler exposes three presets:

| UI selection | First write | Second write | Meaning |
| --- | --- | --- | --- |
| 0 | `SetChargePolicy(0)` | `SetChargeStop(100)` | Standard/full |
| 1 | `SetChargePolicy(4)` | `SetChargeStop(80)` | Custom 80% |
| 2 | `SetChargePolicy(4)` | `SetChargeStop(60)` | Custom 60% |

Important implementation findings:

- Gigabyte writes policy first and the threshold second.
- `SetChargePolicy` sends the selected raw byte as `Data`; an internal UI value of 2 is mapped to raw 0.
- `SetChargeStop` sends the percentage byte directly as `Data`.
- The official UI initializes its selected mode from a registry value named `Charge Mode`, rather than using firmware readback as its source of truth. Our implementation should instead read both firmware values before and after every change.

## Open-source corroboration

- `Ixmoon/Gigabyte-Aorus-Battery-Manager` defines `Standard = 0` and `Custom = 4`, and calls the same two WMI setters with a byte named `Data`.
- `Ixmoon/Gigabyte-Fan-Battery-Center` documents a custom threshold range of 60–100%.
- `tangalbert919/gigabyte-laptop-wmi` maps charge mode to method `0x64`, charge limit to `0x65`, validates 60–100%, and notes that the limit only applies in custom mode.

Primary-source URLs:

- <https://github.com/Ixmoon/Gigabyte-Aorus-Battery-Manager/blob/master/Form1.cs>
- <https://github.com/Ixmoon/Gigabyte-Fan-Battery-Center>
- <https://github.com/tangalbert919/gigabyte-laptop-wmi/blob/master/aorus-laptop.c>

## Required safety design

Before any write, the diagnostic/control layer must:

1. Require an elevated administrator process.
2. Match exact model `AORUS 5 SE` and BIOS `FB0F`.
3. Require the expected `GB_WMIACPI_Get` and `GB_WMIACPI_Set` classes and exact method signatures.
4. Read and save the original policy and stop byte. The current known rollback pair is `0 + 97`, but it must always be read fresh.
5. Refuse before writing if the original pair is outside known-safe policy 0/4 and threshold 60–100, because a verified rollback would not be guaranteed.
6. Accept only a typed charge limit in the inclusive range 60–100; never expose an arbitrary WMI method or raw byte interface.
7. For a custom limit, write raw policy `4` first, then the percentage, matching Gigabyte's order.
8. Read back both fields immediately. Success requires exact `4 + requested limit`.
9. If either call or verification fails, restore the freshly captured original pair and verify the restoration.
10. Serialize the operation so two hardware writes cannot overlap.
11. Write a durable Markdown report containing original state, requested state, outputs, verification, and any rollback result.

Returning to Standard mode should use Gigabyte's official pair `policy 0 + stop 100`. Error rollback is different: it must restore the exact pre-operation pair, currently observed as `0 + 97`.

## Behavioral verification limitation

WMI readback can verify the firmware configuration immediately. At the present 96% charge, selecting 80% while connected to AC should stop further charging, but it will not actively discharge the battery to 80%. Full behavioral confirmation therefore requires observing the battery later after its level has fallen below and crossed the configured threshold.

## Implemented foundation

- `--inspect-battery` is a typed, read-only diagnostic with exact model/BIOS and administrator gates.
- Elevated live report: `research/runs/battery-inspection-20260901-195109.md`.
- A preceding non-elevated run stopped before firmware access as designed: `research/runs/battery-inspection-20260901-195038.md`.
- The shared core now contains `GigabyteWmiBatteryChargeController`, which serializes operations, accepts only 60–100%, restricts policy bytes to 0/4, verifies setter signatures, reads the original pair, follows Gigabyte's write order, verifies readback, and rolls back to the exact original pair on any failure.
- The diagnostic write command requires both a typed action and the separate token `--confirm-battery-write`. A dry guard test requesting 80% without the token was refused before opening the setter: `research/runs/battery-change-20260901-195356.md`.
- Release build after implementation: zero warnings and zero errors.

## First live change — 80%

- At the user's explicit request, the guarded controller set a custom 80% limit.
- Freshly captured original firmware pair: `0 + 97`.
- Write order: `SetChargePolicy(4)`, then `SetChargeStop(80)`.
- Immediate controller readback: exact `4 + 80`.
- Change command exit code: `0`.
- Change report: `research/runs/battery-change-20260901-195616.md`.
- A separate elevated read-only process then independently returned policy `4`, stop `80`, and interpreted it as an active custom 80% limit.
- Independent verification report: `research/runs/battery-inspection-20260901-195627.md`.
- Windows still reported 96% charge. The controller is not expected to force-discharge to 80%; it should prevent further charging until normal use lowers the level sufficiently.
- Rollback was not needed because both the immediate and independent readbacks matched exactly.
