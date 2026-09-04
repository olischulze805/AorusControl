# Testplan: Lüfter, Leistung und Grafik

Zielgerät: AORUS 5 SE / SE4, BIOS FB0F, EC F00B  
Beginn: 2026-09-03

Dieser Plan wird schrittweise abgearbeitet. Nach jedem Lauf werden Rohbeobachtungen in `research/runs/` und dauerhafte Schlussfolgerungen in `research/FAN-POWER-GPU-CONTROL.md` sowie `RESEARCH.md` festgehalten. Ein später Schritt wird erst begonnen, wenn die Sicherheits- und Erfolgskriterien des vorherigen Schritts erfüllt sind.

Gesamtstatus 2026-09-03: **Alle auf FB0F sicher unterstützten Phasen abgeschlossen.** Lüfterprofile, Fixed-Skala, Dynamic/Kurvenänderung und Windows-Leistungsmodi sind praktisch verifiziert. Gigabyte-Systemleistung, physisches GPU-Eco und MUX wurden wegen negativer BIOS-Capabilities ohne Setterversuch beendet. Der abschließende unabhängige Lauf bestätigte Normalzustand, Festwert 57, Duty 66 und alle 15 Originalpunkte: `research/runs/thermal-power-inspection-20260903-140823.md`.

## Phase 1 – Nur lesende Bestandsaufnahme

Status: **abgeschlossen**

### 1.1 Diagnosewerkzeug

- exakte Modell-/BIOS-Allowlist
- keine Setterklasse öffnen und keine Firmware-/EC-Schreibmethode aufrufen
- Firmware-Getter und deren Laufzeittypen erfassen
- aktuelle Lüfterzustände, Temperaturen, RPM und Duty erfassen
- alle 15 gespeicherten Kurvenpunkte lesen
- GPU-Power-, PEG/SG-, MUX-, Dynamic-Boost-, Whisper- und Thermalzustände lesen, soweit die Getter tatsächlich vorhanden sind
- Windows-Energieplan und Overlay-Zustand erfassen
- GPUs, Displays und relevante NVIDIA-PnP-Geräte erfassen
- `nvidia-smi`-Zustand und GPU-Prozesse nur lesend erfassen

Erfolg: ein vollständiger Laufbericht ohne Setteraufruf. Nicht vorhandene oder fehlerhafte Getter gelten als Erkenntnis und nicht als Grund, unbekannte Alternativen zu schreiben.

Ergebnis 2026-09-03: Diagnose gebaut, Release-Build ohne Warnungen, Modell-/BIOS-Gate im nicht erhöhten Trockenlauf bestätigt und erster erhöhter Lauf vollständig gespeichert. Kein Setter wurde geöffnet. Firmwarekurve und Zustände waren lesbar; mehrere GPU-/MUX-Capability-Getter werden vom BIOSdispatcher abgewiesen.

### 1.2 Wiederholungsmessung

- mindestens drei Telemetrieproben im Abstand von einigen Sekunden
- prüfen, welche Felder stabil sind und welche Messwerte darstellen
- Lüfter-RPM/Duty und Temperaturen auf Plausibilität vergleichen

Erfolg: Zustandsfelder von Live-Telemetrie unterschieden; kein Wert außerhalb plausibler Grenzen.

Ergebnis 2026-09-03: Drei Proben bei 2 Sekunden Abstand waren plausibel. Bei CPU 59 °C lagen beide Duty-Rohwerte bei 93 und die Lüfter bei 2557/2698 RPM. Bei 51 °C fiel Duty auf 84 und die Lüfter auf 2414/2545 RPM. CPU- und GPU-Duty waren in allen Proben gleich. Die gespeicherte Kurve blieb in beiden erhöhten Läufen bytegleich. Bericht: `research/runs/thermal-power-inspection-20260903-134200.md`.

## Phase 2 – Lüfterprofile des Herstellers

Status: **abgeschlossen**

### 2.1 Controller und Rollback

- vorher alle Lüfterstatuswerte und 15 Kurvenpunkte sichern
- nur exakte, aus Gigabytes aktuellem signiertem Modul abgeleitete Befehlsfolgen erlauben
- jede Teiloperation zurücklesen
- bei Fehler sofort auf Normal/Firmwaresteuerung zurückkehren
- separaten Rettungsbefehl bereitstellen, der keine Oberfläche benötigt

Ergebnis 2026-09-03: Controller mit Modell-/BIOS-Gate, Setter-Signaturprüfung, Serialisierung, vollständiger Kurvenaufnahme, Readback und Rollback gebaut. Der Negativtest ohne `--confirm-fan-write` verweigerte vor dem Öffnen der Setterklasse. `tools/Start-FanNormalRestore.ps1` ist der unabhängige Normal-Rettungsweg.

