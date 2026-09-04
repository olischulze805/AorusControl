# Lüfter-, Leistungs- und GPU-Steuerung – AORUS 5 SE4 / FB0F

Stand: 2026-09-03

## Ergebnis in Kurzform

Für dieses Notebook sind vier getrennte Steuerungsebenen relevant. Sie dürfen in der Anwendung nicht unter einem einzigen „Eco/Performance“-Schalter vermischt werden:

1. **Lüfterprofil:** steuert, wie aggressiv die beiden Lüfter auf Temperatur reagieren.
2. **Windows-Leistungsmodus:** verändert den Kompromiss zwischen Energiebedarf und Reaktionsleistung des Betriebssystems.
3. **Gigabyte-Systemleistung:** verändert modellabhängig CPU-/GPU-Leistungsgrenzen und Dynamic Boost.
4. **Grafikmodus:** Optimus/Hybrid, erzwungenes Abschalten der NVIDIA-GPU oder – falls tatsächlich unterstützt – ein MUX-/dGPU-Modus.

Lüfterprofile und der Windows-Leistungsmodus sind die besten nächsten Funktionen. Das vollständige Abschalten der RTX 3070 ist technisch wahrscheinlich möglich, benötigt aber einen eigenen abgesicherten Ablauf und darf nicht als einfacher Toggle umgesetzt werden.

## Verwendete lokale Quelle

Das aktuelle offizielle Gesamtpaket `GIGABYTE Control Center_2026_Jul_release_All_Setup_26.08.03.01.exe` enthält das signierte Notebook-Paket `GBT_Notebook_26.06.23.01.exe`. Dieses wurde nur statisch entpackt und dekompiliert; nichts daraus wurde installiert oder gestartet.

Relevante Dateien aus dem Paket sind gültig von `GIGA-BYTE TECHNOLOGY CO., LTD.` signiert:

| Datei | SHA-256 |
| --- | --- |
| `ucNotebook.dll` | `24DE360044E03E1D52592350606D5BD644B5AF2ABE43920E7BA52123C68C65C9` |
| `Ai Power Gear.exe` | `497B0CA9BF3EA78A1D15339803910E744165A945E76AAAEFA0E97952339D6566` |
| `AiPowerGearService.exe` | `9926DAFC9D10116A3429D52B834F2A6D402BBB883618E49D221653232879EF31` |
| `CoreX15.dll` | `7906F4206624F84E0F9F8C3F2060AC3208A3A3C79ED4DB9F1EF44A9AC8A2B222` |
| `NvFunc.dll` | `9DB50EE519CADC3C914A162258CADEBB6F56A02883000F8CB6F9D8568DDBA542` |

Damit beruhen die folgenden Protokollangaben auf Gigabytes eigener aktueller Implementierung und nicht nur auf Vermutungen aus dem gemeinsamen WMI-Schema.

## 1. Lüftersteuerung

### Erste Live-Bestandsaufnahme vom 2026-09-03

Der erste erhöhte, ausschließlich lesende Lauf auf FB0F lieferte:

- CPU 56 °C, GPU 47 °C
- CPU-RPM-Rohwert 22023, byte-getauscht 1878 RPM
- GPU-RPM-Rohwert 51719, byte-getauscht 1994 RPM
- CPU- und GPU-Duty jeweils Rohwert 66
- Fixed, Step und Auto jeweils 0
- `GetFixedFanSpeed` und `GetFanAdjustStatus` jeweils 57
- vollständige 15-Punkte-Kurve: `(0,57)`, `(50,68)`, `(53,80)`, `(56,91)`, `(59,103)`, `(62,114)`, `(65,125)`, `(68,137)`, `(71,148)`, `(74,160)`, `(77,171)`, `(80,183)`, `(83,194)`, `(86,206)`, `(89,229)`

Damit ist die 0-bis-229-Rohskala auf diesem konkreten Gerät live bestätigt. `Duty=66` ist ebenfalls ein Rohwert und nicht automatisch 66 Prozent. Punkt 0 mit Temperatur 0 und Wert 57 ist wahrscheinlich Grund-/Startleistung; die genaue Firmwareinterpolation bleibt noch zu prüfen.

Die Wiederholungsmessung bestätigte die Dynamik: bei CPU 59 °C meldeten beide Duty-Getter 93 und die Lüfter 2557/2698 RPM; nach dem Rückgang auf 51 °C fiel Duty auf 84 und die Lüfter sanken auf 2414/2545 RPM. Beide Duty-Werte waren in jeder Probe identisch. Das aktuelle Profil führt CPU- und GPU-Lüfter somit zumindest in diesem Temperaturbereich gemeinsam. Die Kurve war in beiden erhöhten Läufen vollständig identisch. Bericht: `research/runs/thermal-power-inspection-20260903-134200.md`.

Der erste Normal-Schreibtest bestätigte die Herstellerfolge und den Recovery-Pfad: `fixed=0`, `step=0`, `auto=0`, `thermal=0`; Festwert 57, Duty 66 und alle Kurvenpunkte blieben unverändert. Bericht: `research/runs/fan-normal-change-20260903-134611.md`.

