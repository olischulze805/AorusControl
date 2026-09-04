# AORUS zone brightness byte sweep

- Created: 2026-09-03 11:50:57 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Commands used: zone setter `0x08` selector 1-3 and zone getter `0x88` only
- Global effect command, picture matrix, WMI, and EC: **not used**
- Colour held constant at `#FFFFFF` on all three zones; only the brightness byte varies
- Levels tested: `0`, `25`, `50`, `51`, `60`, `75`, `100`, `150`, `200`, `255`
- Context: the earlier boundary test covered only 0, 1, 25, 49, and 50 and concluded the byte is an off/on gate

## Captured original zone state

- Zone 1: `#0000FF`, brightness `50`
- Zone 2: `#FF02FF`, brightness `50`
- Zone 3: `#FF0006`, brightness `50`

## Levels

| Requested | Stored readback | Owner observation |
|---|---|---|
| `0` | `0` | Es ist jetzt ausgeganegn |
| `25` | `25` | Esbleibt aus |
| `50` | `50` | Jetzt ist es angeganegn und leuchtet weiss |
| `51` | `51` | Nichts passiert leuchtet noch wie bei 50 |
| `60` | `60` | Leichtet wie bei 50 |
| `75` | `75` | Leuchtet wie bei 50 |
| `100` | `100` | Leuchtet wie bei 50 |
| `150` | `150` | Leuchtet wie bei 50 |
| `200` | `200` | Leuchtet wie bei 50 |
| `255` | `255` | leuchtet wie bei 50 |

## Restoration

- Zone 1: `#0000FF`, brightness `50`, exact match: **yes**
- Zone 2: `#FF02FF`, brightness `50`, exact match: **yes**
- Zone 3: `#FF0006`, brightness `50`, exact match: **yes**
