# Netz-/Akkuprofile: Auswahlgrundlage

Aktueller UI-Nachtrag: einfacher Editor mit Persistenz und Zuordnungen eingebunden, Details in PROFILE-EDITOR.md. Die älteren Abschnitte unten beschreiben vorherige Zwischenschritte. Automatische Hardwareanwendung weiterhin offen.

## Nachtrag: Katalog und Persistenz

`ProfileCatalog` verwaltet bis zu 64 Profile mit eindeutigen IDs und optionalen Netz-/Akku-Zuordnungen. Verweise auf fehlende Profile werden abgelehnt. Aktualisieren erhält die Profil-ID; Entfernen löst beide betroffenen Zuordnungen auf. Die Sammlung ist schreibgeschützt.

`ProfileCatalogStore` speichert diesen Katalog einschließlich eigener Kurven in einem versionierten gerätegebundenen JSON-Format. Maximal 256 KiB, Tiefe 12, erforderliche Konstruktorparameter, keine unbekannten Felder. Konstruktorvalidierung greift auch beim Laden. Schreiben erfolgt über eine eigene temporäre Datei mit Flush und Ersetzen samt `.bak`; keine automatische Backup-Wiederherstellung. Laden oder Speichern ruft keine Hardware an. Ein einzelner Besitzer muss Schreibzugriffe koordinieren.

Tests mit eigenen temporären Dateien bestanden: Kurven/Zuordnungen vollständig erhalten, Ersetzen/Backup, doppelte IDs und fehlende Referenzen abweisen, Löschen/Umbenennen, kaputte/leere/falsche Version/Gerätezuordnung/Modi und Größenüberschreitung. Gesamte simulierte Suite bestanden, keine reale Benutzerkonfiguration verändert. Die Persistenz ist noch nicht an die Oberfläche angeschlossen; der spätere Editor muss Schreibfehler anzeigen und darf erfolgloses Speichern nicht als Erfolg melden.

## Nachtrag: Profilmodell implementiert

`LaptopProfile` enthält eine feste ID, einen Namen, Windows-Leistungsmodus sowie Normal/Quiet/Gaming/Maximum/Fixed/Dynamic/CustomCurve. Nur Fixed darf einen festen Rohwert enthalten, nur CustomCurve eine eigene Kurve. Unbekannte Modi, fehlende bzw. widersprüchliche Parameter und ungültige Namen werden abgewiesen. Eine Profildefinition ist keine Freigabe zur automatischen Wiederaufnahme manueller Lüftersteuerung.

Die vorhandene Kurvenvalidierung wurde ohne Lockerung der Grenzen in `FanCurveValidation` zentralisiert: genau 15 Punkte, Indizes 0–14, Rohwerte 57–229, monotone Temperatur/Werte, letzter Punkt spätestens 90 °C bei 229. Der reale WMI-Controller und das Profilmodell verwenden jetzt dieselbe Prüfung. Nullpunkte werden ausdrücklich abgewiesen. Profile kopieren Kurven in eine schreibgeschützte Momentaufnahme; nachträgliche Änderungen am Eingabearray ändern das Profil nicht.

Build ohne Warnungen/Fehler und gesamte simulierte Testsuite erfolgreich. Neue Tests prüfen alle Modi, Fixed-Grenzen, widersprüchliche Parameter, ungültige IDs/Namen und defensive Kurvenkopie. Keine Hardware verändert. Profilpersistenz, Editor und sicherer Anwendungsvorgang bleiben offen; strukturelle Kurvenvalidierung allein beweist keine ausreichende Kühlung unter Last.

Stand 2026-09-03. Modul `Core/Features/PowerProfiles` ergänzt; noch keine automatische Hardwareumschaltung und kein Profil-Editor.

## Konkreter korrigierter Fehler

`WindowsPowerOverlayController.IsOnAcPower` interpretierte zuvor jeden Wert außer 1 als Akkubetrieb. Ein unbekannter Windows-Status (255) konnte dadurch zur Auswahl des DC-Registrywerts führen. Jetzt werden nur 0 als Akku und 1 als Netz erkannt; unbekannt löst einen verständlichen Fehler aus. Diese Korrektur wirkt bereits im vorhandenen Controller.

## Neue Auswahlregeln

- Unabhängige optionale Profil-IDs für Netz und Akku. Nicht zugeordnet bedeutet keine automatische Änderung, nicht ein erfundenes Standardprofil.
- Zwei übereinstimmende Beobachtungen mit mindestens zwei Sekunden Abstand ohne zwischenzeitlich beobachteten Wechsel, bevor eine Profil-ID geliefert wird. Zeitmessung monoton, keine Abhängigkeit von Änderungen der Wanduhr.
- Wechsel oder unbekannter Status verwirft die bisherige Kandidatur. Nach Standby/Verlust der Beobachtung muss der spätere Aufrufer `Reset` verwenden.
- Die Auswahl bestätigt keine erfolgreiche Anwendung. Der spätere Koordinator muss Profilauflösung, aktuelle Stromquelle, Gerätefreigabe, Serialisierung, Rücklesen und Fehlerbehandlung übernehmen und identische bereits angewandte Profile nicht erneut schreiben.

## Tests und offene Integration

Simulierte Tests bestanden für Netz/Akku/unbekannt, Startverzögerung, kurzzeitigen Quellenwechsel, neue Stabilitätsphase nach unbekannt, getrennte Zuordnung, fehlende Zuordnung und Reset nach Resume. Gesamtbuild und bestehende Tests ebenfalls erfolgreich. Keine realen Leistungs-/Lüfterwerte verändert.

Das Modul hat noch keine Ereignisquelle und überwacht nicht selbst die Kontinuität von Messungen. Eine lange Beobachtungspause muss der Aufrufer erkennen und zurücksetzen. Noch erforderlich: Profilmodell mit eigenen Kurven, persistente Zuordnungen, Ereignisanbindung, sicherer Hardware-Koordinator und UI. Ein Test der reinen Auswahl beweist keine funktionierende automatische Umschaltung.

Parallel angeforderter erhöhter Worker-Lesetest: Session 56847 erneut geprüft, weiterhin laufend ohne gestartete Worker-PID oder Bericht. Kein zweiter Start ausgelöst. Dies blockiert die unabhängige Implementierung der Profilgrundlage nicht.
