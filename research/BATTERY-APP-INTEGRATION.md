# Akku-Bedienung in der App

Stand: 2026-09-03.

- Eigenes `BatteryViewModel`, weiterhin geprüfter `IAorusBatteryChargeController` für Hardwarezugriffe. Kein Akku-Schreibzugriff in UI-Events selbst.
- Anzeige trennt bestätigte aktive Firmware-Policy von der ausgewählten Zielzahl. Ein gespeicherter Stopwert bei Standardmodus wird nicht als aktives Limit dargestellt.
- Auswahl 60–100 %, explizites Übernehmen, separater Standardmodus, erneutes Lesen bei Bedarf. Öffnen und Auswahl alleine schreiben nichts.
- Keine periodische Akku-WMI-Abfrage; Lesen bei Initialisierung, Nutzerabfrage und beim geprüften Schreiben.
- Unbekanntes Gerät/Policy/Werte sperren die Bedienung. Fehler bleiben nach dem Nachlesen sichtbar; nicht lesbarer Zustand wird als unbekannt dargestellt.
- Laufende Operation sperrt weitere Änderungen. Normales Schließen wartet auf die Akkuoperation; Dispose setzt das Ladelimit nicht zurück.
- Nachträglich gefundenen Anzeigefehler korrigiert: schlägt die Gerätefreigabe beim erneuten Lesen fehl, bleibt nicht eine alte Aktiv-Anzeige stehen.

## Prüfung

Simulationstests prüfen lesenden Start, Standardmodus mit inaktivem gespeicherten Wert 97, Auswahl ohne Schreiben, Übernahme 80, ungültige 59, Standardmodus, Schreibfehler mit erfolgreichem/fehlgeschlagenem Nachlesen, unbekannte Policy, nicht unterstütztes Gerät, doppelte Klicks während laufendem Schreiben und Dispose ohne Rückstellung.

Alle Tests erfolgreich; Solution Release-Build erfolgreich. Vorhandene RGB-, Verbrauchs- und Lüfterschutztests bestehen ebenfalls. Keine tatsächliche Akku-Schreiboperation während dieser UI-Integration. Echte Firmwaretests aus der früheren Recherche sind in BATTERY-CHARGE-LIMIT.md dokumentiert. UI-Klicktest und visuelle Abnahme stehen noch aus.
