# Lehren aus GCC und anderen RGB-Anwendungen

Recherche: 2026-09-03. Nutzerauftrag: konkrete Kritik in bessere Bedienung, Architektur und Tests übersetzen. Berichte über andere Mainboards/Controller sind keine Beweise für dieselbe Ursache am AORUS 5 SE4. Alte geschlossene Issues sind historische Regressionstests, keine Aussage über heutige Releases.

## 1. Konkurrierende Programme / Hintergrunddienste

SignalRGB dokumentiert Flackern, falsche Farben und Gerätezugriffsprobleme durch konkurrierende RGB-Programme; ein geschlossenes Fenster bedeutet nicht zwingend einen beendeten Dienst.

Quelle: https://docs.signalrgb.com/troubleshooting/conflicting-programs/

Unsere Konsequenz: gemeinsame RGB-Session und höchstens eine UI-Instanz pro Benutzer/Windows-Sitzung. Zweiter Start signalisiert nur „Fenster öffnen“, ohne neue Hardwarecontroller zu initialisieren. Keine beliebigen IPC-Payloads für diesen Vorgang. Noch offen: prozessübergreifender Hardwarebesitz im Worker, andere Benutzersitzungen und konkrete Erkennung fremder GCC-/RGB-Dienste. Kein automatisches Abschießen oder Deinstallieren anderer Software.

## 2. Einstellungen nach Standby verloren

OpenRGB-Issue 1292 beschreibt verlorene Einstellungen nach Suspend auf einem X570 Aorus Ultra. Die liquidctl-Dokumentation beschreibt für einen anderen Gigabyte-Fusion2-Controller einen Reset auf Blau nach dem Aufwachen. Das belegt, dass gespeicherter App-Zustand und tatsächlicher Controllerzustand auseinanderlaufen können, nicht dass unser Keyboard denselben Reset hat.

Quellen:
- https://gitlab.com/CalcProgrammer1/OpenRGB/-/issues/1292
- https://github.com/liquidctl/liquidctl/blob/main/docs/gigabyte-rgb-fusion2-guide.md

Unsere Konsequenz: Resume als eigener Lebenszyklus mit Neuverbindung und Rücklesen; erst anschließend validierte RGB-Auswahl erneut übernehmen. Hardwarezustand, Benutzerwunsch und laufende Animation getrennt halten. RGB-Persistenz ist implementiert und simuliert getestet (RGB-PERSISTENCE.md); Resume-Anbindung und physischer Suspend/Resume-Test bleiben offen. Lüfter dürfen dabei keine alte Fixed-Freigabe erben.

## 3. Hohe Last trotz langsamer Messintervalle

Ein OpenRGB-HardwareSync-Issue beschreibt hohe CPU-Last trotz vergrößertem Messintervall. Die Diskussion unterscheidet ausdrücklich Sensor-Messintervall und LED-Aktualisierungsrate; der Melder bestätigt Besserung nach Änderung der LED-Rate. Das ist ein historischer Einzelbericht, kein allgemeiner Benchmark.

Quelle: https://gitlab.com/OpenRGBDevelopers/OpenRGBHardwareSyncPlugin/-/issues/22

Unsere Konsequenz: Sensorpolling, Effektberechnung und Hardware-Schreibfrequenz getrennt budgetieren. Statische Farben nicht endlos neu senden. Verdecktes Dashboard stoppt normale Telemetrie; manuelle Lüfterüberwachung bleibt aktiv. Der Verbrauchsmonitor wurde bereits auf Sammelabfragen optimiert. RGB-Framebudget von ungefähr 30 Frames/s und Unterdrückung unveränderter Zonen sind implementiert; Cachelogik simuliert getestet (RGB-FRAME-EFFICIENCY.md). Physische Flüssigkeit, Lastmessung und Langzeittest mit laufenden Effekten bleiben offen. Kein Erfolg allein aus dem Sensorintervall ableiten.

## 4. Autostart scheitert an Berechtigungen

SignalRGB dokumentiert Probleme beim normalen Windows-Autostart erhöhter Programme. Unsere aktuelle WPF-App fordert Administratorrechte und ist deshalb kein fertiges Modell für stillen Hintergrund-Autostart.

Quelle: https://docs.signalrgb.com/troubleshooting/startup-issues/

