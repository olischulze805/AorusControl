# AORUS thermal, power and GPU capability inspection

- Created: 2026-09-03 13:39:53 -03:00
- Mode: read-only
- Setter class opened: **no**
- Firmware/EC write methods invoked: **no**

## Compatibility gate

- Model: `AORUS 5 SE`
- BIOS: `FB0F`
- Administrator: no
- Result: exact model/BIOS match

## Windows power state

- Active power scheme: exit `0`
```text
GUID des Energieschemas: 381b4222-f694-41f0-9685-ff5bb260df2e  (Ausbalanciert)
```
- `ActiveOverlayAcPowerScheme`: `00000000-0000-0000-0000-000000000000`
- `ActiveOverlayDcPowerScheme`: `961cc777-2547-4f9d-8174-7d86181b8a7a`

## Windows display and GPU inventory

- Name=`NVIDIA GeForce RTX 3070 Laptop GPU`; Status=`OK`; DriverVersion=`32.0.16.1656`; AdapterCompatibility=`NVIDIA`; PNPDeviceID=`PCI\VEN_10DE&DEV_249D&SUBSYS_15461458&REV_A1\4&1161B092&0&0008`
- Name=`Intel(R) Iris(R) Xe Graphics`; Status=`OK`; DriverVersion=`31.0.101.3616`; AdapterCompatibility=`Intel Corporation`; PNPDeviceID=`PCI\VEN_8086&DEV_46A6&SUBSYS_15461458&REV_0C\3&11583659&1&10`
- Name=`NVIDIA GeForce RTX 3070 Laptop GPU`; Status=`OK`; PNPDeviceID=`PCI\VEN_10DE&DEV_249D&SUBSYS_15461458&REV_A1\4&1161B092&0&0008`
- Name=`NVIDIA High Definition Audio`; Status=`OK`; PNPDeviceID=`HDAUDIO\FUNC_01&VEN_10DE&DEV_009E&SUBSYS_14581546&REV_1001\5&1F18D2A&1&0001`
- Name=`NVIDIA Virtual Audio Device (Wave Extensible) (WDM)`; Status=`OK`; PNPDeviceID=`ROOT\UNNAMED_DEVICE\0000`
- Name=`NVIDIA Broadcast`; Status=`OK`; PNPDeviceID=`ROOT\UNNAMED_DEVICE\0003`
- Name=`NVIDIA Platform Controllers and Framework`; Status=`OK`; PNPDeviceID=`ACPI\NVDA0820\NPCF`
- Active=`True`; InstanceName=`DISPLAY\BOE08B3\4&38f644e8&0&UID8388688_0`

## NVIDIA runtime (read-only)

- GPU state: exit `0`
```text
NVIDIA GeForce RTX 3070 Laptop GPU, P8, 16.69 W, Disabled, [Requested functionality has been deprecated], 47, [N/A]
```
- GPU processes: exit `0`
```text
9236, C:\Program Files\AppControl\ui\AppControl.exe, [N/A]
12256, C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\CrossDeviceResume.exe, [N/A]
14508, C:\Windows\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\StartMenuExperienceHost.exe, [N/A]
14488, C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\SearchHost.exe, [N/A]
17092, C:\Windows\SystemApps\Microsoft.LockApp_cw5n1h2txyewy\LockApp.exe, [N/A]
18452, C:\Program Files (x86)\Microsoft\EdgeWebView\Application\152.0.4191.53\msedgewebview2.exe, [N/A]
19820, C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe, [N/A]
12708, C:\Program Files\AppControl\ui\AppControl.exe, [N/A]
21428, C:\Program Files\UniGetUI\UniGetUI.exe, [N/A]
15452, C:\Program Files\WindowsApps\5319275A.WhatsAppDesktop_2.2632.100.0_x64__cv1g1gvanyjgm\WhatsApp.Root.exe, [N/A]
22244, C:\Program Files\WindowsApps\OpenAI.Codex_26.901.1978.0_x64__2p2nqsd0c76g0\app\ChatGPT.exe, [N/A]
5360, C:\Program Files\WindowsApps\Claude_1.44121.4.0_x64__pzs8sxrjxfjjc\app\claude.exe, [N/A]
8996, C:\Program Files\WindowsApps\Claude_1.44121.4.0_x64__pzs8sxrjxfjjc\app\claude.exe, [N/A]
23268, C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.24.11911.0_x64__8wekyb3d8bbwe\WindowsTerminal.exe, [N/A]
```

## Firmware getter state

- Firmware read refused: administrator rights are required for the ACPI-WMI invocation.