Der vorübergehende Quiet-Test bestätigte `fixed=0`, `step=0`, `auto=0`, `thermal=1`. Bei 51–52 °C wurden Duty und beide Lüfter nach dem Umschalten auf 0 gesetzt; in 12 Sekunden stiegen CPU/GPU nur um ungefähr 1 °C. Ein einzelner erster GPU-RPM-Wert von 7000 bei Duty 0 ist als nicht plausibler Übergangswert zu behandeln, nicht als echte Drehzahl. Vier Folgeproben ergaben 0 RPM. Normal wurde danach verifiziert wiederhergestellt. Bericht: `research/runs/fan-quiet-test-20260903-134826.md`.

Der vorübergehende Gaming/Power-Test bestätigte `auto=1` bei allen anderen Modusfeldern 0. Bei 51/48 °C blieben Duty 66 und ungefähr 1880/2000 RPM über fünf Proben stabil; dieses Profil erzwingt im Leerlauf also keine höhere Drehzahl, sondern schaltet eine andere automatische Firmwarestrategie frei. Normal wurde anschließend verifiziert wiederhergestellt. Bericht: `research/runs/fan-gaming-test-20260903-135023.md`.

Der erste Maximum-Lauf bestätigte `fixed=1`, `step=1`, Festwert und GPU-Duty 229. Die Drehzahlen stiegen auf 5208/5472 RPM und die Temperaturen fielen von 51/48 auf 48/45 °C. Beim integrierten Rücklesen meldete die erste Programmversion Code 6, obwohl die vier Modusschalter bereits auf Normal standen. Ursache war ein Softwarefehler im Verifier: `GetGPUFanDuty` ist außerhalb des Fixed-Modus ein zeitabhängiger Livewert der Kurve und keine persistente Einstellung; er darf beim Rollback nicht mit einem früheren Snapshot identisch sein müssen. Der unabhängige Normal-Rettungsbefehl und eine neue vollständige Inspektion bestätigten Normal, Festwert 57, Duty 66, unveränderte Kurve und etwa 1891/2013 RPM. Es gab keinen verbleibenden Hardwarefehler. Berichte: `research/runs/fan-maximum-test-20260903-135300.md`, `research/runs/fan-normal-change-20260903-135327.md`, `research/runs/thermal-power-inspection-20260903-135342.md`.

Nach der Korrektur bestätigte die Wiederholung Maximum erneut mit Rohwert 229 und bis zu 5220/5417 RPM. Der integrierte Rückweg stellte diesmal alle persistenten Moduswerte, Festwert 57 und den danach wieder von der Kurve bestimmten Duty-Wert 66 verifiziert her. Bericht: `research/runs/fan-maximum-test-20260903-135458.md`.

Die obere Fixed-Skala ist ebenfalls live vermessen: Rohwert 160 liefert ungefähr 4000/4190 RPM, 194 ungefähr 4600/4800 RPM und 229 ungefähr 5220/5420 RPM. Beide Kanäle nahmen jeden Zielwert exakt an; die Reaktion ist in diesem Bereich monoton und annähernd linear. Der vollständige Originalzustand wurde wiederhergestellt. Bericht: `research/runs/fan-fixed-scale-test-20260903-135853.md`.

Die untere Fixed-Skala bestätigt denselben Verlauf: 57 ≈ 1640/1750 RPM, 68 ≈ 1925/2045 RPM, 91 ≈ 2510/2665 RPM, 114 ≈ 3040/3210 RPM und 137 ≈ 3520/3695 RPM. Während des kurzen Leerlauftests blieben CPU/GPU bei höchstens 48/45 °C. 57–229 ist damit der praktisch bestätigte Stellbereich; 57 ist dennoch nicht als dauerhaft sicherer Festwert unter hoher Last bewiesen und benötigt in einer App einen Temperatur-Failsafe. Bericht: `research/runs/fan-fixed-scale-test-20260903-140107.md`.

Dynamic mit unveränderter Originalkurve wurde ebenfalls bestätigt: `step=1`, übrige Modusfelder 0. Bei 48–49 °C wählte die Firmware stabil Rohwert 68 und ungefähr 1930/2045 RPM. Daraus folgt, dass `(0,57)` kein normal interpolierter Temperaturpunkt ist; knapp unter 50 °C wird bereits `(50,68)` verwendet. Normal/Duty 66 wurde danach wiederhergestellt. Bericht: `research/runs/fan-dynamic-test-20260903-140240.md`.

Der konservative Kurvenschreibtest hob ausschließlich Punkt 1 von `(50,68)` auf `(50,80)` an. Der Getter bestätigte die Änderung und alle 14 unveränderten Punkte; Dynamic verwendete sofort Rohwert 80 mit ungefähr 2240/2350 RPM. Danach wurden alle 15 Originalpunkte und der vollständige Normalzustand exakt wiederhergestellt. Setter, Readback und Kurvenrollback sind damit praktisch bewiesen. Bericht: `research/runs/fan-curve-write-test-20260903-140517.md`.

