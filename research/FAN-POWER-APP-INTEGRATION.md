# Einfache App-Integration – 2026-09-03

## Umfang

- Bestehende WPF-App ergänzt, kein neues Design.
- Lüfterprofile: Quiet/Leise, Normal, Gaming, Maximum, Dynamic mit gespeicherter Kurve.
- Fixed-Auswahl: Rohwerte 57, 68, 91, 114, 137, 160, 194, 229. Keine Prozentbehauptung; auch die Telemetrie zeigt Rohwerte / 229.
- Windows-Leistungsmodus: Energieeffizienz, Ausbalanciert, Beste Leistung. Netzbetrieb wird vor dem Schreiben erneut geprüft und danach der Modus rückgelesen. Bei Stromquellenwechsel wird ein Fehler angezeigt; keine automatische Rückschreibung in die andere Stromquelle.
- Keine GPU-Abschaltung, kein MUX-Schalter, kein BIOS-Schreiben, kein Kurveneditor. Windows-Energieeffizienz ist NICHT physisches GPU-Eco.

## Schutzverhalten

- Hardwarezugriff weiterhin über den modell-/BIOS-geprüften Core mit Readback/Rollback.
- Fixed erfordert eine frische Temperaturmessung unter 65 °C für CPU und GPU. Die 65 °C sind eine konservative Software-Testgrenze, keine ermittelte Hardware-Maximaltemperatur.
- Fixed startet die Messung automatisch. Alle zwei Sekunden wird gelesen; bei mindestens 65 °C oder Messfehler wird Normal angefordert. Bei fehlgeschlagener Rückstellung bleiben Warnung und Wiederholungsversuche aktiv.
- Überwachung stoppen stellt einen von der App gesetzten Fixed-Modus zuerst auf Normal. Misslingt dies, läuft die Überwachung weiter.
- Normales Schließen wartet auf laufende Operationen und stellt von der App gesetztes Fixed/Maximum/Dynamic auf Normal. Bei Fehler bleibt das Fenster offen und zeigt eine Warnung. Quiet/Gaming bleiben erhalten.
- Kein unabhängiger Watchdog: Prozessabsturz, blockierter UI-Thread, Standby oder hängendes WMI können den Schutz verhindern. Fixed ist nur für beaufsichtigte Tests gedacht. Fremdseitig vor App-Start gesetzte Fixed-Modi werden durch bloßes Öffnen nicht übernommen oder verändert.
- Fehlertexte bleiben beim anschließenden Rücklesen erhalten.

## Unabhängiger Rückweg

`tools/Start-FanNormalRestore.cmd` startet jetzt die App mit `--restore-fan-normal`, ohne Hauptfenster/RGB-Initialisierung. Dieser Pfad benutzt dieselbe geprüfte Core-Normal-Rückstellung und meldet Erfolg/Fehler per Dialog und Exitcode. Das Skript nutzt die gebaute App; nur wenn sie fehlt, wird das App-Projekt gebaut. Somit ist die Rückstellung unabhängig vom Diagnoseprojekt, aber nicht von Core/WMI.

## Prüfung

- App Release-Build erfolgreich: 0 Warnungen, 0 Fehler.
- `dotnet run --project tests/AorusControl.App.SmokeTests -c Release`: sieben Tests bestanden, ausschließlich mit simuliertem Lüftercontroller und simulierten Messwerten, ohne Hardware-Setter.
- Fälle: Fixed ab 65 °C abweisen; Fixed unter Grenze erlauben und bei Hitze rückstellen; Messausfall rückstellen; Rückstellungsfehler sichtbar halten und wiederholen; beim Schließen rückstellen; Schließfehler melden und erneut versuchen; nach Normal keine doppelte Rückstellung.
- Diese Tests prüfen ViewModel-Logik, nicht echte WMI-Latenzen, Dispatcher-Timing, Fensterbedienung oder physische Lüfterwirkung. Die Hardwareprotokolltests stehen in FAN-POWER-GPU-CONTROL.md.
- Frühere Sichtprüfung zeigte das laufende Fenster und Telemetrie. Automatisiertes Scrollen der erhöht laufenden App war nicht erfolgreich. Neue Regler und neuer Rückstellungs-Starter noch nicht live durchgeklickt.
- Gesamt-Solution-Build aktuell fehlgeschlagen: 159 Fehler im nicht in diesem Schritt bearbeiteten `src/AorusControl.Diagnostics/Program.cs`, u. a. fehlende Funktionen `RunKeyboardSlowColorCycle`, `Escape`, `FindRepositoryRoot` und nicht initialisierte CPU-Sampling-Variablen. Keine spekulative Wiederherstellung dieser anderen Datei vorgenommen. App/Core lassen sich separat bauen.

## Nächste manuelle Prüfung

App über Start-AorusControl.cmd öffnen, nach unten scrollen, zuerst Normal/Leise/Gaming/Maximal wählen und RPM beobachten. Fixed nur ohne Last unter Aufsicht. Danach normales Schließen und Rückstellung prüfen. Windows-Modi separat am Netzteil prüfen. Diagnoseprogramm-Reparatur bleibt eine eigene offene Aufgabe.
