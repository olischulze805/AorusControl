# Dashboard: CPU-Watt und Windows-GPU-Status

Stand: 2026-09-15 (Stromfluss aus dem Akku ergänzt; davor 2026-09-11 GPU-Watt, ursprünglich 2026-09-09).

## Starter-Korrektur

Start-AorusControl.ps1 verwendete eine vorhandene Release-EXE vom 5. September und baute nur bei fehlender Datei. Die neue Dashboard-Funktion war zunächst nur im Debug-Build vorhanden. Der Starter baut jetzt vor jedem Start die gesamte Solution inkrementell in Release, einschließlich Worker, und startet bei Buildfehlern keine alte Version. Der erste Release-Versuch nach dieser Korrektur wurde durch die laufende AorusControl.exe (PID 11324) blockiert. Vor erneutem Start die App über das Tray-Menü vollständig beenden; Fenster-X versteckt die App lediglich.

## Implementiert

- CPU-Kachel: CPU-Paketleistung aus Windows Energy Meter (RAPL_Package0_PKG), auf eine Nachkommastelle gerundet. Enthält die integrierte Grafik; keine Summe über überlappende RAPL-Domänen.
- NVIDIA-Kachel: zuletzt von Windows gemeldeter Gerätezustand D0, D1, D2 oder D3. Fehlende/ungültige Daten werden ausdrücklich als unbekannt angezeigt. Mehrere NVIDIA-Adapter ergeben keine geratenen Einzelwerte.
- Der DashboardPowerReader nutzte anfangs ausschließlich Windows PerformanceCounter und SetupAPI. Seit 2026-09-11 kommt genau eine NVIDIA-Quelle dazu: NVML, und nur unter den beiden Bedingungen im nächsten Abschnitt.
- ReadCategory liest CPU-Energie. SetupDiGetClassDevs mit DIGCF_PRESENT und Display-Klassenguid enumeriert vorhandene Grafikgeräte; Hardware-ID VEN_10DE identifiziert NVIDIA. SPDRP_DEVICE_POWER_DATA enthält die CM_POWER_DATA-Struktur.
- Strukturgröße und Datentyp werden geprüft. DEVICE_POWER_STATE ist einsbasiert: 1=D0, 4=D3. D3 unterscheidet hier nicht D3hot/D3cold und belegt keine physikalischen 0 W.
- Nur bei sichtbarem Dashboard werden die Zusatzwerte abgefragt; Threadpool statt UI-Thread, vorhandenes Intervall zwei Sekunden. Vor Veröffentlichung wird Sichtbarkeit erneut geprüft. Alte Werte werden bei Seitenwechsel, Verstecken, Stoppen oder Telemetriefehler gelöscht.
- Erste CPU-Probe, Abstand über fünf Sekunden, zurückgesetzte Energie, nicht fortschreitende Basiszeit und ungültige Wattwerte werden verworfen. Ein Fehler dieser Zusatzanzeige löst keinen Lüftermoduswechsel aus.
- Vorhandene Gigabyte-Temperatur-/Lüftertelemetrie bleibt unverändert. Der isolierte neue Sensorpfad wurde live geprüft; diese Prüfung ist kein Nachweis des Energieverhaltens sämtlicher bereits bestehender App-Abfragen.

## GPU-Watt: erst abgelehnt, am 2026-09-11 am Netz eingebaut

Am 2026-09-09 wurde gegen GPU-Watt entschieden. Die Begründung galt einem D0-Check als alleiniger Bedingung und bleibt richtig: Die Karte kann zwischen Prüfung und NVML-Aufruf einschlafen, und wiederholtes Abfragen kann ihr Einschlafen verhindern. Ein D0-Check allein ist keine Garantie.

Auf Nutzerwunsch kam am 2026-09-11 eine zweite Bedingung dazu, und die trägt das Gewicht: **Netzbetrieb**. Damit bleibt das eigentliche Schutzziel unberührt. Im Akkubetrieb wird NVML nicht einmal geladen, die schlafende Karte bleibt schlafen, und die Kachel zeigt weiter den Gerätezustand. Am Netz darf die Karte wach gehalten werden – dort kostet das Laufzeit, die niemand zählt.

Die Regel steht als `DashboardPowerReader.MayReadGpuWatts(Stromquelle, D-Zustand)` an einer Stelle und ist einzeln getestet: Netz + D0 erlaubt, alles andere verweigert – auch eine wache Karte im Akkubetrieb und eine unbekannte Stromquelle.

