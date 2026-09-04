# RGB-Effekt-Rätsel: Ursachensuche

Warum lassen sich auf `1044:7A41` (AORUS 5 SE4, Tastatur-Firmware 19.0.4) keine
Lichteffekte mehr auslösen, obwohl der Besitzer bestätigt, dass Breathing/Pulse
und langsame Farbwechsel auf genau diesem Gerät früher sichtbar liefen?

Diese Datei sammelt die Ausschlüsse und die offenen Hypothesen. Die
Protokolldetails stehen in `KEYBOARD-CAPABILITIES.md`, der Modulvergleich in
`OLD-KEYBOARD-MODULE-COMPARISON.md`, die Webrecherche in `RGB-WEB-FINDINGS.md`.

## Sicherheitsgrenze dieser Untersuchung

- Nur statische Datei- und Binäranalyse; keine Firmware wurde geschrieben oder geflasht.
- Keine neuen Kommandobytes an das Gerät gesendet.
- Kein Gigabyte-Paket installiert oder ausgeführt.

## Ausschluss 1: Die Gigabyte-Software ist nicht die Ursache — bewiesen

Das aktuell installierte Modul `GBT_Keyboard 25.07.25.01` und das historische
`GBT_Keyboard 23.03.10.01` enthalten bitgleiche Tastatur-Binaries.

| Datei | SHA-256 | Dateidatum |
|---|---|---|
| `KeyboardModel.dll` | `766ebce1c5e0b3b358d5a0a6271ab0444d304373ddbfee85ef58436f8a3a8f56` | 2023-07-28 |
| `KeyboardDomainLogic.dll` | `d2ad7be1bae798a81babae346707806fe61aa5be8e90fbbce46c76c82047c0df` | 2023-07-28 |
| `GkCenters.dll` | `d13923aeaaacc21b53306ff494fe692aca22dde8bdf9ef363f997401db0a8df9` | 2023-07-28 |
| `ucKeyboard.dll` | `9207e480fddea563b958b2296f013e4e0ec68e5d939c2059091ca136b356505a` | 2023-07-28 |

