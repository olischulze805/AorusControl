# Tastaturhelligkeit: die vier Fn+Space-Stufen

`Fn+Space` schaltet auf dem AORUS 5 SE4 vier physische Helligkeitsstufen.

**Abgeschlossen: die Helligkeit ist lesbar UND setzbar.**

- **Lesen:** 4-Byte-Eingangsreport auf `MI_02/COL_04`, Wert in Byte 2.
- **Setzen:** Zonenkommando `0x08` Selektor 1-3, Byte 6 aus `{0, 24, 32, 50}`.
  Das ueberstimmt die per Fn+Space gesetzte Stufe.

Alle vier Stufen sind damit vom Host aus erreichbar. Frueheren Abschnitten
dieser Datei liegt noch die widerlegte Annahme eines Off/On-Gates zugrunde; sie
sind als korrigiert markiert und wegen der Beweiskette erhalten. Der Abschnitt
"AUFLOESUNG" traegt das Ergebnis.

## Was bereits ausgeschlossen ist

- **Das Zonen-Helligkeitsbyte ist kein PWM-Regler.** Getestet wurden `0`, `1`,
  `25`, `49` und `50`. Jeder Wert las sich exakt zurück, sichtbar blieb die
  Tastatur aber bis `49` aus und sprang erst bei `50` auf volle Helligkeit.
  **Diese Deutung als Off/On-Gate war falsch** — alle geprüften Werte lagen
  neben den vier akzeptierten. Siehe "Auflösung" unten.
- **`EC.KBLL` ändert sich beim Tastendruck nicht.** Ein 25-Sekunden-Monitor las
  `KBLL@0xD7` alle 250 ms, während der Besitzer `Fn+Space` durchschaltete. Der
  Wert blieb konstant `0`.
- **Die Vendor-HID-Kanäle melden nichts.** Im gleichen Lauf sendeten weder
  `MI_01` noch `MI_03` einen einzigen Eingangsreport.
- **Das Tastaturmodul kennt keinen Helligkeitsbefehl.** Die vollständig
  dekompilierten `KeyboardModel.dll` und `KeyboardDomainLogic.dll` führen
  Helligkeit ausschliesslich als Byte 5 bzw. 6 der `0x08`-Pakete.
  `UpdateLightBrightness(v)` speichert `v / 2`, `GetFusionBrightness()` gibt
  `v * 2` zurück — das ist reine UI-Skalierung von 0-100 auf 0-50.

Daraus entstand die bisherige Schlussfolgerung: Der Dimmer läuft
controllerintern, und `Fn+Space` wird vollständig im ITE-Controller verarbeitet.

## Neuer Ansatzpunkt 1: der nie aufgerufene ACPI-Setter

Die WMI-Klassen wurden erstmals vollständig nach helligkeitsnahen Methoden
durchsucht. Ergebnis:

| Klasse | Methode | ID | Bemerkung |
|---|---|---|---|
| `GB_WMIACPI_Get` | `GetKeyBoardBackLight` | `246` = `0xF6` | liest `EC.KBLL@0xD7`, liefert immer `0` |
| `GB_WMIACPI_Set` | **`SetKeyBoardBackLight`** | `246` = `0xF6` | **nie aufgerufen** |
| `GB_WMIACPI_Set` | `SetRGBLed` | `131` = `0x83` | nie aufgerufen, Zweck unklar |
| `GB_WMIACPI_Set` | `SetBrightness` | `192` = `0xC0` | vermutlich Displayhelligkeit |
| `GB_WMIACPI_Set` | `SetBrightnessOff` | `196` = `0xC4` | vermutlich Displayhelligkeit |
| `GB_WMIACPI_Set` | `IncreaseBrightness` | `205` = `0xCD` | vermutlich Displayhelligkeit |

Signatur: `SetKeyBoardBackLight(Data: UInt8)` mit `DataOut: UInt8`.