- Gelesen wird `nvmlDeviceGetPowerUsage` aus nvml.dll, der Bibliothek des Treibers in System32. Kein NVAPI, kein nvidia-smi-Prozess, kein Fremdcode.
- Die Bibliothek wird erst beim ersten erlaubten Lesen geladen und wieder freigegeben, sobald die Karte D0 verlässt, die Dashboardseite verlassen wird, das Fenster verschwindet oder die App endet. Die App wartet stundenlang im Infobereich; ein dort gehaltener Treiber-Handle wäre das Gegenteil des Ziels.
- Die Kachel zeigt entweder Watt oder den Gerätezustand, nie beides: Wer Watt meldet, ist per Definition in D0.
- Ein zweiter NVIDIA-Adapter bricht vor jeder Wattabfrage ab, weil NVMLs Index 0 dann nicht mehr eindeutig ist.

**Ungeprüft bleibt**, ob ein reiner NVML-Lesezugriff eine ohnehin wache Karte länger wach hält. Am Messtag lag die Karte durchgehend in D0, weil ein externer Monitor an ihrem Ausgang hängt (siehe unten); ein D0→D3-Übergang war nicht provozierbar. Die Freigabe beim Verlassen der Seite ist die Antwort darauf, nicht ein Messergebnis.

G-Helper wurde als Referenz gelesen: dort wird GetCurrentPerformanceState mit NVAPI_GPU_NOT_POWERED ausgewertet, bevor GPU-Watt abgefragt werden. Das beweist die Nicht-Aufweckwirkung auf unserem AORUS mit Treiber 616.64 nicht. Kein Fremdcode übernommen.

## Warum die RTX hier ständig wach ist

Am 2026-09-09 meldete die Karte in allen acht Proben D3. Am 2026-09-11 in allen acht Proben D0, bei rund 20,8 W im Leerlauf. Der Unterschied ist kein Softwarefehler:

- `Win32_VideoController` zeigt die RTX 3070 mit 3440 Pixeln Breite bei 144 Hz, die Intel Iris Xe mit 1920. Der externe Ultrawide hängt also am Ausgang der NVIDIA-Karte, der interne Bildschirm an der Intel-Grafik.
- `nvidia-smi` führt entsprechend explorer.exe, SearchHost, StartMenu, Chrome, WhatsApp, Windows Terminal und die Claude-App als Grafikclients der RTX.

Solange dieser Monitor angeschlossen ist, schläft die Karte nie, und die Programmzuordnung unter Leistung & Akku kann daran nichts ändern: Ein Bildschirm an ihrem Ausgang ist ein Verbraucher, den keine Windows-Präferenz wegschalten kann. Das erklärt zugleich, warum die Wattanzeige an diesem Gerät am Netz praktisch immer etwas zeigt.

## Tests und Live-Ergebnis

- Solution-Build erfolgreich, null Warnungen/Fehler.
- Smoke-Suite einschließlich neuer Prüfungen: D0/D3-Abbildung, unbekannt/abgeschnittene Daten, ungültige/reset CPU-Zähler, Sichtbarkeits-Gate, Veröffentlichung und Löschen alter Werte.
- WPF-Layoutprüfung an allen bisherigen Breiten erfolgreich; Dashboard bei 720 Pixeln visuell geprüft.
- Reproduzierbarer nur lesender Check:

```powershell
dotnet run --project tests/AorusControl.HardwareChecks -- --dashboard-power-read-only
```

Bericht: runs/dashboard-power-20260909-183153.md, GPU-Watt ergänzt in runs/dashboard-power-20260911-212415.md (acht Proben, GPU 20,80–20,84 W, alle D0, erste Abfrage 4,9 s im Hintergrundthread, danach 0,57–1,2 ms). Acht Proben über rund 14 Sekunden: NVIDIA in allen Proben D3, CPU nach Basisprobe 26,224–48,640 W. Parallel liefen Builds/Tests; kein CPU-Leerlauf-Benchmark. Erste Initialisierung 599,50 ms; Folgeabfragen 0,54–2,69 ms.

