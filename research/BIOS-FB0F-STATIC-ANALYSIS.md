# AORUS 5 SE4 – statische BIOS-Analyse FB0F

Stand: 2026-09-02

## Kurzfazit

Das sichtbare BIOS des AORUS 5 SE4 ist absichtlich stark vereinfacht. Unter der kleinen Oberfläche steckt jedoch eine umfangreiche AMI-Aptio-/Intel-Alder-Lake-Firmware. Die Setup-Daten enthalten 255 Formulare, ungefähr 2.570 Auswahlfelder, 1.858 Zahlenfelder, 136 Kontrollkästchen, 1.401 `SuppressIf`-Ausblendbedingungen und 324 `GrayOutIf`-Bedingungen.

Das bedeutet ausdrücklich **nicht**, dass alle 4.500 Felder auf diesem Laptop funktionieren oder gefahrlos benutzt werden können. Ein großer Teil stammt aus Intels generischer Referenzplattform, ist für andere Boardvarianten gedacht oder wird zur Laufzeit abhängig von CPU, Chipsatz, Board-ID und Firmware-Flags ausgeblendet.

## Sicherheitsgrenze dieser Untersuchung

- Ausschließlich statische, lesende Analyse der offiziellen Update-Datei.
- Kein BIOS-, EC- oder NVRAM-Schreibzugriff.
- `SmartFlash.exe`, `AFUWINx64.exe` und `amigendrv64.sys` wurden nicht ausgeführt beziehungsweise geladen.
- Es wird kein verstecktes Setup-Byte verändert. Ein falscher Wert kann Boot, Kühlung, Speicherinitialisierung oder Secure Boot beschädigen.

## Untersuchte Dateien und Herkunft

Offizielles Gigabyte-Paket:

- Archiv: `nb-bios-aorus5-ve-win11-64bit-fb0f-ec-f00b.zip`
- Archiv-SHA256: `236F21D352F6EBCD6DC8BB2400E8A61B1F3EEF31F54F5E7804397E63382412E3`
- Selbstentpacker: `X5MVE_BIOS_FB0F_EC_F00B_WEB_26042801.exe`
- EXE-SHA256: `ADE63647E2867D7C0FB55A572CB3AB446F92DA4BC1E1E89F94135365C203DE34`
- Authenticode: gültig, Signatur von GIGA-BYTE TECHNOLOGY CO., LTD.
- ROM: `RX5ME4FB0F.rom`
- ROM-Größe: 38.797.312 Bytes (`0x02500000`)
- ROM-SHA256: `49C5FB7EE8E4A40AB6D7017B6A3D5F7EA0DFE1EC0A98D9D5460A360617E76B6F`

Metadaten aus `info1.xml`:

```xml
<BIOS>FB0F</BIOS>
<EC>F00B</EC>
<BIN>RX5ME4FB0F.rom</BIN>
<CMD>/P /B /N /CAPSULE /Q</CMD>
```

Die Datei wurde mit UEFIExtract A75 und IFRExtractor-RS 1.6.1 von LongSoft untersucht. Beide Werkzeuge lesen nur die Binärstruktur. IFR ist das von UEFI HII verwendete Format für Setup-Menüs, Texte, Werte und Sichtbarkeitsbedingungen.

## Was im sichtbaren, vereinfachten BIOS definiert ist

Die Firmware besitzt einen eigenen kleinen Satz von Notebook-Formularen. Dieser passt sehr gut zu der tatsächlich simplen Oberfläche.

### Main

- Projektname
- BIOS-Firmwareversion
- BIOS-Builddatum und -zeit
- EC-Firmwareversion
- Build-Version
- Intel-ME-Firmwareversion und SKU
- Datum und Uhrzeit

### Advanced

- Thunderbolt-5-V-Stromversorgung: an/aus; Standard aus
- 802.11ax-Sonderregel für bestimmte Länder: an/aus; Standard aus
- Onboard Device Configuration
- NVMe-Geräteinformation/-konfiguration
- USB-Geräteinformation/-konfiguration
- SATA Configuration ist fest mit `SuppressIf True` ausgeblendet

### Onboard Device Configuration

- WLAN: aktiviert oder durch „Command & Control“ verwaltet; Standard aktiviert
- Aufwecken durch WLAN/Bluetooth: an/aus; Standard an
- RTC-Wakeup aus S4/S5: an/aus; Standard aus
- Webcam: aktiviert oder durch „Command & Control“ verwaltet; Standard aktiviert
- Hyper-Threading: an/aus; Standard an
- Intel VMX/CPU-Virtualisierung: an/aus; Standard an
- Bluetooth-Schalter ist vorhanden, aber fest ausgeblendet
- Tastaturlayout-Auswahl Englisch/Europäisch/Japanisch ist vorhanden, aber fest ausgeblendet

### Chipset

Nur lesbare RAM-Informationen:

- DIMM-Belegung
- Speichertakt
- primäre Timings `tCL-tRCD-tRP-tRAS`
- Hersteller
- Gesamtspeicher

### Security

