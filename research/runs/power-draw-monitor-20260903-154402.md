# AORUS system power draw correlation

- Created: 2026-09-03 15:43:40 -03:00
- Mode: read-only. Passive WMI performance counters only
- `nvidia-smi` invoked: **no**, because a single call wakes the discrete GPU and costs about 22 W on this laptop
- Duration: `20` s, interval `4000` ms
- Adapter treated as the integrated GPU: LUID `0x0001149C`
- Discharge rate is the **total** system draw in milliwatts, not the draw of any single component

## Samples

| Time | Draw | CPU | iGPU | dGPU | Top processes by CPU |
|---|---|---|---|---|---|
| `15:43:43` | `38,0` W | `1950,0` % | `13,0` % | `0,0` % | claude#2 54 %, WmiPrvSE#2 24 %, dwm 19 % |
| `15:43:48` | `38,0` W | `1932,0` % | `13,0` % | `0,0` % | claude#2 57 %, claude#5 24 %, dwm 24 % |
| `15:43:53` | `38,0` W | `1945,0` % | `20,0` % | `0,0` % | WmiPrvSE#2 24 %, claude#5 19 %, claude#2 14 % |
| `15:43:58` | `38,0` W | `2000,0` % | `17,0` % | `0,0` % | chrome#15 38 %, dwm 23 %, WmiPrvSE#2 19 % |

## Summary

- Samples: `4`, of which `4` carried a usable discharge rate
- Total draw: minimum `38,0` W, average `38,0` W, maximum `38,0` W
- Spread between quietest and busiest sample: `0,0` W
- Busiest sample `15:43:43` at `38,0` W with CPU `1950,0` %, iGPU `13,0` %, dGPU `0,0` %: claude#2 54 %, WmiPrvSE#2 24 %, dwm 19 %
- **The discrete GPU showed no activity in any sample.** The observed spread therefore comes from CPU and application load, not from the RTX.

## Interpretation boundary

- The discharge rate covers the whole machine: panel, CPU, RAM, storage, radios and every running application.
- A GPU engine percentage is a utilisation figure, not a power figure. Zero utilisation does not prove the adapter is powered down, only that nothing is rendering on it.
- The integrated adapter is identified by LUID `0x0001149C`, inferred from the desktop compositor running on the internal panel.
