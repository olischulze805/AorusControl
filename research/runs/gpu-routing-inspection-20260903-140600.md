# AORUS GPU routing and preference inspection

- Created: 2026-09-03 14:06:00 -03:00
- Mode: read-only
- Registry, device, firmware and process changes: **none**

## Active displays

- Exactly one active monitor: `DISPLAY\BOE08B3\4&38f644e8&0&UID8388688_0`
- Manufacturer/product: `BOE / 08B3`
- No active external monitor was returned by `WmiMonitorID`.
- The internal panel is associated with Intel Iris Xe by the existing system inventory.

## Stored Windows per-app GPU preferences

Relevant examples from `HKCU\Software\Microsoft\DirectX\UserGpuPreferences`:

- `GCC.exe`: `GpuPreference=1` (minimum power / iGPU preference)
- `LogiAiPromptBuilder.exe`, `logioptionsplus_agent.exe`: `GpuPreference=1`
- `ShellHost.exe`, `StartMenuExperienceHost.exe`, `SearchHost.exe`: `GpuPreference=1`
- Chrome: `GpuPreference=0` (Windows decides)
- Netflix and `theHunterCotW_F.exe`: `GpuPreference=2` (high performance / dGPU preference)
- VLC has an explicit NVIDIA adapter ID plus a nonstandard preference value.
- No explicit entries were present for the currently running Codex/ChatGPT, Claude, AppControl, current WhatsApp package, UniGetUI or Edge WebView executables.

Official DXGI meanings are: 0 unspecified, 1 minimum power such as iGPU, 2 high performance such as dGPU.

## NVIDIA activity at inspection time

`nvidia-smi` still listed the following categories on the RTX:

- two `AppControl.exe` processes
- Windows UI: `CrossDeviceResume`, Start, Search, Lock and Text Input
- Edge WebView
- UniGetUI
- current WhatsApp package
- Codex/ChatGPT
- two Claude processes

Some Windows UI processes appeared on NVIDIA despite a stored minimum-power preference. Possible explanations include an already-running process that has not restarted since the preference was written, indirect desktop composition, a driver decision/override, or limitations of the NVIDIA process list. The registry entry alone is therefore not proof of actual iGPU execution.

## Conclusions

- The physical topology is Optimus/hybrid: Intel drives the internal panel, while many programs keep the RTX awake for rendering or composition.
- A per-app Windows preference can guide the adapter chosen at the next app launch, but it is not a physical RTX power switch and must be verified after restarting the target program.
- The application can safely display active NVIDIA processes and stored per-app preferences and can later offer per-app iGPU/high-performance selection.
- It should not promise that setting every visible app to iGPU will power the RTX off; services, composition, displays or driver choices may still keep it active.
- A live test on Codex/Claude would require restarting the applications that contain this active work and was intentionally not performed.
- Physical GPU-Eco remains excluded because FB0F rejects `GetNvPowerConfig` and `getAiPowerCtlCapability`; MUX remains excluded because `GetPEG2orSG2` is rejected.

## Sources

- Microsoft `DXGI_GPU_PREFERENCE`: <https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_6/ne-dxgi1_6-dxgi_gpu_preference>
- NVIDIA Optimus/GPU Activity: <https://www.nvidia.com/content/Control-Panel-Help/vLatest/en-us/mergedProjects/nvcpl/Using_Optimus_Hybrid.htm>

