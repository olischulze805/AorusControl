# Separater Worker: Entwicklungsstand

Stand 2026-09-03. Neues Projekt `src/AorusControl.Worker`, in der Solution enthalten. Noch kein Windows-Dienst, kein Autostart, keine UI-Migration und kein externer Lüfterwächter.

## Architektur dieses Schritts

- Separater .NET-Prozess ohne WPF-Abhängigkeit. Verwendet vorhandene .NET-Pipes/JSON, keine weitere Bibliothek nötig.
- `--serve` startet einen Entwicklungs-Host, `--status` bzw. `--telemetry` senden genau eine Anfrage. Strg+C beendet den Host.
- Pipe-Namen sind benutzerbezogen; `CurrentUserOnly` gilt auf Client und Server. `FirstPipeInstance` und maximal eine Instanz verhindern einen zweiten Host derselben Pipe.
- Pro Verbindung genau eine Anfrage; 5 Sekunden Frist für den Nachrichtenaustausch, Client insgesamt 10 Sekunden. Keine offenen Netzwerkports, keine Shell-/Dateipfad-/WMI-Methoden- oder Rohregisterbefehle.
- Typisiertes Protokoll Version 1 mit Anfragekennung und 4-Byte-Längenpräfix, maximal 16 KiB, JSON-Tiefe 8, Ablehnung unbekannter Felder/Operationen/Versionen. Keine polymorphe Typauflösung.
- Status benötigt keinen Hardwarezugriff. Telemetrie wird nur auf Anfrage nach worker-seitiger Geräteprüfung gelesen. Leerlauf wartet auf die Pipe, kein Polling.
- Absichtlich keine Schreiboperationen: bestehende App bleibt vorerst Hardwarebesitzer. Erst nach unabhängiger Rückstellung und Rechtekonzept dürfen Steueroperationen umziehen.

## Tatsächlich ausgeführt

- Gesamtlösung baut ohne Warnungen/Fehler.
- Echten Worker-Prozess gestartet; separater Client erhielt gültigen Status mit passender Anfragekennung.
- Telemetrieanfrage erreichte den Worker, scheiterte aber in diesem Startkontext mit „Die Gigabyte-WMI-Telemetrie konnte nicht geöffnet werden“. Keine erfolgreiche Temperaturmessung behaupten. Rechte/Provider-Zugriff müssen separat diagnostiziert werden.
- Zweiter echter Host wurde mit „Alle Pipeinstanzen sind ausgelastet“ abgewiesen. Ursprünglicher Host anschließend über Strg+C beendet. Keine Dienste installiert, Hardwarewerte nicht verändert.
- Protokolltests prüfen Roundtrip, ungültige Version/Operation/ID, negative/leere/übergroße Länge, abgeschnittene Nachricht und unbekannte Felder.

## Offene Produktionsanforderungen

### Nachtrag: Ursache des WMI-Fehlers eingegrenzt

Erhöhter Nachtest vorbereitet: `tools/Test-WorkerAccess.ps1` baut den aktuellen Release-Worker und startet ausschließlich `--diagnose-report` über die reguläre Windows-Bestätigung, verborgen und ohne Dienstinstallation. Diese Option erzeugt einen neuen datierten Markdown-Bericht unter `research/runs`; vorhandene Berichte werden nicht überschrieben. Nach Start wird maximal 30 Sekunden auf den Prozess gewartet; bei Überschreitung wird seine PID zur weiteren Prüfung ausgegeben, kein zweiter Prozess gestartet.

Aktueller Versuch: Start angefordert, Aufruf liefert noch keine gestartete Worker-PID und noch keinen Bericht. Möglicherweise wartet die Windows-Bestätigung. Kein erhöhter Zugriffserfolg belegt. Laufende Aufruf-Session 56847 vor jedem neuen Versuch prüfen; nicht blind erneut starten. Build und simulierte Tests nach dieser Ergänzung erfolgreich.

Die neue rein lesende Option `--diagnose` wurde im aktuellen Prozesskontext ausgeführt:

- Administrator-Token: False.
- Gerätefreigabe erfolgreich: GIGABYTE / AORUS 5 SE / FB0F.
- Öffnen der Firmware-WMI-Schnittstelle scheitert mit innerer `ManagementException`: „Zugriff verweigert“, äußerer Fehler `AorusTelemetryException`.

Damit ist eine Zugriffsverweigerung belegt, kein unbekanntes Gerät oder Nullmesswert. Ein erhöhter Worker wurde in diesem Schritt nicht getestet; ein erfolgreicher Zugriff mit dessen Token ist noch nachzuweisen. Die Diagnose zeigt lokale Ausnahmearten/HRESULTs und innere Ursachen, ohne neue Hardwarewerte zu schreiben.

Worker-Antworten besitzen jetzt stabile Fehlerkennungen (`access_denied`, `timeout`, `unsupported_device`, `device_read_failed`). Interne Ausnahmetexte werden bei Gerätefehlern nicht pauschal an Clients weitergereicht. Tests bestätigen verschachtelte Zugriffsfehler, Timeout und neutrale sonstige Fehlermeldungen. Gesamtbuild und komplette simulierte Testsuite erfolgreich. Das Protokoll ist weiterhin unveröffentlichtes Entwicklungsformat Version 1.

`CurrentUserOnly` ist noch keine ACL für einen unter anderem Konto laufenden Windows-Dienst. Unterschiedliche Benutzerrechte/Elevation und Sitzung 0 müssen getestet und gezielt autorisiert werden. Fremde Benutzer und Remotezugriff sind noch nicht adversarial getestet. Keine Aussage über vollständige IPC-Sicherheit aus positiven Verbindungstests ableiten.

Eine native WMI-Operation kann trotz CancellationToken hängen; die Frist beendet sie nicht garantiert. Der aktuelle Host verarbeitet seriell und kann dadurch blockiert werden. Das ist vor Schreibfreigaben mit unabhängiger Überwachung/Prozessisolation zu behandeln. Ein Status-Reply ist kein Lüfter-Sicherheitsheartbeat.

Nächste Schritte: WMI-Zugriff im vorgesehenen privilegierten Startkontext prüfen, schmale Dienst-ACL und Installation/Lebenszyklus ergänzen, externen Wächter implementieren, Ausfalltests, danach App-Controller durch Clients ersetzen. UI und Worker dürfen niemals gleichzeitig dieselben Hardwarewerte schreiben.