Verglichen wurden das installierte Verzeichnis
`C:\Program Files\GIGABYTE\Control Center\Lib\GBT_Keyboard\` und der statische
Auszug des alten Pakets. Zusätzlich wurden `KeyboardModel.dll` und
`KeyboardDomainLogic.dll` des installierten Moduls erstmals dekompiliert
(`third-party/vendor/GBT_Keyboard_25.07.25.01-installed/decompiled/`, 73 Dateien).
Ein vollständiger Datei- und Inhaltsvergleich gegen die alte Dekompilation ergab
keine neue, keine entfernte und keine geänderte Datei.

Folgerung: Zwischen dem Zeitraum, in dem Effekte sichtbar liefen, und heute hat
sich der Host-Code kein einzelnes Byte verändert. Die Ursache liegt damit auf
der Geräteseite — Firmware oder persistenter Gerätezustand.

## Ausschluss 2: Kein fehlendes Initialisierungs- oder Freischaltkommando

`GenericKeyBoard.configKeyboard()` öffnet nur per `SetupDi*` und `CreateFile`,
prüft `VID 1044` und `PID 7a41` im Gerätepfad, verlangt
`FeatureReportByteLength == 9` und setzt dann `isExistIte`, `isZoneRgb` und
`is3a4041`. Danach folgt direkt der Effekt-Write. Es gibt keinen Unlock-,
Handshake- oder Setup-Frame.

`ComHeader.Class1.KB_Pid1` enthält `31297` = `0x7A41`. Die Auswahl trifft
zwingend `MI_03`, weil alle anderen Collections an der 9-Byte-Prüfung scheitern
(`MI_02/COL_07` hat 17 Byte).

## Ausschluss 3: Kein Getter-Race

`IteKeyBoard.GetLightEffect()` ruft `HidD_SetFeature` und unmittelbar danach
`HidD_GetFeature` ohne Wartezeit — anders als `GetRgbZoneInfo()`, das 10 ms
schläft. Das war ein plausibler Verdacht: Der Nullwert könnte nur ein zu früh
gelesener Puffer sein.

Unsere Diagnose wartet jedoch bereits 500 ms zwischen `SET_FEATURE` und
`GET_FEATURE` für Selektor 0 (`Query(0x88, 0, 500)` in `Program.cs`), während
Zonenabfragen mit 65 ms zuverlässig echte Werte liefern. Byte 1 der Antwort
echot korrekt `0x88`, nur die Felder 3 bis 7 bleiben null. Das Gerät antwortet
also, meldet aber inhaltlich "kein Effekt aktiv". Ein Timing-Artefakt ist damit
ausgeschlossen.

## Ausschluss 4: Selektor 0 ist nicht schlicht unbeantwortet

Nachtrag: Die Nullantwort dieses Getters ist inzwischen als Nachweis vollstaendig
entwertet — siehe die Ergebnisse weiter unten. Er meldete auch dann null, wenn
sich die Beleuchtung sichtbar aenderte.

Die Antwort auf `0x88` mit Selektor 0 war stets `00 88 00 00 00 00 00 00 00`:
Kommando-Echo vorhanden, Nutzdaten leer. Effektwert `0` existiert in Gigabytes
Enum nicht — Static ist `1`. Die Firmware führt das Feld also, hält es aber
dauerhaft leer.

## Vollständige Kommandoliste der offiziellen ITE-Implementierung

Aus `IteKeyBoard.cs` erschöpfend ausgelesen. Alle Pakete sind 9 Byte,
Byte 0 ist Report-ID `0x00`, Byte 8 ist `255 - Summe(Byte 1..7)`.

| Byte 1 | Richtung | Bedeutung | Status bei uns |
|---|---|---|---|
| `0x08` sel 0 | set | globaler Effekt: Typ, Speed, Brightness, Farbe, Richtung | Paket wird geparst; nur Palette `0` wirkt (abschalten), alles andere No-op |
| `0x08` sel 1-3 | set | Zonenfarbe und Brightness | funktioniert vollständig |
| `0x88` | get | Effekt (sel 0) bzw. Zone (sel 1-3) | Zone gut; sel 0 immer null, auch bei sichtbarer Wirkung — als Nachweis unbrauchbar |
| `0x80` | get | Firmwareversion | funktioniert, `19.0.4` |
| `0x0D` / `0x8D` | set/get | 512-Byte-Tastenmatrix | nur gelesen |
| `0x11` / `0x91` | set/get | Makroinhalt | nie berührt |
| `0x12` / `0x92` | set/get | Picture-Matrix, 960 Byte | `0x92` gelesen, alle 5 Slots leer; `0x12`-Schreiben wurde nicht angenommen |
| `0x09` | set | Tastaturmodus Standard/Gaming | nie berührt |

Die Konvention ist durchgehend: hohes Bit gesetzt bedeutet Lesen, gelöscht
bedeutet Schreiben.

### Neu entdeckt: der Picture-Matrix-Pfad

`0x92` und `0x12` transportieren eine 960-Byte-Struktur (`PictureMatrix`), die
in 64-Byte-Blöcken über `ReadFile` und `WriteFile` mit 65-Byte-Reports auf
demselben `MI_03`-Handle läuft, nicht über Feature-Reports. 960 Byte
entsprechen 320 mal RGB.

Zugehörige Methoden: `LoadPictureMatrixValue(effect)` adressiert den Slot mit
`effect - 51`, passend zu den Effekt-Enums `Custom1` bis `Custom5` = `51` bis
`55`. `modifySingleBtnColor(index, r, g, b)` schreibt eine einzelne LED,
`SetPictureMatrix2Device(slot)` lädt hoch, und
`SetPictureMatrix2DeviceSleepTime(slot, sleepMs)` erlaubt eine frei wählbare
Wartezeit. Gigabyte hat mit `SynchPictureMatrixColor` und
`SynchPictureMatrixColorSleepTime` über diesen Pfad also selbst host-getriebene
Animation implementiert.

Dies ist ein vollständig ungetesteter zweiter Lichtpfad. Der Lesebefehl `0x92`
ist ein offizieller Getter und liegt damit innerhalb unserer Kommandogrenze.

### Live-Ergebnis: `0x92` wird von Firmware 19.0.4 bedient

Der neue Diagnosemodus `--probe-keyboard-picture-matrix` sendet ausschliesslich
den Getter `0x92` und liest die acht 65-Byte-Eingangsreports. Der Setter `0x12`
ist bewusst nicht implementiert. Alle fünf Custom-Slots wurden abgefragt.

Entscheidend ist der Handshake, gelesen in einen **genullten** Puffer, damit die
Antwort nachweisbar vom Gerät stammt und nicht aus unserer eigenen Vorbelegung
(Gigabytes Originalcode belegt den Puffer vor `GET_REPORT` vor, was seine
Antwort methodisch unbrauchbar macht):

```text
Anfrage  : 00 92 00 02 00 00 00 00 6B
Antwort  : 00 92 00 02 08 00 00 00 00
```

- Byte 1 `0x92` — Kommando-Echo.
- Byte 3 `0x02` — der von uns angeforderte Slot-Index, zurückgespiegelt.
- Byte 4 `0x08` — die Blockzahl, vom Gerät geliefert.

Danach kamen in allen fünf Läufen **8 von 8 Blöcken** ohne Timeout an
(Leselimit 2500 ms). Laut der früheren 25-Sekunden-Korrelationsmessung sendet
`MI_03` von sich aus keine Eingangsreports; die acht Reports sind damit die
Antwort auf `0x92`.

Ergebnis pro Slot: 512 Byte, **0 von 512 Byte ungleich null**, ein einziger
Bytewert. Alle fünf Custom-Speicher sind leer.

| Slot | Effekt-Enum | Antwort | Blöcke | Nutzdaten |
|---|---|---|---|---|
| 0 | 51 (Custom 1) | `009200000800000000` | 8/8 | leer |
| 1 | 52 (Custom 2) | `009200010800000000` | 8/8 | leer |
| 2 | 53 (Custom 3) | `009200020800000000` | 8/8 | leer |
| 3 | 54 (Custom 4) | `009200030800000000` | 8/8 | leer |
| 4 | 55 (Custom 5) | `009200040800000000` | 8/8 | leer |

Berichte: `research/runs/keyboard-picture-matrix-probe-20260902-19*.md`.

Damit steht ein scharfer Kontrast fest:

- `0x92` — Kommando **und Parameter** werden geparst, Blocktransfer läuft.
- `0x88`/Selektor 0 — Kommando wird geechot, die Datenfelder bleiben leer.

Interpretationsgrenze: Ein leerer Nutzdatenblock beweist nicht zwingend, dass
`0x92` inhaltlich implementiert ist. Denkbar bleibt eine generische
Blocktransfer-Maschine, die einen genullten Puffer ausgibt. Dass jedoch der
Slot-Index korrekt zurückgespiegelt wird, spricht deutlich für eine echte
Auswertung des Pakets. Ein leerer Slot erklärt zugleich, warum die
Custom-Effekte 51 bis 55 nichts zeigen würden.

Der nächste Schritt wäre der Setter `0x12`: eine Picture-Matrix in Slot 0
schreiben und Effekt `51` wählen. Das ist ein Schreibzugriff mit einem bisher
nie gesendeten Kommandobyte in den LED-Profilspeicher — nicht in den
Firmware-Code und nicht über den Flash-Report `0x5A`. Er ist durch
Zurückschreiben des gelesenen Nullzustands umkehrbar, bedarf aber der
ausdrücklichen Zustimmung des Besitzers.

## Ausschluss 5: Die alte Oberfläche war nicht zonen-spezifisch

`ucKeyboard.dll` enthält keine einzige Referenz auf `ZoneRgb`. Die Effektliste
in `RgbPageViewModel` — Static, Pulse, Wave, Reactive, Marquee, Ripple, Cycle,
Droplet, Hedge, Spiral — wird für jedes ITE-Gerät gleich angezeigt, und
`changeLedType()` ruft ausschliesslich `FusionLightService.SetLightEffect(index)`.

Gleichzeitig existiert in `KeyboardDomainLogic.Helpers` ein getrennter
Zonenpfad: `saveCurrentProfileLightEffectZoneRgb()` speichert Effekt, Speed und
Richtung, `LoadProfileLightEffectZoneRgb()` verwirft sie beim Laden und schreibt
nur die drei Zonenfarben. Gigabyte wusste also, dass Effekte bei ZoneRgb nicht
wiederherstellbar sind, hat die Auswahl in der Oberfläche aber stehen gelassen.
Das stützt die Leakage-Deutung aus `RGB-WEB-FINDINGS.md`.

## Firmware-Image: Herkunft bestätigt, Disassembly offen

`third-party/vendor/keyboard-firmware-19.0.4-static/` enthält das echte Image:

- `SHFU.ini`: `HWID=USB\VID_1044&PID_7A41`, `BOOTDEVICECHIPID=8298`,
  `UPDATEFILESIZEKB=120`, Codeprüfsumme über `0x2000` bis `0x1E000`.
- `docking_b.bin`, 122 880 Byte. Bei `0x2010` steht unverschlüsselt
  `Gigabyte Fusion_8298:1.9.0.4` — exakt die live gelesene Version 19.0.4.
  Ab `0xB488` ist alles `0xFF`, der belegte Codebereich ist also `0x2000` bis
  `0xB487`, rund 38 KB.

Ein Kommando-Dispatcher liess sich nicht lokalisieren. Suchen nach dem
8051-Muster `CJNE A,#imm` (`0xB4`) für alle bekannten Kommandobytes ergaben null
Treffer, und die Tabellen bei `0x000` und `0x400` passen weder zu 8051 noch zu
ARM Cortex-M noch zu einem sauberen LE32-Zeigerformat. Der ITE-8298-Kern ist
damit nicht identifiziert; eine belastbare Disassembly wäre ein eigenes,
aufwendiges Teilprojekt.

