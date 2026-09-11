# CPU-/GPU-Watt: Live-Prüfung vom 2026-09-09

## Ergebnis

CPU-Paketleistung ist auf diesem Laptop bereits über Windows Energy Meter/RAPL lesbar. Kein zusätzlicher Hardwaretreiber oder installiertes AORUS Control erforderlich. NVIDIA liefert Wattwerte, deren zeitliche Bedeutung und Einfluss auf Runtime-Power-Management vor einer zyklischen Integration weiter geprüft werden müssen.

Die App wurde in diesem Schritt nicht verändert. Keine Hardware-Setter, Treiberinstallation, Änderung von Energieprofilen oder künstlicher Stresstest. Eine einzelne nvidia-smi-Abfrage wurde bewusst nach den CPU-Proben ausgeführt; diese kann die dGPU aufwecken.

## Umgebung und vorhandene Quellen

- Intel Core i7-12700H; NVIDIA RTX 3070 Laptop GPU (Treiber 616.64); Intel Iris Xe (31.0.101.3616).
- Netzbetrieb: PowerOnline=true, Discharging=false. DischargeRate=0 bedeutet hier keine verfügbare Entlademessung.
- Akku-Laderate zu zwei Zeitpunkten: 6160 und 5974 mW. Das ist Akkuladung, nicht Laptop-Leistungsaufnahme.
- Windows WMI: Win32_PerfFormattedData_PowerMeterCounter_EnergyMeter liefert RAPL_Package0_PKG, PP0, PP1, DRAM und _Total.
- Win32_PerfFormattedData_PowerMeterCounter_PowerMeter liefert Power Meter (0) und _Total, beide Power=0. Kein brauchbarer Gesamtverbrauch aus diesen Nullwerten.
- root/Hardware NumericSensor lieferte Allgemeiner Fehler.
- Kein laufender HWiNFO-Prozess gefunden; vorhandene HWiNFO- und WinRing0-Treibereinträge waren gestoppt. Sie wurden nicht gestartet.
- Der vorhandene WindowsPowerSampler misst CPU-/GPU-Auslastung und Akkuentladung, bisher keine CPU-/GPU-Einzelwattwerte. Heutiger separater 15-s-Lauf: runs/power-monitor-v2-20260909-182414.md.

## CPU: Einheiten und Messweg

Lokale Windows-CounterHelp, direkt aus PerformanceCounterCategory("Energy Meter"):

- Power: durchschnittliche Leistungsaufnahme pro Stichprobenintervall, Milliwatt.
- Energy: Gesamtenergie des gemessenen Kanals, Picowattstunden.
- Time: Dauer des Messvorgangs, Millisekunden.

Wichtig: Power ist ein PERF_AVERAGE_BULK-Zähler. RawValue ist kein direkt verwendbarer Milliwattwert. Zwei CounterSample-Proben mit CounterSample.Calculate(previous, current) auswerten und durch 1000 teilen. Kategorie mit ReadCategory einmal je Intervall lesen.

Kontrollrechnung aus derselben zugrunde liegenden Energiequelle:
W = delta(Energy in pWh) * 0.0000036 / delta(Time in ms).

Dies prüft Umrechnung und zeitliche Konsistenz, ist keine unabhängige physikalische Kalibrierung. RAPL liefert Energie-Telemetrie; keine aus Auslastung oder TDP erfundene Wattzahl.

Sechs Intervalle von etwa zwei Sekunden, normale laufende Hintergrundarbeit:
| Intervall | CPU-Paket W | CPU-Kerne W | Intel PP1 W | DRAM W | Kategorieabfrage ms |
|---|---:|---:|---:|---:|---:|
| 1 | 25.1298 | 16.7233 | 0.1942 | 0 | 1.47 |
| 2 | 29.9294 | 21.2305 | 0.3691 | 0 | 0.53 |
| 3 | 22.5716 | 14.2205 | 0.0934 | 0 | 0.51 |
| 4 | 31.8648 | 23.1594 | 0.0893 | 0 | 0.35 |
| 5 | 31.2848 | 22.3893 | 0.3628 | 0 | 0.37 |
| 6 | 27.0793 | 18.3922 | 0.4634 | 0 | 0.32 |

