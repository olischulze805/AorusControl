# AORUS keyboard read-only feature report

- Created: 2026-09-01 19:07:35 -03:00
- Target: `VID 1044 / PID 7A41`
- Operation: USB HID `GET_REPORT (Feature)` only
- Output report sent: **no**
- Feature report set: **no**

## `MI_02 / COL_07`

- Report ID: `0x5A`
- Length: 17 bytes including report ID byte
- Raw bytes: `00FFFFFFFFFFFFFFFFFFFFFFFFFFFFFF00`
- Payload bytes: `FFFFFFFFFFFFFFFFFFFFFFFFFFFFFF00`

## `MI_03`

- Report ID: `0x00`
- Length: 9 bytes including report ID byte
- Raw bytes: `000000000000000000`
- Payload bytes: `0000000000000000`

## Interpretation

- Returned bytes are retained as uninterpreted state until their meaning is confirmed by independent observations.
- Reading a feature report does not reveal whether each byte is a color, mode, brightness, version, or capability flag.