## Sicherheitsbefund: Report-ID `0x5A` ist der Flash-Kanal

`SHFU.ini` setzt `REPORTID=90`. Dezimal 90 ist `0x5A` und damit genau der
17-Byte-Feature-Report auf `MI_02/COL_07`, den die HID-Inventur gefunden hat.
Dieser Kanal ist der Firmware-Update-Pfad des ITE-Flashers.

Harte Ausschlussregel: Auf Report-ID `0x5A` wird nie geschrieben. Der bereits
durchgeführte passive Lesevorgang bleibt die einzige Berührung.

## Verbleibende Hypothesen

1. **Firmwareverhalten geändert (führend).** Das Image datiert vom September
   2023, der bewiesen unveränderte Host-Code vom Juli 2023. Da die Software
   konstant ist, bleibt die Firmware die einzige Variable. Prüfbar nur durch
   Disassembly oder durch ein Downgrade; Letzteres ist ausgeschlossen.
2. **Persistenter Zonen-Latch.** Zeitlich fiel jeder Effekttest nach dem ersten
   Zonen-Write (2026-09-01 19:22), nie zuvor. Möglich ist, dass ein Zonen-Write
   den Controller dauerhaft in den Modus Zone-Static versetzt. Gegenargument:
   Der Zustand überlebt Neustarts, ein blosser Reboot genügt also nicht; es
   bräuchte ein Rücksetzkommando, und `0x08` mit Selektor 0 wäre genau das.
   Billig prüfbar durch eine Abfrage `0x88`/sel 0 als erste HID-Aktion nach
   einem Kaltstart.
