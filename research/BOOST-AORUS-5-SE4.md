# Boost beim AORUS 5 SE4 (BIOS FB0F)

Stand: 2026-09-16. Statische Analyse der archivierten Gigabyte-App und ihrer Notebook-Komponente, Abgleich mit offiziellen Herstellerquellen. In dieser Recherche wurden **keine** Firmware-/WMI-Schreibbefehle und keine GPU-Lasttests ausgeführt. Am 2026-09-16 wurde eine einmalige, nur lesende `nvidia-smi`-Abfrage ergänzt; sie kann die dGPU kurz aufwecken.

## Kurzfazit

„Boost“ bezeichnet mindestens drei verschiedene Mechanismen, die wir in der eigenen App nicht zusammenwerfen sollten:

1. **NVIDIA GPU Boost:** automatische GPU-Taktanpassung innerhalb der Treiber-, Temperatur- und Leistungsgrenzen. Kein Gigabyte-Schalter und keine Grafikumschaltung.
2. **NVIDIA Dynamic Boost 2.0:** dynamische Verteilung des Leistungsbudgets zwischen CPU, GPU und GPU-Speicher. Für AORUS 5 SE4/RTX 3070 von Gigabyte ausdrücklich angegeben; Gigabyte nennt maximal 130 W Graphics Power und 1620 MHz Boost Clock. Der 130-W-Wert ist kein garantierter Dauerverbrauch und beweist nicht, wie viele Watt davon dynamisch zugeschaltet werden.
3. **Gigabyte-Modi/GPU-OC:** Leistungs- und Lüfterprofile sowie für *einige* andere Modelle feste GPU-/Speichertakt-Offsets. Das ist vom NVIDIA Dynamic Boost getrennt.

## Was die alte App bzw. Gigabytes Notebook-Modul wirklich tut

Das archivierte GCC 23.03.02.01 ist überwiegend ein App-Shell-Paket. Die funktionsspezifische Notebook-Komponente liegt hier in einer separat gesicherten, statisch entpackten Version `GBT_Notebook_26.06.23.01`; sie enthält weiterhin die Logik für die 2022er AORUS-Modelle. Das ist **kein Beweis**, dass genau dieser Code in der ursprünglich auf diesem Laptop installierten Version identisch lief. Analysequellen:

- `third-party/vendor/gcc-archives/GCC_23.03.02.01/` (ältere GCC-Hülle)
- `third-party/vendor/GBT_Notebook_26.06.23.01-static/decompiled/ucNotebook/ucNotebook.Helper/Boost.cs`
- `third-party/vendor/GBT_Notebook_26.06.23.01-static/decompiled/ucNotebook/ucNotebook.Views/GeneralVD.cs`
- `third-party/vendor/GBT_Notebook_26.06.23.01-static/decompiled/ucNotebook/ucNotebook.Views/FanControlNb.cs`
- `third-party/vendor/GBT_Notebook_26.06.23.01-static/decompiled/ucNotebook/ucNotebook/NB_Controler.cs`

`Boost.SetDynamicBoost(bool)` ruft `root\\WMI:GB_WMIACPI_Set.SetDynamicBoostStatus` mit `Data=1` bzw. `0` auf (`Boost.cs:233-258`). Der Aufruf läuft asynchron, fängt Fehler ohne Meldung ab und liest den Zustand nicht zurück. Ein angezeigtes „aktiv“ in Gigabytes UI beweist daher nicht, dass die Firmware den Befehl angenommen hat.

Die Auswahl in `GeneralVD` ist zudem **mit einer Änderung der CPU-Stufe gekoppelt**: Bei `gpu_box.SelectedIndex==1` bzw. `gg_box.SelectedIndex==0` wird Dynamic Boost eingeschaltet und bei Netzbetrieb eine andere CPU-Stufe gesetzt; die übrigen Indizes schalten Dynamic Boost aus (`GeneralVD.cs:10211-10325`). Bei gespeicherten Modi kann der Schalter erneut gesetzt werden. Das kann einen einfachen Vorher/Nachher-Benchmark verfälschen: Er misst nicht isoliert Dynamic Boost.

Gigabytes generische Control-Center-Kurzanleitung nennt für „AI GPU Boost“ drei UI-Optionen: „GIGABYTE AI GPU Boost“, „NVIDIA Dynamic Boost“ und „Off“. Die dekompilierte Komponente belegt den WMI-Schalter, aber die exakte Zuordnung *aller* Beschriftungen zu ihren Indizes auf FB0F ist aus diesen Quellen allein noch nicht sicher. Die Anleitung ist modellübergreifend und kein Nachweis, dass alle drei Optionen beim SE4 verfügbar waren.