### 2.2 Normal

- Normalprofil setzen
- mehrere Minuten Temperatur, RPM und Duty beobachten
- Rückkehrpfad unabhängig testen

Ergebnis 2026-09-03: Normalfolge erfolgreich geschrieben und exakt zurückgelesen; Ausgang und Ergebnis `fixed=0, step=0, auto=0, thermal=0`, Festwert 57, Duty 66. Alle 15 Kurvenpunkte blieben bytegleich. Bericht: `research/runs/fan-normal-change-20260903-134611.md`.

### 2.3 Leise/Eco-Lüfterprofil

- nur das Lüfterprofil ändern, keine Windows- oder GPU-Leistung
- Temperaturanstieg und Lüfterreaktion beobachten
- bei unplausibler Temperatur oder stillstehendem Lüfter sofort abbrechen

Ergebnis 2026-09-03: Quiet setzte ausschließlich `thermal` von 0 auf 1, ließ Kurve/Festwert unverändert und führte bei 51–52 °C zu Duty 0 und nach dem Umschalten zu 0 RPM auf beiden Lüftern. Temperaturen stiegen über 12 Sekunden nur um etwa 1 °C. Der erste GPU-RPM-Wert 7000 bei Duty 0 war ein Umschaltausreißer; vier Folgewerte waren 0. Normal wurde anschließend mit Duty 66 verifiziert wiederhergestellt. Bericht: `research/runs/fan-quiet-test-20260903-134826.md`.

### 2.4 Gaming/Power

- Herstellerfolge setzen und überwachen
- Vergleich mit Normal protokollieren

Ergebnis 2026-09-03: `auto=1` wurde exakt zurückgelesen, alle übrigen Modusfelder und die Kurve blieben unverändert. Bei 51/48 °C blieb die Regelung über fünf Proben bei Duty 66 und ungefähr 1880/2000 RPM; der Unterschied zu Normal ist bei Leerlauflast nicht sichtbar. Normal wurde danach verifiziert wiederhergestellt. Bericht: `research/runs/fan-gaming-test-20260903-135023.md`.

### 2.5 Maximal

- kurzzeitig maximale Lüfterleistung testen
- Rohwert, Duty und RPM korrelieren
- danach explizit zu Normal zurückkehren

Zwischenergebnis 2026-09-03: Maximum wurde korrekt mit `fixed=1`, `step=1`, Festwert/Duty 229 aktiviert. Die Lüfter stiegen innerhalb von 12 Sekunden auf 5208/5472 RPM, CPU/GPU sanken von 51/48 auf 48/45 °C. Die erste integrierte Rückkehr meldete fälschlich einen kritischen Verifikationsfehler, obwohl alle Modusschalter bereits Normal waren: der Vergleich behandelte den dynamischen GPU-Duty-Livewert als persistente Konfiguration. Der unabhängige Normal-Rettungsbefehl bestätigte danach `fixed=0, step=0, auto=0, thermal=0`, Festwert 57, Duty 66 und 1891/2013 RPM. Berichte: `research/runs/fan-maximum-test-20260903-135300.md`, `research/runs/fan-normal-change-20260903-135327.md`, `research/runs/thermal-power-inspection-20260903-135342.md`. Der Verifier wurde korrigiert und im nachfolgenden Endergebnis erfolgreich erneut getestet.

Endergebnis 2026-09-03: Die Wiederholung bestätigte erneut Festwert/Duty 229 und bis zu 5220/5417 RPM. Der korrigierte integrierte Rückweg stellte `fixed=0, step=0, auto=0, thermal=0`, Festwert 57 und Live-Duty 66 exakt wieder her. Bericht: `research/runs/fan-maximum-test-20260903-135458.md`.

Erfolg Phase 2: alle vier Profile reproduzierbar, Rücklesen korrekt und Normal-Rettung bewiesen.

## Phase 3 – Windows-Leistungsmodus

Status: **abgeschlossen**

- aktiven Basisplan und aktives Overlay getrennt lesen
- Energieeffizienz, Ausbalanciert und Beste Leistung über die dokumentierte Windows-API setzen
- AC/DC-Zustand berücksichtigen
- nach jedem Wechsel Windows-Zustand zurücklesen
- ursprünglichen Zustand wiederherstellen

