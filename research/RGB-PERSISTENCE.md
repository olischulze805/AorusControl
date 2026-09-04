# RGB-Einstellungen dauerhaft speichern

Stand: 2026-09-03.

## Implementiert

- `KeyboardSettingsStore` speichert ausschließlich Benutzerwünsche: Ein/Aus, letzte eingeschaltete Helligkeit, Effekt, Tempo und drei manuelle Farben. Keine laufenden Animationsbilder und keine beliebigen Hardwarebefehle.
- Produktionsdatei: `%LOCALAPPDATA%\AorusControl\keyboard-v1.json`. Formatversion 1, Gerätezuordnung AORUS-5-SE4-FB0F.
- Beim App-Start werden gültige gespeicherte RGB-Einstellungen übernommen. Ohne Datei wird nur der aktuelle Tastaturzustand gelesen. Das ist noch kein Windows-Autostart.
- Nach erfolgreicher Änderung speichert die App automatisch. Ein Speicherfehler wird als „Aktiv, aber nicht gespeichert“ angezeigt; eine bereits erfolgreiche Hardwareänderung wird dadurch nicht als fehlgeschlagen dargestellt.
- Schreiben über eine eindeutige temporäre Datei im selben Verzeichnis, Flush und Ersetzen mit `.bak` der vorherigen Datei. Keine automatische Wiederherstellung aus dem Backup.
- Größenlimit 16 KiB, begrenzte JSON-Tiefe, unbekannte Felder/Versionen/Geräte und ungültige Hardwarewerte werden abgelehnt. Bei Ladefehler bleibt die Datei unangetastet; die App liest den aktuellen Gerätezustand und zeigt eine Warnung. Eine spätere ausdrückliche RGB-Änderung darf eine neue gültige Datei speichern.

## Nachweise

Gesamte simulierte Testsuite erfolgreich: Roundtrip einschließlich ausgeschaltetem Zustand, Ersetzen/Backup, ungültige Helligkeit, falsche Version/Gerätezuordnung, beschädigte und übergroße Datei, Entfernung eigener temporärer Dateien. ViewModel-Test bestätigt Startwiederherstellung, Helligkeitsgedächtnis, Speicherung und sichtbaren Speicherfehler.

Tests nutzen eigene temporäre Dateien und simulierte Controller; keine Benutzerkonfiguration oder reale Hardware verändert. Tatsächlicher App-Neustart am Gerät und Stromausfall während des Schreibens nicht getestet.

## Offen

Fn+Space-Synchronisierung, Standby/Resume mit Neuverbindung und Konfliktdiagnose für fremde Controller. Nur RGB wird hier wiederhergestellt; keine alte manuelle Lüfterfreigabe oder Leistungswahl. Kein Schutzversprechen gegen jeden Datenträgerfehler.