Unsere Konsequenz: UI und privilegierten Hardware-Worker trennen. Einmalige transparente Installation, regulärer Start ohne wiederholte UAC-Interaktion, prüfbarer Status und reversibles Entfernen. Nicht als Ausweg UAC oder Virenschutz abschalten. Noch kein Autostart installiert.

## 5. Unübersichtliches Speichern / wirkungslose Bedienelemente

Bei uns bereits lokal belegt: scheinbar fehlende Helligkeitsstufen durch falsche Werte und Effekte, die im Firmwarepfad nicht sichtbar funktionieren. Deshalb keine universelle RGB-Oberfläche mit ungeprüften Optionen übernehmen.

Unsere Konsequenz: nur geprüfte Stufen, klare Trennung manuelle Farben/eigene Effektpalette, aktiver Modus sichtbar, Ein/Aus merkt die aktuelle Auswahl, „Manuell“ stellt Benutzerfarben wieder her. Module, ViewModel-Wechsel und Speicherung über Neustarts simuliert getestet. Noch offen: Fn+Space-Synchronisierung, flüssige Parameteränderung ohne Renderer-Neustart und visuelle/physische Abnahme.

## 6. Privilegierte Dienste brauchen eine enge Schnittstelle

GIGABYTEs offizieller Hinweis CVE-2026-4416 beschreibt unsichere Deserialisierung im EasyTune Engine Service über Named Pipes/.NET Remoting mit BinaryFormatter. Dies ist ein bestätigter Fehler bestimmter älterer Komponenten, keine Diagnose der aktuell installierten Version dieses Laptops.

Quelle: https://www.gigabyte.com/Support/Security/2376

Unsere Konsequenz für den geplanten Worker: kein BinaryFormatter/.NET Remoting, keine frei ausführbaren Befehle, begrenzte typisierte Nachrichten, Wertevalidierung im Dienst, Zugriffsrechte und Protokollversion prüfen. Kein Netzwerk-Pairing für lokale Laptopsteuerung erforderlich. Diese Anforderungen sind noch nicht als fertiger Dienst umgesetzt.

## In diesem Schritt tatsächlich geändert und geprüft

- App-Single-instance-Sperre mit reiner Aktivierungsmeldung ergänzt. Test mit zwei Handles bestätigt Eigentümer, Aktivierung und Freigabe; noch kein echter Zwei-Prozess-/Benutzerwechseltest.
- Schließen versteckt das Fenster im Infobereich; Tray bietet Öffnen/Beenden. Explizites Beenden benutzt weiterhin die asynchronen Rückstellungen.
- Verstecktes Dashboard pausiert normale Telemetrie. Test bestätigt, dass Fixed-Lüfter weiterhin temperaturüberwacht werden.
- Build und gesamte simulierte Testsuite erfolgreich. Kein realer Tray-Klicktest, keine visuelle Abnahme, kein Autostart/Dienst installiert, keine Hardware-Schreibtests dieses Schritts.

## Abnahmematrix für die fertige App

| Fall | Erwartung | Stand |
|---|---|---|
| Doppelstart | vorhandenes Fenster öffnen, kein zweiter Schreiber | Mechanismus getestet; Prozess-/UI-Test offen |
| X / Tray-Öffnen / Beenden | konsistenter Lebenszyklus, Beenden wartet auf Rückstellung | implementiert, UI-Test offen |
| Statische Farbe / RGB aus | keine Animationsschleife | Session vorhanden; Langzeitmessung offen |
| RGB-Effekt, Dashboard verborgen | Effekt aktiv, normale Diagnose pausiert | implementiert, physische Messung offen |
| Manuelle Lüfter, Dashboard verborgen | Überwachung bleibt aktiv | simuliert bestätigt |
| Standby / Hibernate / Resume | Neuverbindung, bestätigter Zustand, keine alte Fixed-Freigabe | offen |
| Beschädigte Konfiguration / Upgrade | validierter Fallback, keine stillen gefährlichen Writes | RGB-Dateiprüfung und Startlogik implementiert, simuliert getestet; keine Migration unbekannter Versionen |
| Fn+Space während Effekt | neue Helligkeit übernehmen, kein Gegensteuern | offen |
| Fremder RGB-Dienst | verständliche Diagnose, kein versteckter Schreibkampf | offen |
| Worker-Absturz / WMI hängt | Rückstellung versuchen, Grenzen/Fehler wahrheitsgemäß anzeigen | Supervisor simuliert; externer Wächter offen |