3. **Zweiter Lichtpfad.** Effekte liefen früher nicht über `0x08`/sel 0, sondern
   über die Picture-Matrix (`0x12`, Slots Custom1 bis Custom5). Prüfbar mit dem
   reinen Lesebefehl `0x92`.
4. **Host-gerenderte Animation.** Für unsere Anwendung ist das der Weg, der
   garantiert funktioniert: Die drei Zonenfarben sind frei und verifiziert
   schreibbar. Gigabytes 65 ms Wartezeit pro Zone begrenzen auf rund
   5 Zonenframes pro Sekunde; ob kürzere Abstände zulässig sind, ist ungetestet.

## Gelöst auf der Anwendungsebene: host-gerenderte Effekte

Unabhängig von der offenen Firmwarefrage ist das eigentliche Ziel erreicht.

Gigabytes 65 ms Wartezeit nach jedem Zonen-Write sind **keine**
Firmwareanforderung. Gemessen wurde mit je sechs verifizierten Schreibvorgängen
auf Zone 1 bei 65, 40, 25, 15, 10 und 5 ms: **alle sechs Intervalle lieferten
6 von 6 korrekten Rückmeldungen**, auch 5 ms. Damit sind rund 20 bis 66
Dreizonen-Bilder pro Sekunde möglich statt der bisher dokumentierten 5,1. Die
tatsächliche Grenze ist die Timerauflösung von Windows, nicht das Gerät.

