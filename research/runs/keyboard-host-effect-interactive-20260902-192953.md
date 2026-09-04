# AORUS interactive host-rendered RGB-effect test

- Created: 2026-09-02 19:27:52 -03:00
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
- Ran for `11,5` s, `246` three-zone frames, `21,4` frames/s
- Owner observation: Ja in grün

### 2. `Breathing`

- Expected appearance: Sollte langsam heller und dunkler werden, ohne Farbwechsel.
- Ran for `9,2` s, `198` three-zone frames, `21,4` frames/s
- Owner observation: Ja es funktioniert

### 3. `Pulse`

- Expected appearance: Sollte deutlich schneller und harter blinken als Breathing.
- Ran for `6,0` s, `129` three-zone frames, `21,4` frames/s
- Owner observation: Ja funktioniert

### 4. `Colour cycle`

- Expected appearance: Alle drei Zonen sollten gemeinsam durch das ganze Farbspektrum wandern.
- Ran for `6,3` s, `135` three-zone frames, `21,3` frames/s
- Owner observation: Ja funktioniert

### 5. `Rainbow marquee`

- Expected appearance: Die drei Zonen sollten unterschiedliche Farben zeigen, die nach rechts wandern.
- Ran for `29,4` s, `413` three-zone frames, `14,1` frames/s
- Owner observation: Jaes funktioniert

### 6. `Wave`

- Expected appearance: Eine helle Zone sollte weich von links nach rechts laufen, Rest gedimmt.
- Ran for `6,4` s, `137` three-zone frames, `21,4` frames/s
- Owner observation: Ja es funktioniert

### 7. `Marquee`

- Expected appearance: Wie Wave, aber schneller und mit hartem Wechsel.
- Ran for `9,0` s, `192` three-zone frames, `21,4` frames/s
- Owner observation: Ja funktionirt

### 8. `Rotate`

- Expected appearance: Die helle Zone sollte hin und zurueck pendeln, nicht nur in eine Richtung.
- Ran for `14,6` s, `312` three-zone frames, `21,3` frames/s
- Owner observation: Ja funktioniert

### 9. `Raindrop`

- Expected appearance: Einzelne Zonen sollten unregelmaessig kurz aufblitzen.
- Ran for `15,1` s, `324` three-zone frames, `21,4` frames/s
- Owner observation: Ja funktioniert

### 10. `Fade sweep`

- Expected appearance: Die Zonen sollten nacheinander aufleuchten und langsam ausklingen.
- Ran for `12,5` s, `265` three-zone frames, `21,2` frames/s
- Owner observation: Ja funktioniert

## Restoration

- Zone 1: `#0000FF`, brightness `50`, verified **yes**
- Zone 2: `#FF02FF`, brightness `50`, verified **yes**
- Zone 3: `#FF0006`, brightness `50`, verified **yes**
