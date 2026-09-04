# Diagnose-Wiederherstellung – 2026-09-03

## Ausgangspunkt

Gesamtbuild hatte 159 Fehler. Im aktuellen Program.cs fehlten viele vorhandene CLI-Funktionen und gemeinsame Helfer. Die letzte vorhandene Diagnostics-DLL vom 15:44 enthielt diese Funktionen noch. Neuere CPU-Monitor-Änderungen im Quelltext durften nicht durch die ältere Fassung ersetzt werden.

## Vorgehen und Sicherung

- Unveränderte Kopie des zuvor vorhandenen Program.cs sowie DLL/PDB unter `research/recovery/diagnostics-20260903/` gesichert.
- Mit bereits vorhandenem ILSpy dekompiliert; Ergebnis unter `decompiled/Program.decompiled.cs`. Keine fremde Programmdatei ausgeführt.
- `tools/RecoverDiagnostics` nutzt den Roslyn-Syntaxbaum, um ausschließlich auf oberster Ebene fehlende lokale Funktionen einzufügen. 43 Funktionen ergänzt; keine bestehenden Funktionskörper durch dekompilierte Fassungen ersetzt.
- CPU-Snapshot-Initialisierung vor frühe CLI-Aufrufe verschoben.
- Namensauflösung korrigiert: alte WMI-Aufrufe verwenden nun `Query2`, HID-Abfragen bleiben separat; Batch1 ruft die wiedergefundene Batch-Funktion auf. Verlorene Tuple-Feldnamen im dekompilierten Code ergänzt.
- Nullability-Warnungen und unbenutzte lokale Hilfsfunktionen sind NUR im dekompilierten Abschnitt vorübergehend ausgenommen. Das ist ausdrücklich technische Restarbeit, nicht ein Nachweis sauberer Modularität. Bei Extraktion/Überarbeitung sollen die Ausnahmen verschwinden.
- Hardwarefreies `--help` ergänzt, damit Einstiegspunkt ohne Gerätezugriff getestet werden kann.

## Ergebnis und Grenzen

Gesamt-Solution Release-Build wieder erfolgreich: 0 Fehler, 0 Warnungen unter den oben beschriebenen lokalen Ausnahmen. Hardwarefreier CLI-Start mit `--help` erfolgreich. Die sieben vorhandenen simulierten App-Schutztests bestehen weiterhin. Die vorherigen Notizen zu 159 Buildfehlern beschreiben den historischen Stand vor dieser Wiederherstellung.

Keine Hardware-Schreibtests ausgeführt. Ein erfolgreicher Build bestätigt nicht die physische Wirkung oder Reversibilität jeder wiederhergestellten Diagnose. Die Originalassembly und der Ausgangstext bleiben für Vergleich und weitere Tests erhalten. Vor allem der Verbrauchsmonitor hat weiterhin die in PROJECT-HANDOVER-REVIEW-20260903.md genannten Logikprobleme und ist noch nicht als zuverlässiges Messmodul freigegeben.

Das vollständige App-Ziel samt RGB-Bedienlogik ist in APP-ROADMAP.md festgehalten. Diese Wiederherstellung ist ein Zwischenschritt, kein Abschluss des App-Ziels.
