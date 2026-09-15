# Wohin die Watt gehen

Stand: 2026-09-15. Gemessen am AORUS 5 SE4, BIOS FB0F, im Akkubetrieb, NVIDIA-Karte
durchgehend in D3 (schlafend), Windows-Leistungsmodus „Energieeffizienz".

Anlass: Die neue Stromfluss-Karte zeigte 45 W, obwohl die Grafikkarte schlief und die CPU nur
17,9 W meldete. Die Frage war, wo der Rest herkommt.

## Zuerst das Messgerät

Der Akku ist die einzige Quelle für den Gesamtverbrauch, und bevor man ihm etwas glaubt, muss
man wissen, wie schnell er antwortet. Eine Minute lang im Sekundentakt abgefragt:

```
  0,2s  28945 mW
 16,8s  24243 mW
 30,3s  23165 mW
 38,7s      0 mW     <- Aussetzer, kein Messwert
 47,0s  22355 mW
```

**Die Rate erneuert sich nur alle 8 bis 16 Sekunden**, und gelegentlich kommt für eine Probe
eine glatte 0 zurück. Daraus folgen zwei Dinge, die jede weitere Auswertung bestimmen:

- Ein Vergleich Sekunde für Sekunde gegen die RAPL-Werte der CPU ist **unzulässig**. Nur
  Mediane über Minuten tragen.
- Der erste Helligkeitsversuch mit 20 Sekunden pro Stufe ergab entsprechend Unsinn: 0 %
  angeblich teurer als 50 %, ohne monotonen Verlauf. Verworfen, nicht interpretiert.

Die Aussetzer erklären zusätzlich ein Flackern der Dashboardkarte zwischen Zahl und
Gedankenstrich; sie werden seither über `BatteryFlowMath.Bridge` für die Dauer eines
Aktualisierungsintervalls überbrückt.

## Die Aufschlüsselung

90 Proben über drei Minuten, Helligkeit 10 %
(`runs/power-breakdown-20260915-193425.md`):

| Posten | Minimum | Median | Maximum | Streuung |
| --- | ---: | ---: | ---: | ---: |
| **Gesamt (Akku)** | 18,1 | **24,4** | 33,1 | 2,5 |
| CPU-Paket (PKG) | 8,8 | 12,1 | 19,8 | 1,8 |
| … davon Kerne (PP0) | 2,4 | 4,1 | 11,5 | 1,5 |
| … davon iGPU (PP1) | 0,0 | 0,2 | 0,6 | – |
| … davon **Uncore** (PKG−PP0−PP1) | 6,3 | **7,8** | 9,4 | **0,5** |
| Arbeitsspeicher (DRAM) | 0,0 | 0,0 | 0,0 | – |
| **Rest** (Gesamt−PKG−DRAM) | 4,7 | **12,1** | 19,5 | 2,4 |
| CPU-Last % | 13 | 21 | 40 | 5 |

### Zwei Grundbeträge, ein kleiner variabler Anteil

**Der Uncore ist der grösste Einzelposten der CPU und praktisch konstant.** Von 12 W
Paketleistung sind nur 4 W die Rechenkerne; 7,8 W gehen an Speichercontroller, Ring,
Display-Engine und PCIe-Anbindung — mit einer Streuung von 0,5 W über die gesamte Messung,
unabhängig davon, ob die Last bei 13 % oder 40 % liegt.

**Der Rest ist ebenfalls lastunabhängig.** Korrelation zur CPU-Last: −0,27, also keine. Die
zehn ruhigsten Proben ergeben 12,4 W, die zehn geschäftigsten 12,6 W.

| | ruhigste 10 Proben | geschäftigste 10 |
| --- | ---: | ---: |
| CPU-Last | 16 % | 32 % |
| CPU-Paket | 10,7 W | 15,9 W |
| Gesamt | 23,8 W | 25,7 W |
| Rest | 12,4 W | 12,6 W |

