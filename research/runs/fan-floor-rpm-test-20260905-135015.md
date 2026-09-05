# AORUS fan-curve floor probe, running

- Created: 2026-09-05 13:49:37 -03:00
- Question: the EC stores values below raw 57 - does it also drive the fans at them?
- Method: lower only the points below 60 C, activate Dynamic, sample, restore.
- Everything from 60 C upwards keeps its original value, so heat still ramps the fans normally.
- Aborts at 80 C on either sensor.
- Explicit curve-write confirmation present: yes

- Start: CPU 45 °C, GPU 43 °C, CPU 1745 RPM / raw 57
- Points lowered to raw 0: 8 (every point below 60 °C)
- (30,0), (34,0), (37,0), (40,0), (44,0), (48,0), (52,0), (59,0), (64,73), (68,87), (72,101), (77,115), (82,126), (87,137), (90,229)

## Dynamic with the lowered curve

- Dynamic: fixed `0`, step `1`, auto `0`, thermal `0`, stored fixed speed `57`, current GPU duty `0`

- Sample 1: CPU 45 °C, GPU 43 °C, CPU 0 RPM / raw duty 0, GPU 0 RPM / raw duty 0
- Sample 2: CPU 46 °C, GPU 43 °C, CPU 0 RPM / raw duty 0, GPU 0 RPM / raw duty 0
- Sample 3: CPU 47 °C, GPU 43 °C, CPU 0 RPM / raw duty 0, GPU 0 RPM / raw duty 0
- Sample 4: CPU 47 °C, GPU 44 °C, CPU 0 RPM / raw duty 0, GPU 0 RPM / raw duty 0
- Sample 5: CPU 47 °C, GPU 44 °C, CPU 0 RPM / raw duty 0, GPU 0 RPM / raw duty 0
- Sample 6: CPU 48 °C, GPU 44 °C, CPU 0 RPM / raw duty 0, GPU 0 RPM / raw duty 0

## Restore

- All 15 original points restored and verified: yes
- Restored: fixed `0`, step `0`, auto `0`, thermal `0`, stored fixed speed `57`, current GPU duty `103`