Getter und Setter teilen die Methoden-ID `0xF6`; der Unterschied liegt nur im
Dispatcher, `WMBC` für Lesen und `WMBD` für Schreiben. Der Getter liefert
konstant `0`, obwohl die Beleuchtung an ist. Damit ist offen, ob `KBLL` auf
diesem Modell ungenutzt ist, nur schreibbar ist, oder ob `0` schlicht die
aktuelle Stufe bezeichnet. Ein reiner Lesevorgang kann das nicht klären — der
Schreibpfad ist die einzige verbleibende Auskunftsquelle.

Wichtige Einschränkung: Das Vorkommen in der geteilten MOF beweist nicht, dass
`WMBD` für `0xF6` überhaupt einen Fall implementiert. Der FB0F-DSDT liegt nicht
als Datei vor; die frühere Analyse wurde nicht gesichert. Der Test beantwortet
das empirisch über den Rücklesewert.

### Test: `--test-backlight-level`

Start über `tools\Start-BacklightLevelTest.cmd`.

Gates identisch zum Akku-Schreibtest: exaktes Modell `AORUS 5 SE` und BIOS
`FB0F`, Administratorrechte, getipptes `JA` im Starter und zusätzlich das Token
`--confirm-backlight-write`. Ohne Token bricht der Test ab, bevor irgendein
Firmwarezugriff erfolgt — verifiziert.

Ablauf: Ausgangswert per `GetKeyBoardBackLight` sichern, dann die Werte aus
`--levels` schreiben, nach jedem Wert zurücklesen und die Beobachtung des
Besitzers erfassen. Im `finally` wird der Ausgangswert zurückgeschrieben und
verifiziert.

Der erste Lauf schrieb `0` bis `4` und blieb wirkungslos. Nach der Entdeckung
der echten Stufenwerte ist der Standard jetzt `0,24,32,50`.

Nicht berührt: Akku, Lüfter, Ladepolitik, Tastenmatrix, Makros, HID, BIOS,
Firmware.

## Neuer Ansatzpunkt 2: das Helligkeitsbyte oberhalb von 50

Der frühere Grenztest deckte nur `0`, `1`, `25`, `49` und `50` ab. Gigabytes
Oberfläche sendet nie mehr als `50`, weil sie eine 0-100-Prozentangabe halbiert.
**Der gesamte Bereich `51` bis `255` ist damit ungetestet.**

Das ist relevant, weil ein Off/On-Verhalten bei genau `50` drei verschiedene
Ursachen haben kann:

- exakter Vergleich `== 50`, dann bleibt alles darüber aus;
- Schwellenvergleich `>= 50`, dann bleibt alles darüber an, ohne Abstufung;
- echte Skala mit hoher Mindestschwelle, dann werden Werte über `50` heller.

Nur der dritte Fall wäre ein nutzbarer Regler, aber alle drei Fälle sind
informativ, und der Test kostet nichts.

### Test: `--sweep-zone-brightness`

Start über `tools\Start-ZoneBrightnessSweep.cmd`, ohne Administratorrechte.

Die Farbe bleibt konstant `#FFFFFF` auf allen drei Zonen, nur das
Helligkeitsbyte wandert über `0`, `25`, `50`, `51`, `60`, `75`, `100`, `150`,
`200`, `255`. Nach jedem Wert wird der gespeicherte Rücklesewert protokolliert
und die Beobachtung erfasst. Verwendet werden ausschliesslich der bewiesene
Zonensetter `0x08` Selektor 1-3 und der Getter `0x88`. Die Ausgangsfarben werden
am Ende wiederhergestellt und verifiziert.

## Ergebnis Ansatzpunkt 1: KBLL ist beschreibbar, aber wirkungslos

Der ACPI-Test lief vollständig durch. Gerät und BIOS wurden als freigegeben
erkannt, Administratorrechte lagen vor, Ausgangswert war `0`.

| Geschrieben | Zurückgelesen | Beobachtung |
|---|---|---|
| `0` | `0` | alles bleibt an |
| `1` | `1` | keine Änderung |
| `2` | `2` | keine Änderung |
| `3` | `3` | keine Änderung |
| `4` | `4` | keine Änderung |