Die unabhängige Abschlussinspektion nach sämtlichen Tests bestätigte den sauberen Endzustand: `fixed=0`, `step=0`, `auto=0`, `thermal=0`, Festwert und FanAdjust 57, Duty 66, ungefähr 1890/2000 RPM und alle 15 Originalpunkte exakt von `(0,57)` bis `(89,229)`. Bericht: `research/runs/thermal-power-inspection-20260903-140823.md`.

### Was Gigabyte anbietet

Die aktuelle Notebook-Komponente definiert diese Modi:

| Interner Wert | Modus | Verhalten |
| --- | --- | --- |
| 1 | Game/Power | automatische, aggressivere Firmware-Regelung |
| 2 | Eco/Quiet | ruhigeres Profil über NVIDIA-Thermalziel und Firmwarezustand |
| 3 | Normal | normale Firmware-Regelung |
| 4 | Turbo/Max | feste maximale Lüfterleistung |
| 5 | Fixed | vom Benutzer festgelegte Leistung beider Lüfter |
| 6 | Dynamic | benutzerdefinierte Kurve mit 15 Stützpunkten |

Gigabytes offizielle Anleitung nennt Power/Energy-Saving/Normal/Turbo sowie Fixed und Dynamic und dokumentiert 15 anpassbare Kontrollpunkte.

### Bestätigte Firmware-Schnittstellen

Auf FB0F sind über `root\WMI` bereits die passenden Getter/Setter registriert. Aus der DSDT und der aktuellen Gigabyte-Komponente sind insbesondere bestätigt:

- `Get/SetAutoFanStatus` (`0x71`)
- `Get/SetFixedFanStatus` (`0x6A`)
- `Get/SetFixedFanSpeed` (`0x6B`)
- `Get/SetStepFanStatus` (`0x67`)
- `Get/SetFanIndexValue` (`0x68`) mit Index, Temperatur und Wert
- `Get/SetGPUFanDuty` (`0x47`)
- `GetCPUFanDuty` (`0x46`)
- `GetFanSpeed` (`0x7D`)
- Temperatur- und RPM-Getter, die unsere Telemetrie bereits erfolgreich liest

### Wichtig: Rohwert ist nicht immer Prozent

Bei älteren Notebook-Generationen rechnet Gigabyte einen UI-Prozentwert auf einen Rohbereich bis **229** um. Das AORUS 5 wird in der aktuellen Komponente über den älteren Notebook-Pfad behandelt. Daher darf unsere Anwendung nicht blind `100` als Maximum oder `50` als 50 Prozent interpretieren. Vor festen Werten müssen wir die Zuordnung live und reversibel vermessen.

### Sichere erste Version

Zunächst nur vier Herstellerprofile anbieten:

- Leise
- Normal
- Gaming/Power
- Maximal

Die Anwendung soll vor jeder Änderung alle Statuswerte und die 15 Kurvenpunkte sichern, die neue Einstellung zurücklesen und bei Fehlern auf **Normal/Firmwaresteuerung** zurückfallen. Feste Drehzahl und eigene Kurven kommen erst danach. Eigene Kurven müssen monoton sein, Mindestwerte besitzen und bei hohen Temperaturen zwingend bis zum sicheren Maximum ansteigen.

## 2. Windows-Leistungsmodus

Dieser Modus ist von der Lüfterregelung unabhängig. Gigabytes Komponente verwendet dafür die dokumentierte Windows-Overlay-API und kennt sinngemäß:

- Beste Energieeffizienz
- Ausbalanciert
- Beste Leistung

Das ist ein vergleichsweise sicherer App-Baustein. Wir können ihn mit dem echten Windows-Zustand synchronisieren, getrennte Wünsche für Netz- und Akkubetrieb speichern und jederzeit auf Ausbalanciert zurücksetzen. Er ersetzt keine Gigabyte-spezifischen CPU-/GPU-Limits.

Der AC-Rundlauf am 2026-09-03 bestätigte alle drei GUIDs mit exaktem Readback: Energieeffizienz `961cc777-2547-4f9d-8174-7d86181b8a7a`, Ausbalanciert `00000000-0000-0000-0000-000000000000`, Beste Leistung `ded574b5-45a0-4f42-8737-46345c09c238`. Anschließend wurde der ursprüngliche ausgeglichene AC-Zustand wiederhergestellt. Kein Gigabyte-Firmwarebefehl war beteiligt. Bericht: `research/runs/windows-power-overlay-test-20260903-135645.md`.

## 3. Gigabyte-Systemleistung und Dynamic Boost

Eine weitere Schicht in der aktuellen Komponente heißt `SystemPerformance` und hat Eco, Balance und Performance, jeweils für Netz- und Akkubetrieb. Sie ruft modellabhängige CPU- und GPU-Leistungsfunktionen auf:

- Eco: niedrigste CPU-/GPU-Leistungsstufe
- Balance: mittlere Stufe
- Performance: höchste Stufe
- zusätzlich Benachrichtigungen an Lüfter- und System-Power-Logik

