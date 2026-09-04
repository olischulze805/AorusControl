# Tastatur-RGB in AORUS Control

Stand: 2026-09-03

## Implementierter Umfang

Die bestehende WPF-Anwendung enthält jetzt eine bewusst einfache, funktionale Tastatursteuerung:

- Beleuchtung ein/aus
- separater Farbwähler für Zone 1, Zone 2 und Zone 3
- optional alle drei Zonen gemeinsam ändern
- live eingelesene Hex-Farbe je Zone
- neun host-gerenderte Effekte: Atmen, Pulsieren, Farbwechsel, Regenbogen-Lauflicht, Welle, Lauflicht, Pendel, Regentropfen und Ausblendende Welle
- Effekt starten, durch einen anderen Effekt ersetzen und gezielt stoppen
- sichtbare Erfolgs- und Fehlermeldung

Eine irreführende Software-Helligkeitsregelung und Profile sind in diesem Schritt nicht enthalten.

## Sicherheitsgrenze

Der gemeinsame Hardwarecontroller `GigabyteHidKeyboardRgbController` öffnet ausschließlich:

- USB VID `1044`
- USB PID `7A41`
- Interface `MI_03`
- Feature-Report-Länge `9`

Wenn kein eindeutiges Gerät gefunden wird, wird nichts geschrieben. Vor jeder statischen Änderung werden alle drei Zonen gelesen. Nach der Änderung werden alle drei Zonen erneut gelesen und bytegenau mit dem Sollzustand verglichen. Bei einem Fehler versucht der Controller, alle drei vorherigen Zonenwerte wiederherzustellen und prüft auch diese Wiederherstellung.

Vor dem Start eines Effekts werden ebenfalls alle drei Zonen gespeichert. Die Animation verändert ausschließlich die drei RGB-Zonen. Beim Stoppen, beim Wechsel zu einem anderen Effekt, vor einer statischen Farb- oder Ein-/Aus-Änderung und beim Schließen der Anwendung werden die gespeicherten Werte zurückgeschrieben und bytegenau geprüft. Einzelne Animationsbilder werden bewusst nicht gelesen, da das den sichtbaren Ablauf stark abbremsen würde; die abschließende Wiederherstellung wird vollständig verifiziert.

## Protokoll

- Zonen lesen: Befehl `0x88`, Selektor `1` bis `3`
- Zone schreiben: Befehl `0x08`, Selektor `1` bis `3`
- Farben: direkte 8-Bit-Werte für Rot, Grün und Blau
- Beleuchtung aus: Helligkeitsbyte `0`
- Beleuchtung ein: Helligkeitsbyte `50`
- Prüfsumme: `255 - Summe(Byte 1..7)`
- Gigabytes bestätigte Wartezeit: 65 ms nach jedem Feature-Befehl
- Animation: 5 ms Abstand zwischen den drei Zonenschreibvorgängen, in der Praxis etwa 21 vollständige Bilder pro Sekunde

Das Ein-/Ausschalten behält die drei Farben bei und ändert nur das Helligkeitsbyte. Ein Farbwähler behält den aktuellen Ein-/Aus-Zustand bei. So können Farben auch im ausgeschalteten Zustand vorbereitet werden.

Die Effekte werden auf dem PC berechnet und als schnelle Folge normaler Zonenfarben gesendet. Sie verwenden nicht den auf Firmware `19.0.4` unzuverlässigen globalen Effektselektor `0x08/0` und nicht die Picture-Matrix- oder Firmware-Flash-Kanäle. Atmen und Pulsieren verwenden die beim Start gespeicherte Farbe der ersten Zone als Grundfarbe; die übrigen Effekte besitzen zunächst feste, gut erkennbare Farbmuster.

## Dateien

- `src/AorusControl.Core/Services/GigabyteHidKeyboardRgbController.cs`
- `src/AorusControl.Core/Services/IAorusKeyboardRgbController.cs`
- `src/AorusControl.Core/Models/KeyboardRgbColor.cs`
- `src/AorusControl.Core/Models/KeyboardRgbZoneState.cs`
- `src/AorusControl.Core/Models/KeyboardRgbState.cs`
- `src/AorusControl.Core/Models/KeyboardRgbEffect.cs`
- `src/AorusControl.App/ViewModels/MainWindowViewModel.cs`
- `src/AorusControl.App/MainWindow.xaml`

## Verifikation

- Release-Build der gesamten Lösung: erfolgreich
- Warnungen: 0
- Fehler: 0
- Startverhalten: RGB-Zustand wird nur gelesen; Schreiben erfolgt erst nach einer Benutzeraktion
- Die zugrunde liegenden zehn host-gerenderten Muster einschließlich statischer Farbe wurden zuvor auf dem Gerät interaktiv sichtbar bestätigt; die App stellt die neun bewegten Varianten bereit
- Die neu eingebaute App-Bedienung ist kompiliert, aber noch nicht durch den Besitzer visuell durchgeklickt

## Nachtrag 2026-09-03: vier Helligkeitsstufen