Ergebnis 2026-09-03: Negativtest ohne Bestätigungsmerkmal verweigerte vor dem Windows-Setter. Der bestätigte AC-Rundlauf las für Energieeffizienz, Ausbalanciert und Beste Leistung jeweils exakt den erwarteten GUID zurück und stellte danach den ursprünglichen ausgeglichenen Overlay-GUID wieder her. Kein Firmware-/EC-Befehl wurde verwendet. Bericht: `research/runs/windows-power-overlay-test-20260903-135645.md`.

Erfolg: drei Windows-Modi ohne Änderung der Gigabyte-Lüfter- oder GPU-Power-Zustände.

## Phase 4 – Feste Lüfterleistung und Kurven

Status: **abgeschlossen**

### 4.1 Rohwertskala

- wenige sichere, hohe Testpunkte verwenden
- CPU- und GPU-Lüfter getrennt beobachten
- UI-Prozent, Rohwert, Duty und RPM dokumentieren
- nie mit 0 oder einem unbekannt niedrigen Wert beginnen

Zwischenergebnis 2026-09-03: 160 ergab ungefähr 4000/4190 RPM, 194 ungefähr 4600/4800 RPM und 229 ungefähr 5220/5420 RPM. Beide Duty-Getter und der gespeicherte Festwert entsprachen an jedem Punkt exakt dem Ziel. Originalzustand mit Festwert 57 und Duty 66 wurde verifiziert wiederhergestellt. Bericht: `research/runs/fan-fixed-scale-test-20260903-135853.md`.

Endergebnis 2026-09-03: Die untere Hälfte ergab 57 ≈ 1640/1750 RPM, 68 ≈ 1925/2045 RPM, 91 ≈ 2510/2665 RPM, 114 ≈ 3040/3210 RPM und 137 ≈ 3520/3695 RPM. Höchste Temperatur war 48/45 °C; der Originalzustand wurde wiederhergestellt. Der bestätigte nutzbare Fixed-Bereich ist damit 57–229. Bericht: `research/runs/fan-fixed-scale-test-20260903-140107.md`.

### 4.2 Fixed

- sichere Mindestgrenze bestimmen
- beide Lüfter setzen und überwachen
- automatische Rückkehr zu Normal bei Prozessende/Fehler

Ergebnis: Setter, Readback, Temperaturgrenze und Wiederherstellung wurden über acht feste Werte von 57 bis 229 erfolgreich getestet. Ein längerfristig sicherer Minimalwert unter Last ist damit noch nicht bewiesen; die App darf 57 nicht ohne Temperaturwächter dauerhaft erzwingen.

### 4.3 Dynamic

- bestehende 15 Punkte sichern
- zunächst konservative monotone Kurve testen
- Mindestleistung und zwingendes Maximum bei hoher Temperatur validieren
- Originalkurve wiederherstellen und verifizieren

Modus-Zwischenergebnis 2026-09-03: Mit unveränderter Werkskurve setzte Dynamic `step=1` und wählte bei 48–49 °C stabil Rohwert 68 mit etwa 1930/2045 RPM. Die Kurve blieb bytegleich und Normal/Duty 66 wurde wiederhergestellt. Bericht: `research/runs/fan-dynamic-test-20260903-140240.md`.

Kurvenschreib-Ergebnis 2026-09-03: Nur Punkt 1 wurde konservativ von `(50,68)` auf `(50,80)` angehoben. Readback bestätigte die Änderung und 14 unveränderte Punkte; Dynamic verwendete Rohwert 80 mit ungefähr 2240/2350 RPM. Danach wurden alle 15 Originalpunkte und Normal/Duty 66 exakt wiederhergestellt. Bericht: `research/runs/fan-curve-write-test-20260903-140517.md`.

Abschlussprüfung: unabhängiger read-only Lauf bestätigte `fixed=0`, `step=0`, `auto=0`, `thermal=0`, Festwert/FanAdjust 57, Duty 66, ungefähr 1890/2000 RPM und die vollständige Originalkurve von `(0,57)` bis `(89,229)`. Bericht: `research/runs/thermal-power-inspection-20260903-140823.md`.

Erfolg: bekannte Rohwertabbildung, sichere Grenzen und bewiesenes Rollback.

## Phase 5 – Gigabyte-Systemleistung

Status: **auf diesem Modell ausgelassen**

- nur fortsetzen, wenn Phase 1 echte Unterstützung auf diesem Modell zeigt
- native Gigabyte-CPU-/GPU-Pfade weiter statisch analysieren
- Originalwerte und AC/DC-Unterschiede bestimmen
- Eco, Balance und Performance zuerst ohne Kopplung an Lüfter testen
- Dynamic Boost und thermische Ziele separat beobachten

Erfolg: modellspezifisch reproduzierbare Werte. Bei mehrdeutiger Capability wird diese Phase ausgelassen.