Rollback auf `0` verifiziert.

Zwei Erkenntnisse daraus:

1. **`WMBD` implementiert den Fall `0xF6` tatsächlich.** Jeder geschriebene Wert
   `0` bis `4` kam exakt zurück. `EC.KBLL@0xD7` ist also ein echtes, funktionierendes
   Speicherbyte, und der Setter ist nicht bloss ein MOF-Eintrag.
2. **Nichts liest dieses Byte.** Kein Wert hatte eine sichtbare Wirkung auf die
   Beleuchtung.

Damit ist auch die frühere Beobachtung erklärt: Der Getter lieferte nicht
"immer 0", weil er defekt wäre, sondern weil dort tatsächlich `0` gespeichert
war. Auf diesem Modell ist `KBLL` ein verwaistes Feld — vorhanden, beschreibbar,
aber nicht mit den LEDs verdrahtet. Als Helligkeitsregler ist es erledigt.

Bericht: `research/runs/keyboard-backlight-level-20260903-115048.md`.

## Ergebnis Ansatzpunkt 2: Schwellenvergleich, kein PWM

Der Sweep lief über `0`, `25`, `50`, `51`, `60`, `75`, `100`, `150`, `200`,
`255`, Farbe konstant weiss. **Jeder Wert las sich exakt zurück.** Sichtbar:
`0` und `25` aus, `50` an, und `51` bis `255` ausnahmslos genauso hell wie `50`.

Von den drei zuvor formulierten Möglichkeiten trifft die zweite zu: Die Firmware
vergleicht `>= 50`. Das Byte sei ein Schwellenschalter, kein Regler.

**Diese Schlussfolgerung ist zurückgezogen.** Die Werteliste enthielt keinen der
akzeptierten Zwischenwerte, konnte die beiden mittleren Stufen also nicht sehen.
Siehe "Auflösung" unten.

Bericht: `research/runs/keyboard-zone-brightness-sweep-20260903-115057.md`.

## Zwischenstand

Von den vier denkbaren Steuerwegen sind drei erschöpfend widerlegt: das
Zonen-Helligkeitsbyte, das EC-Feld `KBLL` und ein eigener Befehl im
Tastaturprotokoll.

Offen war damit nur noch das **Lesen** der aktuellen Stufe über die bisher nicht
abgehörten HID-Collections. Genau das hat funktioniert — siehe unten.

## Noch offener Ansatzpunkt 3: die übrigen HID-Collections

Die HID-Inventur fand 11 Collections, darunter einen System-Controller sowie
Consumer-Control-Collections. Der bisherige `Fn+Space`-Monitor hörte nur auf
`MI_01` und `MI_03`.

Ein Helligkeits-Hotkey wird auf vielen Geräten als Consumer-Control- oder
System-Control-Usage gemeldet, nicht auf einem Vendor-Kanal. Diese Collections
sind keine Standard-Tastaturschnittstellen, ein Mithören dort erfasst also keine
Tastenanschläge und bleibt innerhalb der bisherigen Datenschutzgrenze.

Selbst wenn sich die Stufe damit nur **lesen** liesse, wäre das ein Fortschritt:
Die Anwendung könnte den Zustand anzeigen und ihre eigene Helligkeitsdarstellung
daran ausrichten.

### Test: `--hunt-brightness-signal`

Start über `tools\Start-BrightnessSignalHunt.cmd`, ohne Administratorrechte,
reiner Lesevorgang.

Der Test greift zwei nie betretene Stellen gleichzeitig an:

1. **Selektoren über 3.** Der offizielle Getter `0x88` wurde bisher nur mit den
   Selektoren `0` bis `3` befragt, obwohl der Selektor ein ganzes Byte ist.
   Abgefragt werden jetzt `0` bis `15`.
