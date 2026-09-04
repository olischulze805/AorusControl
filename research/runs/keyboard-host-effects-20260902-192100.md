# AORUS host-rendered keyboard effect test

- Created: 2026-09-02 19:20:34 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88` only
- Global effect command `0x08` selector 0 used: **no**
- Picture-matrix commands `0x12` / `0x92` used: **no**
- Report ID `0x5A` (ITE flash channel) touched: **no**
- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**
- Seconds per effect: `8`
- Zone brightness byte held at `50` throughout; animation is rendered in the RGB values, because raw brightness is a proven off/on gate on this firmware.

## Captured original zone state

- Zone 1: `#0000FF`, brightness `50`
- Zone 2: `#FF02FF`, brightness `50`
- Zone 3: `#FF0006`, brightness `50`

## Minimum reliable write interval

| Interval | Verified writes | Result |
|---|---|---|
| `65 ms` | `6 / 6` | reliable |
| `40 ms` | `6 / 6` | reliable |
| `25 ms` | `6 / 6` | reliable |
| `15 ms` | `6 / 6` | reliable |
| `10 ms` | `6 / 6` | reliable |
| `5 ms` | `6 / 6` | reliable |

- Fastest interval verified on every attempt: `5 ms` per zone write.
- Resulting full three-zone frame rate: about `66` frames/s.

## Rendered effects

- **Breathing**: all three zones share one hue whose RGB values follow a sine ramp. Frames rendered: `64` in `8,0` s, achieved `8,0` frames/s.
- **Colour cycle**: one hue rotating through the full spectrum on all three zones. Frames rendered: `171` in `8,0` s, achieved `21,3` frames/s.
- **Wave**: a bright zone travelling left to right over a dim base. Frames rendered: `171` in `8,0` s, achieved `21,3` frames/s.

## Restore of the original zone state

- Zone 1: `#0000FF`, brightness `50`, exact match: **yes**
- Zone 2: `#FF02FF`, brightness `50`, exact match: **yes**
- Zone 3: `#FF0006`, brightness `50`, exact match: **yes**