Die CPU-Seite führt in Gigabytes native/undokumentierte OC-/Power-Bibliotheken. Die GPU-Seite kann `SetNvPowerConfig`, NVAPI, Dynamic Boost und weitere modellabhängige Parameter verwenden. Die Methodennamen existieren im gemeinsamen Schema, aber das beweist noch nicht, dass genau diese neue Schicht auf dem AORUS 5 SE4 freigeschaltet ist.

**Folgerung:** Erst die Fähigkeiten und Rohwerte live nur lesen. Diese Leistungsgrenzen nicht aus einem anderen Gigabyte-Modell übernehmen. Wenn der Capability-Test negativ oder mehrdeutig ist, beschränken wir uns auf Windows-Leistungsmodus plus Lüfterprofil.

Der Capability-Test ist auf FB0F negativ: `getAiPowerCtlCapability` und mehrere zugehörige Power-/Turbo-Getter werden abgewiesen. Diese Systemleistungsschicht wird deshalb nicht implementiert. Bestätigte, getrennte Windows-Overlays und Lüfterprofile decken die sichere Funktionalität ab.

## 4. Grafikmodi und „Eco“

### Was Optimus wirklich macht

Das AORUS 5 SE4 wird von Gigabyte offiziell mit NVIDIA Optimus ausgewiesen. Im Hybridbetrieb treibt die Intel Iris Xe das interne Display. Bei einer anspruchsvollen Anwendung rendert die RTX und übergibt die fertigen Bilder an die Intel-Grafik; wenn keine Anwendung oder Anzeige die RTX benötigt, kann Optimus sie abschalten.

„Nur CPU rendern“ ist daher nicht die richtige Beschreibung. Im Sparbetrieb rendert die **integrierte Intel-GPU (iGPU)**. Die CPU führt weiterhin das Programm aus, übernimmt aber nicht das normale Grafikrendering.

### Aktueller Zustand dieses Geräts

Read-only festgestellt:

- internes BOE-Display: Intel Iris Xe, 1920×1080
- NVIDIA GeForce RTX 3070 Laptop GPU: vorhanden und betriebsbereit
- `nvidia-smi`: P5, ungefähr 16,8 W, Anzeige nicht aktiv
- NVIDIA Display, HD Audio, Virtual Audio und Platform Controllers: vorhanden

Das ist Hybrid-/Optimus-Topologie: Intel treibt das interne Panel, während die RTX momentan trotzdem wach ist. Ein Prozess, ein NVIDIA-Hilfsdienst oder ein an die RTX verdrahteter externer Anschluss kann sie wach halten.

Eine weitere read-only Prüfung fand nur das interne BOE-Panel und keinen externen Monitor. Gleichzeitig listete NVIDIA unter anderem AppControl, Windows-Oberflächen, Edge WebView, UniGetUI, WhatsApp, Codex und Claude als RTX-Prozesse. Einige Windows-Oberflächen besitzen bereits die gespeicherte Mindestenergie-/iGPU-Präferenz, erscheinen aber dennoch in der laufenden NVIDIA-Liste. Per-App-Präferenzen wirken typischerweise erst beim nächsten Start und sind keine Garantie für physisches Abschalten. Bericht: `research/runs/gpu-routing-inspection-20260903-140600.md`.

### Gigabytes echter GPU-Eco-Ablauf

Die aktuelle signierte Gigabyte-Implementierung macht für „GPU aus“ wesentlich mehr als ein normales Windows-Grafikprofil:

1. externe Monitore prüfen;
2. mit `nvidia-smi` Prozesse suchen, welche die NVIDIA-GPU verwenden;
3. den Benutzer die Programme schließen lassen;
4. NVIDIA-HD-Audio und `NVIDIA Platform Controllers and Framework` behandeln;
5. WMI `SetNvPowerConfig(3)` für Ausschalten aufrufen;
6. Zustand prüfen und bei Fehler zurückrollen.

Für Einschalten verwendet sie `SetNvPowerConfig(4)`, stößt mit `pnputil /scan-devices` eine Gerätesuche an und aktiviert die zugehörigen NVIDIA-Geräte wieder. Der ältere Dienst enthält zusätzlich PnP-Disable/Enable-Logik.

### Warum ein einfacher Geräte-Manager-Schalter nicht reicht

„Deaktiviert“ im Geräte-Manager bedeutet nicht zwangsläufig physisch stromlos. Außerdem können dabei ein externer Bildschirm oder HDMI-Audio verschwinden, GPU-Programme abstürzen und nicht gespeicherte Arbeit verloren gehen. Gigabytes eigener mehrstufiger Ablauf ist der Beleg, dass diese Funktion Transaktions-, Prüf- und Wiederherstellungslogik benötigt.

### MUX / dGPU-only

Die Komponente kennt außerdem `SetPEG2orSG2` für diskrete bzw. hybride Display-Routen und fordert bei entsprechenden Wechseln einen Neustart. Das gemeinsame WMI-Schema allein beweist aber weder einen physischen MUX im AORUS 5 SE4 noch die Bedeutung aller Werte auf FB0F. Gigabytes öffentliche AORUS-5-SE4-Spezifikation nennt Optimus, nicht Advanced Optimus oder einen MUX-Schalter. Deshalb gilt dGPU-only vorerst als **unbestätigt** und wird nicht implementiert, bevor der live gelesene Capability-Zustand und das Verhalten auf diesem Gerät eindeutig sind.