2. **Die kleinen `MI_02`-Collections.** Der alte Monitor hörte nur `MI_01` und
   `MI_03`. Jetzt wird auf allen Nicht-Tastatur-Collections mitgehört,
   insbesondere `COL_03` und `COL_04`, die keine Usages deklarieren.

Ablauf: fünf Runden. In jeder drückt der Besitzer `Fn+Space`, beschreibt die
sichtbare Helligkeit und drückt Enter. Während der Wartezeit läuft auf jeder
geöffneten Collection ein Lesethread. Nach Enter wird der gesamte lesbare
Zustand abgefragt. Am Ende vergleicht der Bericht alle Runden Feld für Feld und
markiert jedes Byte, das sich geändert hat.

Datenschutzgrenze: Collections werden übersprungen, wenn sie Tastatur-Usages
`0x0007` auf Daten- oder Collection-Ebene deklarieren, die Collection-Usage
`0x00010006` tragen, oder ihr Gerätepfad auf `\kbd` endet. Praktisch
ausgeschlossen sind damit `MI_00` und `MI_02/COL_05`, also beide
Tastaturschnittstellen. Ein erster Aufbau prüfte nur die Collection-Usages und
liess beide durch — Tastatur-Collections deklarieren ihre Tasten nämlich erst auf
den Datenelementen. Windows verweigerte das Öffnen zwar von sich aus, aber
darauf darf sich das Gate nicht verlassen; die Prüfung wurde entsprechend
verschärft und verifiziert.

Abgehört werden damit acht Collections: `MI_01`, `MI_03` und
`MI_02/COL_01` bis `COL_04`, `COL_06`, `COL_08`. `MI_02/COL_07` hat keine
Eingangsreports und bleibt der unangetastete Flash-Kanal.

## GEFUNDEN: die Stufe ist auf `MI_02/COL_04` lesbar

Der Suchlauf hat ein Signal gefunden. Die Collection `MI_02/COL_04` — eine der
beiden ohne jede deklarierte Usage, und nie zuvor abgehört — sendet bei jedem
`Fn+Space` einen 4-Byte-Eingangsreport.

| Stufe | Empfangener Report | Byte 2 dezimal | Byte 2 hex |
|---|---|---|---|
| Aus | `04 01 00 00` | `0` | `0x00` |
| Niedrig | `04 01 18 00` | `24` | `0x18` |
| Mittel | `04 01 20 00` | `32` | `0x20` |
| Hell | `04 01 32 00` | `50` | `0x32` |

Struktur: Byte 0 ist die Report-ID `0x04`, Byte 1 ist konstant `0x01` und dürfte
den Ereignistyp bezeichnen, **Byte 2 trägt die Helligkeitsstufe**, Byte 3 ist
null.

Damit ist die bisherige Schlussfolgerung "controllerintern und nicht lesbar"
**widerlegt**. Sie beruhte darauf, dass der frühere Monitor nur `MI_01` und
`MI_03` abhörte. Der Controller meldet die Stufe durchaus an den Host, nur auf
einem Kanal, den niemand beobachtet hat.

### Wertetabelle vollständig

Der kontinuierliche Monitor erfasste 32 Ereignisse und alle vier Stufen. Die
vermutete `40` war **falsch**: Die hellste Stufe meldet `50` = `0x32`.

Die Schaltreihenfolge von `Fn+Space` ist damit belegt:
`0` → `24` → `32` → `50` → `0`.

Byte 1 war in allen 32 Ereignissen konstant `0x01`, Byte 3 immer `0x00`. Über
diesen Kanal meldet sich in diesem Zeitfenster also nur ein Ereignistyp.

**Die entscheidende Beobachtung:** `0, 24, 32, 50` ist Gigabytes Skala `0-50`,
genau dieselbe wie beim Zonen-Helligkeitsbyte, wo `50` die Ein-Schwelle ist. Es
ist also kein Index `0-3`, sondern ein Wert auf der bekannten Skala. In
UI-Prozent wären das 0, 48, 64 und 100.

