# RGB-Frame-Effizienz

Stand 2026-09-03.

## Befund im aktuellen Code

`GigabyteHidKeyboardRgbController.PlayEffect` schrieb bisher in jeder Schleife drei Zonen mit jeweils 5 ms Pause. Es gab kein separates Framebudget und keine Prüfung auf unveränderte Farben. Gerade Pulse/Marquee-Haltephasen verursachten deshalb wiederholte identische HID-Befehle. Das ist ein Codebefund, keine gemessene CPU-Last oder belegte Ursache früherer Firmwareprobleme.

## Umsetzung

- Pro Effektlauf neuer `KeyboardFrameWriter`: merkt nur erfolgreich gesendete Farbe und Helligkeit jeder Zone. Erster Frame schreibt alle drei Zonen, danach ausschließlich Änderungen.
- Fehlgeschlagene Writes aktualisieren den Cache nicht. Helligkeitsänderungen gelten auch bei gleicher Farbe als Änderung. Validierung erfolgt vor Hardwarewrites.
- Rund 33,33 ms Mindestbudget je Schleife inklusive Übertragungsdauer, entsprechend ungefähr 30 Frames/s. Langsame HID-Aufrufe führen nicht zu Aufhol-Bursts. Unveränderte Frames führen nicht zu einer Busy-Loop.
- Bisherige 5 ms Zwischenpausen bleiben erhalten. Geschwindigkeit bleibt eine Zeitskalierung des Effekts, unabhängig vom Framebudget.
- Wartephase ist abbrechbar; Abbruch während des Zonendurchlaufs ist normaler Stopp und führt weiterhin zur bisherigen geprüften Rückstellung.

## Prüfung und Grenzen

Build ohne Warnungen/Fehler, gesamte simulierte Testsuite erfolgreich. Neuer Test: erster Frame drei Writes, 300 identische Frames null zusätzliche Writes, einzelne Farbänderung ein Write, Helligkeitsänderung drei Writes, Fehler bleibt wiederholbar, Abbruch/ungültige Helligkeit keine Writes.

Keine realen Hardwarewrites oder Lastmessungen in diesem Schritt. 30 Frames/s ist ein vorläufiges Softwarebudget, keine zugesicherte physische Darstellungsrate. Die drei Zonen werden weiterhin nacheinander geschrieben. Physische Flüssigkeit, Zeitversatz, Ressourcenverbrauch und Stopplatenz müssen am Gerät geprüft werden. Der Thread bleibt während des Effekts reserviert; ein vollständig asynchroner Renderer steht aus.

Der Cache gilt nur innerhalb eines Effektlaufs, nicht als Hardwarebestätigung. Bei Neustart/Neuverbindung ist ein frischer Cache nötig. Fremde Programme, Fn+Space und Standby benötigen weiterhin explizite Synchronisierung; dieser Cache löst deren Konflikte nicht. Parameteränderungen starten den Renderer weiterhin neu.
