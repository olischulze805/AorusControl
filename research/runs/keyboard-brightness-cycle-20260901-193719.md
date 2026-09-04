# AORUS keyboard software-brightness cycle

- Created: 2026-09-01 19:36:56 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Steps: raw `0`, `17`, `33`, `50` (approximately 0%, 34%, 66%, 100%)
- Hold time: five seconds per step
- Final requested brightness: raw `50` / 100%

- Colors preserved: zone 1 `#00FF00`, zone 2 `#00FF00`, zone 3 `#00FF00`

- Step 0%: Z1=0, Z2=0, Z3=0; verified **yes**
- Step 34%: Z1=17, Z2=17, Z3=17; verified **yes**
- Step 66%: Z1=33, Z2=33, Z3=33; verified **yes**
- Step 100%: Z1=50, Z2=50, Z3=50; verified **yes**
- Final 100% readback verified: **yes**