Das hat eine unangenehme Nebenwirkung für die frühere Auswertung: Der
ACPI-Schreibtest hat nur `0` bis `4` in `KBLL` geschrieben — also nie einen der
echten Stufenwerte. Sein Ergebnis "wirkungslos" ist damit **nicht abschliessend**.
Der Test wurde entsprechend erweitert: `--levels` ist neu und schreibt
standardmässig `0,24,32,50`.

Ebenfalls offen bleibt, ob Byte 1 weitere Ereignistypen kennt, also ob andere
Fn-Tasten über denselben Kanal melden.

Bericht: `research/runs/keyboard-brightness-events-20260903-123822.md`.
- **Setzen ist über diesen Weg nicht zu erwarten.** Die HID-Inventur führt für
  `COL_04` ausschliesslich einen Eingangsreport und keinen Ausgangs- oder
  Feature-Report. Der Kanal ist eine Meldung, kein Steuerkanal.

### Was daraus für die Anwendung folgt

Die App kann die physische Helligkeitsstufe **live anzeigen** und ihre eigene
Darstellung daran ausrichten, auch ohne sie setzen zu können. Für ein Bedienfeld
ist das der Unterschied zwischen einem blinden und einem synchronen Zustand.

Bericht: `research/runs/keyboard-brightness-signal-hunt-20260903-123301.md`.

### Nebenbefund: Selektoren über 3 sind leer

Der Getter `0x88` wurde erstmals mit den Selektoren `4` bis `15` befragt. Jede
Antwort echot korrekt das Kommando und den Selektor, etwa
`00 88 0F 00 00 00 00 00 00`, führt aber ausschliesslich Nullen in den
Datenfeldern. Über die drei Zonen und den globalen Slot hinaus gibt es keinen
weiteren lesbaren Zustand. Auch die Firmware-Abfrage `0x80` blieb über alle
Runden konstant.

### Nebenbefund: zwei Collections sind nicht öffnenbar

`MI_02/COL_01` und `COL_06` liessen sich in jeder Runde nicht öffnen; Windows
hält sie über den Mausklassentreiber exklusiv. Beide deklarieren Maus-Usages und
sind für die Helligkeitsfrage ohne Belang.

### Test: `--monitor-brightness-events`

Start über `tools\Start-BrightnessEventMonitor.cmd`, ohne Administratorrechte,
reiner Lesevorgang.

Der Monitor hört durchgehend auf `MI_02/COL_04`, statt in Runden zu arbeiten, und
zeigt jedes Ereignis sofort mit Zeitstempel, Rohbytes und decodierter Stufe. Der
Bericht listet danach alle unterschiedlichen Werte von Byte 1 und Byte 2 und sagt
ausdrücklich, ob alle vier Stufen erfasst wurden. Laufzeit über `--seconds`,
Standard 45.

Gates: exaktes Gerät, die Collection muss die erwartete Reportlänge von 4 Byte
haben und darf keine Tastatur-Usages deklarieren. Es wird ausschliesslich
gelesen; kein Ausgangs-, Feature-, WMI- oder EC-Zugriff.

## Offene Frage: wirken Hardware-Stufe und Zonen-Byte zusammen?

Der Besitzer weist darauf hin, dass sich die Helligkeit "manchmal setzen lässt
und manchmal nicht". Zuerst die Entwirrung, denn es waren drei verschiedene
Felder, und keines verhielt sich widersprüchlich:

| Feld auf `0` gesetzt | Wirkung | Reproduzierbar |
|---|---|---|
| Zonen-Helligkeitsbyte, `0x08` sel 1-3 Byte 6 | Tastatur aus | ja, im Sweep |
| Effekt-Palette, `0x08` sel 0 Byte 6 = Schwarz | Tastatur aus | ja, 5 von 5 Schritten |
| `EC.KBLL` über ACPI `0xF6` | keine | ja, in beiden Läufen |

Das Ausschalten kam also stets über das Tastatur-HID-Protokoll, nie über `KBLL`.

