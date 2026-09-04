# Hintergrundbetrieb und Ausfallverhalten

Stand: 2026-09-03. Entwurf aus dem erweiterten Nutzerziel, noch kein installierter Dienst und keine Zusage getesteter Ausfallsicherheit.

## Ziel und Aufteilung

Implementierungsstand: UI-unabhängiger FanSafetySupervisor mit befristeten Freigaben und simulierten Fehler-/Ablauftests vorhanden; Details in FAN-SUPERVISOR.md. Separater Entwicklungs-Worker mit lesendem IPC ist implementiert und als Zwei-Prozess-Statusverbindung getestet (WORKER-HOST.md). Dienstinstallation, produktive Rechtevergabe, schreibendes IPC und unabhängiger Prozesswächter fehlen noch. Die App verwendet bislang ihre alte Fixed-Steuerung.

- Ein privilegierter Hardware-Worker besitzt Lüfter-/Akku-Schreibzugriffe. Das Fenster besitzt keine eigene konkurrierende Steuerung mehr.
- Die Benutzeroberfläche startet nur auf Wunsch. Aufwendige Prozess-/GPU-Diagnostik und Diagramme werden nur bei sichtbarer Ansicht angefordert, nicht im normalen Hintergrundbetrieb.
- RGB-Animationen werden nur bei aktivem Effekt berechnet. Statische Farben und das Akkulimit benötigen keine laufenden Wiederholungsschreibvorgänge. RGB-HID und Fn+Space-Ereignisse können einen Agenten in der angemeldeten Benutzersitzung erfordern; tatsächlicher Zugriff aus Dienst/Sitzung 0 muss geprüft werden, nicht vorausgesetzt.
- IPC bietet ausschließlich typisierte, geprüfte Befehle und Status an. Keine freien WMI-Methodennamen, Shell-Befehle, Dateipfade oder Rohregister aus der UI. Zugriffsrechte auf autorisierte lokale Benutzer begrenzen; Dienst überprüft Gerät und Werte selbst.
- Ein Prozess besitzt die aktive Steuerung. Zweiter App-Start öffnet/aktiviert das vorhandene Fenster oder verbindet sich mit demselben Worker; er startet keinen zweiten Hardware-Schreiber.

## Hintergrundlast

- Firmware-Automatik braucht keine hochfrequenten App-Regelbefehle. Temperatur/RPM nur bedarfsgerecht für sichtbare Anzeige oder aktive Sicherheitsüberwachung lesen.
- Manuell geregelte Lüfter erfordern auch ohne Fenster frische Telemetrie. Diese Überwachung darf nicht zusammen mit der UI pausieren.
- GPU-/Verbrauchsmonitor wird beim Schließen seiner Ansicht angehalten und freigegeben. Das bisher gemessene V2-Modul ist ein Ansatz, kein bereits erfülltes Gesamt-Ressourcenbudget.
- Startkosten, CPU-Zeit, Speicher und Handles getrennt für ruhenden Hintergrund, RGB-Effekt und geöffnetes Dashboard messen. Keine pauschale Erfolgsaussage nur aus einem Kurzlauf.

## Vorgesehenes Ausfallmodell

1. **Fenster geschlossen:** Worker bleibt nur gemäß noch zu bestätigender Tray-/Autostart-Entscheidung aktiv. Schließen ist nicht automatisch Beenden.
2. **UI abgestürzt:** Worker behält einen geprüften Zustand und die Lüfterüberwachung. Keine Abhängigkeit von WPF-Timern.
3. **Worker abgestürzt oder hängt:** Ein unabhängiger Wächter erkennt einen nicht mehr erneuerten Steuerungszeitraum und versucht Normal/Firmware-Automatik mit Rücklesen herzustellen. Er darf keine alten manuellen Einstellungen nach Neustart wiederholen.
4. **Telemetrie fehlt oder wird zu alt:** Manuelle Lüftersteuerung läuft aus; Normal wird angefordert. Wiederholungen sind begrenzt/protokolliert; Fehler bleibt sichtbar.
5. **Standby/Resume:** Vor Suspend Normal anfordern, sofern Zeit und Gerät verfügbar. Nach Resume zuerst neu identifizieren, Normal rücklesen/setzen und Datenbasis aufbauen; keine alte manuelle Freigabe übernehmen.
6. **Normales Beenden/Deinstallieren:** Neue Befehle sperren, laufende Transaktionen abschließen, Normal anfordern und prüfen, erst dann Worker/Wächter beenden. RGB- und Akkupersistenz gemäß bestätigter Bedienregeln.

## Wichtige Grenze

Eine normale Windows-Anwendung kann bei vollständigem Betriebssystemstillstand, Ausfall aller Überwachungsprozesse oder blockiertem WMI keine Rückstellung garantieren. Auch ein Wächter hilft nicht, wenn der Firmwarezugriff selbst hängt. Ein nachgewiesener Firmware-Watchdog wäre stärker, ist hier aber noch nicht belegt. Die Behauptung „bei jedem Absturz übernimmt das BIOS automatisch“ ist daher unzulässig.

Für den Produktionsmodus bleiben ungesicherte feste Lüfterwerte gesperrt, bis die unabhängige Überwachung implementiert und die Fehlerfälle am Gerät geprüft sind. Die bestehende Fixed-Funktion ist weiterhin nur ein beaufsichtigter Entwicklungs-/Testmodus mit UI-abhängiger Absicherung.

## Pflichtprüfungen vor Aktivierung des Hintergrund-Autostarts

- UI-Prozess gezielt beenden: Worker/Temperaturüberwachung laufen weiter.
- Worker-Prozess gezielt beenden: Wächter stellt Normal her, Readback und RPM beobachten.
- Worker hängt, Heartbeat läuft aus, Messwerte sind veraltet: keine endlose Verlängerung manueller Steuerung.
- Wächterfehler, WMI-Fehler und blockierende Operation simulieren: verbleibende Risiken sichtbar, keine falsche Erfolgsmeldung.
- Zweiter Start, Benutzerwechsel, Ab-/Anmelden, Standby/Resume und Windows-Neustart.
- Korrekte Zugriffsprüfung der IPC und Ablehnung unbekannter Befehle/Geräte/Werte.
- Installation/Deinstallation reversibel, Autostart opt-in und Zustand transparent.

## Noch offene Nutzerentscheidungen

Die Grundentscheidungen sind bestätigt: Tray beim Schließen, Hintergrund-Autostart ohne Fenster, RGB-Persistenz, Fn+Space-Vorrang, modernes Nicht-Gaming-Design und keine parallele GCC-Nutzung. Zusätzlich eigene Lüfterkurven/Profile und getrennte Netz-/Akku-Zuordnungen. Maßgeblich ist PRODUCT-DECISIONS.md. Die konkrete sichere Profilwiederaufnahme und Dienstinstallation müssen noch implementiert und getestet werden.
