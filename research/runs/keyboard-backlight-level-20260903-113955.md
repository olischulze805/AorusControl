# AORUS keyboard backlight level test

- Created: 2026-09-03 11:39:55 -03:00
- Interface: `GB_WMIACPI_Get.GetKeyBoardBackLight` and `GB_WMIACPI_Set.SetKeyBoardBackLight`, WMI method ID `0xF6`
- FB0F DSDT target: EC field `KBLL` at offset `0xD7`
- Gates: exact model and BIOS, administrator rights, and `--confirm-backlight-write`
- Battery, fan, charge, key matrix, macros, HID, BIOS, and firmware: **not touched**
- Rollback: the original value is read first and rewritten plus verified in `finally`

- Refused before any firmware access: `--confirm-backlight-write` was not supplied.
