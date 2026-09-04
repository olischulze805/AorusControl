# Bestätigte Produktentscheidungen

Vom Nutzer bestätigt, 2026-09-03. Diese Entscheidungen ersetzen frühere offene Empfehlungen.

1. Fenster schließen minimiert in den Infobereich neben der Windows-Uhr. Explizites Beenden ist getrennt und stellt manuell gesteuerte Lüfter auf Firmware/Normal zurück.
2. Autostart: Hintergrundsteuerung startet ohne Fenster. Oberfläche und zusätzliche Anzeigen werden beim Öffnen geladen. Installation und Fehlerverhalten noch implementieren/testen.
3. Automatische Lüfterprofile plus erweiterte manuelle Steuerung. Eigene Lüfterkurven und benutzerdefinierte Profile gehören ausdrücklich zum Zielumfang, nicht nur feste Drehzahlen.
4. RGB-Einstellungen dauerhaft speichern und wiederherstellen. Kühlung/Leistung erhalten getrennte, benutzerdefinierbare Zuordnungen für Netz- und Akkubetrieb. Profile können Lüftermodus/-kurve und Windows-Leistungsmodus koordinieren. Welche manuellen Einstellungen nach Neustart automatisch sicher aktiviert werden dürfen, ist anhand Schutzkonzept und Tests festzulegen; keine pauschale Freigabe unüberwachter Fixed-Werte.
5. Fn+Space hat Vorrang: Die App übernimmt die manuell gewählte Helligkeit in ihren Zustand und schreibt nicht dagegen an.
6. Moderne, ruhige Oberfläche ohne Gaming-Look. Systemdesign hell/dunkel bleibt eine geeignete Ausgangsannahme, nicht ausdrücklich separat bestätigte Farbwahl.
7. Keine gleichzeitige Steuerung mit Gigabyte Control Center vorgesehen. Unsere App soll die alleinige Steueranwendung sein. Daraus folgt keine pauschale Erlaubnis, vorhandene Software ungefragt zu deinstallieren oder Dienste abzuschalten.

## Daraus folgende Arbeiten

Umsetzungsstand: Tray-Menü und Verstecken beim Schließen sind implementiert, ebenso eine UI-Single-instance-Sperre innerhalb derselben Benutzer-/Windows-Sitzung. Aktivierungssignal und pausierte Dashboard-Telemetrie sind simuliert getestet; echte Tray-/Mehrprozess-/Shutdown-Prüfung fehlt. Hintergrund-Autostart und separater Dienst sind weiterhin offen. Zusätzliche Regressionstests aus Fremdsoftware-Erfahrungen: RGB-SOFTWARE-LESSONS.md.

- Tray-Lebenszyklus und explizite Beenden-Aktion; Single-instance und Wiederöffnen.
- Hintergrundhost mit abgesichertem Hardwarebesitz, beschränkter IPC und Start ohne UI.
- Versionierte, validierte Speicherung von RGB und Profilen; keine rohen Firmwarewerte blind aus Konfigurationsdateien ausführen.
- Profilmodell mit getrennten Netz-/Akku-Zuordnungen, aktiver Quelle, manueller Übersteuerung und klarer Anzeige des tatsächlich bestätigten Zustands. Quelle unbekannt/Wechsel während Transaktion gesondert behandeln.
- Kurveneditor auf Basis der nachgewiesenen 15-Punkt-Schnittstelle: zulässige Grenzen, Plausibilitätsprüfung, Readback, Rollback und Wiederherstellung der Firmware-Automatik.
- Fn+Space-Ereignisse in die gemeinsame RGB-Session integrieren; Animation darf übernommene Helligkeit nicht wieder überschreiben.
- Modernes, zusammenhängendes UI, dessen Seiten auf denselben Zustandsmodellen wie Hintergrundsteuerung und Profile beruhen.
- Alle Persistenz-/Quellenwechsel-/Absturz-/Standbyfälle testen. Bestätigte Produktentscheidungen sind noch kein Nachweis implementierter Funktionen.
