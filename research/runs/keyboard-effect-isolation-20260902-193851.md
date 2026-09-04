# AORUS global effect selection isolation

- Created: 2026-09-02 19:36:55 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88`, global effect `0x08` selector 0
- Picture-matrix commands `0x12` / `0x92` used: **no**
- Report ID `0x5A` (ITE flash channel) touched: **no**
- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**
- Baseline before every step: all three zones written to `#FFFFFF` at brightness `50` and verified
- Purpose: separate the blackout cause — effect selection `51` versus the failed picture-matrix output reports

## Captured original zone state

- Zone 1: `#0000FF`, brightness `50`
- Zone 2: `#FF02FF`, brightness `50`
- Zone 3: `#FF0006`, brightness `50`

## Steps

### 1. Effect `51` (Custom 1)

- White baseline verified: **yes**
- Request: `00080033053200018C`
- Global readback: `008800000000000000`
- Zone 1 readback after the effect: `#FFFFFF`, brightness `50`
- Owner observation: Ja alles dunkel

### 2. Effect `1` (Static)

- White baseline verified: **yes**
- Request: `0008000105320001BE`
- Global readback: `008800000000000000`
- Zone 1 readback after the effect: `#FFFFFF`, brightness `50`
- Owner observation: Es wurde einmal von links nach rechts einmal weiss aber ist gleich wieder aus gegangen, also flashte nur einmal über

### 3. Effect `52` (Custom 2)

- White baseline verified: **yes**
- Request: `00080034053200018B`
- Global readback: `008800000000000000`
- Zone 1 readback after the effect: `#FFFFFF`, brightness `50`
- Owner observation: Wieder so wie vorher, einmal kurz weiss und dann aus

### 4. Effect `2` (Breathing)

- White baseline verified: **yes**
- Request: `0008000205320001BD`
- Global readback: `008800000000000000`
- Zone 1 readback after the effect: `#FFFFFF`, brightness `50`
- Owner observation: Ja wieder das mit dem weissen flashen

### 5. Effect `8` (Neon)

- White baseline verified: **yes**
- Request: `0008000805320001B7`
- Global readback: `008800000000000000`
- Zone 1 readback after the effect: `#FFFFFF`, brightness `50`
- Owner observation: und hier wieder

## Restoration

- Zone 1: `#0000FF`, brightness `50`, exact match: **yes**
- Zone 2: `#FF02FF`, brightness `50`, exact match: **yes**
- Zone 3: `#FF0006`, brightness `50`, exact match: **yes**