Der erste Live-Lauf verschärft diese Bewertung: `GetNvPowerConfig`, `GetPEG2orSG2` und `getAiPowerCtlCapability` sind zwar im installierten gemeinsamen MOF sichtbar, werden vom FB0F-Geräteobjekt aber mit „Ungültiges Objekt“ abgewiesen. `GetPEGorSG` antwortet mit Rohwert 66 statt einem plausiblen 0/1-Zustand (siehe die Nachprüfung unten: dieser Wert ist ein Artefakt). `GetEcValueBoostStatus`, `GetSmartCool`, `GetSmartTurbo` und `GetTurboMode` werden ebenfalls abgewiesen. Dagegen antworten `GetDynamicBoostStatus=0`, `GetWhisperMode=0` und `GetNvThermalTarget=0` gültig. Bis gegenteilige modellspezifische Evidenz vorliegt, werden physisches GPU-Eco und MUX daher als **auf FB0F nicht unterstützt** behandelt und nicht geschrieben.

Laufbericht: `research/runs/thermal-power-inspection-20260903-134039.md`.

## Vorgeschlagene App-Struktur

### Seite „Kühlung“

- CPU-/GPU-Temperatur, RPM und Duty live
- Leise, Normal, Gaming/Power, Maximal
- Button „Firmware/Normal wiederherstellen“ immer sichtbar
- später: fester Wert für beide Lüfter
- zuletzt: Kurveneditor mit 15 Punkten und Sicherheitsgrenzen

### Seite „Leistung“

- Windows-Modus getrennt: Energieeffizienz, Ausbalanciert, Beste Leistung
- Gigabyte-Leistungsstufe nur anzeigen/anbieten, wenn der Capability-Probe sie für dieses Modell bestätigt
- Netz- und Akkubetrieb getrennt behandeln

### Seite „Grafik“

- **Hybrid/Automatisch:** Optimus, Standardempfehlung
- **iGPU-Sparmodus:** RTX wirklich ausschalten, erst nach vollständiger Absicherung
- Liste der Programme und Anzeigen, welche die RTX wach halten
- klare Warnung bei externem Monitor
- kein automatisches Beenden von Programmen
- eigener „RTX wieder einschalten“-Wiederherstellungsweg, der auch ohne laufende Hauptoberfläche funktioniert
- **dGPU-only/MUX:** nur falls später live bestätigt, mit Neustarthinweis

## Implementierungs- und Testreihenfolge

1. **Read-only Capability Report:** alle relevanten Getter, alle 15 Kurvenpunkte, GPU-Power-/PEG-Zustand, Dynamic Boost/Whisper/Thermalziel, Windows-Modus, Displays, PnP-Geräte und NVIDIA-Prozesse protokollieren.
2. **Hersteller-Lüfterprofile:** einzeln und überwacht testen; Temperatur, RPM und Rückgabewerte beobachten; Normal als Rettungszustand.
3. **Windows-Leistungsmodus:** über dokumentierte Windows-API implementieren und verifizieren.
4. **Fixed/Dynamic Fan:** Rohskala zunächst vermessen, dann erst feste Werte und sichere Kurven freigeben.
5. **Gigabyte-Systemleistung:** nur bei bestätigter Modellfähigkeit und reproduzierbaren Originalwerten.
6. **GPU-Eco:** Preflight, Sicherung, Ausschalten, unabhängige Verifikation und robustes Einschalten/Rollback als eine Transaktion.
7. **MUX/dGPU-only:** nur wenn auf FB0F zweifelsfrei vorhanden; ansonsten bewusst weglassen.

## Sicherheitsregeln

- Exakte Allowlist für Modell `AORUS 5 SE`/SE4, BIOS FB0F und EC F00B.
- Nie aus der bloßen Existenz einer gemeinsamen WMI-Methode auf Unterstützung schließen.
- Nur eine Hardwareänderung gleichzeitig; kein paralleles Schreiben.
- Vorherzustand erfassen, jeden Setter zurücklesen, Fehler automatisch zurückrollen.
- GPU-Eco nie mit einem von NVIDIA versorgten externen Monitor oder aktiven GPU-Prozessen erzwingen.
- Anwendungen nie ungefragt beenden.
- Lüfter bei Kommunikationsfehler sofort an Firmware/Normal zurückgeben.
- Wiederherstellungswerkzeug und Protokoll müssen unabhängig von der Oberfläche funktionieren.

## Prüfvermerk vom 2026-09-03 (unabhängige Gegenprüfung)

Die Angaben dieses Dokuments wurden gegen das live registrierte WMI-Schema
geprüft, indem `GB_WMIACPI_Get` und `GB_WMIACPI_Set` vollständig über
`Get-CimClass` ausgelesen und die `WmiMethodId`-Qualifizierer verglichen wurden.
Reine Metadatenabfrage, kein Methodenaufruf.

### Bestätigt