Maximale Abweichung CounterWatts gegenüber Kontrollrechnung: etwa 0,0102 W.
Die Kategorieabfrage dauerte 0,32–1,47 ms, ohne Erstinitialisierung. Der gesamte PowerShell-Prozess verbrauchte im Messfenster 1062,5 ms CPU-Zeit; darin stecken Skriptverarbeitung/JIT und Ausgabevorbereitung. Daraus keine CPU-Kosten der späteren C#-Integration ableiten.

## Interpretation der Intel-Kanäle

- PKG: CPU-Paket; geeigneter Hauptwert im Dashboard.
- PP0: CPU-Kerne, Teil des Pakets.
- PP1: nach Intels RAPL-Domänenmodell integrierte Grafik; auf diesem System 0,0893–0,4634 W. Zuordnung auf diesem Modell noch nicht durch gezielte iGPU-Last gegengeprüft.
- CPU-Paket und PP0/PP1 nicht summieren: überlappende Domänen.
- DRAM blieb mit Energiezuwachs=0 konstant null: nicht als nachgewiesene 0-W-RAM-Messung präsentieren.
- _Total ist kein Gesamtverbrauch des Laptops und kein sinnvoller Ersatz für PKG.

## NVIDIA: genau eine aktuelle Probe

Befehl:
nvidia-smi --query-gpu=name,driver_version,pstate,power.draw,power.draw.average,power.draw.instant,utilization.gpu,temperature.gpu,display_active --format=csv

Ergebnis:
- RTX 3070 Laptop GPU, Treiber 616.64
- P0
- power.draw: 27,09 W
- power.draw.average: 1,00 W
- power.draw.instant: 27,09 W
- utilization.gpu: 0 %
- Temperatur: 47 °C
- display_active: Disabled

Die Abfrage bestätigt Verfügbarkeit, nicht Genauigkeit im vorherigen Schlafzustand. Durchschnitts- und Momentanwert beziehen sich auf unterschiedliche Zeitfenster. Ein möglicher Aufwachübergang ist eine Erklärung für die Diskrepanz, wurde heute aber nicht unabhängig beobachtet. Kein Dauerpolling gestartet.

NVIDIA dokumentiert POWER_AVERAGE als 1-s-Mittel und POWER_INSTANT als aktuellen Wert. Die alte Notiz GPU-IDLE-POWER.md erklärt power.draw pauschal für unbrauchbar und leitet RTD3 teils aus 0-%-Aktivität, Gesamtsystemverbrauch oder fehlendem VRAM ab. Diese Aussagen sind zu kategorisch:
- 0 % GPU-Aktivität beweist keinen D3-Zustand.
- Unterschiedliche Messfenster von Akku und GPU dürfen nicht als gleichzeitige Präzisionsmessung verglichen werden.
- Frühere Akku-Baseline/Weckreiz/Erholung unterstützen die Hypothese eines Aufweckeffekts, isolieren aber nicht sämtliche Hintergrundlast oder Treibereffekte.
- Ein zuverlässiges Schlafsignal und der aktuelle Mehrverbrauch durch Monitoring sind heute nicht nachgewiesen.

## Integration und verbleibende Prüfungen

1. CPU-Paket-Watt über Windows Energy Meter ist der beste unmittelbar belegte Integrationsweg. Nach Start/Resume erste Probe überspringen; Zählerreset, fehlende Instanz und nicht fortschreitende Zeit erkennen.
2. 1–2 Sekunden Intervall bei sichtbarem Dashboard; danach tatsächliche C#-Kosten messen.
3. Intel-PP1 optional mit transparenter Kennzeichnung und gezielter Lastprüfung.
4. Für RTX keine blinde Dauerabfrage. Nächster Test: kontrollierter Akkubetrieb mit Ruhephase, begrenzter Abfragephase und Erholung; danach aktive GPU-Last mit einer unabhängigen Anzeige vergleichen. Dafür muss das Netzteil physisch getrennt werden. Keine Abwesenheit von Messwerten als Schlafzustand oder 0 W ausgeben.
5. Gesamter Laptopverbrauch nur über gültige Akkuentladung im Akkubetrieb beziehungsweise ein externes Messgerät am Netz. CPU+GPU ist nicht der Gesamtverbrauch.
6. Kein Installieren eines Low-Level-Treibers nötig, um die hier gefundenen CPU-Kanäle zu lesen.

