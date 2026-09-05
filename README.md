# AORUS Control

A replacement control panel for my Gigabyte AORUS 5 SE4 laptop: fan profiles and a custom
fan curve, three-zone keyboard lighting, battery charge limit, and the Windows power mode —
in one window that starts fast and stays out of the way.

I built it because Gigabyte Control Center never really worked on this machine. It was slow,
it forgot settings, parts of it silently did nothing, and it wanted a background stack far
larger than the handful of registers it actually writes. So I took the same hardware
interfaces apart myself and wrote something small that does the few things I need, reliably.

**This project is completely vibe coded.** I described what I wanted and Claude wrote it;
I did not hand-write the code line by line. What keeps that honest is in the design rather
than in my review of every statement:

- Every hardware write is read back and verified, and rolled back if the readback disagrees.
- Everything is locked to this exact machine — model `AORUS 5 SE`, BIOS `FB0F`, and the exact
  USB keyboard interface. On anything else the app refuses to write rather than guessing.
- The panel shows what the device reports, not what was last clicked. A write that fails moves
  the highlight back instead of pretending it worked.
- 51 automated checks run without any hardware attached.

## ⚠️ Before you use this

It is built for one laptop model and one firmware version. Fan control writes to the
embedded controller, so on a machine it does not recognise it stops instead of
experimenting — but do not remove those gates to "make it work" on your device. BIOS and EC
firmware are never flashed or modified.

Fixed fan speed is held by a lease in a separate process, so if the panel crashes or is
killed, that process still returns the fans to firmware control on its own.

## What it does

| Section | |
|---|---|
| **Dashboard** | CPU/GPU temperature, fan RPM and duty, live |
| **Kühlung** | Fan profiles, a fixed speed limited to the eight tested steps, and a draggable 15-point fan curve |
| **Tastatur** | Three RGB zones, nine effects, four brightness steps, plus a live preview of the actual keyboard |
| **Leistung & Akku** | Windows power mode and the battery charge limit |
| **Info & Updates** | Version, autostart, logs |

## Build and run

Requires the .NET SDK pinned in `global.json`; Windows only.

```powershell
dotnet build AorusControl.slnx --configuration Release
dotnet run --project tests/AorusControl.App.SmokeTests
```

Start it with `tools\Start-AorusControl.cmd`. Hardware access needs administrator rights,
so Windows shows its normal UAC prompt. `tools\` also holds a launcher for each hardware
experiment, and `tools\Start-FanNormalRestore.cmd` puts the fans back under firmware
control if anything ever goes sideways.

Settings and logs live under `%LocalAppData%\AorusControl\`.

## Layout

| Path | |
|---|---|
| `src/` | `Core` (device gates and guarded setters), `App` (the WPF panel), `Worker` (keeps fixed fan mode crash-safe), `Diagnostics` (read-only reports) |
| `tests/` | One console suite, no test framework, no hardware needed |
| `tools/` | Launchers for the app and for each experiment |
| `research/` | How the hardware was worked out, and why each decision went the way it did |
| `third-party/` | Not in version control — vendor installers and analysis tools; see its README |

If you want detail: `RESEARCH.md` in the root has the verified ACPI method map, and the
documents in `research/` cover the fan supervisor, the RGB protocol, the UI design system
and the reverse-engineering notes behind them.

## License

MIT — see `LICENSE`. Use it, change it, ship it; just keep the copyright notice.

This covers the code and documentation here. Gigabyte's own software and firmware are not
part of this repository, and `research/` documents findings about the hardware rather than
reproducing vendor source.
