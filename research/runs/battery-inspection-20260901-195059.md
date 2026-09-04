# AORUS battery charge-limit inspection

- Created: 2026-09-01 19:50:59 -03:00
- Mode: read-only
- Firmware/EC write methods invoked: **no**

## Compatibility gate

- Model: `AORUS 5 SE`
- BIOS: `FB0F`
- Administrator: yes
- Result: exact model/BIOS match

## Windows battery state

- Name: `Aorus 15`
- BatteryStatus: `2`
- EstimatedChargeRemaining: `96%`
- DesignVoltage: `16692 mV`

## Firmware charge state

- `GetChargePolicy`: `0` — Standard/BIOS mode; stored stop byte is not an active custom limit
- `GetChargeStop`: `97`
- Effective interpretation: Standard/BIOS mode; stored stop byte is not an active custom limit