- Administrator-Passwort, 3 bis 20 Zeichen
- Secure-Boot-Systemzustand
- Werks-Schlüssel einspielen
- alle Secure-Boot-Variablen löschen

### Boot

- NumLock beim Start; Standard an
- UEFI-Bootreihenfolge
- OS Type besitzt in der kleinen Oberfläche nur `UEFI OS`
- Legacy-Gerätereihenfolge ist bedingt und normalerweise nicht verfügbar

### Save & Exit

- Änderungen speichern/verwerfen
- neu starten oder beenden
- Standardwerte laden
- Boot Override, sofern zur Laufzeit Bootziele vorhanden sind

## Umfangreiche, normalerweise ausgeblendete Intel-/AMI-Seiten

Der große interne Setup-Satz enthält unter anderem folgende Gruppen. „Vorhanden“ heißt hier zunächst nur: Formular, Hilfetext und Variablenzuordnung sind im FB0F-Image enthalten.

| Gruppe | Nachweis im FB0F-Setup | Einschätzung für das AORUS 5 SE4 |
|---|---|---|
| CPU | P-/E-Core-Information, Hyper-Threading, VMX, VT-d, SpeedStep, Speed Shift, Turbo, C-States, Turbo-Ratios, CPU Locks | Grundfunktionen sind real; Feintuning ist überwiegend verborgen und teils durch CPU/Firmware gesperrt |
| CPU-Leistung | PL3, Config TDP, VR-Stromgrenzen, Spannungsmodi, CEP, CFG Lock, Overclocking Lock | Code und Variablen vorhanden; bei i7-12700H keinesfalls automatisch als freies OC/Undervolting interpretieren |
| Arbeitsspeicher | Frequenz, Timings, Training, thermische Limits, „Memory Overclocking Menu“ | RAM-Info real; Tuning-Seiten größtenteils Intel-Referenzcode und nicht als unterstützt bestätigt |
| Grafik | iGPU/PEG/Hybrid Graphics, primäre Anzeige, DVMT, Resizable BAR, Above-4G-MMIO, Switchable Graphics | Hybridgrafik und Resizable BAR sind plausibel/relevant; manuelles Umschalten ist nicht als sicher verfügbar bestätigt |
| Thunderbolt/USB4 | PCIe-Tunneling über USB4, integriertes/discretes Thunderbolt, Wake, Root-Port-Konfiguration | Thunderbolt 4 ist Modellhardware; viele Low-Level-Optionen bleiben absichtlich verborgen |
| Speichergeräte | NVMe, SATA, SMART, VMD/RST, Secure Erase, SATA Link Power Management | NVMe/VMD sind real; falsche VMD/SATA-Änderungen können Windows unbootbar machen |
| Sicherheit | TPM/PTT, Secure Boot und Schlüsselverwaltung, BIOS Guard, TXT, TCG-Laufwerke, UEFI-Variablenschutz | Secure Boot und PTT sind real; AMT/TXT/TCG-Unterseiten sind SKU-/Hardware-abhängig |
| Netzwerk-Boot | IPv4/IPv6 PXE, IPv4/IPv6 HTTP Boot, TLS, DHCP, UEFI-Netzwerkstack | Module sind im Image vorhanden, Oberfläche blendet sie normalerweise aus |
| Boot-Kompatibilität | Fast Boot, CSM, Legacy Option ROMs, UEFI Shell, Treiberreihenfolge | Interner AMI-Code vorhanden; kleine Oberfläche erzwingt praktisch UEFI |
| Energie/Thermik | Intel Dynamic Tuning, CPU-/Plattform-Thermik, ACPI D3Cold, PEP, FIVR, PMC | DTT und OEM-Thermik sind real; Werte sind plattformspezifisch und gefährlich zu raten |
| Lüfter | ACPI Active Trip Points, Notfall-CPU-Lüfterwert sowie FAN1/FAN2/FAN3-Referenzfelder | Kein Beweis für eine nutzbare BIOS-Lüfterkurve; die echte Regelung des Laptops läuft primär über EC/Gigabyte-Steuerung |
| Geräte | WLAN, Bluetooth, Webcam, Audio, SDIO, Serial-I/O, Touchpad, Touchpanel, Fingerprint, MIPI-Kamera | WLAN/Webcam sind im kleinen Menü real; viele weitere Seiten sind generische Plattformoptionen |
| Unternehmensfunktionen | MEBx, AMT, ASF, Remote Erase, One Click Recovery | Formularcode vorhanden; auf Consumer-SKU wahrscheinlich nicht bereitgestellt |
| Debug | Intel-/AMI-Debug, VT-d-Debug, ME-Debug, serielle Konsole | Entwicklungs-/Herstellerfunktionen, nicht für normale Nutzung vorgesehen |

## Konkrete interessante versteckte Einträge

Einige belegte Beispiele mit Variablenspeicher und Offset:

