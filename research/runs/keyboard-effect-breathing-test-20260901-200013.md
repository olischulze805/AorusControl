# AORUS guarded Breathing-effect test

- Created: 2026-09-01 20:00:01 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Temporary request: Breathing `2`, raw speed `5` (official UI 50/100), brightness `50`, Green `2`, direction `1`
- Visible hold: 10 seconds
- Restore policy: capture all three zones first; restore and verify them in `finally`
- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**

## Captured zone state

- Zone 1: `#00FF00`, brightness `50`
- Zone 2: `#00FF00`, brightness `50`
- Zone 3: `#00FF00`, brightness `50`

## Effect request and readback

- Request: `0008000205320201BB`
- Global readback: `008800000000000000`
- Decoded readback: effect `0`, speed `0`, brightness `0`, color `0`, direction `0`
- The effect was then left visible for ten seconds for direct observation.

## Restoration

- Zone 1: `#00FF00`, brightness `50`, verified **yes**
- Zone 2: `#00FF00`, brightness `50`, verified **yes**
- Zone 3: `#00FF00`, brightness `50`, verified **yes**
