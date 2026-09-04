# AORUS thermal, power and GPU capability inspection

- Created: 2026-09-03 14:31:43 -03:00
- Mode: read-only
- Setter class opened: **no**
- Firmware/EC write methods invoked: **no**

## Compatibility gate

- Model: `AORUS 5 SE`
- BIOS: `FB0F`
- Administrator: yes
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
NVIDIA GeForce RTX 3070 Laptop GPU, P8, 14.06 W, Disabled, [Requested functionality has been deprecated], 47, [N/A]
```
- GPU processes: exit `0`
```text
9236, C:\Program Files\AppControl\ui\AppControl.exe, [N/A]
12256, C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\CrossDeviceResume.exe, [N/A]
14508, C:\Windows\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\StartMenuExperienceHost.exe, [N/A]
14488, C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\SearchHost.exe, [N/A]
1904, C:\Windows\System32\dwm.exe, [N/A]
17092, C:\Windows\SystemApps\Microsoft.LockApp_cw5n1h2txyewy\LockApp.exe, [N/A]
18452, C:\Program Files (x86)\Microsoft\EdgeWebView\Application\152.0.4191.53\msedgewebview2.exe, [N/A]
19820, C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe, [N/A]
20888, C:\Program Files\GamingIntelligence\OSDPopupHandler.exe, [N/A]
10684, C:\Program Files\GamingIntelligence\GamingIntelligence.exe, [N/A]
12708, C:\Program Files\AppControl\ui\AppControl.exe, [N/A]
21428, C:\Program Files\UniGetUI\UniGetUI.exe, [N/A]
15452, C:\Program Files\WindowsApps\5319275A.WhatsAppDesktop_2.2632.100.0_x64__cv1g1gvanyjgm\WhatsApp.Root.exe, [N/A]
22244, C:\Program Files\WindowsApps\OpenAI.Codex_26.901.1978.0_x64__2p2nqsd0c76g0\app\ChatGPT.exe, [N/A]
5360, C:\Program Files\WindowsApps\Claude_1.44121.4.0_x64__pzs8sxrjxfjjc\app\claude.exe, [N/A]
8996, C:\Program Files\WindowsApps\Claude_1.44121.4.0_x64__pzs8sxrjxfjjc\app\claude.exe, [N/A]
7676, C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.24.11911.0_x64__8wekyb3d8bbwe\WindowsTerminal.exe, [N/A]
```

## Firmware getter state

- Live instance: `\\AORUS5\root\WMI:GB_WMIACPI_Get.InstanceName="ACPI\\PNP0C14\\DCK_0"`
- `getCpuTemp` (in [], out [Data:UInt16]): Data=50
- `getGpuTemp1` (in [], out [Data:UInt16]): Data=46
- `getGpuTemp2` (in [], out [Data:UInt16]): Data=0
- `getRpm1` (in [], out [Data:UInt16]): Data=18440
- `getRpm2` (in [], out [Data:UInt16]): Data=51720
- `GetCPUFanDuty` (in [], out [Data:UInt8]): Data=75
- `GetGPUFanDuty` (in [], out [Data:UInt8]): Data=75
- `GetFixedFanStatus` (in [], out [Data:UInt16]): Data=0
- `GetFixedFanSpeed` (in [], out [Data:UInt16]): Data=194
- `GetFanAdjustStatus` (in [], out [Data:UInt8]): Data=194
- `GetAutoFanStatus` (in [], out [Data:UInt8]): Data=0
- `GetStepFanStatus` (in [], out [Data:UInt16]): Data=0
- `GetFanSpeed` (in [], out [Data:UInt8]): Data=0
- `GetNvPowerConfig` (in [], out [Data:UInt8]): error (Ungültiges Objekt )
- `GetNvThermalTarget` (in [], out [Data:UInt8]): Data=0
- `GetPEGorSG` (in [], out [Data:UInt8]): Data=75
- `GetPEG2orSG2` (in [], out [Data:UInt8]): error (Ungültiges Objekt )
- `getAiPowerCtlCapability` (in [], out [Data:UInt8]): error (Ungültiges Objekt )
- `GetDynamicBoostStatus` (in [], out [Data:UInt8]): Data=0
- `GetEcValueBoostStatus` (in [], out [Data:UInt8]): error (Ungültiges Objekt )
- `GetSmartCool` (in [], out [Data:UInt16]): error (Ungültiges Objekt )
- `GetSmartTurbo` (in [], out [Data:UInt8]): error (Ungültiges Objekt )
- `GetTurboMode` (in [], out [Data:UInt8]): error (Ungültiges Objekt )
- `GetWhisperMode` (in [], out [Data:UInt8]): Data=0
- `GetTppStatus`: not exposed by installed MOF
- `GetSuperQuiet`: not exposed by installed MOF
- `GetDeepFan` (in [], out [Speed0:UInt8, Speed1:UInt8, Speed2:UInt8, Speed3:UInt8, Speed4:UInt8, Temperature0:UInt8, Temperature1:UInt8, Temperature2:UInt8, Temperature3:UInt8, Temperature4:UInt8]): error (Ungültiges Objekt )
- `GetThermalData` (in [], out [Thermal1:UInt8, Thermal2:UInt8, Thermal3:UInt8]): error (Ungültiges Objekt )
- `GetFanHealth` (in [], out [Data:UInt8]): error (Ungültiges Objekt )
- `GetFanPWMStatus` (in [], out [Data:UInt8]): Data=194
- `QueryThermalSensor` (in [], out [Data:UInt16]): error (Ungültiges Objekt )
- `GetBatteryTemperature` (in [], out [Data:UInt16]): error (Ungültiges Objekt )
- `GetFan3Duty` (in [], out [Data:UInt8]): error (Ungültiges Objekt )
- `GetFan4Duty` (in [], out [Data:UInt8]): error (Ungültiges Objekt )
- `getRpm3` (in [], out [Data:UInt16]): error (Ungültiges Objekt )
- `getRpm4` (in [], out [Data:UInt16]): error (Ungültiges Objekt )

## Repeated thermal samples

RPM values are decoded with the byte order already established by the existing telemetry reader.

- Sample 1 at 14:31:44: CPU 50 °C, GPU 46 °C, CPU fan raw 23304 / 2139 RPM, GPU fan raw 47624 / 2234 RPM, CPU duty raw 75, GPU duty raw 75, fixed-speed raw 194, fan-adjust raw 194, fan-pwm raw 194
- Sample 2 at 14:31:46: CPU 49 °C, GPU 46 °C, CPU fan raw 17416 / 2116 RPM, GPU fan raw 48904 / 2239 RPM, CPU duty raw 75, GPU duty raw 75, fixed-speed raw 194, fan-adjust raw 194, fan-pwm raw 194
- Sample 3 at 14:31:48: CPU 49 °C, GPU 46 °C, CPU fan raw 23559 / 1884 RPM, GPU fan raw 51719 / 1994 RPM, CPU duty raw 66, GPU duty raw 66, fixed-speed raw 194, fan-adjust raw 194, fan-pwm raw 194

## Stored 15-point fan curve

- Signature: `in [Index:UInt8], out [Temperture:UInt8, Value:UInt8]`
- Point 0: Temperture=0, Value=57
- Point 1: Temperture=50, Value=68
- Point 2: Temperture=53, Value=80
- Point 3: Temperture=56, Value=91
- Point 4: Temperture=59, Value=103
- Point 5: Temperture=62, Value=114
- Point 6: Temperture=65, Value=125
- Point 7: Temperture=68, Value=137
- Point 8: Temperture=71, Value=148
- Point 9: Temperture=74, Value=160
- Point 10: Temperture=77, Value=171
- Point 11: Temperture=80, Value=183
- Point 12: Temperture=83, Value=194
- Point 13: Temperture=86, Value=206
- Point 14: Temperture=89, Value=229
