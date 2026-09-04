# Profil-Editor: einfache WPF-Anbindung

## Nachtrag: asynchrone Dateioperationen

Initiales Laden, Neuladen, Speichern, Löschen und Zuordnungsspeichern führen Dateiarbeit jetzt außerhalb des UI-Threads aus. Ergebnisse werden erst nach erfolgreichem Abschluss im aufrufenden Kontext übernommen. Wiederverwendbarer AsyncRelayCommand ergänzt; ein gemeinsamer Busy-Zustand verhindert auch Überschneidungen verschiedener Editorbefehle. Währenddessen sind Editor-Eingaben gesperrt und Fensterschließen wird zurückgehalten; Status zeigt den laufenden Dateivorgang. Es gibt keinen künstlichen Timeout, der einen noch schreibenden Vorgang als beendet ausgibt.

Neuer deterministischer Test hält einen simulierten Schreibvorgang an: Aufrufer bekommt sofort ein Task zurück, Editor ist busy, weitere Save/Assign/New-Befehle erzeugen keine konkurrierende Operation oder vorzeitige Katalogänderung. Nach Freigabe wird genau ein Profil veröffentlicht und der Editor entsperrt. Alle bisherigen Tests bestehen; WPF-Renderlauf bei beiden Breiten weiterhin erfolgreich. Native Fensterreaktion auf einem tatsächlich langsamen Datenträger noch nicht geprüft. Die älteren Hinweise auf synchrone Dateioperationen sind überholt.

## Nachtrag: echte WPF-Renderprüfung

`tests/AorusControl.UiChecks` rendert das kompilierte ProfileWindow mit den Produktionsressourcen aus App.xaml und injizierten Testprofilen. Kein App-Start, keine Hardwarecontroller, keine Benutzerdateien und kein sichtbares natives Fenster. Ausgaben in `research/runs/profile-ui`, je 600/760 Pixel Inhaltsbreite und obere/untere Scrollposition (96 DPI).

Die erste Renderprüfung zeigte technische Typ-/Recordnamen in den geschlossenen ComboBoxen. Ursache: gemeinsamer ContentPresenter übernahm den ItemTemplateSelector nicht. Ergänzt; erneute Bilder zeigen korrekt Profilname, „Ausbalanciert“, „Eigene Kurve“ und „Keine automatische Zuordnung“. Außerdem explizite konsistente Hintergrund-/Textfarben und TextBox-Stil für das Profilfenster ergänzt. Tabelle behält gut kontrastierende helle Zeilen.

600 Pixel oben/unten und 760 Pixel oben visuell geprüft: Beschriftungen und Umbruch lesbar, Zuordnungen/Speicherknöpfe über Scrollen erreichbar. Die Tabelle hat weiterhin einen eigenen Scrollbereich. Renderlauf, gesamter Build und simulierte Tests erfolgreich. Dies beweist Layout/gebundene Anzeige, nicht native Klick-/Tastaturbedienung, Popupdarstellung, Dialogverhalten oder DPI-Skalierung. Kein Abschluss der finalen Gestaltung.

## Nachtrag: tabellarischer Kurveneditor

Das Textfeld wurde durch eine feste Tabelle mit 15 Punkten, Temperatur (°C) und Lüfter-Rohwert ersetzt. Sortieren, Zeilen hinzufügen/löschen und Spaltenumordnung sind ausgeschaltet, damit Firmware-Punktindizes nicht versehentlich vertauscht werden. Zahlen bleiben während der Bearbeitung Text, damit ungültige Eingaben nicht still auf einen früheren Zahlenwert zurückfallen. Beim Speichern nennt ein Formatfehler die betroffene Punktnummer; anschließend gilt die gemeinsame Kurvenvalidierung.

ViewModel-Tests bestätigen Übernahme gespeicherter Kurven in die Tabelle, Speichern einer Tabellenänderung, Erkennung ungespeicherter Änderungen und Zurückweisen nichtnumerischer Eingaben unter Erhalt des Entwurfs. Gesamtbuild und alle simulierten Tests bestanden. Kein Hardwaretest und keine reale DataGrid-Klick-/Tastaturabnahme. Ein Diagramm mit verschiebbaren Punkten ist weiterhin nicht implementiert. Die unten beschriebene Textlisten-Bedienung ist damit historisch.

