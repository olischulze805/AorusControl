# AORUS conservative fan-curve write test

- Created: 2026-09-03 14:04:46 -03:00
- Change: point 1 value 68 to 80; no temperature lowered
- Explicit curve-write confirmation present: yes
- Mandatory restore: all 15 original points plus original mode

## Curve readback

- Original point 1: (50, 68)
- Modified point 1: (50, 80)
- Other 14 points unchanged: yes

## Dynamic result with modified point

- Dynamic: fixed `0`, step `1`, auto `0`, thermal `0`, stored fixed speed `57`, current GPU duty `80`
- Sample 1: CPU 49 °C, GPU 47 °C, CPU 2250 RPM / raw 80, GPU 2346 RPM / raw 80
- Sample 2: CPU 50 °C, GPU 47 °C, CPU 2232 RPM / raw 80, GPU 2346 RPM / raw 80
- Sample 3: CPU 50 °C, GPU 47 °C, CPU 2241 RPM / raw 80, GPU 2364 RPM / raw 80

## Restore

- Verified original: fixed `0`, step `0`, auto `0`, thermal `0`, stored fixed speed `57`, current GPU duty `66`
- All 15 original points restored exactly: True
