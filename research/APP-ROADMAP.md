# Zielbild und Arbeitsplan

Stand: 2026-09-03. Verbindliches Ziel ist eine brauchbare, ressourcenschonende, stabile und schöne App speziell für den AORUS 5 SE4. Ein grüner Build oder eine Sammlung von Testknöpfen erfüllt dieses Ziel nicht.

## Anforderungen

Bestätigte Bedien- und Produktentscheidungen stehen in PRODUCT-DECISIONS.md: Tray, Hintergrund-Autostart, eigene Kurven/Profile, getrennte Netz-/Akku-Zuordnungen, RGB-Persistenz, Fn+Space-Vorrang und modernes Design ohne parallele GCC-Steuerung.

Erweitertes Ziel: effizienter Windows-Hintergrundbetrieb, zusätzliche UI-Funktionen erst bei Bedarf laden und Ausfallverhalten von UI/Worker unabhängig absichern. Entwurf und Pflichtprüfungen in BACKGROUND-SERVICE-DESIGN.md. Noch kein installierter Autostart/Dienst.

- Gerätespezifische, belegte Einstellungen für Tastatur, Akku, Kühlung und Leistung; weitere Fähigkeiten anhand einer dokumentierten Capability-Liste prüfen. Nicht unterstützte oder unbewiesene Firmwarefunktionen nicht als funktionierende Regler anbieten.
- Klare Feature-Module, testbare Zustandslogik, verständlicher Code und eine begrenzte Schnittstelle für privilegierte Hardwarezugriffe.
- Geeignete Bibliotheken/Frameworks bewusst auswählen; bestehendes .NET/WPF ist Ausgangspunkt, kein Grund für einen unbelegten Rewrite. UI und Hardwaredienst getrennt entwickeln und messen.
- Einheitliches, benutzerfreundliches Bedienkonzept: Zustände und Folgen sichtbar, keine konkurrierenden Hintergrundschreiber, keine widersprüchlichen Einstellungen.
- Ressourcenverbrauch mit und ohne aktive Effekte messen; keine Hardwareabfragen, die unnötig Geräte aufwecken. Abfrageintervalle an Sichtbarkeit und Bedarf anpassen.
- Fehlerbehandlung, Rückstellung, Start/Stop, Standby/Resume, Geräteverlust, Mehrfachstart und externe Änderungen explizit testen.
- Gestaltung und Bedienung der fertigen Oberfläche visuell am Gerät prüfen, nicht nur kompilieren.

## RGB: gemeinsame Zustandslogik statt unabhängiger Knöpfe

Gewünschtes Modell: ein Controller besitzt den Zustand `Ein/Aus + Modus + gespeicherte manuelle Zonenfarben + Helligkeit + Effekttempo`. Nur dieser Controller schreibt auf die Tastatur.

- Aus stoppt Animationen und schaltet LEDs aus, behält jedoch Modus, Farben und letzte eingeschaltete Helligkeit.
- Ein setzt den gewählten Zustand fort, nicht pauschal immer volle Helligkeit.
- Helligkeit gilt für statische Farben und Effekte; eine Änderung beendet nicht überraschend den Effekt.
- Manuelle Farben werden getrennt vom momentan ausgegebenen Animationsbild gespeichert. Im manuellen Modus sind die Zonen auswählbar; bei Effekten wird klar angezeigt, ob sie Basisfarben nutzen oder ihre eigene Palette haben.
- Tempo ist nur bei Effekten relevant. Tempoänderungen dürfen keine alten Zustandskopien über neuere Benutzerentscheidungen zurückschreiben.
- Übergänge werden serialisiert; Abbruch eines alten Effekts darf einen inzwischen gewählten neuen Zustand nicht wiederherstellen.
- Fn+Space/externe Änderungen brauchen eine explizite Konfliktregel. Ereignisquelle ist dokumentiert, vollständige Synchronisierung noch zu implementieren und zu testen.
- Die Oberfläche zeigt Modus, Helligkeit und Farben zusammenhängend an. Deaktivierte oder irrelevante Felder werden verständlich erklärt.

Gemeinsame Session und WPF-Anbindung sind inzwischen implementiert und simuliert getestet. Änderungen erhalten die Effektauswahl, starten den Renderer aber noch kontrolliert neu; phasenstabile Übergänge stehen aus. RGB-Persistenz ist implementiert (RGB-PERSISTENCE.md).

## Reihenfolge und Nachweise

1. Diagnosebasis retten und reproduzierbar bauen. Wiederherstellung erfolgt, Verhalten des zurückgewonnenen Codes noch zu prüfen.
2. Verbrauchsmonitor als eigenes Modul: V2 implementiert, Rechentests und zwei Netzbetrieb-Live-Läufe erfolgreich, Abfrageaufwand reduziert. Adapter-IDs dynamisch, Modellnamen noch offen; Akku-/Langzeit-/Fehlerprüfungen fehlen. Details in POWER-MONITOR-V2.md. Bestehende alte Nullwertberichte nicht als Nachweis verwenden.
3. Feature-/Capability-Inventar mit Status: beobachtet, lesbar, schreibbar, rückgelesen, physisch bestätigt, UI-integriert. Akku-UI ist als eigenes ViewModel implementiert und simuliert getestet (BATTERY-APP-INTEGRATION.md); physische UI-Prüfung offen. Weitere bestätigte Geräteeinstellungen erfassen.
4. RGB: Zustandsmodell, serialisierte Session, Persistenz und WPF-Anbindung samt Modul-/ViewModel-Simulationstests vorhanden. Durchlaufender Renderer, Fn+Space-Synchronisierung, Resume und physische UI-Prüfung noch offen. Siehe RGB-SESSION-STATE.md und RGB-PERSISTENCE.md.
5. Lüfter-/Leistungsprofile und Akku logisch koordinieren; Windows-Leistung, Kühlung und GPU-Routing begrifflich getrennt halten. Sicherheitsverhalten ohne UI-Abhängigkeit ausarbeiten.
6. Dienst-/UI-Grenze, Berechtigungen, Single-instance und Lebenszyklus stabilisieren; Framework-Auswahl dokumentieren.
7. Konsistente Oberfläche auf Basis der Zustandsmodelle implementieren und auf dem Laptop prüfen.
8. Ressourcen-/Langzeit-/Fehler-/Standbytests und reproduzierbare Veröffentlichung. Erst dann Gesamterfüllung bewerten.