Worker-Lesetest Session 56847 erneut geprüft: Aufruf weiterhin laufend ohne Ausgabe, keine worker-access-Berichte vorhanden. Kein neuer Berechtigungsdialog gestartet. Rechteprüfung bleibt offen.

## Nachtrag: Schutz ungespeicherter Änderungen

Entwurf und Netz-/Akku-Zuordnungen werden nun unabhängig gegen ihren gespeicherten Stand verglichen. Neu/Bearbeiten und Löschen des bearbeiteten Profils fragen bei geändertem Entwurf nach; Datei-Neuladen und Fensterschließen berücksichtigen auch offene Zuordnungen. Dialog-Vorauswahl ist Nein. Ein leerer frisch geöffneter Entwurf verursacht keine Warnung.

Zusätzlich einen Datenverlust im vorherigen Editor korrigiert: Profilspeichern setzte bislang noch nicht gespeicherte Zuordnungen auf den Dateistand zurück. Diese bleiben jetzt erhalten. Umgekehrt lässt Zuordnungsspeichern den Profilentwurf unverändert. Gelöschte Profil-IDs werden weiterhin aus den Zuordnungen entfernt.

Neue simulierte Tests prüfen Ablehnen/Bestätigen des Verwerfens, Schutz beim Neuladen und unabhängigen Erhalt beider Bearbeitungsbereiche. Echte Dialog-/Klickprüfung weiterhin offen. Dieser Nachtrag ersetzt die unten genannte offene Warnung vor ungespeicherten Änderungen; synchrone Dateioperationen und grafischer Kurveneditor bleiben offen.

Stand 2026-09-03. Unter „Windows-Leistungsmodus“ öffnet „Eigene Profile verwalten …“ ein separates modales Fenster. ViewModel und Datei werden erst bei Bedarf geladen, kein neuer Hintergrundtimer.

## Bedienung

- Neu, ausgewähltes Profil ausdrücklich in den Entwurf laden, speichern, löschen mit Bestätigung.
- Name, Windows-Leistungsmodus und alle modellierten Lüftermodi. Fixed-Feld nur im Fixed-Modus aktiv. Kurvenfeld nur bei eigener Kurve aktiv.
- Einfacher Kurveneditor: genau 15 Zeilen `Temperatur:Rohwert`. Kein vorgetäuschtes Prozentmaß; zentrale bestehende Grenzprüfung bleibt maßgeblich.
- Separate Netz-/Akku-Zuordnung inklusive „Keine automatische Zuordnung“. Eigener Speicherknopf; Zuordnungen sind nicht schon durch die ComboBox-Auswahl dauerhaft geändert.
- Datei `%LOCALAPPDATA%\AorusControl\profiles-v1.json`, vorheriger Stand als `.bak`.
- Sichtbarer Hinweis: Speichern ist nicht Anwenden; automatische Hardwareumschaltung ist noch inaktiv. Dieser Editor besitzt keine Hardwarecontroller.

## Fehlerverhalten und Tests

Erst nach erfolgreichem Speichern wird der Katalog im ViewModel ersetzt. Ein Ladefehler sperrt spätere Schreibversuche, bis Laden erfolgreich war; keine leere Ersatzdatei über beschädigte Nutzerdaten schreiben. Fehlermeldung bleibt sichtbar, Entwurf kann korrigiert und erneut gespeichert werden.

Build ohne Warnungen/Fehler und gesamte simulierte Suite erfolgreich. ViewModel-Test deckt Neuanlage, identitätserhaltendes Bearbeiten, beide Zuordnungen, Löschung samt Zuordnungen, Kurvenvalidierung/-speicherung, Speicherfehler und Schutz nach Ladefehler ab. Keine realen Hardwarewrites, keine echte Benutzerdatei geschrieben.

## Verbleibende Arbeit

Keine visuelle oder reale Klick-Abnahme dieses Fensters. Ein einfacher Text-Kurveneditor ist noch nicht der gewünschte grafische Kurveneditor. Entwurfswechsel/Schließen warnen noch nicht vor ungespeicherten Änderungen; abschließende Benutzerführung steht aus. Dateioperationen sind derzeit synchron und größenbegrenzt; langsame Datenträger müssen später ohne blockierendes UI behandelt werden. Hardware-Anwendung, automatische Umschaltung, Worker-Sicherheit und abschließendes modernes Design bleiben offen.
