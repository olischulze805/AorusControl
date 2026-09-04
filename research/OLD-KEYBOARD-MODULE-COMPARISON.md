# Vergleich: altes GBT-Keyboard-Modul und Firmware 19.0.4

Stand: 2026-09-02

## Kurzfazit

Das alte, original signierte `GBT_Keyboard 23.03.10.01` erkennt die Tastatur des AORUS 5 SE4 ausdrücklich als `USB VID_1044 / PID_7A41`, als ITE-Controller und als Drei-Zonen-RGB-Tastatur. Seine Live-RGB-Seite bietet Animationen an und sendet sie direkt an genau diesen Controller. Das von uns rekonstruierte globale Effektpaket ist bytegenau dasselbe Format wie im alten Modul.

Damit ist die bisherige Arbeitshypothese „falsches Paketformat“ widerlegt. Der heutige Controller nimmt statische Zonenfarben an, ignoriert aber denselben globalen Effektbefehl bzw. liefert dafür nur Nullen zurück. Der stärkste neue Verdacht ist deshalb eine Verhaltensänderung in der späteren Tastatur-Firmware `1.9.0.4`. Ein heute fehlender Initialisierungszustand bleibt theoretisch möglich, wird durch den alten Code aber deutlich weniger wahrscheinlich. Das ist noch keine endgültig bewiesene Ursache.

Es wurde nichts installiert, nichts ausgeführt und keine Firmware geschrieben.

## Herkunft des alten Moduls

- Offizieller Gigabyte-Updatepfad: `https://mb.download.gigabyte.com/FileList/Swhttp/LiveUpdate4/GCC/GBT_Keyboard/GBT_Keyboard_23.03.10.01.exe`
- Lokale Datei: `third-party/vendor/gcc-archives/GBT_Keyboard_23.03.10.01/GBT_Keyboard_23.03.10.01.exe`
- Größe: `43,382,192` Bytes
- SHA-256: `73341CAE5B3F9F9E74E119920332FE5337E8C5CF1B4FDDDE4E5CB52B3F00E64C`
- Authenticode: gültig
- Signierer: `GIGA-BYTE TECHNOLOGY CO., LTD.`
- Das Paket wurde nur statisch extrahiert und mit ILSpy dekompiliert.

Wichtige enthaltene Dateien:

| Datei | interne Version | SHA-256 |
|---|---:|---|
| `ucKeyboard.dll` | `23.03.10.01` | `9207E480FDDEA563B958B2296F013E4E0EC68E5D939C2059091CA136B356505A` |
| `KeyboardDomainLogic.dll` | `22.07.25.03` | `D2AD7BE1BAE798A81BABAE346707806FE61AA5BE8E90FBBCE46C76C82047C0DF` |
| `KeyboardModel.dll` | `22.07.25.07` | `766EBCE1C5E0B3B358D5A0A6271AB0444D304373DDBFEE85EF58436F8A3A8F56` |

Die Dateizeitstempel der Kernbibliotheken liegen am 26.–28. Juli 2023. Der Modulname ist daher älter als ein Teil seines tatsächlich ausgelieferten Inhalts.

## Exakte Geräteerkennung

In `KeyboardModel/KeyboardModel/GenericKeyBoard.cs`, Zeilen 129–138, steht eine eigene Verzweigung für `1044:7a41`. Bei einer Feature-Report-Länge von neun Bytes setzt sie:

- `myPid = "7a41"`
- `isExistIte = true`
- `isZoneRgb = true`
- `is3a4041 = true`

Das ist direkte Evidenz für unsere konkrete Tastatur und nicht nur gemeinsam genutzter Code für andere Gigabyte-Geräte.

## Alte sichtbare RGB-Funktionen

`ucKeyboard.ViewModels/RgbPageViewModel.cs`, Zeilen 58–120, erzeugt folgende sichtbare Auswahlliste:

| UI-Name | intern gesendeter Effekt |
|---|---:|
| Static | `1` Static |
| Pulse | `2` Breathing |
| Wave | `3` Wave |
| Reactive | `4` Fade on keypress |
| Marquee | `5` Marquee |
| Ripple | `6` Ripple |
| Cycle | `8` Neon |
| Droplet | `10` Raindrop |
| Hedge | `12` Hedge |
| Spiral | `13` Rotate |

Beim Ändern der Auswahl ruft die Seite direkt `FusionLightService.SetLightEffect(...)` auf. Besonders `Pulse` und `Cycle` stimmen sehr gut mit den vom Besitzer erinnerten funktionierenden Modi überein.

Der interne Schalter kennt darüber hinaus `Flash on keypress`, `Rainbow marquee`, `Circle marquee` und fünf Custom-Slots; die kompakte Seite zeigt jedoch nur die zehn Einträge oben.

## Bytegenaues Effektprotokoll

`KeyboardModel/KeyboardModel/IteKeyBoard.cs`, Zeilen 535–555, erzeugt einen neun Byte langen HID-Feature-Report:

| Byte | Bedeutung |
|---:|---|
| 0 | Report-ID / `0` |
| 1 | Befehl `0x08` |
| 2 | Selektor `0` für globalen Effekt |
| 3 | Effekt-ID |
| 4 | Geschwindigkeit |
| 5 | Helligkeit |
| 6 | Palettenfarbe |
| 7 | Richtung |
| 8 | `255 - Summe(Byte 1..7)` |

Danach folgen `HidD_SetFeature(..., 9)` und 65 ms Wartezeit. Das ist exakt das Format, das unsere Diagnose benutzt hat. Auch der alte Getter ist identisch: `0x88` mit Selektor `0`, anschließend `HidD_GetFeature(..., 9)`.

Die alten Standardwerte sind Orange `5`, Geschwindigkeit `5`, Helligkeit `50` und ITE-Richtung `1`. Ein exakter alter Standard-Pulse wäre daher:

`00 08 00 02 05 32 05 01 B8`

Der frühere Test mit Grün unterschied sich nur im Palettenbyte und der daraus folgenden Prüfsumme:

`00 08 00 02 05 32 02 01 BB`

Da der Controller bereits Effekt-ID und globalen Selektor ignoriert und beim Getter nur Nullen liefert, war die Palettenfarbe keine überzeugende Erklärung. Der orange Originalstandard wurde am 2026-09-02 dennoch exakt getestet: `0008000205320501B8` wurde erfolgreich gesendet und absichtlich aktiv gelassen, aber der unmittelbare globale Readback war erneut `008800000000000000`. Die drei gespeicherten grünen Zonenwerte blieben unverändert. Sichtbare Wirkung muss der Besitzer bestätigen. Bericht: `research/runs/keyboard-old-default-pulse-20260902-182252.md`.

## Geschwindigkeit und Profile

Die alte UI rechnet ihren Wert so um:

`rawSpeed = 10 - floor(uiSpeed / 10)`, mindestens `1`.

Damit ergeben sich neun unterscheidbare Controllerwerte: langsam `9` bis schnell `1`; UI 90 und 100 landen beide auf `1`.

Wichtig ist die Trennung zwischen Live-Seite und Profil-Lader:

- Die Live-Seite sendet den globalen Effekt direkt.
- `LoadProfileLightEffectZoneRgb(...)` liest Effekt, Helligkeit, Farbe, Geschwindigkeit und Richtung aus XML, verwirft diese Werte aber und schreibt nur die drei Zonenfarben.

Der defekte oder eingeschränkte Profil-Lader beweist daher nicht, dass die alte Live-Steuerung ebenfalls nur statisch war.

## Prüfung der alten Initialisierung

Die alte RGB-Seite führt beim ersten Effektwechsel `FusionController.InitKeyboard()` aus. Dieser Aufruf sendet keinen versteckten Freischalt- oder RGB-Befehl:

- Er erstellt nur das ITE-Objekt, falls nötig.
- `configKeyboard()` zählt die HID-Geräte auf, öffnet den passenden Handle und setzt die Geräteklassifikation.
- Anschließend ruft die Seite unmittelbar `SetLightEffect(...)` auf.

Auch `SetKeyboardMode(1)` / Befehl `0x09` ist nicht Teil dieses RGB-Ablaufs; er gehört zur getrennten Gaming-/Tastenbelegungsfunktion. Damit ist kein zusätzlicher alter Initialisierungsbefehl erkennbar, den unser RGB-Test vergessen hätte. Denkbar bleiben äußere Unterschiede wie ein anderer Treiberzustand oder ein damals parallel laufender Dienst, aber der direkte alte Seitenpfad benötigt laut Code nur Geräteöffnung plus Effektpaket.

## Braucht der alte RGB-Pfad einen alten Treiber?

Die statische Prüfung spricht klar dagegen:

- Das alte `GBT_Keyboard 23.03.10.01` enthält keine `.inf`-, `.sys`- oder `.cat`-Datei und damit keinen installierbaren Tastatur-Gerätetreiber.
- Das Paket enthält nur die UI-/Logik-DLLs, Konfigurationsdaten und einen Uninstaller; es enthält keinen eigenen dauerhaften Keyboard-Dienst oder Keyboard-Prozess.
- Die Dateien `GvDll/HidDriver.dll` und `GvDll/HoltekDriver.dll` sind Programmbibliotheken, keine Windows-Kerneltreiber.
- Der dekompilierte `7A41`-Pfad öffnet die normale HID-Geräteschnittstelle mit `CreateFile` und sendet über Windows `HidD_SetFeature`.
- Das aktuell angeschlossene `7A41` verwendet für `MI_03` Microsofts signierten Standardtreiber `input.inf`, Version `10.0.26100.8972`; alle zugehörigen HID/USB-Schnittstellen melden Status `OK`.

Die Installation des alten Keyboard-Moduls würde daher keinen „alten Tastaturtreiber“ einspielen. Sie würde hauptsächlich ältere GCC-DLLs und UI-Code ablegen. Da unser Test bereits denselben direkten HID-Aufruf mit exakt denselben Bytes ausgeführt hat, ist nicht zu erwarten, dass eine Installation allein den Controller anders reagieren lässt. Ein kompletter alter GCC-Stack könnte theoretisch einen noch unbekannten äußeren Zustand erzeugen, doch im untersuchten Live-RGB-Pfad ist ein solcher Schritt nicht sichtbar. Das Risiko von Versionskonflikten mit dem heutigen GCC ist größer als der derzeit erkennbare Erkenntnisgewinn.

## Firmwarepaket 19.0.4

Das lokal vorhandene offizielle Paket `904_20230913_C_WEB.exe` wurde nur statisch entpackt nach `third-party/vendor/keyboard-firmware-19.0.4-static`.

- Paketdatum: 14. September 2023
- Authenticode: gültig, Giga-byte Technology Co., Ltd.
- Ziel laut `SHFU.ini`: `USB\\VID_1044&PID_7A41`
- Bootloader-Ziel: `USB\\VID_048D&PID_89DB`
- Firmwaredatei: `docking_b.bin`, 122,880 Bytes
- Firmware-SHA-256: `DE6F360BE145FF01553FE981FE251A49FCA574003CE51F46D516FC22C713886F`
- Klartextsignatur in der Firmware: `Gigabyte Fusion_8298:1.9.0.4`
- Live ausgelesene Controller-Version: `19.0.4`

Die Schreibdatei wurde am 6. September 2023 erstellt und liegt damit nach den Kernbibliotheken des alten Keyboard-Moduls vom Juli 2023. Diese zeitliche Reihenfolge macht die Firmware zu einem plausiblen Wendepunkt. Sie beweist jedoch allein nicht, dass gerade das Update die Effekte entfernt hat.