**Der Hinweis hat dennoch einen berechtigten Kern.** Sämtliche
Zonen-Helligkeitstests liefen bei einer beliebigen, nicht protokollierten
Hardware-Stufe. Ob die beiden Grössen zusammenwirken, wurde nie geprüft. Wäre
die sichtbare Helligkeit eine Verknüpfung aus Fn+Space-Stufe und Zonen-Byte, dann
verhielte sich das Zonen-Byte je nach Stufe unterschiedlich — und genau das wäre
"manchmal ja, manchmal nichts".

Bemerkenswert ist ausserdem, dass die Hardware-Stufen mit `0, 24, 32, 50` genau
auf der Skala liegen, deren obere Grenze `50` beim Zonen-Byte die Ein-Schwelle
bildet.

### Test: `--test-brightness-interaction`

Start über `tools\Start-BrightnessInteractionTest.cmd`, ohne
Administratorrechte.

Der Test baut die vollständige Matrix: für jede der vier Fn+Space-Stufen wird das
Zonen-Byte über `0`, `25` und `50` geschaltet, Farbe konstant weiss, und nach
jedem Wert die Beobachtung erfasst. Zwölf Felder.

Entscheidend für die Beweiskraft: **Die aktive Hardware-Stufe wird live aus
`MI_02/COL_04` mitgelesen, nicht geraten.** Jede Zeile trägt einen gemessenen
Wert; kommt kein Ereignis, wird die Zeile ausdrücklich als "angenommen, nicht
gemessen" markiert. Die Werte über `--zone-values` anpassbar.

Verwendet werden nur der bewiesene Zonensetter `0x08` Selektor 1-3, der Getter
`0x88` und passives Mitlesen. Kein globales Effektkommando, keine
Picture-Matrix, kein WMI, kein EC. Die Ausgangsfarben werden am Ende
wiederhergestellt und verifiziert.

### Ergebnis: das Zonen-Byte ueberstimmt die Hardware-Stufe

Die Matrix wurde vollstaendig durchlaufen, alle vier Stufen live gemessen.

| Hardware-Stufe | Zonen-Byte `0` | Zonen-Byte `25` | Zonen-Byte `50` |
|---|---|---|---|
| `0` aus | aus | aus | **an, volle Helligkeit** |
| `24` niedrig | aus | aus | **an, volle Helligkeit** |
| `32` mittel | aus | aus | **an, volle Helligkeit** |
| `50` hell | aus | aus | **an, volle Helligkeit** |

Das Verhalten ist in allen vier Stufen identisch, die beiden Groessen wirken
also **nicht** multiplikativ zusammen. Entscheidend ist etwas anderes:

**Das Zonen-Byte hat Vorrang.** Ein Schreiben von `50` schaltet die Beleuchtung
auf volle Helligkeit — auch dann, wenn die Hardware-Stufe auf `0`, also
ausgeschaltet, steht. Ein Wert unter `50` schaltet sie vollstaendig ab,
unabhaengig von der Stufe.

Damit ist die Frage des Besitzers beantwortet: Setzen funktioniert immer, es
kennt aber nur zwei Zustaende. Der Eindruck "manchmal geht es, manchmal nicht"
entstand daraus, dass drei verschiedene Felder im Spiel waren, von denen nur zwei
wirken.

Praktische Folge fuer die Anwendung: Nach einem Zonen-Write stimmt der zuletzt
ueber `MI_02/COL_04` gemeldete Stufenwert nicht mehr mit der sichtbaren
Helligkeit ueberein. Eine Live-Anzeige muss einen eigenen Zonen-Write als
Ueberschreiben behandeln und darf den alten Stufenwert nicht weiter anzeigen.

Bericht: `research/runs/keyboard-brightness-interaction-20260903-124922.md`.

### Maengel dieses Laufs

Die Zonen-Werte waren `0`, `25` und `50`. Das war schlecht gewaehlt: `25` stammt
aus der Sweep-Liste von vor der Entdeckung der Stufenwerte, und `32` fehlte
ganz. Die exakten Stufenwerte wurden also nie gegen eine passende Hardware-Stufe
gestellt.

