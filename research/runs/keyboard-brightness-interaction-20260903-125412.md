# AORUS brightness interaction matrix

- Created: 2026-09-03 12:53:16 -03:00
- Exact target device: `VID 1044 / PID 7A41`
- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88`, plus read-only input listening
- Hardware step read live from `MI_02 / COL_04`, report ID `0x04`, byte 2
- Global effect command, picture matrix, WMI, and EC: **not used**
- Privacy gate: collections declaring keyboard usage page `0x0007` are never opened
- Zone brightness values per step: `0`, `24`, `32`, `50`
- Purpose: determine whether the zone brightness byte behaves differently depending on the active hardware step

- `MI_02 / COL_04` opened for live step reading: **yes**

## Captured original zone state

- Zone 1: `#0000FF`, brightness `50`
- Zone 2: `#FF02FF`, brightness `50`
- Zone 3: `#FF0006`, brightness `50`

## Matrix

| Hardware step | Zone byte | Stored | Owner observation |
|---|---|---|---|
| `0 (gemessen)` | `0` | `0` | Aus |
| `0 (gemessen)` | `24` | `24` | Niedrig 1 in weiss |
| `0 (gemessen)` | `32` | `32` | 2 mittel |
| `0 (gemessen)` | `50` | `50` | 3 hell |
| `24 (gemessen)` | `0` | `0` | Aus |
| `24 (gemessen)` | `24` | `24` | 1 |
| `24 (gemessen)` | `32` | `32` | 2 |
| `24 (gemessen)` | `50` | `50` | 3 |
| `32 (gemessen)` | `0` | `0` | 0 |
| `32 (gemessen)` | `24` | `24` | 1 |
| `32 (gemessen)` | `32` | `32` | 2 |
| `32 (gemessen)` | `50` | `50` | 3 |
| `50 (gemessen)` | `0` | `0` | 0 |
| `50 (gemessen)` | `24` | `24` | 1 |
| `50 (gemessen)` | `32` | `32` | 2 |
| `50 (gemessen)` | `50` | `50` | 3 |

## Restoration

- Zone 1: `#0000FF`, brightness `50`, exact match: **yes**
- Zone 2: `#FF02FF`, brightness `50`, exact match: **yes**
- Zone 3: `#FF0006`, brightness `50`, exact match: **yes**
