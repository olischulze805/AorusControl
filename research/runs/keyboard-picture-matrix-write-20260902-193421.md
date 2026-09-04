# AORUS picture-matrix write test

- Created: 2026-09-02 19:33:55 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature / 65-byte Input and Output report`
- Official commands used: getter `0x92`, setter `0x12`, effect selector `0x08` selector 0, zone getter `0x88`
- Target custom slot: `0` (effect enum `51`, Custom 1)
- Written memory: LED profile storage only. Firmware code, key matrix, macros, BIOS, EC, and battery: **not written**
- Report ID `0x5A` (ITE flash channel) touched: **no**
- Rollback: the slot is read and saved first, then rewritten and verified in `finally`; the three zone colours are restored the same way

## Saved original state

- Slot 0 matrix read: `512` bytes, `0` non-zero
- Slot 0 SHA-256: `076A27C79E5ACE2A3D47F9DD2E83E4FF6EA8872B3C2218F66C92B89B55F36560`
- Zone 1: `#0000FF`, brightness `50`
- Zone 2: `#FF02FF`, brightness `50`
- Zone 3: `#FF0006`, brightness `50`

## Written matrix

- Pattern: all `128` four-byte slots set to `00 FF 00 00` (pure red)
- Candidate SHA-256: `345EDFE9A87D73986BAED930C81D53DF5743A58FF99652DE42418C1D2BE296B4`
- Readback SHA-256: `076A27C79E5ACE2A3D47F9DD2E83E4FF6EA8872B3C2218F66C92B89B55F36560`
- Readback non-zero bytes: `0 / 512`
- Exact readback match: **no**

## Custom effect activation

- Request: `00080033053200018C`
- Global readback: `008800000000000000`
- Decoded: effect `0`, speed `0`, brightness `0`, colour `0`, direction `0`

## Owner observation

- Es ist nur alles ausgegangen und nichts leuchtet

## Rollback

- Slot 0 matrix rewritten, exact match: **yes**
- Zone 1: `#0000FF`, brightness `50`, exact match: **yes**
- Zone 2: `#FF02FF`, brightness `50`, exact match: **yes**
- Zone 3: `#FF0006`, brightness `50`, exact match: **yes**