Der Modus `--test-keyboard-host-effects` hat Breathing, einen vollen
Farbspektrum-Durchlauf und eine wandernde Dreizonen-Welle gerendert. **Der
Besitzer hat bestätigt, dass die Effekte sichtbar angezeigt wurden.** Danach
wurden alle drei Ausgangszonen exakt wiederhergestellt und verifiziert.

Verwendet werden ausschliesslich die beiden auf diesem Gerät bewiesenen
Kommandos: Zonensetter `0x08` Selektor 1–3 und Zonengetter `0x88`. Kein
globales Effektkommando, kein neues Kommandobyte, keine Picture-Matrix.

Für die systematische Beurteilung folgt `--interactive-host-effect-test`
(`tools/Start-HostEffectTest.cmd`) mit zehn Effekten — Static, Breathing, Pulse,
Colour cycle, Rainbow marquee, Wave, Marquee, Rotate, Raindrop, Fade sweep.
Jeder Effekt läuft dauerhaft, bis Enter gedrückt wird; eingegebener Text wird
als Beobachtung gespeichert, `/stop` beendet vorzeitig.

Der vollstaendige interaktive Durchlauf ist erledigt: **Alle zehn Effekte wurden
vom Besitzer als funktionierend bestaetigt.** Der Durchsatz lag konstant bei
21,2 bis 21,4 Dreizonen-Bildern pro Sekunde, begrenzt durch die
Windows-Timeraufloesung, nicht durch das Geraet. Alle drei Ausgangszonen wurden
exakt wiederhergestellt und verifiziert. Bericht:
`research/runs/keyboard-host-effect-interactive-20260902-192953.md`.

Damit ist die Firmwarefrage von der Produktfrage entkoppelt: Die Anwendung
braucht den Firmware-Effektmotor nicht.

## Live-Ergebnis `0x12`: Schreiben scheitert, aber Custom 1 wird sichtbar wirksam

Der Schreibtest lief mit Slot 0. Ergebnis in zwei Teilen.

**Teil 1 — der Schreibvorgang wurde nicht angenommen.** Geschrieben wurden alle
128 Slots als `00 FF 00 00`, uebertragen als Header `0x12` plus acht
65-Byte-Ausgangsreports. Das anschliessende Ruecklesen lieferte jedoch exakt den
gespeicherten Ausgangszustand:

| Groesse | SHA-256 |
|---|---|
| Sollzustand (rot) | `345EDFE9A87D73986BAED930C81D53DF5743A58FF99652DE42418C1D2BE296B4` |
| Rueckgelesen | `076A27C79E5ACE2A3D47F9DD2E83E4FF6EA8872B3C2218F66C92B89B55F36560` |

Der rueckgelesene Hash ist identisch mit dem leeren Ausgangs-Slot: 0 von 512
Byte ungleich null. Die Matrix blieb also leer.

