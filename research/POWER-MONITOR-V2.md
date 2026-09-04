# Verbrauchsmonitor V2 – 2026-09-03

## Implementierung

- Wiederverwendbare Messung in `Core/Features/PowerMonitoring`, Konsolenablauf/Bericht in `Diagnostics/Features/PowerMonitoring`. Alte lokale Monitorfunktionen durch einen kleinen Aufruf ersetzt; wiederhergestellte RGB-Diagnosen unverändert.
- CPU über `GetSystemTimes`-Differenzen; Kernelzeit enthält Idle und wird entsprechend verrechnet. Keine Abfrage jedes Prozesses und kein WMI-CPU-Sampling.
- GPU über einen gemeinsamen `PerformanceCounterCategory.ReadCategory`-Snapshot pro Intervall. Vorherige Rohproben bleiben erhalten; Berechnung mit `CounterSample.Calculate(previous, current)`.
- Adapter-/Engine-IDs aus jeder aktuellen Zählerinstanz, keine feste LUID. Noch keine Zuordnung zu Intel/NVIDIA-Modellnamen. Neue Instanzen benötigen eine Basisprobe; unvollständige Intervalle erscheinen nicht als Null.
- Pro Engine werden Prozesswerte summiert; angezeigt wird die stärkste Engine je Adapter, begrenzt auf 0–100 %. Das ist Aktivität, nicht Watt oder ein D3-Stromzustand.
- Akku nur bei aktivem, tatsächlich entladendem Akku mit gültiger Rate. Netzbetrieb, Null und Unknown-Sentinel bedeuten nicht verfügbar. Messfehler stehen in Konsole und Bericht.
- Kein nvidia-smi, NVML oder Hardware-Setter. Dies allein beweist nicht, dass jede Windows-Zählerabfrage auf jeder Treiberversion vollkommen ohne Power-Nebeneffekt ist.
- Der Starter baut den aktuellen Diagnosequelltext ausdrücklich vor dem Start, statt stillschweigend eine ältere EXE zu verwenden.

## Messung und Optimierung

Erster Lauf mit einzelnen persistenten PerformanceCounter-Objekten: `runs/power-monitor-v2-20260903-171618.md`. Nach dem Start 562–599 ms pro Probe, 4234 ms eigene CPU-Zeit bei 19,8 s Laufzeit, 61,9 MiB Working Set.

Diese Kosten waren für dauerhaftes Monitoring zu hoch. Deshalb Sammelabfrage statt einer Providerabfrage je Instanz eingeführt.

Zweiter Lauf: `runs/power-monitor-v2-20260903-171744.md`. Startprobe 746 ms, Folgeproben 20–29 ms. Eigene CPU-Zeit 719 ms bei 18,9 s Laufzeit, 58,5 MiB Working Set. CPU-Auslastung 9,9–15,9 % nach Basisprobe; drei GPU-IDs mit unterschiedlichen Aktivitätswerten. Beide Läufe am Netz: Akkuentladung korrekt nicht verfügbar.

Die kurzen Läufe haben unterschiedliche Systemlast und beweisen keine exakte allgemeine Einsparquote. Sie zeigen eine deutliche Reduktion des Messaufwands auf diesem Laptop. Working Set und CPU-Zeit betreffen die Diagnosetools inklusive Startaufwand, nicht die fertige App.

## Tests

- Solution Release-Build erfolgreich, weiterhin 0 gemeldete Warnungen/Fehler (lokale Ausnahmen im wiederhergestellten Legacy-Code bleiben dokumentiert).
- Acht neue deterministische Rechentests: CPU-Normalisierung, leeres Intervall, zurückgesetzte Zähler, ungültiges Idle, mW→W, Netzbetrieb, Unknown-Sentinel, fehlende Rate.
- Sieben vorhandene simulierte App-Lüfterschutztests bestehen weiterhin.
- Zwei echte Read-only-Läufe erfolgreich beendet und gespeichert. Keine Hardwarezustände absichtlich verändert.

## Noch offen

- Akkubetrieb live, Langzeittest, Berechtigungsfehler, Standby/Resume und Adapter-/Prozesswechsel gezielt testen.
- GPU-Aggregation mit simulierten Instanzwechseln testen und mit einer unabhängigen Anzeige vergleichen; bisher nur Live-Plausibilität, keine vollständige Genauigkeitsvalidierung.
- Lesbare Adapternamen über belegte LUID-Zuordnung ergänzen; unbekannte/virtuelle Adapter nicht der NVIDIA-GPU zuschlagen.
- Der Sampler ist für einen einzelnen Hintergrund-Worker ausgelegt. Noch keine Einbindung als dauerhaft laufende UI-/Dienstfunktion. Bei Integration sichtbarkeitsabhängige Abfrage und Lebenszyklus berücksichtigen.
- Frühere GPU-Strombehauptungen und Nullwertberichte bleiben historisch; V2 leitet keine Verbrauchsursache aus fehlender Aktivität ab.

## Primärquellen zur verwendeten Berechnung

- [GetSystemTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes): Kernelzeit enthält Idle, Zeiten über die Prozessoren aggregiert.
- [ReadCategory](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.performancecountercategory.readcategory?view=windowsdesktop-10.0): gemeinsames Auslesen der Kategorie statt wiederholter Einzelabfragen.
- [CounterSample.Calculate](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.countersample.calculate?view=windowsdesktop-10.0): Berechnung aus zwei Proben.