## Quellen

- Lokal: Windows Energy Meter CounterHelp und heutige echte Messproben.
- Intel RAPL Energie-Domänen: https://www.intel.com/content/www/us/en/developer/articles/technical/software-security-guidance/advisory-guidance/running-average-power-limit-energy-reporting.html
- Intel Software Developer Manual, RAPL PP0/PP1: https://cdrdv2-public.intel.com/868137/325462-089-sdm-vol-1-2abcd-3abcd-4.pdf
- NVIDIA Felddefinitionen: https://docs.nvidia.com/deploy/nvml-api/group__nvmlFieldValueEnums.html
- NVIDIA Geräteabfragen: https://docs.nvidia.com/deploy/nvml-api/group__nvmlDeviceQueries.html

## Rohwerte des CPU-Intervalltests

```json
[
  {
    "Sample": 1,
    "Channel": "RAPL_Package0_PKG",
    "CounterWatts": 25.1298,
    "EnergyDeltaPWh": 14478078888,
    "TimeDeltaMs": 2074,
    "DeltaWatts": 25.1307,
    "QueryMs": 1.47
  },
  {
    "Sample": 1,
    "Channel": "RAPL_Package0_PP0",
    "CounterWatts": 16.7233,
    "EnergyDeltaPWh": 9634831389,
    "TimeDeltaMs": 2074,
    "DeltaWatts": 16.7239,
    "QueryMs": 1.47
  },
  {
    "Sample": 1,
    "Channel": "RAPL_Package0_PP1",
    "CounterWatts": 0.1942,
    "EnergyDeltaPWh": 111867223,
    "TimeDeltaMs": 2074,
    "DeltaWatts": 0.1942,
    "QueryMs": 1.47
  },
  {
    "Sample": 1,
    "Channel": "RAPL_Package0_DRAM",
    "CounterWatts": 0,
    "EnergyDeltaPWh": 0,
    "TimeDeltaMs": 2074,
    "DeltaWatts": 0,
    "QueryMs": 1.47
  },
  {
    "Sample": 2,
    "Channel": "RAPL_Package0_PKG",
    "CounterWatts": 29.9294,
    "EnergyDeltaPWh": 16912317778,
    "TimeDeltaMs": 2034,
    "DeltaWatts": 29.9333,
    "QueryMs": 0.53
  },
  {
    "Sample": 2,
    "Channel": "RAPL_Package0_PP0",
    "CounterWatts": 21.2305,
    "EnergyDeltaPWh": 11996819166,
    "TimeDeltaMs": 2034,
    "DeltaWatts": 21.2333,
    "QueryMs": 0.53
  },
  {
    "Sample": 2,
    "Channel": "RAPL_Package0_PP1",
    "CounterWatts": 0.3691,
    "EnergyDeltaPWh": 208552222,
    "TimeDeltaMs": 2034,
    "DeltaWatts": 0.3691,
    "QueryMs": 0.53
  },
  {
    "Sample": 2,
    "Channel": "RAPL_Package0_DRAM",
    "CounterWatts": 0,
    "EnergyDeltaPWh": 0,
    "TimeDeltaMs": 2034,
    "DeltaWatts": 0,
    "QueryMs": 0.53
  },
  {
    "Sample": 3,
    "Channel": "RAPL_Package0_PKG",
    "CounterWatts": 22.5716,
    "EnergyDeltaPWh": 12582944445,
    "TimeDeltaMs": 2007,
    "DeltaWatts": 22.5703,
    "QueryMs": 0.51
  },
  {
    "Sample": 3,
    "Channel": "RAPL_Package0_PP0",
    "CounterWatts": 14.2205,
    "EnergyDeltaPWh": 7927492222,
    "TimeDeltaMs": 2007,
    "DeltaWatts": 14.2197,
    "QueryMs": 0.51
  },
  {
    "Sample": 3,
    "Channel": "RAPL_Package0_PP1",
    "CounterWatts": 0.0934,
    "EnergyDeltaPWh": 52070278,
    "TimeDeltaMs": 2007,
    "DeltaWatts": 0.0934,
    "QueryMs": 0.51
  },
  {
    "Sample": 3,
    "Channel": "RAPL_Package0_DRAM",
    "CounterWatts": 0,
    "EnergyDeltaPWh": 0,
    "TimeDeltaMs": 2007,
    "DeltaWatts": 0,
    "QueryMs": 0.51
  },
  {
    "Sample": 4,
    "Channel": "RAPL_Package0_PKG",
    "CounterWatts": 31.8648,
    "EnergyDeltaPWh": 17714705000,
    "TimeDeltaMs": 2002,
    "DeltaWatts": 31.8546,
    "QueryMs": 0.35
  },
  {
    "Sample": 4,
    "Channel": "RAPL_Package0_PP0",
    "CounterWatts": 23.1594,
    "EnergyDeltaPWh": 12875049723,
    "TimeDeltaMs": 2002,
    "DeltaWatts": 23.1519,
    "QueryMs": 0.35
  },
  {
    "Sample": 4,
    "Channel": "RAPL_Package0_PP1",
    "CounterWatts": 0.0893,
    "EnergyDeltaPWh": 49664166,
    "TimeDeltaMs": 2002,
    "DeltaWatts": 0.0893,
    "QueryMs": 0.35
  },
  {
    "Sample": 4,
    "Channel": "RAPL_Package0_DRAM",
    "CounterWatts": 0,
    "EnergyDeltaPWh": 0,
    "TimeDeltaMs": 2002,
    "DeltaWatts": 0,
    "QueryMs": 0.35
  },
  {
    "Sample": 5,
    "Channel": "RAPL_Package0_PKG",
    "CounterWatts": 31.2848,
    "EnergyDeltaPWh": 17521606111,
    "TimeDeltaMs": 2016,
    "DeltaWatts": 31.2886,
    "QueryMs": 0.37
  },
  {
    "Sample": 5,
    "Channel": "RAPL_Package0_PP0",
    "CounterWatts": 22.3893,
    "EnergyDeltaPWh": 12539515833,
    "TimeDeltaMs": 2016,
    "DeltaWatts": 22.392,
    "QueryMs": 0.37
  },
  {
    "Sample": 5,
    "Channel": "RAPL_Package0_PP1",
    "CounterWatts": 0.3628,
    "EnergyDeltaPWh": 203197778,
    "TimeDeltaMs": 2016,
    "DeltaWatts": 0.3629,
    "QueryMs": 0.37
  },
  {
    "Sample": 5,
    "Channel": "RAPL_Package0_DRAM",
    "CounterWatts": 0,
    "EnergyDeltaPWh": 0,
    "TimeDeltaMs": 2016,
    "DeltaWatts": 0,
    "QueryMs": 0.37
  },
  {
    "Sample": 6,
    "Channel": "RAPL_Package0_PKG",
    "CounterWatts": 27.0793,
    "EnergyDeltaPWh": 15171818333,
    "TimeDeltaMs": 2017,
    "DeltaWatts": 27.0791,
    "QueryMs": 0.32
  },
  {
    "Sample": 6,
    "Channel": "RAPL_Package0_PP0",
    "CounterWatts": 18.3922,
    "EnergyDeltaPWh": 10304662222,
    "TimeDeltaMs": 2017,
    "DeltaWatts": 18.3921,
    "QueryMs": 0.32
  },
  {
    "Sample": 6,
    "Channel": "RAPL_Package0_PP1",
    "CounterWatts": 0.4634,
    "EnergyDeltaPWh": 259639722,
    "TimeDeltaMs": 2017,
    "DeltaWatts": 0.4634,
    "QueryMs": 0.32
  },
  {
    "Sample": 6,
    "Channel": "RAPL_Package0_DRAM",
    "CounterWatts": 0,
    "EnergyDeltaPWh": 0,
    "TimeDeltaMs": 2017,
    "DeltaWatts": 0,
    "QueryMs": 0.32
  }
]
```