`FanControlNb` ruft bei Game/Turbo zusätzlich `Boost.SetGpu(1)` und bei anderen Modi `SetGpu(0)` auf. `Boost.SetGpu` wählt je nach `ComData.bNvPowerCfg` entweder `SetNvPowerCfg` oder `SetGpuOC` (`Boost.cs:106-118`). `NB_Controler` ordnet `AORUS 5 SE4` dem 5/S-Modellzweig (`MODEL_X5MSE`) zu, ohne dort `bNvPowerCfg` zu aktivieren (`NB_Controler.cs:1409-1473`). In `SetGpuOC` existieren feste positive Offsets für mehrere AORUS-15/17-Modelle, aber **kein Fall für AORUS 5 SE4** (`Boost.cs:154-230`). Daher bleiben in dieser statisch betrachteten Version die an `NvFunc.dll` übergebenen GPU- und Speicher-OC-Werte für dieses Modell bei null. Das heißt **nicht**, dass Game/Turbo nutzlos ist: Leistungsbudget, CPU, Lüfter und thermisches Verhalten können sich unabhängig davon ändern. Es ist auch keine Aussage über den internen NVIDIA-GPU-Boost.

`SetNvPowerCfg` ruft eine *andere* WMI-Methode (`SetNvPowerConfig`) mit Datenwert 0 oder 1 auf (`Boost.cs:121-152`). Diese Codewerte nicht mit Werten aus einem GPU-Deaktivierungs-/Eco-Pfad gleichsetzen; derselbe Methodenname kann an anderer Stelle anders verwendet werden. Der frühere Live-Getter `GetNvPowerConfig` wurde auf FB0F abgewiesen (siehe `FAN-POWER-GPU-CONTROL.md`).

Neuere AiNexus/Gpilot-Funktionen im Modul sind für weitere Geräteserien vorhanden, aber aus ihrem bloßen Vorhandensein folgt keine SE4-Unterstützung. Frühere Live-Tests lehnten die einschlägige AI-Power-Capability ab.

## Abgleich mit offiziellen Quellen

