# UI-unabhängige Lüfterüberwachung

Stand 2026-09-03. Implementierung: `Core/Features/Cooling/FanSafetySupervisor.cs`.

## Verhalten

- Ein eigener asynchroner 2-Sekunden-Prüfzyklus, ohne Dispatcher/WPF. Fixed wird nur freigegeben, wenn dieser Zyklus gestartet ist.
- Manuelle Freigabe trägt eine zufällige ID und gilt 10 Sekunden. Verlängerung erfordert die richtige ID, eine noch gültige Freigabe und frische gültige Temperaturmessung. Abgelaufene IDs können nicht wiederbelebt werden.
- Ablaufmessung über monotone Zeit. Telemetrie darf höchstens 5 Sekunden alt sein und nicht mehr als 1 Sekunde in der Zukunft liegen.
- Konservative bestehende Testgrenze: CPU und GPU unter 65 °C, Nulltemperatur abweisen. Dies ist keine ermittelte Hardware-Maximaltemperatur.
- Fixed-Schreibfehler führen ebenfalls zum Rückstellungsversuch, weil ein teilweise ausgeführter Schreibvorgang möglich ist.
- Normal wird über den vorhandenen Controller mit Readback angefordert. Fehlgeschlagene Rückstellung entzieht die Freigabe, behält den Fehlerzustand und wird im nächsten Tick erneut versucht.
- Ohne manuelle Freigabe keine Temperatur- oder Lüfterabfrage aus diesem Baustein. Nur der leichte Timer bleibt aktiv.
- Stop/Cancellation wartet die Normal-Rückstellung ab und meldet Fehler, statt erfolgreichen Abschluss vorzutäuschen. Nach Stop keine neue Fixed-Freigabe.

## Tests

Simulierte Uhr und Geräte: Fixed ohne gestartete Überwachung abweisen; Leerlauf ohne Schreiben; gültige Verlängerung; fremde ID; Ablauf und Versuch einer verspäteten Verlängerung; Temperaturgrenze; Rückstellungsfehler mit erneutem Versuch; veraltete Daten; Messfehler; explizites Freigeben; Worker-Cancellation mit Rückstellung; kein erneuter Start manueller Steuerung nach Stop.

Alle Tests bestanden, übrige RGB-/Akku-/Verbrauchs-/Lüftertests ebenfalls. Solution baut. Keine echten Hardware-Schreibtests.

## Integrationsgrenzen

Dieser Baustein ist noch NICHT der installierte Windows-Dienst und noch nicht an Stelle der bestehenden WPF-Fixed-Steuerung eingebunden. Der aktuelle App-Schutz bleibt deshalb vorerst UI-abhängig. Kein Autostarteintrag oder Hintergrundprozess installiert.

Nächste Stufe: dedizierter Worker-Host, eng begrenzte IPC, Authentifizierung, einheitlicher Hardwarebesitz und unabhängiger Wächter. Der Supervisor kann seinen eigenen Prozessabsturz nicht erkennen. Eine im nativen WMI-Aufruf hängende Operation kann außerdem die serialisierte Ausführung blockieren; Cancellation ist dafür kein Abbruchbeweis. Echte Prozessabsturz-/Hang-/Standbytests sind vor produktivem Fixed-Modus Pflicht.

Die spätere Freigabeerneuerung darf nicht blind an einem UI-Timer hängen: Der Worker entscheidet anhand frischer Messwerte und der gültigen manuellen Sitzung. Ein unabhängiger Wächter muss den Workerzustand gesondert überwachen. Ein vollständiger OS-/Firmware-Ausfall bleibt ohne nachgewiesenen Firmware-Watchdog außerhalb der Softwaregarantie.