Alle acht angegebenen Methoden-IDs stimmen exakt:

| Methode | Angabe | Gemessene ID |
| --- | --- | --- |
| `Get/SetAutoFanStatus` | `0x71` | 113 = `0x71` |
| `Get/SetFixedFanStatus` | `0x6A` | 106 = `0x6A` |
| `Get/SetFixedFanSpeed` | `0x6B` | 107 = `0x6B` |
| `Get/SetStepFanStatus` | `0x67` | 103 = `0x67` |
| `Get/SetFanIndexValue` | `0x68` | 104 = `0x68` |
| `Get/SetGPUFanDuty` | `0x47` | 71 = `0x47` |
| `GetCPUFanDuty` | `0x46` | 70 = `0x46` |
| `GetFanSpeed` | `0x7D` | 125 = `0x7D` |

Ebenfalls tragfähig ist die Korrektur am Maximum-Test: `GetGPUFanDuty` ist
ausserhalb des Fixed-Modus ein zeitabhängiger Livewert der Kurve. Ihn beim
Rollback mit einem früheren Schnappschuss vergleichen zu wollen war ein Fehler im
Verifier, nicht ein Hardwarefehler — die Diagnose ist korrekt.

### Lücke 1: ein zweiter Kurvenpfad ist unberührt

`GetDeepFan` und `SetDeepFan`, Methoden-ID 96 = `0x60`, tragen je fünf
Geschwindigkeits- und fünf Temperaturparameter (`Speed0`–`Speed4`,
`Temperature0`–`Temperature4`). Das ist eine **zweite, 5-Punkt-Kurve** neben dem
verwendeten 15-Punkt-Pfad über `FanIndexValue`.

Sie ist in diesem Dokument nirgends erwähnt und wurde nie abgefragt. Ob sie auf
FB0F implementiert ist, ist offen; ein reiner Lesetest würde es klären. Solange
das nicht geschehen ist, sollte die Aussage "15 Stützpunkte" als *die* Kurve
relativiert werden.

### Lücke 2: ein gefährlicher Setter ist nicht ausgeschlossen

`TurnOffFan`, Methoden-ID 117 = `0x75`, Signatur `Data:UInt8`, existiert in
`GB_WMIACPI_Set`. Er ist weder in den Sicherheitsregeln erwähnt noch in
`AorusDeviceProfile.FanNormalSetterMethods` gelistet — die Allowlist verhindert
den Aufruf also faktisch, aber unausgesprochen.

**Erledigt.** `AorusDeviceProfile.FanNormalSetterMethods` trägt jetzt einen
ausdrücklichen Ausschlussvermerk für `TurnOffFan` sowie für
`SetCurrentFanStep` und `SetFanModeNotify`, deren Semantik auf FB0F unbestätigt
ist. Die Allowlist bleibt das wirksame Gate; der Vermerk macht die Absicht
schriftlich, analog zum Flash-Kanal `0x5A` bei der Tastatur.

### Lücke 3: unabgefragte Getter für den Capability-Report

**Erledigt.** Die zehn Methoden wurden der Sondenliste von
`--inspect-thermal-power` hinzugefügt. Alle deklarieren ausschliesslich
`out`-Parameter, was per `Get-CimClass` geprüft wurde; jeder Aufruf ist damit ein
reiner Lesevorgang ohne Eingabewert. `GetDeepFan` liefert bei Erfolg fünf
Geschwindigkeits- und fünf Temperaturwerte und klärt damit gleichzeitig Lücke 1.

Schritt 1 der Testreihenfolge verlangt "alle relevanten Getter". Diese waren im
Schema vorhanden und wurden nicht abgefragt:

| Methode | ID | Warum interessant |
| --- | --- | --- |
| `GetFanHealth` | 98 = `0x62` | mögliche Fehlerdiagnose der Lüfter |
| `GetFanPWMStatus` | 111 = `0x6F` | zweite Sicht auf den Regelzustand |
| `GetThermalData` | 86 = `0x56` | drei Thermalwerte in einem Aufruf |
| `QueryThermalSensor` | 249 = `0xF9` | zusätzlicher Sensor |
| `GetBatteryTemperature` | 138 = `0x8A` | Akkutemperatur, für Failsafes relevant |
| `GetFan3Duty` / `GetFan4Duty` | 69 / 68 | prüft, ob nur zwei Lüfter existieren |
| `getRpm3` / `getRpm4` | 232 / 233 | dito |
| `SetCurrentFanStep` | 102 = `0x66` | unklare Semantik, nur lesend nicht prüfbar |
| `SetFanModeNotify` | 237 = `0xED` | Gigabyte benachrichtigt damit die Lüfterlogik |

Die vier Getter zu Fan 3/4 und RPM 3/4 sind besonders billig und würden die
Annahme "zwei Lüfter" belegen statt voraussetzen.

### Korrektur: der Failsafe darf nicht nur für Fixed gelten

Das Dokument verlangt einen Temperatur-Failsafe für **feste** Drehzahlen. Der
Quiet-Testbericht zeigt jedoch, dass auch das **Herstellerprofil Quiet** Duty und
beide Lüfter bei 51–52 °C auf `0` setzt, also die Lüfter vollständig anhält.

