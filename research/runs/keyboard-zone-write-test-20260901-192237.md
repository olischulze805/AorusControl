# AORUS guarded RGB zone write verification

- Created: 2026-09-01 19:22:36 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Scope: zone 1 only; temporary color; original state restored in `finally`
- Key matrix, macros, effects, firmware, BIOS, and EC modified: **no**

- Original: `#3E0066`, brightness `50`
- Temporary test: `#66003E`, brightness `50`
- Readback during test: `#66003E`, brightness `50`
- Temporary write verified: **yes**
- Final readback: `#3E0066`, brightness `50`
- Original state restored and verified: **yes**
