# Eigene Lüfterkurven speichern und laden

Stand: 2026-09-09. Ersetzt die frühere kombinierte Profilverwaltung auf ausdrücklichen Nutzerwunsch.

Unter Kühlung → Dynamic (eigene Kurve) liegen jetzt „Kurve speichern …“ und „Kurve laden …“. Windows-Dateidialoge erlauben beliebig benannte JSON-Dateien, beispielsweise Leise.json und Spielen.json.

Speichern exportiert die im Editor gezeichnete Kurve als validierte 15-Punkte-Firmwarekurve; die bestehenden Firmwaregrenzen und die Umrechnung gelten wie bei „Kurve übernehmen“. Laden setzt diese Kurve in den Editor. Beide Aktionen ändern weder Lüfter noch Windows-Leistungsmodus. Erst „Kurve übernehmen“ schreibt und aktiviert die Kurve. Speichern entfernt deshalb nicht die Markierung „noch nicht übernommen“.

Bei ausstehenden Änderungen fragt Laden vor dem Ersetzen des Entwurfs nach. Ungültige, zu große, fremde oder fehlende Dateien werden abgelehnt; der Entwurf bleibt erhalten. Dateiarbeit läuft im Hintergrund, konkurrierende Kühlungsaktionen sind gesperrt. Beim Überschreiben bestätigt der Dateidialog den Vorgang; der bestehende FanCurveStore erstellt zusätzlich ein .bak-Backup.

Entfernt: Profilverwaltungsbutton unter Leistung & Akku, ProfileWindow samt ViewModel, kombinierte LaptopProfile-/ProfileCatalog-Speicherung, ungenutzte AC/DC-Profilzuordnung und deren Tests. Die weiterhin benötigte Netz-/Akku-Erkennung bleibt. Historische Forschungsnotizen bleiben als Archiv erhalten, sind keine Beschreibung der aktuellen Oberfläche. Eventuell vorhandene profiles-v1.json des Benutzers wird nicht gelöscht; frühere Forschungsstände sind über Git rekonstruierbar.

Prüfung: gesamte Smoke-Suite und Layoutprüfung erfolgreich. Neuer Test prüft Export/Import, ausstehende Änderungen, unveränderte Hardware-Schreibzähler und Entwurfserhalt bei fehlender Datei. Die bestehenden FanCurveStore-Tests decken beschädigte Dateien, Grenzen, Gerätezuordnung, Version und Backups ab.
