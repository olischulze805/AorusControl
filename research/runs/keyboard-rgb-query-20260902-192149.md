# AORUS keyboard RGB query

- Created: 2026-09-02 19:21:48 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Official Gigabyte query commands sent with `SET_FEATURE`: **yes (`0x80` firmware, `0x88` lighting)**
- State-changing Gigabyte command sent in this mode: **no**
- Output report sent: **no**

## Keyboard firmware

- Raw response: `008013040000000000`
- Gigabyte-formatted version: `19.0.4`

## Global lighting state

- Raw response: `008800000000000000`
- Effect code: `0` (`0x00`)
- Speed: `0`
- Nominal brightness byte: `0` (Gigabyte UI scale label: 0%; not proven as visible PWM on this firmware)
- Color code: `0` (`0x00`)
- Direction code: `0` (`0x00`)

## Three RGB zones

### Zone 1

- Raw response: `0088010000FF320000`
- RGB: `(0, 0, 255)`
- Hex color: `#0000FF`
- Nominal brightness byte: `50` (`50`=on and tested values below `50`=off on firmware 19.0.4)

### Zone 2

- Raw response: `008802FF02FF320000`
- RGB: `(255, 2, 255)`
- Hex color: `#FF02FF`
- Nominal brightness byte: `50` (`50`=on and tested values below `50`=off on firmware 19.0.4)

### Zone 3

- Raw response: `008803FF0006320000`
- RGB: `(255, 0, 6)`
- Hex color: `#FF0006`
- Nominal brightness byte: `50` (`50`=on and tested values below `50`=off on firmware 19.0.4)

## Interpretation boundary

- Byte meanings come from Gigabyte's signed `GBT_Keyboard 25.07.25.01` implementation for this exact USB identity.
- Official enum mappings are documented in `research/KEYBOARD-CAPABILITIES.md`; the all-zero global response is outside the defined effect enum and is therefore reported without guessing.