Damit ist Quiet sicherheitstechnisch nicht harmloser als ein niedriger Festwert.
Der Failsafe muss jedes Profil abdecken, das die Lüfter stoppen kann, nicht nur
den Fixed-Modus. Dass sich die Temperatur im Test in zwölf Sekunden nur um etwa
1 °C bewegte, ist ein Leerlaufwert und sagt nichts über Last.

### Ergebnis des erweiterten Capability-Reports

Lauf `research/runs/thermal-power-inspection-20260903-142711.md`, erhöht und
ausschliesslich lesend. Neun der zehn nachgetragenen Getter werden vom
FB0F-Geräteobjekt mit "Ungültiges Objekt" abgewiesen, einer antwortet.

| Methode | Ergebnis | Folgerung |
| --- | --- | --- |
| `GetDeepFan` | abgewiesen | **Lücke 1 geschlossen:** der 5-Punkt-Kurvenpfad existiert auf FB0F nicht. Der 15-Punkt-Pfad über `FanIndexValue` ist die einzige Kurvenschnittstelle. |
| `GetFan3Duty`, `GetFan4Duty`, `getRpm3`, `getRpm4` | alle abgewiesen | Die Annahme "zwei Lüfter" ist damit **belegt** und nicht mehr nur vorausgesetzt. |
| `GetThermalData`, `QueryThermalSensor` | abgewiesen | keine zusätzlichen Thermalsensoren über WMI. |
| `GetBatteryTemperature` | abgewiesen | Akkutemperatur steht für einen Failsafe nicht zur Verfügung. |
| `GetFanHealth` | abgewiesen | keine Lüfter-Fehlerdiagnose. |
| `GetFanPWMStatus` | **`194`** | implementiert; neu entdecktes Live-Register. |

Damit ist auch klar, was die Anwendung nicht bekommt: keine Akkutemperatur,
keine Zusatzsensoren, keine Lüfterdiagnose. Ein Failsafe kann sich nur auf
`getCpuTemp` und `getGpuTemp1` stützen.

### Neuer Verdacht: `GetFixedFanSpeed` ist möglicherweise kein gespeicherter Wert

Im selben Lauf meldeten drei Getter **denselben** Wert:

- `GetFanPWMStatus` = 194
- `GetFixedFanSpeed` = 194
- `GetFanAdjustStatus` = 194

Das deutet darauf hin, dass alle drei dasselbe EC-Register lesen.

Entscheidend ist die Historie dieses Werts über die fünf Inspektionen:

| Bericht | `GetFixedFanSpeed` |
| --- | --- |
| `…-134039` | 57 |
| `…-134200` | 57 |
| `…-135342` | 57 |
| `…-140823` | 57 |
| `…-142711` | **194** |

Zwischen `140823` und `142711` ist **kein Lüfter-Schreibvorgang protokolliert**;
der jüngste Bericht davor ist die GPU-Routing-Inspektion. Entweder ist der Wert
also kein persistenter Festwert, sondern ein Livewert, oder er wurde von etwas
ausserhalb unserer Protokolle geschrieben.

**Warum das sicherheitsrelevant ist:** `ModeStateEquals` in
`GigabyteWmiFanController` vergleicht `FixedSpeedRaw` und beschriftet ihn als
`stored-fixed`, behandelt ihn also als persistent. `RestoreExactState` wirft eine
Ausnahme, wenn er abweicht. Das ist genau dieselbe Annahme, die beim
Maximum-Test schon einmal einen falschen Fehler erzeugt hat — dort wurde
`GpuDutyRaw` deshalb bewusst aus dem Vergleich genommen, `FixedSpeedRaw` aber
nicht.

Wenn der Wert live ist, kann eine korrekte Wiederherstellung als gescheitert
gemeldet werden. Ein Rollback, der grundlos fehlschlägt, ist in einer
Lüftersteuerung kein kosmetisches Problem.

### Geklärt: der Wert ist persistent, der Verdacht war falsch

Der Messlauf `research/runs/thermal-power-inspection-20260903-143152.md`
entscheidet die Frage gegen die Livewert-Vermutung.

| Probe | CPU-Duty | `GetFixedFanSpeed` | `GetFanAdjustStatus` | `GetFanPWMStatus` |
| --- | --- | --- | --- | --- |
| 1 | 75 | 194 | 194 | 194 |
| 2 | 75 | 194 | 194 | 194 |
| 3 | 66 | 194 | 194 | 194 |

Der Duty-Wert wanderte innerhalb des Laufs sichtbar von 75 auf 66, die drei
Register blieben konstant. Sie folgen der Kurve also **nicht** und sind keine
Livewerte.

Folgerung: `ModeStateEquals` darf `FixedSpeedRaw` weiterhin vergleichen. Die
vermutete Parallele zum `GetGPUFanDuty`-Fehler besteht nicht, und es ist keine
Änderung am Controller nötig.

### Herkunft der 194

