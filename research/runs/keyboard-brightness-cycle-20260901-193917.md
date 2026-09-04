# AORUS keyboard software-brightness cycle

- Created: 2026-09-01 19:38:49 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Boundary steps: raw `0`, `1`, `25`, `49`, `50` (approximately 0%, 2%, 50%, 98%, 100%)
- Hold time: five seconds per step
- Final requested brightness: raw `50` / 100%

- Colors preserved: zone 1 `#00FF00`, zone 2 `#00FF00`, zone 3 `#00FF00`

- Step 0%: Z1=0, Z2=0, Z3=0; verified **yes**
- Step 2%: Z1=1, Z2=1, Z3=1; verified **yes**
- Step 50%: Z1=25, Z2=25, Z3=25; verified **yes**
- Step 98%: Z1=49, Z2=49, Z3=49; verified **yes**
- Step 100%: Z1=50, Z2=50, Z3=50; verified **yes**
- Final 100% readback verified: **yes**