Dies spricht dagegen, dass die neue Abfrage die RTX dauerhaft nach D0 versetzt. Sehr kurze unbeobachtete Übergänge werden damit nicht ausgeschlossen. Kein aktueller Akkuvergleich, kein gezielter D0→D3-Übergang provoziert, keine NVIDIA-Sensorabfrage in diesem Umsetzungsschritt. Keine Neuinstallation der zuvor entfernten App.

## Gesamtverbrauch: nur der Akku kann ihn messen

Die Frage war, ob sich die **gesamte** Leistungsaufnahme des Geräts messen lässt, ohne die
Grafikkarte zu wecken. Antwort: ja, aber nur im Akkubetrieb - und der Weg dorthin führt nicht
über einen Systemsensor, sondern über den Akku selbst.

### Was es auf diesem Gerät nicht gibt

Drei Quellen wurden geprüft und ausgeschlossen:

- **Kein Plattform-Energiezähler.** Windows hätte dafür die Energy Metering Interface
  (ACPI `PNP0CA2`). Dieses Gerät hat keine; die Kategorie „Energy Meter" enthält ausschließlich
  die vier RAPL-Instanzen der CPU (`PKG`, `DRAM`, `PP0`, `PP1`).
- **Nichts in der Gigabyte-WMI.** Alle 96 Getter von `GB_WMIACPI_Get` durchgesehen: Ladepolitik,
  Ladeschwelle, Akkutemperatur, Kapazität, Zyklen, Lüfter, Thermodaten - kein Wert für
  Netzteil- oder Systemleistung. (`getPdChargeInStatus` existiert, betrifft aber USB-C-Laden
  und ist ungeprüft.)
- **Zusammenrechnen wäre unseriös.** CPU-Paket plus GPU lässt Bildschirm, WLAN, SSD und die
  Wandlerverluste aus - bei einem Laptop schnell 15 W. Eine Zahl, die genauer aussieht als sie
  ist, wäre schlechter als keine.

### Was es gibt

`BatteryStatus` aus `root\wmi`, gelesen über ACPI und den Embedded Controller. Kein
Grafiktreiber beteiligt, die schlafende Karte bleibt schlafen. Die Einheiten sind Milliwatt
beziehungsweise Milliwattstunden - bestätigt über `BatteryFullChargedCapacity` = 99.013 bei
16.709 mV Auslegungsspannung; in Milliamperestunden wären das absurde 1.600 Wh.

Drei Zustände, in `BatteryFlowMath.From` an einer Stelle und zeilenweise getestet:

| Netz | Rate | Anzeige |
| --- | --- | --- |
| nein | Entladen > 0 | **Gesamtverbrauch** des ganzen Geräts, Bildschirm und Grafik eingeschlossen |
| ja | Laden > 0 | was **in den Akku** geht - ausdrücklich nicht, was das Netzteil liefert |
| ja | beide 0 | **Akku ruht**, das Netzteil trägt das Gerät (der Zustand einer erreichten Ladeschwelle) |

Die Flags `Charging` und `Discharging` werden bewusst ignoriert: Dieses Gerät meldet am Netz
bei 97 % `Discharging = true` mit Rate 0. Maßgeblich sind `PowerOnline` und die beiden Raten;
eine Entladerate am Netz ist keine Entladung. Genau dafür gibt es eine Testzeile.

### Nebeneffekt: der ehrlichste GPU-Wattmesser, den wir haben

Im Akkubetrieb einmal messen, während die RTX schläft, und einmal, während sie läuft: Die
Differenz ist, was sie wirklich kostet - inklusive Wandlerverlusten, ohne die Karte je
anzufassen. Noch nicht durchgeführt.

### Kosten

Die WMI-Abfrage läuft im selben Hintergrund-Tick wie die übrigen Dashboardwerte und kostet
7-20 ms gegenüber 0,6-1,2 ms vorher (`runs/dashboard-power-20260915-191630.md`). Auf dem
Hintergrundthread, die Oberfläche merkt davon nichts.

## Restlaufzeit statt nur Watt (2026-09-18)

Die Kachel STROMFLUSS zeigt jetzt neben den Watt auch, wie lange sie reichen.

### Windows kann es auf diesem Gerät nicht

Beide Wege, auf denen Windows eine Laufzeitschätzung anbietet, liefern hier nur ihren
Platzhalter für „unbekannt":