- [Gigabyte AORUS 5 SE4 Spezifikation](https://www.gigabyte.com/jp/Laptop/AORUS-5--Intel-12th-Gen/sp): RTX 3070 Laptop GPU, 1620 MHz Boost Clock, bis 130 W Graphics Power, Dynamic Boost 2.0 und Optimus.
- [Gigabyte Laptop GPUs Full Technology Guide](https://www.gigabyte.com/WebPage/784/): AORUS 5/VE unterstützt Dynamic Boost 2.0 und Optimus, aber keinen statischen MUX. Für 12.-Gen-Modelle nennt Gigabyte Gaming/Turbo als Kombination aus höherem Systemleistungsbudget und Dynamic Boost; Turbo setzt zusätzlich die Lüfter voll. Die MUX-Empfehlung dort ist ausdrücklich auf AORUS 17X beschränkt. Gigabyte verweist zur Kontrolle auf NVIDIA Control Panel → System Information → Details.
- [Gigabyte Control Center Quick Start Guide v1.2](https://download.gigabyte.com/FileList/Manual/VE_ControlCenter_QSG_Manual_v1.2.pdf?v=059d07658e3c7811b169c4fa5e545bdb): die drei erwähnten „AI GPU Boost“-Optionen; nicht modellspezifisch.
- [NVIDIA zu Dynamic Boost 2.0](https://www.nvidia.com/en-us/geforce/news/gfecnt/20211/rtx-30-series-laptops/): dynamische CPU-/GPU-/Speicher-Leistungsverteilung. „Bis zu 20 W“ ist eine allgemeine Technologieangabe, **kein** nachgewiesener SE4-Zuschlag.
- [NVIDIA Control Panel: Dynamic Boost](https://www.nvidia.com/content/Control-Panel-Help/vLatest/en-gb/mergedProjects/nv3dENG/Manage_3D_Settings_(reference).htm): On/Off ist ein möglicher Treiberschalter; tatsächliche Sichtbarkeit hängt von System und Treiber ab. On erlaubt bedarfsabhängige Verteilung, Off belässt Standardleistungswerte.

## Abgleich mit dem statisch analysierten FB0F-BIOS

Die zuvor extrahierten Setup-/IFR-Texte aus `RX5ME4FB0F.rom` enthalten **keinen benannten Menüeintrag für „Dynamic Boost“ oder „GPU Boost“**. Es gibt einen versteckten Intel-`Turbo Mode` (`CpuSetup`, Offset `0x16`); der betrifft den **CPU-Turbo**, nicht NVIDIA Dynamic Boost (`body.2.3.en-US.uefi.ifr.txt:2694`). Ebenso gibt es generische CPU-Power-Limit-Felder; diese sind keine direkten Regler für den RTX-3070-TGP.

Unter `Switchable Graphics` enthält das IFR bei `SG Mode Select` ausschließlich `Muxless` als Option (`:9565-9572`); `Primary Display` führt `HG`/Hybrid Graphics als Default (`:12241-12260`). Das passt zu Optimus ohne MUX, belegt aber allein weder die tatsächliche Laufzeitkonfiguration noch einen Dynamic-Boost-Schalter. Der allgemeine Gigabyte-GPU-Guide nennt beim AORUS 5/VE ebenfalls keinen statischen MUX.

Wichtig: IFR beschreibt Setup-Formulare und Texte, **nicht sämtliche ACPI-/EC-Routinen oder NVIDIA-Treiberfunktionen**. Dass der Menütext fehlt, widerlegt die offiziell bestätigte Dynamic-Boost-Unterstützung nicht. Die ROM-Analyse war zudem nicht vollständig bis in jedes komprimierte Modul; siehe `research/BIOS-FB0F-STATIC-ANALYSIS.md`.

## Was auf *diesem* Gerät noch offen ist

- Unsere App hat derzeit nur einen Diagnose-Getter für Dynamic Boost (`src/AorusControl.Diagnostics/Program.cs`), aber keinen produktiven Schalter. Die Windows-„Boost“-Beschreibungen in `WindowsSettingsViewModel` beziehen sich auf allgemeines CPU-/Windows-Leistungsverhalten, nicht auf Gigabytes Dynamic-Boost-WMI-Aufruf.
- Ein früherer read-only WMI-Lauf lieferte `GetDynamicBoostStatus=0`. Da mehrere benachbarte Getter auf FB0F nicht unterstützt werden und andere WMI-Antworten sich als Buffer-Artefakt erwiesen, ist das **kein verifizierter NVIDIA-Off-Zustand**. Kein Schreibtest wurde vorgenommen.
- Wir wissen nicht, ob `SetDynamicBoostStatus(0/1)` auf FB0F wirksam ist, ignoriert wird oder nur Gigabytes UI-Zustand beeinflusst.
- Die tatsächliche NVIDIA-Control-Panel-Anzeige, das *unter Last wirksame* GPU-Power-Limit und die Ursache der angezeigten Differenz zwischen 115 und 130 W sind noch nicht verifiziert. `nvidia-smi`/NVML-Abfragen können die schlafende RTX 3070 aufwecken; deshalb nicht nebenbei im Idle pollen.
- Der genaue Unterschied zwischen Gaming und Turbo auf *dieser Firmware* ist noch nicht quantitativ vermessen. Die offizielle Aussage und die statischen Aufrufe belegen die Richtung, aber keine stabilen Watt-/Taktwerte.

## Sinnvoller nächster Test (separat, mit Zustimmung und kontrolliert)

Zuerst nur read-only im NVIDIA Control Panel prüfen, ob Dynamic Boost als unterstützte/aktive Funktion erscheint; danach WMI-Getter isoliert mehrfach mit frisch erzeugtem Objekt lesen. Wenn der Nutzer einen Funktionstest will: am Netzteil, gleiche reproduzierbare GPU-Last, gleiche Lüfter-/CPU-Konfiguration, Temperaturen stabilisieren lassen; je Durchlauf GPU-Leistung, Takt, Temperatur, Auslastung und CPU-Leistung loggen. Erst danach einen *gezielten*, rücksetzbaren WMI-Schalttest erwägen, Ausgangszustand sichern, Resultat und Readback protokollieren. Kein blindes OC und kein BIOS-Flash. Für die eigene App wäre Dynamic Boost vorerst eine **experimentelle, capability-geprüfte** Option; nicht als GPU-Ein/Aus oder festes Watt-Slider ausgeben.

## Live-Treiberwerte und Temperaturgrenzen (2026-09-16)

Einmalig, nur lesend: `nvidia-smi -q -d TEMPERATURE,POWER,CLOCK,PERFORMANCE` auf dem laufenden Gerät, Treiber 616.92. **Keine GPU-Last, kein Setter.** Die Abfrage selbst weckte die GPU kurz; P0/1560 MHz und die momentanen 27,83 W sind daher *kein* belastbarer Leerlaufzustand. GPU-Temperatur beim Snapshot: 46 °C. Event-Grund `Idle=Active`, thermische und Power-Cap-Gründe nicht aktiv. Die Zähler seit Treiberstart zeigten 49.054 µs SW Power Capping und 0 µs SW/HW Thermal Slowdown; daraus folgt ohne Last-/Zeitkontext keine Aussage über Spieleleistung.

| Treiberfeld | Gemeldeter Wert | Einordnung |
| --- | ---: | --- |
| GPU Target Temperature | 87 °C | Zieltemperatur, die der Treiber unter Last anzustreben versucht; **nicht** die Notabschaltung und kein Beleg, dass Drosselung erst dort beginnt. |
| GPU Slowdown Temp | 98 °C | Grenze für starken Hardware-Taktabfall zur Kühlung. |
| GPU Shutdown Temp | 101 °C | Hardware-Schutzabschaltung; niemals als Betriebsziel verwenden. |
| GPU Max Operating Temp | 105 °C | Vom Treiber so ausgegeben, liegt aber *über* Slowdown und Shutdown. Die Felder sind in diesem Snapshot widersprüchlich; 105 °C **nicht** als sichere Grenze interpretieren. |
| Default Power Limit | 115 W | Vom NVIDIA-Treiber gemeldeter Standardwert. |
| Current / Max Power Limit | 130 / 130 W | Momentanes Treiberlimit im Snapshot, nicht gemessener Verbrauch. Der Abstand 15 W passt rechnerisch zu Dynamic Boost, beweist aber nicht dessen wirksamen Zustand oder Verhalten unter Last. |
| Min Power Limit | 1 W | Gemeldete technische Untergrenze, **keine** Empfehlung oder Bestätigung, dass der Nutzer einen sicheren 1–130-W-Regler hat. |

NVIDIA unterscheidet `SW Power Cap`, `SW Thermal Slowdown`, `HW Thermal Slowdown` und `HW Power Brake` als verschiedene Taktrücknahme-Gründe. Niedrigerer Takt ist also nicht automatisch „zu heiß“; bei GPU-Leistung nahe TGP kann sie auch bei deutlich weniger als 87 °C durch das Leistungsbudget begrenzt sein. Bei geringer Last taktet sie absichtlich herunter. Ein einmaliger P0-Snapshot ist kein Last-Benchmark. [NVIDIA nvidia-smi-Dokumentation](https://docs.nvidia.com/deploy/nvidia-smi/)

## Was wir tatsächlich einstellen können – und was nicht bestätigt ist

- **Bestätigt steuerbar:** Gigabyte-Lüfterprofile, feste Lüfterleistung und die 15-Punkt-Kurve (separat geprüft und mit Readback/Rollback dokumentiert in `research/FAN-POWER-GPU-CONTROL.md`). Die gespeicherte Werkskurve steigt von Rohwert 57 bei 0 °C bis 229 bei 89 °C; das ist ein *Lüfter-Stützpunkt*, **keine** GPU-Throttle- oder Abschalttemperatur.
- **Bestätigt steuerbar:** Windows-Leistungsmodus. Er beeinflusst Leistungspräferenz, setzt aber keinen festen GPU-Takt oder GPU-TGP.
- **Vorhanden, Funktion auf FB0F nicht getestet:** Gigabyte-WMI `SetDynamicBoostStatus(0/1)`. Die alte App verwendet es, aber wir haben keine Wirksamkeit durch Lasttest/Readback bestätigt.
- **Gigabyte `SetNvThermalTarget(0/1)`:** im Projekt bereits für Normal/Leise benutzt und zurückgelesen. Das ist ein binäres Gigabyte-/EC-Profilflag, **kein** Temperaturwert in °C und nicht nachweislich der NVIDIA-87-°C-Regler.
- **Nicht als nutzbare SE4-Einstellung belegt:** manuelles GPU-Power-Limit, GPU-Zieltemperatur per NVIDIA-CLI/NVML, GPU-/Speichertakt-Offset. NVIDIA dokumentiert entsprechende generische Schnittstellen, aber Treiber-/OEM-/Geräteunterstützung und Sicherheitsbereich wurden auf diesem Laptop nicht geprüft. Der positive Gigabyte-GPU-OC-Offset fehlt für `AORUS 5 SE4` in der analysierten Notebook-Komponente.
- **BIOS:** versteckter Intel-CPU-Turbo und generische PL1/PL2-Felder sind kein GPU-Boost-Menü und sollten nicht blind geschrieben werden.

Zur CPU: Der i7-12700H hat laut [Intel-Spezifikation](https://www.intel.com/content/www/us/en/products/sku/132228/intel-core-i712700h-processor-24m-cache-up-to-4-70-ghz/specifications.html) `Tjunction=100 °C`, 45 W Processor Base Power und 115 W Maximum Turbo Power. 100 °C ist eine Schutz-/Spezifikationsgrenze, **kein Zielwert** für unsere Lüfterkurve; der konkrete Takt wird vorher schon durch PL1/PL2, Temperatur, Last und Gigabyte-Profil beeinflusst. Die tatsächlich auf FB0F gesetzten CPU-Power-Limits haben wir hier nicht live ausgelesen.

## Wie viel zusätzlicher GPU-Boost ist als App-Funktion realistisch?

Abgleich 2026-09-16: Gigabyte bewirbt **130 W maximale Graphics Power** für das SE4. Auf *diesem* Laptop meldete der NVIDIA-Treiber `Default Power Limit=115 W`, `Current Power Limit=130 W`, `Max Power Limit=130 W`. Ein Händler führt die SE4-Konfiguration als `115 W + 15 W Dynamic Boost 2.0` auf; das passt zu den Live-Grenzen, ist aber keine direkte Messung der dynamischen Umverteilung unter Last. Quellen: [Gigabyte-Spezifikation](https://www.gigabyte.com/jp/Laptop/AORUS-5--Intel-12th-Gen/sp), [SE4-Produktangabe von Inet](https://www.inet.se/produkt/1974205/gigabyte-aorus-5-se4-15-6-i7-16gb-512gb-rtx-3070-144hz).

**Obergrenze nach aktuellem Beleg: 130 W, also nominell +15 W gegenüber dem 115-W-Standardwert.** Dynamic Boost *reserviert* diese 15 W nicht dauerhaft für die GPU; abhängig von CPU-Last, Thermik und Treiberzustand kann der tatsächliche Verbrauch niedriger bleiben. Ein Programm kann nicht per normalem Schalter 145/150 W als neues, sicheres SE4-Limit freischalten. Dafür wären Änderungen außerhalb der bestätigten OEM-Grenzen nötig (etwa VBIOS-/Firmware-Modifikation), die wir nicht als App-Funktion verfolgen.

NVIDIA dokumentiert `nvidia-smi --power-limit` bzw. `nvmlDeviceSetPowerManagementLimit` nur für **unterstützte** Geräte, mit Admin-Rechten und innerhalb gemeldeter Grenzen. Dass eine Abfrage hier `Min=1 W`, `Max=130 W` zeigt, ist **kein Nachweis**, dass ein Schreibversuch auf der Laptop-GPU akzeptiert würde oder dass 1 W ein sinnvoller Regelwert wäre. Wir haben keinen Power-Limit-Setter ausgeführt. Der Gigabyte-Modellpfad für `SetNvPowerConfig` ist beim SE4 nicht gesetzt, und dessen Getter scheiterte auf FB0F. Quellen: [NVIDIA nvidia-smi](https://docs.nvidia.com/deploy/nvidia-smi/), [NVIDIA NVML Power-Limit-API](https://docs.nvidia.com/deploy/archive/R520/nvml-api/group__nvmlDeviceCommands.html).

Für die eigene App ist deshalb zunächst eine **Dynamic-Boost-Option `Auto/Ein` oder `Aus`** realistisch, *falls* wir den Gigabyte-WMI-Aufruf auf FB0F mit Readback und Lastmessung bestätigen. Alternativ könnten Profile die bereits bestätigten Lüfter-/Windows-Leistungsmodi kombinieren, ohne ein freies Watt-Limit vorzutäuschen. Ein Watt-Slider wäre erst nach einem separaten, explizit genehmigten Schreibtest sinnvoll, der Schreibunterstützung, zulässige Werte, Persistenz, Konflikt mit Dynamic Boost und Rücksetzung beweist. Selbst dann nur innerhalb der OEM-/Treibergrenze, niemals darüber. Die UI sollte Ist-Watt, 115-W-Standard und 130-W-Maximum klar unterscheiden.
