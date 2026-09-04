# third-party/

Everything in this folder is **excluded from version control** (see `.gitignore`) and has
to be obtained locally. It holds two kinds of material, neither of which is ours to
redistribute:

- **Vendor software and firmware** — Gigabyte's installers plus the archives extracted and
  statically analysed from them. The research documents cite paths under
  `third-party/vendor/`, so the evidence trail stays readable even though the files are not
  in the repository.
- **Analysis tools** — third-party binaries downloaded to inspect the above.

The application itself never needs any of this: `dotnet build AorusControl.slnx` works on a
clean clone. Only reproducing the reverse-engineering documented under `research/` does.

## Expected layout

| Path | What it is | Where it comes from |
|---|---|---|
| `downloads/GIGABYTE Control Center_*_Setup_*.exe` | Vendor control-centre installer | Gigabyte support site |
| `downloads/GCC_*.zip` | Same package as an archive | Gigabyte support site |
| `downloads/X5MVE_BIOS_FB0F_EC_F00B_WEB_*.exe` | BIOS/EC update self-extractor | Gigabyte support site |
| `downloads/nb-bios-aorus5-ve-*.zip` | BIOS package archive | Gigabyte support site |
| `downloads/nb-driver-64bit-aorus5-ve-keyboardfirmware-*.zip` | Keyboard firmware package | Gigabyte support site |
| `downloads/904_*_C_WEB.exe` | Older vendor package kept for comparison | Gigabyte support site |
| `vendor/` | Everything extracted or decompiled from the packages above | produced locally |
| `ilspy/` | ILSpy command-line decompiler (`ilspycmd.exe`) | https://github.com/icsharpcode/ILSpy |
| `uefitool-a75/` | UEFITool, for the BIOS image | https://github.com/LongSoft/UEFITool |
| `ifrextractor-rs-1.6.1/` | IFR extractor, for BIOS setup forms | https://github.com/LongSoft/IFRExtractor-RS |
| `ilspy-copy-from-research/` | Duplicate ILSpy copy from the old `research/tools/` location | same as `ilspy/` |

Nothing here is executed by the application, the tests, or any launcher in `tools/`. The
decompilation and extraction that produced `vendor/` were static only — no vendor software
was installed or run; `RESEARCH.md` records that in detail.