- `Intel(R) SpeedStep(tm)`: `CpuSetup`, Offset `0x09`
- `Intel(R) Speed Shift Technology`: `CpuSetup`, Offset `0x0B`
- `Turbo Mode`: `CpuSetup`, Offset `0x16`
- `CFG Lock`: `CpuSetup`, Offset `0x43`
- `Intel (VMX) Virtualization Technology`: `CpuSetup`, Offset `0xB9`
- `Overclocking Lock`: `CpuSetup`, Offset `0x10E`
- `VT-d`: `SaSetup`, Offset `0x7D`
- `Enable VMD controller`: `SaSetup`, Offset `0xF8`
- `PCIE Resizable BAR Support`: `SaSetup`, Offset `0x42B`
- `PCIE Tunneling over USB4`: `Setup`, Offset `0x9A9`

Diese Offsets werden nur dokumentiert, um die Struktur zu verstehen. Sie sind **keine Empfehlung**, sie mit Setup-Variablenwerkzeugen zu verändern.

## Lüfter und Akku: wichtige Abgrenzung

Das versteckte Referenz-Setup enthält generische ACPI-Temperaturpunkte und einen „CPU Fan Speed“-Notfallwert für den Fall, dass das Betriebssystem hängt. Daraus folgt nicht, dass das BIOS eine fertige Lüfterkurven-Oberfläche besitzt. Unsere bisherigen Live-Analysen zeigen, dass Gigabyte die normale Lüftersteuerung über EC/WMI und Control Center umsetzt.

Für das Akkuladelimit gibt es im IFR keinen einfachen sichtbaren „80 %“-Eintrag. Das passt zu unserem bestätigten Ergebnis: Ladepolitik und Ladestopp werden über Gigabytes ACPI/WMI-Schnittstelle und EC-Felder (`Get/SetChargePolicy`, `Get/SetChargeStop`) gesteuert, nicht über das normale BIOS-Menü.

## Aufbau des ROMs

UEFIExtract erkennt im Update-Container derzeit:

- 27 Firmware Volumes
- 341 UEFI-Dateien
- 1.100 Sections
- AMI Aptio
- Intel Alder Lake-P Consumer-Plattform
- mehrere NVRAM-/Default-Sätze, darunter `Setup`, `AMITSESetup`, `CpuSetup`, `SaSetup`, `PchSetup`, `MeSetup`, `MeSetupStorage`, `SiSetup`, `AmiWrapperSetup`, `SioSetupData` und `SecureBootSetup`

Wichtige Firmwaremodule umfassen Boot Guard, BIOS Guard, TPM 2.0/PTT, TXT/TCG, NVMe, RST/VMD, CSM, Intel-Grafik, USB4, Thunderbolt-nahe Plattformlogik, Netzwerk-Boot, TLS und OEM Power Limit.

## Besonderheit beim Entpacken

Der wichtigste DXE-/Setup-Bereich liegt in einer AMI-LZMA-GUID-Section. Das signierte Update-Image ließ sich mit UEFIExtract nicht direkt vollständig dekomprimieren. Eine rein lesende manuelle LZMA-Extraktion lieferte die ersten 7.010.440 Bytes des erwarteten Blocks. Dieser Teil enthält bereits vollständige HII-Pakete für:

- das große AMI-/Intel-Setup: 2.012.418 Bytes IFR-Text
- Intel MEBx/AMT
- Acoustic Management

Obwohl der Datenstrom danach abbricht, sind die genannten HII-Pakete vollständig genug, dass IFRExtractor sie konsistent auflisten und in lesbare Formulare umwandeln konnte. Noch nicht extrahierte spätere Module können zusätzliche Formsets enthalten.

## Vorläufige Schlussfolgerung für unser eigenes Windows-Programm

Das eigene Programm sollte weiterhin nur die gut verstandenen, laufzeitfähigen Hersteller-Schnittstellen verwenden: Sensoren, Lüfterprofile/-steuerung nach weiterer Verifikation, GPU-Leistungsparameter, Akkuladelimit, RGB und bestätigte Geräte-/LED-Schalter. Versteckte Intel-Setup-Variablen sollten nicht als normale App-Funktionen angeboten werden. Viele erfordern einen Neustart, können Sicherheitszustände ändern oder das System unbootbar machen.

## Rohdaten

- UEFI-Strukturbericht: `third-party/vendor/bios-fb0f-static/suit/file/RX5ME4FB0F.rom.report.txt`
- Vollständiger extrahierter IFR-Text: `third-party/vendor/bios-fb0f-static/manual-lzma-main-dxe/body.2.3.en-US.uefi.ifr.txt`
- MEBx-IFR: `third-party/vendor/bios-fb0f-static/manual-lzma-main-dxe/body.0.1.en-US.uefi.ifr.txt`
- Acoustic-Management-IFR: `third-party/vendor/bios-fb0f-static/manual-lzma-main-dxe/body.1.2.en-US.uefi.ifr.txt`

## Quellen

- Gigabyte, offizielle AORUS-5-Unterstützungsseite: <https://www.gigabyte.com/Laptop/AORUS-5--Intel-12th-Gen/support>
- LongSoft UEFITool: <https://github.com/LongSoft/UEFITool>
- LongSoft IFRExtractor-RS: <https://github.com/LongSoft/IFRExtractor-RS>