**Teil 2 — die Effektauswahl hatte erstmals eine sichtbare Wirkung.** Nach
`00 08 00 33 05 32 00 01 8C` (Effekt `51` = Custom 1, Speed 5, Brightness 50,
Richtung 1) berichtete der Besitzer: *"Es ist nur alles ausgegangen und nichts
leuchtet."* Die Tastatur wurde dunkel.

Das ist das erste Mal ueberhaupt, dass ein `0x08`/Selektor-0-Paket auf diesem
Geraet etwas Sichtbares bewirkt hat. Der globale Getter meldete weiterhin
`008800000000000000`, also alles null — der Getter ist damit als
Wirkungsnachweis endgueltig unbrauchbar, denn hier stand eine sichtbare
Zustandsaenderung *neben* einer Nullantwort.

Die naheliegende Deutung: Die Firmware hat auf Custom 1 umgeschaltet und dessen
Picture-Matrix gerendert. Da diese leer ist, entspricht das Schwarz auf allen
Tasten. Das wuerde bedeuten, dass der Effektmotor sehr wohl arbeitet und die
bisherigen Standardeffekte aus einem anderen Grund unsichtbar blieben.

Zwei Ursachen sind durch diesen Lauf noch nicht getrennt:

1. die Effektauswahl `51`, oder
2. die acht Ausgangsreports des gescheiterten Schreibvorgangs.

Beide gingen dem Dunkelwerden voraus. Der naechste Test isoliert das.

Das Zurueckrollen funktionierte vollstaendig: Slot 0 und alle drei Zonenfarben
wurden exakt wiederhergestellt und verifiziert. Das Schreiben von Zonenfarben
holte die Beleuchtung zurueck, was fuer sich genommen bereits zeigt, dass ein
Zonen-Write den Controller aus dem dunklen Zustand zurueckholt.

Bericht: `research/runs/keyboard-picture-matrix-write-20260902-193421.md`.

## Isolationslauf und ein Fehler im Testaufbau

Der Isolationstest setzte vor jedem Schritt alle drei Zonen auf verifiziertes
Weiss und sendete danach genau ein Effektpaket, ohne jedes
Picture-Matrix-Kommando. Ergebnis bei allen fuenf Effekt-IDs `51`, `1`, `52`,
`2` und `8`: Der Besitzer sah einen weissen Durchlauf von links nach rechts —
das ist unser eigener Zonen-Write mit 65 ms Abstand — und unmittelbar danach
wurde die Tastatur dunkel.

Damit ist die erste Frage beantwortet: Das Dunkelwerden kam **nicht** von den
Ausgangsreports des gescheiterten `0x12`-Schreibvorgangs, sondern von der
Effektauswahl selbst.

**Fehler im Testaufbau.** Alle Pakete enthielten Farbbyte `0`. In
`FusionLightColor` ist `Black = 0`. Der Test hat also fuenfmal "Effekt in der
Farbe Schwarz" angefordert, und das Geraet hat das korrekt gerendert. Die
Dunkelheit ist damit **kein** Beleg fuer einen toten Effektmotor, sondern eher
das Gegenteil: Das Geraet hat den Farbparameter ausgewertet.

Wichtig ist auch, was dabei nebenbei feststand: Der globale Getter meldete in
jedem Schritt `008800000000000000`, waehrend sichtbar eine Zustandsaenderung
stattfand. Der Getter ist als Wirkungsnachweis damit endgueltig unbrauchbar.
Alle frueheren Schlussfolgerungen, die sich auf seine Nullantwort stuetzten,
sind entsprechend zu entwerten.

Die Zonenregister behielten waehrend der Dunkelheit ihre Werte: Zone 1 las sich
weiterhin als `#FFFFFF` bei Brightness `50`. Die Beleuchtung folgt im
Effektmodus also nicht mehr den Zonenwerten. Ein anschliessender Zonen-Write
holt sie zurueck.

### Neue Deutung der alten Effekttests

