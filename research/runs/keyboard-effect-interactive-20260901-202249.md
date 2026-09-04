# AORUS interactive visible RGB-effect test

- Created: 2026-09-01 20:22:31 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Tested list: Gigabyte's ten compact ITE UI effects
- Shared parameters: raw speed `5`, brightness `50`, direction `1`
- Advancement: user-controlled; no fixed timeout
- Restore policy: capture all zones; restore and verify in `finally`

## Effect observations

### 1. `Static` (ID `1`)

- Request: `0008000105320201BC`
- Global readback: `008800000000000000`
- User observation: No note entered.

### 2. `Breathing` (ID `2`)

- Request: `0008000205320201BB`
- Global readback: `008800000000000000`
- User observation: No note entered.

### 3. `Wave` (ID `3`)

- Request: `0008000305320801B4`
- Global readback: `008800000000000000`
- User observation: No note entered.

### 4. `Fade-on-keypress` (ID `4`)

- Request: `0008000405320201B9`
- Global readback: `008800000000000000`
- User observation: No note entered.

### 5. `Marquee` (ID `5`)

- Request: `0008000505320801B2`
- Global readback: `008800000000000000`
- User observation: No note entered.

### 6. `Ripple` (ID `6`)

- Request: `0008000605320201B7`
- Global readback: `008800000000000000`
- User observation: No note entered.

### 7. `Neon` (ID `8`)

- Request: `0008000805320801AF`
- Global readback: `008800000000000000`
- User observation: No note entered.

### 8. `Raindrop` (ID `10`)

- Request: `0008000A05320801AD`
- Global readback: `008800000000000000`
- User observation: No note entered.

### 9. `Hedge` (ID `12`)

- Request: `0008000C05320801AB`
- Global readback: `008800000000000000`
- User observation: No note entered.

### 10. `Rotate` (ID `13`)

- Request: `0008000D05320801AA`
- Global readback: `008800000000000000`
- User observation: No note entered.

## Restoration

- Zone 1: `#00FF00`, brightness `50`, verified **yes**
- Zone 2: `#00FF00`, brightness `50`, verified **yes**
- Zone 3: `#00FF00`, brightness `50`, verified **yes**