Die Anwendung besitzt in `MainWindowViewModel.FixedFanRawChoices` einen
Festwert-Wähler mit den Stufen `57, 68, 91, 114, 137, 160, 194, 229`. `194` ist
eine davon.

`SetNormalAsync` ruft `ChangeProfile` mit `fixedSpeed: null` auf und schreibt den
gespeicherten Festwert damit bewusst **nicht** zurück. Wer in der Anwendung den
Festwert 194 wählt und anschliessend auf Normal zurückstellt, hinterlässt genau
das beobachtete Bild: alle Modusflags 0, gespeicherter Festwert 194.

Das ist funktional harmlos, weil der Wert ausserhalb des Fixed-Modus nicht
angewendet wird. Es korrigiert aber eine stillschweigende Annahme in diesem
Dokument: **`57` ist kein Werksbasiswert**, sondern lediglich der zuletzt
geschriebene Festwert. Frühere Formulierungen wie "Festwert 57" beschreiben eine
Momentaufnahme, keine Gerätekonstante.

`GetFanPWMStatus` liest offenbar dasselbe EC-Register wie `GetFixedFanSpeed` und
`GetFanAdjustStatus` und bringt damit keine zusätzliche Information.

### Nachgeprüft: `GetPEGorSG` liefert überhaupt keinen Zustand

Der Rohwert 66 wurde als "unplausibel, aber geantwortet" eingeordnet. Ein
Vergleich über alle sechs erhöhten Inspektionen zeigt etwas anderes:

| Bericht | `GetPEGorSG` | `GetCPUFanDuty` | `GetGPUFanDuty` |
| --- | --- | --- | --- |
| `…-134039` | 66 | 66 | 66 |
| `…-134200` | 93 | 93 | 93 |
| `…-135342` | 66 | 66 | 66 |
| `…-140823` | 66 | 66 | 66 |
| `…-142711` | 66 | 66 | 66 |
| `…-143152` | 75 | 75 | 75 |

`GetPEGorSG` spiegelt in jedem Lauf exakt den Lüfter-Duty-Wert, einschliesslich
der Ausreisser 93 und 75. Das ist keine Grafikmodus-Auskunft, sondern der
Inhalt des Rückgabepuffers des zuvor aufgerufenen Getters.

Damit sind **alle** Grafik-Getter auf FB0F unbrauchbar: `GetNvPowerConfig`,
`GetPEG2orSG2` und `getAiPowerCtlCapability` werden abgewiesen, und `GetPEGorSG`
antwortet nur scheinbar. Es gibt keinen lesbaren GPU-Power- oder MUX-Zustand.

**Methodische Regel für alle künftigen Capability-Proben:** Auf diesem Gerät
liefert eine nicht implementierte Methode nicht zwingend einen Fehler, sondern
möglicherweise einen gespiegelten Wert aus einem vorherigen Aufruf. Eine
plausibel aussehende Zahl ist deshalb **kein** Beleg für Implementierung. Nur
Werte, die sich unabhängig von anderen Gettern verhalten, zählen als echte
Antwort. Um das zu erkennen, muss dieselbe Methode in Läufen mit
unterschiedlichem Systemzustand verglichen werden.

Diese Regel entwertet keine der bisherigen Lüfterergebnisse — die Modusflags,
der Festwert und die Kurvenpunkte verhalten sich nachweislich unabhängig vom
Duty-Wert —, aber sie sollte bei jedem neuen Getter angewendet werden.

### Nicht nachgeprüft

Die Aussagen zu abgewiesenen Methoden (`getAiPowerCtlCapability`,
`GetNvPowerConfig`, `GetPEG2orSG2`, `GetTurboMode` und weitere) sowie alle
Live-Messwerte beruhen auf den erhöhten Läufen des ursprünglichen Autors und
wurden hier nicht wiederholt. Die Methoden sind im Schema vorhanden, was mit der
Darstellung übereinstimmt; ob das FB0F-Geräteobjekt sie abweist, lässt sich nur
mit Administratorrechten nachvollziehen.

## Quellen

- GIGABYTE, AORUS 5 (Intel 12th Gen), offizielle Spezifikation: <https://www.gigabyte.com/Laptop/AORUS-5--Intel-12th-Gen/sp>
- GIGABYTE Control Center Quick Start Guide, Fan Control: <https://download.gigabyte.com/FileList/Manual/VE_ControlCenter_QSG_Manual_v1.1.pdf>
- NVIDIA, Using NVIDIA's Power-Saving GPU Technology: <https://www.nvidia.com/content/Control-Panel-Help/vLatest/en-us/mergedProjects/nvcpl/Using_Optimus_Hybrid.htm>
- NVIDIA, Optimus Technology Whitepaper: <https://www.nvidia.com/content/dam/en-zz/Solutions/geforce/optimus-whitepaper-final.pdf>
- Microsoft, GPU preference (`DXGI_GPU_PREFERENCE`): <https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_6/ne-dxgi1_6-dxgi_gpu_preference>
- Microsoft, Power slider / Overlay modes: <https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/customize-power-slider>
- Microsoft, `powercfg` and overlay schemes: <https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/powercfg-command-line-options>