Nahe Werte sind zwar bereits abgedeckt — der Grenztest vom 1. September prueft
`17`, `33` und `49`, der Sweep `25`, und alle blieben aus —, aber genau `24` und
`32` sind ungetestet, und die Paarung gleicher Werte wurde nie geprueft. Der
Standard von `--zone-values` ist deshalb jetzt `0,24,32,50`. Ein
Wiederholungslauf schliesst die Luecke; ein abweichendes Ergebnis ist nicht zu
erwarten, aber die Aussage waere dann belegt statt interpoliert.

## AUFLOESUNG: die Helligkeit ist vollstaendig steuerbar

Der Wiederholungslauf mit den gemessenen Stufenwerten als Zonen-Werten loest die
gesamte Frage auf.

| Hardware-Stufe | Zone `0` | Zone `24` | Zone `32` | Zone `50` |
|---|---|---|---|---|
| `0` gemessen | aus | Stufe 1 | Stufe 2 | Stufe 3 |
| `24` gemessen | aus | Stufe 1 | Stufe 2 | Stufe 3 |
| `32` gemessen | aus | Stufe 1 | Stufe 2 | Stufe 3 |
| `50` gemessen | aus | Stufe 1 | Stufe 2 | Stufe 3 |

**Das Zonen-Helligkeitsbyte steuert alle vier Stufen.** Das Verhalten ist in
jeder Hardware-Stufe identisch, die Fn+Space-Stufe wird also vollstaendig
ueberstimmt.

### Warum das so lange verborgen blieb

Die Firmware akzeptiert offenbar nur die vier exakten Werte. Alles, was daneben
liegt und unter `50` bleibt, wird als aus behandelt; alles ueber `50` als volle
Helligkeit. Jede frueher geprueft Werteliste traf genau daneben:

| Lauf | Geprueft | Traf einen gueltigen Zwischenwert? |
|---|---|---|
| Zyklus 2026-09-01 | `0`, `17`, `33`, `50` | nein — `17` und `33` liegen neben `24` und `32` |
| Grenztest 2026-09-01 | `0`, `1`, `25`, `49`, `50` | nein |
| Sweep 2026-09-03 | `0`, `25`, `50`, `51` … `255` | nein |
| Matrix, erster Lauf | `0`, `25`, `50` | nein |

Drei Laeufe hintereinander haben denselben Fehler wiederholt, weil die
Nachfolgeliste jeweils aus der vorigen abgeleitet wurde statt aus dem
Erkenntnisstand. Der Besitzer hat den Fehler benannt — die Frage, warum nicht die
erkannten Stufenwerte verwendet wurden — und die Wiederholung mit `0,24,32,50`
hat es sofort gezeigt.

Methodische Lehre fuer dieses Projekt: Wenn eine Messung neue diskrete Werte
liefert, muessen die Wertelisten aller verwandten Tests daran ausgerichtet
werden, nicht aus den alten Listen fortgeschrieben.

### Was noch zu praezisieren ist

Dass `32` die Stufe 2 schaltet, `33` aber aus ist, waere eine sehr scharfe
Quantisierung. Das folgt aus zwei getrennten Laeufen und ist noch nicht in einem
Lauf direkt nebeneinander geprueft. Fuer die Umsetzung genuegt es, ausschliesslich
die vier exakten Werte zu schreiben; die genaue Vergleichsregel der Firmware
bleibt offen. Ein Nachbarschafts-Sweep ueber `23, 24, 25, 31, 32, 33, 49, 50`
wuerde sie klaeren.

Bericht: `research/runs/keyboard-brightness-interaction-20260903-125316.md`.

### Folge fuer die Anwendung

