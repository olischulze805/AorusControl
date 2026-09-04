# Archived GIGABYTE Control Center packages

Stand: 2026-09-02

## GCC 23.03.02.01

The older `GCC_23.03.02.01.zip` package was located and downloaded for **static analysis only**. Nothing from it was installed or executed.

### Why this version matters

- KBench listed it on 2023-03-31 as a Gigabyte notebook utility: <https://kbench.com/software/?q=node/85064>
- Mirrored release notes explicitly mention `Add: Zone RGB keyboard gen 1 & gen 2 detect functions`.
- The same notes mention fixes for keyboard firmware naming, keyboard backlight brightness, switching Sync All from Neon to Off, and other RGB behavior: <https://drivers.softpedia.com/get/MOTHERBOARD/GIGABYTE/Gigabyte-H810M-H-Control-Center-Utility-23-03-02-01.shtml>
- This makes it a strong candidate for comparison with the current module after the owner's confirmation that Breathing, Flash/Pulse, and slow full-color transitions previously worked on the same AORUS 5 SE4.

### Provenance and integrity

- Original historic Gigabyte URL: `https://download.gigabyte.com/FileList/Utility/GCC_23.03.02.01.zip`
- The original Gigabyte URL now returns HTTP 404.
- Archived download source used: Softpedia mirror of the named Gigabyte package.
- Local ZIP: `third-party/vendor/gcc-archives/GCC_23.03.02.01.zip`
- ZIP size: `89,161,552` bytes.
- ZIP SHA-256: `6052E52DAC41FEF1FCFB2A08EF19243659CE8A8ADF328FCCD951C038362FCEBF`
- Contained outer installer: `GIGABYTE Control Center_23.03.02.01.exe`
- Outer installer size: `89,179,096` bytes.
- Outer installer SHA-256: `99671D0F584A8A0B87EB7E183A89CEB6F4378F38A35E586A0907A6C148755885`
- Authenticode status: **Valid**.
- Signer: `GIGA-BYTE TECHNOLOGY CO., LTD.`
- Certificate expiry: 2024-11-23; validity is preserved by its timestamped signature.

The signed outer 7-Zip SFX contains a second signed Gigabyte GCC installer and a signed RGB sync component:

| Component | SHA-256 | Authenticode |
|---|---|---|
| `GIGABYTE Control Center_23.03.02.01.exe` | `62AA3F09EB88393332F7268B5B839DB7E9ADCE10B94695A5FEE2A2A6E84D9439` | Valid, GIGA-BYTE |
| `GBT_RGB_Sync_Control_23.03.02.01.exe` | `1C87D6DAD58B54F932B136F3A5C28E5893632CC41B5AE385701643E3F9A28D13` | Valid, GIGA-BYTE |

Some individual managed libraries inside the signed installer are not separately signed. Their provenance is established by containment in the valid signed installer; they must still not be executed during analysis.

### Static contents of interest

The installer was extracted without running it. Relevant files include:

- `GHidApi.dll`
- `GbtCloudMatrix.exe`
- `Lib/COMMDLL/RGBFI.dll`
- `Lib/COMMDLL/RgbCommon.dll`
- `Lib/COMMDLL/UIEffect.dll`
- `sp.xml`, containing effect configuration sections including Breathing
- the signed `GBT_RGB_Sync_Control_23.03.02.01.exe`

Static strings in `GbtCloudMatrix.exe` contain the `ZoneRgb`, `YEKeyboard`, `Ite`, `SetLightEffect`, `GetLightEffect`, `SetLightEffectAorusSynch`, `UpdateSpeed`, `UpdateBrightness`, `Breathing`, `Pulse`, `Cycling`, `Neon`, `Flashonkeypress`, `Fadeonkeypress`, `Wave`, `Ripple`, `Raindrop`, `Rotate` and other effect symbols. This is not yet proof of the exact packet path, but it is a stronger lead than the current `ZoneRgb` profile loader that writes only three static colors.

### Next analysis steps

These steps were completed on 2026-09-02. The old updater revealed Gigabyte's official module server and the exact signed historical keyboard module `GBT_Keyboard_23.03.10.01` was still downloadable from it. Static decompilation proves that it explicitly recognizes `1044:7A41`, presents the old Pulse/Cycle effect UI, and sends the same nine-byte global HID packet already reconstructed by our diagnostic. The later official firmware image identifies itself as `Gigabyte Fusion_8298:1.9.0.4`, exactly matching the live `19.0.4` controller.

Full hashes, source locations, packet comparison, profile/live-path distinction, firmware timeline, and evidence grading: `research/OLD-KEYBOARD-MODULE-COMPARISON.md`.

Remaining work is limited to comparing initialization sequences and optionally sending the exact old default Orange Pulse packet. No old package should be installed and no keyboard firmware should be flashed.

## Other identified versions

- `GCC_22.12.02.01_600.zip`, listed 2022-12-16 as a notebook GCC package: <https://kbench.com/software/?q=node/84673>
- `mb_utility_gcc_22.09.23.01.zip`, listed 2022-11-04 as a notebook GCC package: <https://kbench.com/software/?q=node/84513>

Their original Gigabyte URLs and KBench's old file host are no longer reachable. The placeholder 162-byte responses produced during the failed KBench attempts were overwritten/removed and are not treated as archives.
