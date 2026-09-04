# AORUS picture-matrix write test

- Created: 2026-09-02 19:31:50 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature / 65-byte Input and Output report`
- Official commands used: getter `0x92`, setter `0x12`, effect selector `0x08` selector 0, zone getter `0x88`
- Target custom slot: `0` (effect enum `51`, Custom 1)
- Written memory: LED profile storage only. Firmware code, key matrix, macros, BIOS, EC, and battery: **not written**
- Report ID `0x5A` (ITE flash channel) touched: **no**
- Rollback: the slot is read and saved first, then rewritten and verified in `finally`; the three zone colours are restored the same way

- Refused before any device access: `--confirm-picture-matrix-write` was not supplied.