Der interaktive Lauf vom 2026-09-01 verwendete Palette `02` = Gruen, zum Beispiel
`0008000105320201BC` fuer Static. Die Tastatur stand zu diesem Zeitpunkt bereits
auf statischem Gruen. Die Beobachtung des Besitzers lautete woertlich: "Macht es
grade war aber von vorher schon so eingestellt."

Ein korrekt gerenderter gruener Effekt waere von statischem Gruen visuell nicht
zu unterscheiden gewesen. Die damalige Schlussfolgerung "Effekte wirken nicht"
beruht damit auf einem Aufbau, der Wirkung und Nichtwirkung gar nicht trennen
konnte.

Bericht: `research/runs/keyboard-effect-isolation-20260902-193655.md`.

### Aufloesung: nur Palette 0 wird ausgefuehrt

`--test-effect-palette` setzte dieselbe verifizierte weisse Ausgangslage, sendete
aber kraeftige Farben. Ergebnis: **in allen fuenf Schritten blieb alles weiss.**

| Effekt | Palette | Sichtbares Ergebnis |
|---|---|---|
| `1` Static | `1` Rot | keine Aenderung |
| `1` Static | `4` Blau | keine Aenderung |
| `2` Breathing | `1` Rot | keine Aenderung |
| `3` Wave | `8` Random | keine Aenderung |
| `8` Neon | `8` Random | keine Aenderung |

Effekt `1` kommt in beiden Laeufen vor, und nur das Farbbyte unterscheidet sich:
mit Palette `0` wurde es dunkel, mit Palette `1` aenderte sich nichts. Damit ist
der Mechanismus isoliert:

- **Palette `0` (Schwarz) wird ausgefuehrt und schaltet die Beleuchtung ab.** Das
  Paket wird also empfangen und ausgewertet.
- **Jeder andere Palettenwert ist ein No-op.** Keine Farbe, keine Animation,
  gleichgueltig ob statische oder animierte Effekt-ID.
- Im abgeschalteten Zustand behalten die Zonenregister ihre Werte; die LEDs
  folgen ihnen nur nicht mehr. Ein Zonen-Write holt die Beleuchtung zurueck.

Bericht: `research/runs/keyboard-effect-palette-20260903-112527.md`.

## Ergebnis der Untersuchung

Auf `1044:7A41` mit Firmware `19.0.4` ist das globale Effektkommando
`0x08`/Selektor 0 auf eine einzige funktionierende Aufgabe reduziert: abschalten
per Palette `0`. Der Effektmotor rendert weder Farben noch Animationen.

Das ist jetzt mit einem tragfaehigen Aufbau belegt — verifizierte weisse
Ausgangslage, isoliertes Einzelkommando, kraeftige Farben — und nicht mehr mit
dem Getter, der sich als wertlos erwiesen hat.

Da Gigabytes Host-Code bitgleich mit der Fassung von 2023 ist, die der Besitzer
funktionierend erinnert, bleibt eine Verhaltensaenderung der Firmware die
fuehrende Erklaerung. Ein Beweis waere nur ueber eine Disassembly des
ITE-8298-Kerns oder ein Firmware-Downgrade zu fuehren; Letzteres ist
ausgeschlossen.

Fuer das Produkt ist die Frage ohne Bedeutung: Zehn host-gerenderte Effekte sind
vom Besitzer bestaetigt, bei rund 21 Bildern pro Sekunde.

## Nächste sichere Schritte

1. Reiner Lesetest `0x92` (Picture-Matrix, Slot 0) — offizieller Getter, analog
   zum bereits erledigten `0x8D`-Matrixlesen.
2. Kaltstart-Erstabfrage `0x88`/sel 0 vor jedem anderen HID-Zugriff.
3. Test der minimal zulässigen Zonen-Schreibfrequenz als Grundlage
   host-gerenderter Effekte.
4. Nur bei Bedarf und nach ausdrücklicher Zustimmung: Disassembly-Teilprojekt
   für den ITE-8298-Kern.