Die bisherige Beschraenkung auf aus und volle Helligkeit ist ueberholt. Die
Firmware akzeptiert im Zonenpaket genau vier Werte in Byte 6: `0`, `24`, `32`
und `50`. Nachweis und Messreihen in `KEYBOARD-BRIGHTNESS.md`.

Umsetzung:

- `KeyboardBrightnessLevel` in `AorusControl.Core.Models` fuehrt die vier Stufen
  als Enum mit den Rohwerten. `KeyboardBrightnessLevels.FromRawValue` bildet
  fremde Werte auf aus beziehungsweise hell ab, statt eine Zwischenstufe zu
  erfinden.
- `IAorusKeyboardRgbController.SetBrightness(level)` ist neu. `SetLighting(bool)`
  bleibt erhalten und delegiert auf `Off` und `High`.
- `KeyboardRgbState.Brightness` liefert die aktuelle Stufe aus dem gelesenen
  Zustand.
- Der Effektrenderer schreibt nicht mehr fest volle Helligkeit, sondern die
  Stufe, die vor dem Effekt eingestellt war. Steht sie auf aus, wird auf hell
  ausgewichen, damit der Effekt sichtbar bleibt.
- Die Oberflaeche ersetzt das Ein-Aus-Kaestchen durch eine Auswahl mit vier
  deutschen Bezeichnungen. Ein Schieberegler wird bewusst nicht angeboten, weil
  jeder andere Wert entweder aus oder volle Helligkeit bedeutet.

Unveraendert bleiben alle Sicherheitsmerkmale: exakte Geraetesperre auf
`1044:7A41 / MI_03 / 9 Byte`, Erfassen aller Zonen vor jeder Aenderung,
Verifikation per Ruecklesen und Wiederherstellung des vorherigen Zustands bei
Fehlern.

## Nachtrag 2026-09-03: Effektgeschwindigkeit

Gigabytes Speed-Byte gehoert zum globalen Effektkommando `0x08` Selektor 0, das
auf Firmware `19.0.4` nichts rendert. Seine neun diskreten Stufen sind fuer uns
damit bedeutungslos. Weil unsere Effekte im Host entstehen, ist die
Geschwindigkeit eine reine Zeitskalierung und nicht an neun Werte gebunden.

Umsetzung:

- `KeyboardEffectSpeed` mit fuenf Stufen und `ToTimeScale()`: `0,25`, `0,5`,
  `1,0`, `2,0`, `4,0`. `Normal` ist exakt `1,0`, damit die vom Besitzer
  bestaetigten Effekttimings unveraendert bleiben.
- `PlayEffectAsync(effect, speed, token)` multipliziert die verstrichene Zeit mit
  dem Faktor, bevor der Frame berechnet wird. Alle Effekte skalieren dadurch
  gleichmaessig, auch die mit Modulo-Schwellen.
- Die Oberflaeche hat eine Auswahl "Tempo" neben dem Effekt. Wird sie geaendert,
  waehrend ein Effekt laeuft, startet dieser neu, weil der Renderer die
  Zeitskalierung beim Start einmalig liest.

Nicht implementiert ist ein stufenloser Regler. Fuenf benannte Stufen sind
ausreichend und halten die Oberflaeche schmal; der Faktor liesse sich jederzeit
auf einen Schieberegler erweitern.

## Nachtrag 2026-09-03: dunkles Design der Steuerelemente

Der Besitzer meldete, dass Knoepfe und Auswahlfelder kaum zu sehen sind.

Ursache war der globale impliziten `TextBlock`-Style in `App.xaml`, der eine
helle Schriftfarbe setzte. WPF rendert den Text in Buttons und ComboBox-Eintraegen
ueber einen `ContentPresenter`, der dafuer einen `TextBlock` erzeugt — und ein
impliziter Style schlaegt vererbte Werte. Die helle Schrift landete deshalb auf
den hellen Windows-Standardflaechen und war praktisch unsichtbar.

Behoben:

- Der implizite `TextBlock`-Style setzt keine Schriftfarbe mehr, nur die
  Schriftart. Das Fenster setzt `Foreground` einmal, alles andere erbt.
- Eigene dunkle Vorlagen fuer `Button`, `ComboBox`, `ComboBoxItem` und
  `CheckBox`, jeweils mit ausdruecklicher Schriftfarbe, sichtbarem
  Deaktiviert-Zustand und dunklem Popup.
- Die Button-Vorlage bindet `Background` weiterhin per `TemplateBinding`, damit
  die drei Zonen-Farbfelder ihre gebundene Farbe zeigen.
- Die Helligkeits- und Tempo-Auswahl verwenden ein ausdrueckliches
  `ItemTemplate`. Mit `DisplayMemberPath` allein blieb
  `SelectionBoxItemTemplate` in der eigenen Vorlage leer, sodass im geschlossenen
  Feld die `ToString`-Ausgabe des Records erschien.
- Der Inhaltsbereich liegt in einem `ScrollViewer`, damit auf niedrigen
  Bildschirmen kein Bedienelement unerreichbar wird.

Geprueft wurde das Ergebnis anhand von Bildschirmaufnahmen der laufenden
Anwendung, nicht nur am Quelltext.
