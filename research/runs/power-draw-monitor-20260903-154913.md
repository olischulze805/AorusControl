# AORUS system power draw correlation

- Created: 2026-09-03 15:48:57 -03:00
- Mode: read-only. Passive WMI performance counters only
- `nvidia-smi` invoked: **no**, because a single call wakes the discrete GPU and costs about 22 W on this laptop
- Duration: `15` s, interval `3000` ms
- Adapter treated as the integrated GPU: LUID `0x0001149C`
- Discharge rate is the **total** system draw in milliwatts, not the draw of any single component
- CPU percentages are normalised across `20` logical processors, so the total ranges from 0 to 100
- **The monitor influences its own measurement.** Each sample enumerates every process through WMI, which shows up as `WmiPrvSE` load. Interactive sessions and the tool itself are part of the reported draw, so compare samples within one run rather than against an untouched idle machine.

## Samples

| Time | Draw | CPU | iGPU | dGPU | Top processes by CPU |
|---|---|---|---|---|---|
| `15:48:58` | `22,6` W | `0,0` % | `0,0` % | `0,0` % | - |
| `15:49:02` | `22,6` W | `0,0` % | `0,0` % | `0,0` % | - |
| `15:49:06` | `22,6` W | `0,0` % | `0,0` % | `0,0` % | - |
| `15:49:10` | `35,7` W | `0,0` % | `0,0` % | `0,0` % | - |

## Summary

- Samples: `4`, of which `4` carried a usable discharge rate
- Total draw: minimum `22,6` W, average `25,9` W, maximum `35,7` W
- Spread between quietest and busiest sample: `13,0` W
- Busiest sample `15:49:10` at `35,7` W with CPU `0,0` %, iGPU `0,0` %, dGPU `0,0` %: -
- **The discrete GPU showed no activity in any sample.** The observed spread therefore comes from CPU and application load, not from the RTX.

## Interpretation boundary

- The discharge rate covers the whole machine: panel, CPU, RAM, storage, radios and every running application.
- A GPU engine percentage is a utilisation figure, not a power figure. Zero utilisation does not prove the adapter is powered down, only that nothing is rendering on it.
- Per-process CPU values are not normalised; a single process can exceed 100 when it spans several cores.
- The integrated adapter is identified by LUID `0x0001149C`, inferred from the desktop compositor running on the internal panel.