Am 2026-09-02 ist die Firmwaredatei knapp drei Jahre alt: 2 Jahre, 11 Monate und 27 Tage seit dem Build vom 2023-09-06; das veröffentlichte Paket vom 2023-09-14 ist 2 Jahre, 11 Monate und 19 Tage alt. Eine erneute Suche auf der offiziellen AORUS-5-Supportseite und nach dem exakten Paketnamen fand keine öffentlich indexierte neuere Keyboard-Firmware. Weil Gigabytes Downloadliste dynamisch geladen wird, ist dies kein sicherer Beweis, dass `19.0.4` die letzte je veröffentlichte Version ist.

`SHFU.ini` enthält unter anderem `NEEDERASE=1`, `CHECKSUMMODE=1`, einen 120-KB-Flashbereich und einen Neustart nach dem Upgrade. Deshalb wird der Updater weder gestartet noch für Versuche verwendet. Ein Firmware-Downgrade wäre deutlich riskanter als ein gewöhnlicher Softwaretest und gehört nicht zum geplanten Vorgehen.

## Installationsspuren

Die aktuell vorhandenen Windows-SetupAPI-Protokolle enthalten die HID-Schnittstellen der `1044:7A41`, aber keine Treffer für `ITESHFU`, `docking_b` oder den Paketnamen. Auch eine aktuelle Prefetch-Datei für `ITESHFU.exe` wurde nicht gefunden. Diese Protokolle können rotiert oder bereinigt worden sein; daraus lässt sich weder ein Installationszeitpunkt noch sicher ableiten, ob das Update automatisch oder manuell eingespielt wurde.

## Neue Schlussfolgerung

Evidenzstufen:

1. **Bewiesen:** Alte Gigabyte-Software klassifiziert `7A41` ausdrücklich als Drei-Zonen-RGB-ITE-Tastatur.
2. **Bewiesen:** Die alte Live-Seite bot Pulse, Cycle und weitere Modi an und rief den globalen Effektpfad auf.
3. **Bewiesen:** Unser globales HID-Paketformat entspricht dem alten Originalcode.
4. **Bewiesen:** Statische Drei-Zonen-Befehle funktionieren heute; globale Effektbefehle zeigen heute keine Wirkung und lesen als Null zurück.
5. **Plausibel, noch nicht bewiesen:** Firmware `1.9.0.4` änderte oder entfernte die globale Effektbehandlung. Eine fehlende Initialisierung ist nach Prüfung des alten Live-Pfads weniger wahrscheinlich.

An additional interpretation from the shared WMI schema and same-generation owner reports: `get/SetPostAnimate` likely toggles a preset keyboard RGB color cycle during power-on/POST. This is separate from the runtime `0x08` effect path and from the on-screen GIGABYTE splash logo. The owner's remembered slow full-color transition could therefore have been the boot animation, although this does not explain remembered runtime Pulse/Flash behavior by itself.

## Sichere nächste Schritte

1. Abgeschlossen: exakten alten Standard-Pulse mit Orange senden; Firmware meldet weiterhin nur Nullen zurück. Sichtbare Wirkung durch den Besitzer bestätigen.
2. Nach einer älteren, original signierten Firmware suchen, sie aber ausschließlich statisch vergleichen und keinesfalls flashen.
3. Optional den damaligen kompletten GCC-Host statisch auf äußere Seitenwechsel-/Sync-Aufrufe untersuchen; das Keyboard-Modul selbst bringt weder eigenen Gerätetreiber noch Dienst mit.
4. Falls sich kein anderer Hostpfad findet, die eigene App auf zuverlässig funktionierende statische Drei-Zonen-RGB-Steuerung beschränken und Effekte als experimentell kennzeichnen.
