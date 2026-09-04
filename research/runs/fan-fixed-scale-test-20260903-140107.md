# AORUS fixed fan raw-scale test

- Created: 2026-09-03 14:00:00 -03:00
- Targets: 57, 68, 91, 114, 137 (low range)
- Explicit write confirmation present: yes
- Mandatory exact restore of the original persistent state

## Raw 57

- Verified fixed: fixed `1`, step `1`, auto `0`, thermal `0`, stored fixed speed `57`, current GPU duty `57`
- Sample 1: CPU 48 °C, GPU 45 °C, CPU 1634 RPM / raw 57, GPU 1755 RPM / raw 57
- Sample 2: CPU 48 °C, GPU 45 °C, CPU 1648 RPM / raw 57, GPU 1748 RPM / raw 57

## Raw 68

- Verified fixed: fixed `1`, step `1`, auto `0`, thermal `0`, stored fixed speed `68`, current GPU duty `68`
- Sample 1: CPU 48 °C, GPU 45 °C, CPU 1925 RPM / raw 68, GPU 2049 RPM / raw 68
- Sample 2: CPU 48 °C, GPU 45 °C, CPU 1923 RPM / raw 68, GPU 2045 RPM / raw 68

## Raw 91

- Verified fixed: fixed `1`, step `1`, auto `0`, thermal `0`, stored fixed speed `91`, current GPU duty `91`
- Sample 1: CPU 48 °C, GPU 45 °C, CPU 2495 RPM / raw 91, GPU 2675 RPM / raw 91
- Sample 2: CPU 48 °C, GPU 45 °C, CPU 2521 RPM / raw 91, GPU 2655 RPM / raw 91

## Raw 114

- Verified fixed: fixed `1`, step `1`, auto `0`, thermal `0`, stored fixed speed `114`, current GPU duty `114`
- Sample 1: CPU 47 °C, GPU 44 °C, CPU 3032 RPM / raw 114, GPU 3208 RPM / raw 114
- Sample 2: CPU 48 °C, GPU 44 °C, CPU 3049 RPM / raw 114, GPU 3213 RPM / raw 114

## Raw 137

- Verified fixed: fixed `1`, step `1`, auto `0`, thermal `0`, stored fixed speed `137`, current GPU duty `137`
- Sample 1: CPU 47 °C, GPU 44 °C, CPU 3506 RPM / raw 137, GPU 3692 RPM / raw 137
- Sample 2: CPU 47 °C, GPU 43 °C, CPU 3540 RPM / raw 137, GPU 3698 RPM / raw 137

## Restore

- Verified original: fixed `0`, step `0`, auto `0`, thermal `0`, stored fixed speed `57`, current GPU duty `66`
- Result: exact persistent original restored
