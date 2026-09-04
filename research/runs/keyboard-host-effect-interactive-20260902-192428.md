# AORUS interactive host-rendered RGB-effect test

- Created: 2026-09-02 19:24:27 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88` only
- Global effect command `0x08` selector 0 used: **no**
- Picture-matrix commands `0x12` / `0x92` used: **no**
- Report ID `0x5A` (ITE flash channel) touched: **no**
- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**
- Frame interval per zone write: `5 ms`
- Animation is rendered in the RGB values; the brightness byte stays at `50`, because raw brightness is a proven off/on gate on this firmware.
- Advancement: owner-controlled; no fixed timeout
- Restore policy: capture all zones; restore and verify in `finally`

## Captured original zone state

- Zone 1: `#0000FF`, brightness `50`
- Zone 2: `#FF02FF`, brightness `50`
- Zone 3: `#FF0006`, brightness `50`

## Effects and owner observations

### 1. `Static`

- Expected appearance: Sollte ruhig und unveraenderlich in einer Farbe leuchten.
- Ran for `0,0` s, `1` three-zone frames, `65,2` frames/s
- Owner observation: (keine Beschreibung eingegeben)

## Restoration

- Zone 1: `#0000FF`, brightness `50`, verified **yes**
- Zone 2: `#FF02FF`, brightness `50`, verified **yes**
- Zone 3: `#FF0006`, brightness `50`, verified **yes**
