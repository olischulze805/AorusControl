# AORUS keyboard backlight level test

- Created: 2026-09-03 11:47:02 -03:00
- Interface: `GB_WMIACPI_Get.GetKeyBoardBackLight` and `GB_WMIACPI_Set.SetKeyBoardBackLight`, WMI method ID `0xF6`
- FB0F DSDT target: EC field `KBLL` at offset `0xD7`
- Gates: exact model and BIOS, administrator rights, and `--confirm-backlight-write`
- Battery, fan, charge, key matrix, macros, HID, BIOS, and firmware: **not touched**
- Rollback: the original value is read first and rewritten plus verified in `finally`

## Gates

- Detected device: `GIGABYTE` / `AORUS 5 SE` / `FB0F`
- Exact approved device: **yes**
- Administrator: **yes**

## Original value

- `GetKeyBoardBackLight` before any write: `0`

## Levels

| Written | Readback | Owner observation |
|---|---|---|
| `0` | `0` | Ist noch alles an |
| `1` | `1` | Alles bleibt wwie vorher |
| `2` | `2` | Nichtspassiert |
| `3` | `3` | Alles bleibt |
| `4` | `4` | Alles bleibt |

## Rollback

- Original value `0` rewritten, readback `0`, exact match: **yes**
