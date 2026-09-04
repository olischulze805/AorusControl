# AORUS interactive visible RGB-effect test

- Created: 2026-09-01 20:13:52 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Tested list: Gigabyte's ten compact ITE UI effects
- Shared parameters: raw speed `5`, brightness `50`, direction `1`
- Advancement: user-controlled; no fixed timeout
- Restore policy: capture all zones; restore and verify in `finally`

## Effect observations

### 1. `Static` (ID `1`)

- Request: `0008000105320201BC`
- Global readback: `008800000000000000`
- User observation: Test stopped by user before an observation was recorded.

## Restoration

- Zone 1: `#00FF00`, brightness `50`, verified **yes**
- Zone 2: `#00FF00`, brightness `50`, verified **yes**
- Zone 3: `#00FF00`, brightness `50`, verified **yes**
