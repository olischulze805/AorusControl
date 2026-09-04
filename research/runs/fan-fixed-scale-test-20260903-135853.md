# AORUS fixed fan raw-scale test

- Created: 2026-09-03 13:58:02 -03:00
- Targets: 160, 194, 229 (high values only)
- Explicit write confirmation present: yes
- Mandatory exact restore of the original persistent state

## Raw 160

- Verified fixed: fixed `1`, step `1`, auto `0`, thermal `0`, stored fixed speed `160`, current GPU duty `160`
- Sample 1: CPU 49 °C, GPU 47 °C, CPU 4000 RPM / raw 160, GPU 4186 RPM / raw 160
- Sample 2: CPU 53 °C, GPU 47 °C, CPU 4000 RPM / raw 160, GPU 4170 RPM / raw 160
- Sample 3: CPU 49 °C, GPU 46 °C, CPU 3963 RPM / raw 160, GPU 4203 RPM / raw 160

## Raw 194

- Verified fixed: fixed `1`, step `1`, auto `0`, thermal `0`, stored fixed speed `194`, current GPU duty `194`
- Sample 1: CPU 48 °C, GPU 45 °C, CPU 4627 RPM / raw 194, GPU 4781 RPM / raw 194
- Sample 2: CPU 47 °C, GPU 44 °C, CPU 4627 RPM / raw 194, GPU 4813 RPM / raw 194
- Sample 3: CPU 47 °C, GPU 44 °C, CPU 4558 RPM / raw 194, GPU 4791 RPM / raw 194

## Raw 229

- Verified fixed: fixed `1`, step `1`, auto `0`, thermal `0`, stored fixed speed `229`, current GPU duty `229`
- Sample 1: CPU 46 °C, GPU 43 °C, CPU 5158 RPM / raw 229, GPU 5417 RPM / raw 229
- Sample 2: CPU 45 °C, GPU 43 °C, CPU 5259 RPM / raw 229, GPU 5445 RPM / raw 229
- Sample 3: CPU 45 °C, GPU 42 °C, CPU 5220 RPM / raw 229, GPU 5417 RPM / raw 229

## Restore

- Verified original: fixed `0`, step `0`, auto `0`, thermal `0`, stored fixed speed `57`, current GPU duty `66`
- Result: exact persistent original restored
