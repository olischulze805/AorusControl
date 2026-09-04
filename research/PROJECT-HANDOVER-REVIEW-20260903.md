# Sichtung des gemeinsamen Projektstands

Stand: 2026-09-03. Auftrag: die zwischenzeitlichen Arbeiten sichten, nicht reparieren. Keine Hardwarezustände geändert. Ohne Git-Historie ist die Autorenschaft einzelner Änderungen nicht sicher nachweisbar; diese Sichtung beschreibt Dateien und Befunde, nicht eine persönliche Zuordnung von Fehlern.

## Vorhandene Fortschritte

- `KEYBOARD-BRIGHTNESS.md`: dokumentierte Auflösung der Helligkeitsfrage. Der Wiederholungstest mit 0/24/32/50 zeigt aus/niedrig/mittel/hell unabhängig von der zuvor per Fn+Space gewählten Stufe. Frühere Zwischenwerte 17/25/33 verfehlten die gültigen Stufen. Die exakte Behandlung aller Nachbarwerte bleibt offen. Bereits im Core und in der App vorhanden.
- `APP-KEYBOARD-RGB.md`: fünf hostseitige Effektgeschwindigkeiten und verbesserte Lesbarkeit der vorhandenen WPF-Steuerelemente dokumentiert; Umsetzung vorhanden.
- `BIOS-FB0F-STATIC-ANALYSIS.md`: statische Analyse umfangreicher, größtenteils verborgener Setup-Strukturen vorhanden. Enthaltene Optionen sind ausdrücklich kein Nachweis nutzbarer oder sicher schreibbarer Funktionen.
- `GPU-IDLE-POWER.md`: Untersuchung des GPU-Leerlaufs mit bewusstem nvidia-smi-Weckreiz; dokumentierte Systemverbrauchswerte ungefähr 20,8 W vorher, 34,3 W im Mittel danach (43 W Spitze), 21,5 W nach Ruhe. Plausibler Hinweis auf vorübergehenden Mehrverbrauch durch die Abfrage und anschließende Erholung. Kein direkt gemessener D3-Stromzustand.
- `Start-PowerDrawMonitor.ps1` sowie `RunPowerDrawMonitor`/`CapturePowerSample` und zwei Laufberichte vorhanden. Ziel: Akkuentladung mit CPU-/GPU-Aktivität korrelieren, ohne zyklisches nvidia-smi.

## Offene technische Befunde

1. Gesamtbuild erneut geprüft: 159 Fehler, 0 Warnungen. In Diagnostics fehlen unter anderem Definitionen von RGB-Testfunktionen und gemeinsamen Helfern (`Escape`, `FindRepositoryRoot`). Die CPU-Snapshot-Variablen stehen nach den frühen Aufrufen und führen zusätzlich zu CS0165. Der aktuelle Diagnosequelltext ist daher kein fertiger übernehmbarer Stand. Ursache/Autorenschaft der fehlenden Teile ist nicht nachweisbar.
2. GPU-Counter werden in `CapturePowerSample` pro Messung neu angelegt, einmal mit `NextValue()` gelesen und verworfen. Für differenzbasierte Counter fehlt damit die zweite Probe. Microsoft dokumentiert den ersten Wert solcher Counter als 0. Quelle: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.performancecounter.nextvalue?view=windowsdesktop-10.0
3. Batterie-/GPU-Ausnahmen werden in `PowerSample.Error` gesammelt, aber nicht in den Tabellen ausgegeben. Fehler können dadurch wie echte Nullwerte aussehen.
4. GPU-Zuordnung nutzt fest eingebauten LUID `0x0001149C`, statt die Adapter im jeweiligen Lauf zu identifizieren. Alle anderen Instanzen werden als dedizierte GPU gezählt; das muss vor zuverlässiger Nutzung ersetzt werden.
5. Bericht 15:43 zeigt CPU-Gesamtwerte bis 2000 %. Bericht 15:48 zeigt durchgehend 0 % für CPU und beide GPUs. Beides ist keine belastbare Verbrauchszuordnung. Die aktuelle Codefassung versucht CPU-Zeitdifferenzen statt WMI zu verwenden, ist aber nicht erfolgreich gebaut; alte Berichte validieren diese neue Fassung nicht.
6. Der automatisch erzeugte Satz „keine dGPU-Aktivität, daher stammt die Verbrauchsschwankung nicht von der RTX“ folgt nicht aus den Daten. Keine gemessene Rechenaktivität ist kein Nachweis fehlender Leistungsaufnahme; der Bericht nennt diese Einschränkung später sogar selbst.
7. `GPU-IDLE-POWER.md` formuliert „RTD3 bewiesen“, „es gibt kein Problem“ und die exklusiv der GPU zugeordneten ca. 22 W zu absolut. Die Gesamtverbrauchsmessung ist ein Indiz, kein direkter Zustandsnachweis und keine isolierte GPU-Leistungsmessung. Auch „externer Monitor ist der einzige Fall“ ist durch diese Versuche nicht belegt.
8. Das GPU-Dokument widerspricht sich am Ende: erst kein zyklisches Abfragen von P-State/Leistung, dann doch eine Diagnoseseite mit genau diesen Anzeigen. Vor Umsetzung bereinigen. Behauptungen zu Treiberdeaktivierung und Deinstallation sind historische Notizen, keine in dieser Sichtung neu bestätigten Handlungsanweisungen.
9. Verbrauchs-Starter baut nur bei fehlender EXE. Eine vorhandene ältere EXE kann vom heutigen Quellstand abweichen. Vor weiteren Tests explizit erfolgreich neu bauen.

## Empfohlene Fortsetzung

Zuerst fehlende Diagnosefunktionen aus einer verifizierten Sicherung/IDE-Historie wiederfinden, ohne aktuelle Änderungen zu überschreiben. Dann Verbrauchsmonitor separat strukturieren, Messfehler als nicht verfügbar anzeigen, Counter zwischen Proben behalten und Adapter dynamisch zuordnen. Erst nach erfolgreichem Build und plausiblen Messwerten die GPU-Verbrauchshypothese erneut kontrolliert prüfen. Keine GPU-Abschaltung oder BIOS-Schreibversuche daraus ableiten.

App und Lüfterintegration sind separat baubar; deren letzte Schutztests und Grenzen stehen in `FAN-POWER-APP-INTEGRATION.md`. In dieser Sichtung wurde ausschließlich diese zusätzliche Dokumentation erstellt, kein Programmcode verändert.
