# Gemeinsamer RGB-Zustand – Implementierungsverlauf

## Nachtrag: ausdrücklicher Geräte-Neuabgleich

`ReapplyAsync` umgeht gezielt die Optimierung für unveränderte App-Auswahl. Eine zurückgesetzte Tastatur kann damit wieder auf den unveränderten Benutzerwunsch gebracht werden. Vorher wird ein laufender Renderer vollständig beendet, danach die vorhandene geprüfte ApplyState-Schnittstelle benutzt und gegebenenfalls genau ein Effekt neu gestartet. Der HID-Transport öffnet dabei die genaue Geräteschnittstelle neu. Keine neue Firmwarefunktion.

App-Knopf „Auswahl erneut an Tastatur senden“ ergänzt, mit bestehender Busy-Sperre und Fehler-/Speicherbehandlung. Das ist eine ausdrückliche Benutzeraktion, kein regelmäßiges Überschreiben externer Änderungen. Test simuliert einen Geräte-Reset bei unverändertem App-Zustand und bestätigt Wiederherstellung; zweiter Test prüft serialisierten Effektneustart ohne parallele Schreiber. Build und alle simulierten Tests bestanden. Keine reale Hardware verändert.

Automatischer Resume-Aufruf und gesamter Standby-Lebenszyklus fehlen weiterhin. Ein Rückstellfehler des alten Renderers wird nicht verschluckt; nach Fehleranzeige kann erneut versucht werden. Die neue Schaltfläche ist noch nicht visuell geprüft.

Aktueller Nachtrag 2026-09-03: RGB-Persistenz und Startwiederherstellung sind jetzt implementiert und simuliert getestet; Details in RGB-PERSISTENCE.md. Single-instance und Tray sind ebenfalls ergänzt (RGB-SOFTWARE-LESSONS.md). Ältere Abschnitte unten beschreiben den jeweiligen Zwischenschritt. Normales Fensterschließen versteckt inzwischen nur; explizites Beenden führt die Rückstellungen aus.

Stand 2026-09-03. Das neue Modul liegt in `Core/Features/Keyboard`. Nach dem ersten Modultest ist jetzt auch das WPF-ViewModel auf die gemeinsame Session umgestellt.

## Implementiert

- Unveränderlicher Benutzerzustand: Ein/Aus, letzte eingeschaltete Helligkeit, optionaler Effekt, Tempo und drei gespeicherte manuelle Farben.
- Aus behält Helligkeit, Effekt und Farben; Ein kann denselben Zustand fortsetzen.
- Helligkeit und manuelle Farbänderungen verändern den gewählten Effekt nicht automatisch. Die Oberfläche muss erklären, welche Effekte die Basisfarbe benutzen und welche eine eigene Palette haben.
- `KeyboardLightingSession` serialisiert alle Übergänge mit einer einzigen Sperre. Änderungen werden auf dem zuletzt bestätigten Benutzerzustand berechnet, nicht auf alten UI-Kopien.
- Alte Animation vollständig stoppen und deren Rückstellung abwarten, erst danach neuen Zustand schreiben. Dadurch kann kein alter Worker eine neuere Auswahl nachträglich überschreiben.
- `ApplyState` schreibt drei Zonen samt Helligkeit über die vorhandene geprüfte HID-Schnittstelle mit Readback und Rollback. Keine neuen Protokollbytes eingeführt.
- Nur erfolgreiche statische Übernahme aktualisiert den gespeicherten Zustand. Asynchrone Effektfehler können über `CheckEffectAsync` abgeholt werden; UI-Anbindung dafür steht noch aus.
- Der Aufrufer besitzt den Transport und muss andere Schreiber ausschließen. Initialisierung ist nur lesend; Dispose stoppt einen aktiven Effekt und wartet dessen Rückstellung ab.

## Prüfung

Gesamtbuild erfolgreich. Neue Tests mit simuliertem Transport prüfen: initial kein Schreiben, Helligkeitsgedächtnis beim Aus/Ein, Helligkeit mit laufendem Effekt, getrennte manuelle Farben, Aus stoppt Animation, Ein setzt den Modus fort, schnelle aufeinanderfolgende Änderungen ohne verlorenen Zustand, höchstens ein Worker, keine Schreiboperation während alter Rückstellung, Rückkehr zu aktuellen manuellen Farben, fehlgeschlagene Übernahme und Dispose.

Acht Verbrauchs-Rechentests und sieben bestehende Lüfterschutztests bleiben erfolgreich. Keine echten RGB-Schreibtests in diesem Schritt.

## Nächste Arbeit / Grenzen

### Nachtrag: WPF-Anbindung

- Alte eigene Effekt-Task-/Cancellation-Verwaltung im MainWindowViewModel entfernt. Alle RGB-Aktionen laufen über die Session; Transport wird erst danach entsorgt.
- Ein/Aus-Knopf ergänzt. Er merkt sich den Zustand innerhalb der laufenden App. Noch keine Speicherung über App-Neustarts hinweg.
- Helligkeit/Tempo/Farben erhalten den gewählten Effekt. „Effekt anwenden“ wählt ihn ausdrücklich und schaltet ein; „Manuell“ wechselt zu den gespeicherten Zonenfarben, ohne eine ausgeschaltete Beleuchtung einzuschalten.
- Farbfelder zeigen gespeicherte manuelle Farben, keine animierten Momentanbilder. Hinweis zeigt, ob ein Effekt die linke Basisfarbe oder seine eigene Palette verwendet.
- Effektfehler werden durch einen separaten, nur bei aktivem Effekt laufenden Timer geprüft; kein Geräte-Polling dafür. Damit bleibt die Fehleranzeige auch bei gestoppter Temperaturüberwachung verfügbar.
- Normales Schließen stoppt den Effekt vor dem Entsorgen. Bei fehlgeschlagener Rückstellung bleibt das Fenster offen; der Dialog nennt jetzt allgemeine Hardwarefehler statt fälschlich nur Lüfter.
- Zusätzlicher ViewModel-Integrationstest mit simuliertem Transport bestätigt Helligkeit/Farbe bei Effekt, Aus/Ein-Wiederaufnahme und Rückkehr zu manuellen Farben. Alte Einzel-Setter werfen im Test absichtlich Fehler, damit versehentliche Parallelpfade auffallen.
- Gesamtbuild und alle bestehenden Tests bestanden. Noch kein physischer RGB-Klicktest und keine visuelle Layout-Abnahme dieser UI-Änderung.

- WPF-Anbindung implementiert; physische Bedienprüfung und finale Gestaltung stehen aus.
- Aktueller Renderer wird bei geänderten Einstellungen kontrolliert gestoppt und neu gestartet; das kann kurz die Basisfarben zeigen und setzt die Animationsphase zurück. Ein späterer dauerhaft laufender Renderer mit aktuellen Zustands-Snapshots soll dies vermeiden.
- Fehlerzustand der UI, Wiederholen und Geräteverlust testen; noch kein prozessübergreifender Schutz gegen GCC oder eine zweite App-Instanz.
- Fn+Space-Synchronisierung, Standby/Resume, persistente Einstellungen und physische Übergänge noch offen.
- Dieses Modul ist ein getesteter Baustein, kein fertig integriertes RGB-Feature und kein Abschluss des Gesamtziels.