Die bisherige Beschraenkung auf aus und volle Helligkeit ist ueberholt. Der
gemeinsame Kern und die Oberflaeche koennen vier Helligkeitsstufen anbieten,
ueber genau dasselbe Kommando, das schon fuer die Zonenfarben verifiziert ist:
`0x08` Selektor 1-3, Byte 6 aus der Menge `{0, 24, 32, 50}`.

Die Live-Anzeige aus `MI_02/COL_04` behaelt ihren Wert, denn sie zeigt die per
Fn+Space gesetzte Stufe. Nach einem eigenen Zonen-Write ist sie jedoch veraltet,
weil der Write die Stufe ueberstimmt; die Anwendung muss ihren eigenen
geschriebenen Wert als gueltigen Zustand fuehren.

## Abschliessendes Ergebnis zum Setzen

**Lesen: ja. Setzen: nein.**

Die Stufe wird als 4-Byte-Eingangsreport auf `MI_02/COL_04` gemeldet, Werte
`0`, `24`, `32`, `50` in Byte 2. Für das Setzen ist jeder erreichbare Weg
widerlegt.

Der Nachtest von `KBLL` mit den echten Stufenwerten schliesst die letzte Lücke:

| Geschrieben | Zurückgelesen | Beobachtung |
|---|---|---|
| `0` | `0` | keine Änderung |
| `24` | `24` | keine Änderung |
| `32` | `32` | keine Änderung |
| `50` | `50` | keine Änderung |

Jeder Wert wurde exakt gespeichert, keiner hatte eine sichtbare Wirkung.
Rollback verifiziert. `EC.KBLL@0xD7` ist damit endgültig als verwaistes
Speicherbyte belegt — beschreibbar, aber mit den LEDs nicht verdrahtet. Die
Einschränkung des ersten Laufs ist aufgehoben, weil nun auch die echten
Stufenwerte geprüft sind.

Bericht: `research/runs/keyboard-backlight-level-20260903-124228.md`.

### Vollständige Übersicht der geprüften Wege

| Weg | Ergebnis |
|---|---|
| Zonen-Helligkeitsbyte `0x08` | **vollständiger Regler**: `0`, `24`, `32`, `50` schalten alle vier Stufen |
| `EC.KBLL@0xD7` über ACPI `0xF6` | beschreibbar, wirkungslos; `0`-`4` und `0/24/32/50` geprüft |
| Eigener Befehl im Tastaturprotokoll | existiert nicht; Module vollständig dekompiliert |
| Getter `0x88`, Selektoren `4`-`15` | alle leer |
| `MI_02/COL_04` | **liest die Stufe**, nur Eingangsreport, kein Steuerkanal |

### Was theoretisch noch übrig ist

Zwei Wege sind nie berührt worden, beide mit geringer Erfolgsaussicht und
ausserhalb der bisherigen Kommandogrenze:

- **`MI_01`**, Vendor-Usage-Page `0xFF00`, 65-Byte Ein- und Ausgangsreports. Für
  diesen Kanal ist kein Protokoll bekannt; Gigabytes Module benutzen ihn nicht.
  Etwas hinzusenden wäre das Senden undokumentierter Daten an einen
  undokumentierten Kanal und bedürfte ausdrücklicher Zustimmung.
- **`SetRGBLed`**, WMI-ID `131` = `0x83`, nie aufgerufen und mit unklarem Zweck.
  Auf diesem Modell vermutlich für eine nicht vorhandene Leuchte gedacht.

Beide sollten nur angefasst werden, wenn das Anzeigen der Stufe nicht genügt.

### Folge für die Anwendung

Die Oberfläche kann die physische Stufe live anzeigen und ihren Zustand
synchron halten. Setzen bleibt dem Nutzer über `Fn+Space` vorbehalten. Das ist
kein Mangel der Anwendung, sondern eine Eigenschaft der Firmware.

## Ausgeschlossen

Report-ID `0x5A` auf `MI_02/COL_07` ist der ITE-Flash-Kanal laut `SHFU.ini`
(`REPORTID=90`). Dort wird nie geschrieben, auch nicht zur Helligkeitssuche.