Entscheidung 2026-09-03: `getAiPowerCtlCapability`, `GetNvPowerConfig`, `GetEcValueBoostStatus`, `GetSmartCool`, `GetSmartTurbo` und `GetTurboMode` werden vom FB0F-Geräteobjekt als ungültig abgewiesen. Einzelne Getter wie Dynamic Boost, Whisper und NVIDIA-Thermalziel antworten zwar, belegen aber nicht die gesamte Gigabyte-Systemleistungsfunktion. Keine CPU-/GPU-Leistungsgrenze wird geschrieben. Die App bleibt bei den bestätigten Windows-Leistungsmodi und getrennten Lüfterprofilen.

## Phase 6 – GPU-Hybrid und Anwendungszuordnung

Status: **read-only Bestandsaufnahme abgeschlossen; Schreibtest zurückgestellt**

- anzeigen, welche GPU das interne Display treibt
- anzeigen, welche Programme/Displays die RTX verwenden
- Windows-GPU-Präferenz pro Anwendung nur über dokumentierte Schnittstellen verwalten
- bestätigen, dass die RTX im Optimus-Leerlauf tatsächlich in einen niedrigen Energiezustand fällt

Erfolg: Hybridmodus transparent und kontrollierbar, ohne Geräte abzuschalten.

Ergebnis 2026-09-03: Nur das interne BOE-Panel ist aktiv und wird über Intel betrieben. Zahlreiche Desktopprogramme halten die RTX aktiv. Einige Windows-Systemprogramme besitzen bereits `GpuPreference=1`, erscheinen aber trotzdem in der aktuellen NVIDIA-Prozessliste; eine gespeicherte Präferenz ist daher kein Beweis für den tatsächlich laufenden Adapter. Das Ändern von Codex/Claude würde einen Neustart dieser laufenden Arbeitsumgebung erfordern und wurde nicht durchgeführt. Bericht: `research/runs/gpu-routing-inspection-20260903-140600.md`.

## Phase 7 – RTX vollständig ausschalten (GPU-Eco)

Status: **auf FB0F nicht unterstützt; kein Schreibtest**

- Preflight: internes Display auf Intel, kein NVIDIA-externer Monitor, keine aktiven GPU-Prozesse
- Benutzerprogramme niemals automatisch beenden
- vorher alle PnP- und Firmwarezustände sichern
- Gigabytes bestätigte Folge mit `SetNvPowerConfig(3)` ausführen
- physisches Ergebnis über PnP und `nvidia-smi` verifizieren
- mit `SetNvPowerConfig(4)`, Gerätesuche und PnP-Wiederherstellung einschalten
- Einschalten und Rollback aus einem separaten Rettungsprogramm ermöglichen

Erfolg: Aus und Ein jeweils unabhängig bestätigt; keine Anzeige-/Audioprobleme; Recovery funktioniert auch nach Neustart der Haupt-App.

Entscheidung: Das BIOS weist `GetNvPowerConfig` und `getAiPowerCtlCapability` ab. Ohne gültigen Capability- und Readback-Pfad wird `SetNvPowerConfig(3/4)` nicht experimentell gesendet. Softwareseitige Windows-GPU-Präferenzen bleiben davon getrennt möglich.

## Phase 8 – MUX / dGPU-only

Status: **auf FB0F nicht unterstützt; kein Schreibtest**

- nur fortsetzen, wenn Phase 1 und weitere statische Analyse einen echten MUX für FB0F bestätigen
- aktuelle Route lesen und Wertbedeutung zweifelsfrei bestimmen
- Neustartpflicht klar behandeln
- externer Bildschirm und interner Panelpfad prüfen

Erfolg: eindeutige Modellunterstützung und sicherer Rückweg zu Hybrid. Andernfalls bleibt diese Funktion bewusst aus der App.

Entscheidung: `GetPEG2orSG2` wird vom Geräteobjekt abgewiesen, `GetPEGorSG` liefert den unplausiblen Rohwert 66 statt 0/1, und Gigabytes Modellspezifikation nennt nur Optimus. Kein MUX-Setter wird aufgerufen.

## Globale Abbruchregeln

- Modell, BIOS oder EC stimmen nicht mit der Freigabeliste überein.
- Ein benötigter Getter/Setter fehlt oder liefert einen unerwarteten Typ.
- Readback stimmt nicht mit der angeforderten Änderung überein.
- CPU/GPU-Temperatur, RPM oder Duty werden unplausibel.
- Externer Monitor oder aktiver GPU-Prozess blockiert GPU-Eco.
- Der Rückweg ist nicht bereits vor dem eigentlichen Test implementiert und trocken geprüft.