| Quelle | Wert am 2026-09-18 |
| --- | --- |
| `root\WMI:BatteryRuntime.EstimatedRuntime` | `4294967295` (= `0xFFFFFFFF`) |
| `Win32_Battery.EstimatedRunTime` | `71582788` (derselbe Platzhalter in Minuten) |

### Selbst gerechnet

Beide Zutaten liest das Dashboard ohnehin schon für seine Wattanzeige:

```
Restkapazität (Wh)  ÷  Rate (W)  =  verbleibende Stunden
```

Bei den am 2026-09-15 gemessenen 24,4 W im Leerlauf und 79,2 Wh Rest ergibt das 3 h 15 min.

Zwei Entscheidungen machen daraus eine brauchbare Anzeige statt einer springenden Zahl:

- **Geglättet über drei Minuten.** Die Momentanrate ändert sich zwischen ruhendem Bildschirm
  und scrollender Seite um das Doppelte. `MovingAverage` mittelt über ein Zeitfenster, nicht
  über eine Anzahl Messwerte - der Akku meldet alle 8 bis 16 Sekunden, das Dashboard fragt
  alle 2, und ein Mittel über „die letzten n Messungen" würde ein langsames Instrument danach
  gewichten, wie oft eine schnelle Uhr es zufällig angesehen hat.
- **Beim Laden zählt die Zeit bis zum Ladelimit**, nicht bis 100 %. Bei Limit 80 % hört das
  Gerät dort auf; eine Anzeige „voll in 40 Minuten" wäre ein Versprechen, das das Limit bricht.

Über 24 Stunden wird keine Zahl mehr angezeigt. Restkapazität geteilt durch eine sehr kleine
Rate ergibt „zwei Tage" und bedeutet „die Rate ist Rauschen".

## Akkutemperatur: auf diesem Gerät nicht lesbar (2026-09-18)

Geprüft, weil danach gefragt wurde. Zwei unabhängige Wege, beide negativ:

| Weg | Ergebnis |
| --- | --- |
| ACPI `root\WMI:BatteryTemperature` | Klasse vorhanden, **keine Instanz** |
| Gigabyte-WMI `GetBatteryTemperature` | auf FB0F abgewiesen („Ungültiges Objekt"), bereits am 2026-09-03 dokumentiert |

Eine Temperaturkachel wurde deshalb **nicht** gebaut. Was der Pack sonst noch meldet und was
davon brauchbar ist:

| Feld | Wert | Brauchbar? |
| --- | ---: | --- |
| `FullChargedCapacity` | 99.013 mWh | ja, Grundlage der Restzeit |
| `Voltage` | 16.152 mV | ja, aber ohne Aussagekraft für den Nutzer |
| `DesignCapacity` (aus `powercfg /batteryreport`) | 99.013 mWh | **identisch mit der Ladekapazität** |
| `CycleCount` | 0 | wird nicht gemeldet |

Die letzten beiden schließen eine „Akkugesundheit"-Anzeige aus: Sie stünde dauerhaft auf
100 % und wäre damit keine Messung, sondern eine Behauptung.

## Messung des Dashboard-Ticks (2026-09-18)

Zehn Durchläufe, der erste verworfen:

| Teil | Dauer |
| --- | ---: |
| `Energy Meter` `ReadCategory` (CPU-Watt) | < 1 ms |
| `BatteryFlowReader.Read` (WMI) | **6 ms** |
| ganzer `DashboardPowerReader.Read` | 7 ms |

Die WMI-Abfrage des Akkus war also fast der gesamte Aufwand eines Ticks - und drei von vier
dieser Abfragen holten eine Zahl, die sich seit der letzten nicht geändert hatte, weil der
Pack nur alle 8 bis 16 Sekunden aktualisiert. `BatteryFlowReader` hält eine Antwort jetzt
5 Sekunden lang; das liegt sicher innerhalb des Intervalls des Instruments und spart rund zwei
Drittel der Abfragen. Gelesen wird ohnehin nur, solange das Dashboard sichtbar ist.

## Quellen

- Microsoft, Windows setzt PowerData und liefert CM_POWER_DATA: https://learn.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-powerdata
- Microsoft, most recent power state: https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdm/ns-wdm-cm_power_data_s
- Vergleich G-Helper: https://github.com/seerge/g-helper/blob/main/app/Gpu/NVidia/NvidiaGpuControl.cs
- Lokale Einheiten und vorherige CPU-Kontrollrechnung: CPU-GPU-WATTS-20260909.md.
