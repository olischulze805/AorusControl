# AORUS keyboard RGB query

- Created: 2026-09-01 19:21:14 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Official Gigabyte query commands sent with `SET_FEATURE`: **yes (`0x80` firmware, `0x88` lighting)**
- State-changing Gigabyte command sent: **no (`0x08` is not implemented here)**
- Output report sent: **no**

## Keyboard firmware

- Raw response: `008013040000000000`
- Gigabyte-formatted version: `19.0.4`

## Global lighting state

- Raw response: `008800000000000000`
- Effect code: `0` (`0x00`)
- Speed: `0`
- Brightness raw: `0` (Gigabyte UI scale estimate: 0%)
- Color code: `0` (`0x00`)
- Direction code: `0` (`0x00`)

## Three RGB zones

### Zone 1

- Raw response: `0088013E0066320000`
- RGB: `(62, 0, 102)`
- Hex color: `#3E0066`
- Brightness raw: `50` (Gigabyte UI scale estimate: 100%)

### Zone 2

- Raw response: `0088023E0066320000`
- RGB: `(62, 0, 102)`
- Hex color: `#3E0066`
- Brightness raw: `50` (Gigabyte UI scale estimate: 100%)

### Zone 3

- Raw response: `0088033E0066320000`
- RGB: `(62, 0, 102)`
- Hex color: `#3E0066`
- Brightness raw: `50` (Gigabyte UI scale estimate: 100%)

## Interpretation boundary

- Byte meanings come from Gigabyte's signed `GBT_Keyboard 25.07.25.01` implementation for this exact USB identity.
- Effect, speed, color, and direction remain numeric until their enum mappings are independently documented.
