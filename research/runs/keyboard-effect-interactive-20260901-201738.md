# AORUS interactive visible RGB-effect test

- Created: 2026-09-01 20:15:13 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Tested list: Gigabyte's ten compact ITE UI effects
- Shared parameters: raw speed `5`, brightness `50`, direction `1`
- Advancement: user-controlled; no fixed timeout
- Restore policy: capture all zones; restore and verify in `finally`

## Effect observations

### 1. `Static` (ID `1`)

- Request: `0008000105320201BC`
- Global readback: `008800000000000000`
- User observation: Macht es grade war aber von vorher schon so eingestellt

### 2. `Breathing` (ID `2`)

- Request: `0008000205320201BB`
- Global readback: `008800000000000000`
- User observation: Macht grade nichts

### 3. `Wave` (ID `3`)

- Request: `0008000305320801B4`
- Global readback: `008800000000000000`
- User observation: Nichts

### 4. `Fade-on-keypress` (ID `4`)

- Request: `0008000405320201B9`
- Global readback: `008800000000000000`
- User observation: Nichts

### 5. `Marquee` (ID `5`)

- Request: `0008000505320801B2`
- Global readback: `008800000000000000`
- User observation: Nichts

### 6. `Ripple` (ID `6`)

- Request: `0008000605320201B7`
- Global readback: `008800000000000000`
- User observation: Nichts

### 7. `Neon` (ID `8`)

- Request: `0008000805320801AF`
- Global readback: `008800000000000000`
- User observation: Nichts

### 8. `Raindrop` (ID `10`)

- Request: `0008000A05320801AD`
- Global readback: `008800000000000000`
- User observation: Nichts

### 9. `Hedge` (ID `12`)

- Request: `0008000C05320801AB`
- Global readback: `008800000000000000`
- User observation: Nichts

### 10. `Rotate` (ID `13`)

- Request: `0008000D05320801AA`
- Global readback: `008800000000000000`
- User observation: Nichts. Ich glaube dadurch das ich fn strg die helligkeit verändert habe setzt es nicht mehr die dinge die wirklich gesetzt wurden. Ich glaube selbst im richtigen programm hatte ich das problem schon

## Restoration

- Zone 1: `#00FF00`, brightness `50`, verified **yes**
- Zone 2: `#00FF00`, brightness `50`, verified **yes**
- Zone 3: `#00FF00`, brightness `50`, verified **yes**

## Final interpretation

- Direct three-zone static RGB remains the only visibly verified lighting mode.
- Breathing, Wave, Fade-on-keypress, Marquee, Ripple, Neon, Raindrop, Hedge, and Rotate produced no visible change according to the user.
- The global getter returned all zeros after every request, consistent with the controller ignoring global-effect state.
- Gigabyte's exact `ZoneRgb` profile loader reads the stored effect/speed/direction fields but does not apply them; it sends only three `SetZoneColors` calls. This matches the live result and indicates the animation enum belongs to the wider shared ITE implementation rather than the functional `7A41` feature set.
- The user's Fn-brightness-state hypothesis cannot be completely excluded, but it is not required to explain the result and is less consistent with the exact ZoneRgb software path.
- Application decision: expose independent static RGB for zones 1–3 and lighting on/off. Do not expose firmware effects, effect speed, effect palette, or direction for exact device `1044:7A41` / firmware `19.0.4`.
