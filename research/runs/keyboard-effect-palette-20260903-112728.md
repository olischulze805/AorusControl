# AORUS effect and palette test

- Created: 2026-09-03 11:25:27 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88`, global effect `0x08` selector 0
- Picture-matrix commands `0x12` / `0x92` used: **no**
- Report ID `0x5A` (ITE flash channel) touched: **no**
- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**
- Baseline before every step: all three zones written to `#FFFFFF` at brightness `50` and verified
- Correction under test: the previous isolation run sent palette byte `0` = `FusionLightColor.Black` in every step, which makes its blackout uninformative about the effect engine

## Captured original zone state

- Zone 1: `#0000FF`, brightness `50`
- Zone 2: `#FF02FF`, brightness `50`
- Zone 3: `#FF0006`, brightness `50`

## Steps

### 1. Effect `1` (Static) with palette `1` (Red)

- White baseline verified: **yes**
- Request: `0008000105320101BD`
- Global readback: `008800000000000000`
- Owner observation: Alles weiss leuchten

### 2. Effect `1` (Static) with palette `4` (Blue)

- White baseline verified: **yes**
- Request: `0008000105320401BA`
- Global readback: `008800000000000000`
- Owner observation: Alles bleibt weiss

### 3. Effect `2` (Breathing) with palette `1` (Red)

- White baseline verified: **yes**
- Request: `0008000205320101BC`
- Global readback: `008800000000000000`
- Owner observation: Alles bleibt weiss

### 4. Effect `3` (Wave) with palette `8` (Random)

- White baseline verified: **yes**
- Request: `0008000305320801B4`
- Global readback: `008800000000000000`
- Owner observation: Alles bleibt weiss

### 5. Effect `8` (Neon) with palette `8` (Random)

- White baseline verified: **yes**
- Request: `0008000805320801AF`
- Global readback: `008800000000000000`
- Owner observation: Alles bleibt weiss

## Restoration

- Zone 1: `#0000FF`, brightness `50`, exact match: **yes**
- Zone 2: `#FF02FF`, brightness `50`, exact match: **yes**
- Zone 3: `#FF0006`, brightness `50`, exact match: **yes**
