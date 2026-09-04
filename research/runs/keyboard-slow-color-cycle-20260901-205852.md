# AORUS slow full-color-cycle request

- Created: 2026-09-01 20:58:51 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Combined request: Neon/Cycle effect `8`, slowest raw speed `9`, brightness `50`, Random palette `8`, direction `1`
- Persistence: effect request deliberately left active for visual observation; static zone values were not overwritten
- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**

## Static zone state before request

- Zone 1: `#00FF00`, brightness `50`
- Zone 2: `#00FF00`, brightness `50`
- Zone 3: `#00FF00`, brightness `50`

## Request and immediate global readback

- Request: `0008000809320801AB`
- Readback: `008800000000000000`
- Decoded: effect `0`, speed `0`, brightness `0`, color `0`, direction `0`