Die Zeile „Gesamt" in dieser Tabelle zeigt zugleich die Grenze des Verfahrens: Das Paket
steigt um 5,2 W, der Gesamtwert nur um 1,9 W. Das ist kein Widerspruch, sondern die
Trägheit des Akkus — er hat den Ausschlag noch nicht gesehen.

### Der Arbeitsspeicher wird nicht gemessen

Die RAPL-Domäne `DRAM` meldet konstant 0,0 W. Sie ist auf dieser Plattform nicht bestückt.
Der Arbeitsspeicher ist also nicht kostenlos, er steckt unsichtbar im Rest.

## Was der Bildschirm kostet

Differenzmessung mit Rückkehr auf die Ausgangsstufe, 65 Sekunden pro Stufe — angepasst an
das Aktualisierungsintervall des Akkus (`runs/brightness-power-20260915-195408.md`):

| Helligkeit | Gesamt | CPU-Paket | Rest ohne CPU |
| ---: | ---: | ---: | ---: |
| 0 % | 24,4 W | 12,2 W | 12,2 W |
| 100 % | 29,5 W | 12,0 W | 17,5 W |
| 0 % (Kontrolle) | 25,0 W | 11,8 W | 13,1 W |

**Der Bildschirm kostet von minimaler bis voller Helligkeit rund 5 W.** Die Paketleistung
blieb dabei innerhalb von 0,4 W konstant, der Unterschied ist also wirklich das Panel und
nicht die CPU. Die Kontrollmessung weicht um 0,6 W von der Ausgangsmessung ab; das ist die
Messunsicherheit dieses Verfahrens.

Wichtig: 0 % ist bei diesem Panel nicht „aus", sondern minimale Hintergrundbeleuchtung. Deren
Grundbetrag steckt weiterhin im Rest.

## Die Bilanz

Bei rund 24 W im Leerlauf mit schlafender Grafikkarte und 10 % Helligkeit:

| Posten | Watt | Art |
| --- | ---: | --- |
| Rechenkerne | ~4 | lastabhängig, 2 bis 12 |
| Uncore der CPU | ~8 | fest, solange der Rechner wach ist |
| Rest: Board, RAM, SSD, WLAN, Panel-Minimum, Wandlerverluste | ~12 | fest |
| Bildschirm zusätzlich bei voller Helligkeit | bis +5 | vom Nutzer steuerbar |

Zur Ausgangsfrage nach den 45 W: Dort meldete das Paket 17,9 W statt der hier gemessenen 12 W
— also deutlich mehr Last —, und der Gesamtwert kann wegen der Trägheit des Akkus zusätzlich
aus einem geschäftigeren Moment stammen als die daneben stehende CPU-Zahl. Ein
augenblicklicher „Rest" aus der Differenz der beiden Karten ist deshalb nicht belastbar; über
Minuten gemittelt ist er es.

## Was daraus folgt

- **Die grössten Hebel für die Laufzeit** sind die Helligkeit (bis 5 W) und alles, was die
  CPU aus dem Leerlauf holt. Der Uncore und der Grundbetrag der Plattform — zusammen 20 W —
  sind von aussen nicht zu beeinflussen.
- **Die schlafende Grafikkarte kostet nichts.** Sie lag durchgehend in D3 und taucht in
  keiner Zahl auf. Die Programmzuordnung unter Leistung & Akku arbeitet also an der richtigen
  Stelle.
- **Noch offen:** eine Differenzmessung mit geweckter RTX. Sie wäre die einzige ehrliche
  Angabe, was die Karte im Akkubetrieb wirklich kostet.

## Werkzeuge

```powershell
tools\Start-PowerBreakdown.ps1 -Minutes 3
tools\Start-BrightnessPowerSweep.ps1 -Levels @(0,100,0) -SettleSeconds 25 -SamplesPerLevel 20
```

Beide nur lesend, ausgenommen die Helligkeit, die der Sweep setzt und am Ende — auch bei
Abbruch — wiederherstellt. Beide brechen am Netz mit einer Erklärung ab, weil es dort keinen
Gesamtwert gibt, gegen den sich rechnen liesse.
