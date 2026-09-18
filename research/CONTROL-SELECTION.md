# Welches Bedienelement wofür - und was das für diese App heißt

Stand 2026-09-18. Recherche in den beiden maßgeblichen Quellen, danach ein Abgleich mit jeder
Karte dieser App.

## Die Theorie, auf drei Regeln eingedampft

Aus [Microsofts Richtlinie für App-Einstellungen](https://learn.microsoft.com/en-us/windows/apps/design/app-settings/guidelines-for-app-settings)
und den [Toggle-Switch-Guidelines von Nielsen Norman](https://www.nngroup.com/articles/toggle-switch-guidelines/):

| Was eingestellt wird | Element |
| --- | --- |
| Zwei sich ausschließende Zustände, sofort wirksam | **Schalter** (Toggle) |
| Eine Wahl aus bis zu fünf sich ausschließenden Möglichkeiten | **Optionsfelder** (Radio) |
| Eine Wahl aus einer kompakten Liste | **Auswahlfeld** (ComboBox) |
| Mehrere unabhängige Ja/Nein | **Kontrollkästchen** |
| Ein Wert aus einem Bereich | **Regler** |
| Eine Handlung, kein Zustand | **Knopf** |

Dazu drei Regeln, die genauso viel wert sind wie die Tabelle:

1. **Sofort wirksam, kein Bestätigungsknopf.** „When a user changes a setting, the app should
   immediately reflect the change." Ein Schalter, dessen Wirkung erst ein zweiter Klick
   auslöst, ist laut NN/g der klassische Fehlgriff - die Leute wissen dann nicht mehr, ob ihre
   Wahl schon gilt.
2. **Die Beschriftung benennt den Ein-Zustand**, nicht die Frage. „Ladelimit", nicht „Soll das
   Ladelimit gelten?". Probe: die Beschriftung laut lesen und „ein"/„aus" anhängen.
3. **Ein deaktiviertes Element muss sagen, warum.** Nicht nur ausgrauen.

## Abgleich mit dieser App

| Karte | Element | Urteil |
| --- | --- | --- |
| Lüfterprofil (5 Stück) | `RadioButton` mit `GroupName`, als Chips gestaltet | **richtig** - und die Semantik stimmt auch für Tastatur und Screenreader, nicht nur die Optik |
| Windows-Leistungsmodus (3) | ebenso | richtig |
| Tastatur-Effekte | ebenso, als Kacheln | richtig |
| GPU-Automatik | `ToggleSwitch`, sofort wirksam | richtig |
| GPU-Regel je Programm | zwei `ComboBox` zu je drei Werten | richtig - drei Werte sind für Optionsfelder je Zeile zu viel Fläche |
| Lüfterkurve | eigener Editor | eigenes Element, keine der Standardkategorien |
| Helligkeit, Tempo, Ladelimit | `Slider` | richtig - Werte aus einem Bereich |
| **Ladelimit ein/aus** | **war ein Knopf „Standardladen"** | **falsch, korrigiert** |

### Der eine echte Fehlgriff

„Standardladen" war ein Knopf für einen von zwei sich ausschließenden Zuständen. Die Folgen
waren alle drei Regeln auf einmal:

- Der Knopf führte nur **in eine Richtung**. Zurück zum Limit kam man, indem man am Regler
  wackelte, bis die App die Änderung bemerkte.
- **Der Zustand war nicht am Element ablesbar**, sondern nur an einer Zeile Fließtext darüber.
- Der Regler blieb unter Standardladen **bedienbar, ohne etwas zu bewirken**.

Jetzt trägt ein Schalter „Ladelimit" den Zustand, und der Regler ist seine Unteroption - unter
Standardladen ausgegraut. „Erneut lesen" bleibt ein Knopf: das ist eine Handlung, kein
Zustand.

## Was bewusst nicht geändert wurde

- **Breite 880 px** statt der empfohlenen 1000-1100. Die Empfehlung gilt Einstellungsseiten
  voller Textzeilen; hier stehen Karten mit Werten darin, und 880 wurde bereits einmal bewusst
  gewählt.
- **„Jetzt anwenden" und „Erneut lesen"** sind keine Bestätigungsknöpfe im Sinne der Regel.
  Sie ändern keine Einstellung, sie führen eine aus beziehungsweise lesen das Gerät neu.
- **„Überwachung starten/stoppen"** auf dem Dashboard bleibt, obwohl es technisch klingt: An
  ihm hängt die Aufsicht über festgesetzte Lüfter, und die darf nicht unsichtbar sein.

## Hintergrundlast, gemessen statt vermutet (2026-09-18)

Der Wunsch war „wenig Arbeitsspeicher und Last im Hintergrund". Gemessen am laufenden Prozess,
im Infobereich, Fenster zu:

| | Wert |
| --- | ---: |
| CPU über 20 Sekunden | **0,00 %** eines Kerns |
| Arbeitsspeicher (Working Set) | **7,2 MB** |
| Threads | 21 |

Daran ist nichts zu optimieren. Die Abfragen laufen nur, solange das Dashboard sichtbar ist;
die Tastatur-Animation ist der einzige dauerhafte Verbraucher und läuft nur, wenn ein Effekt
gewählt ist. GC-Schalter oder ein erzwungenes Trimmen des Working Sets wären hier Änderungen
ohne gemessenen Anlass - also genau das, was dieses Projekt sonst ablehnt.

## Autostart

Die eingetragene Aufgabe wurde am Gerät ausgelesen. Richtig waren: kein Akku-Verbot, kein
Zeitlimit, Priorität 5, `IgnoreNew`, `StartWhenAvailable`. Falsch war **`RestartCount = 0`** -
ein Absturz hinterließ ein Gerät ohne Lüfteraufsicht und ohne Beleuchtungssteuerung, bis es
jemandem auffiel. Jetzt drei Neustarts im Abstand einer Minute.

Die Reihenfolge der Elemente in der Aufgabendatei ist schemagebunden; eine falsch sortierte
Datei wird abgewiesen, und das äußert sich als Autostart, der stillschweigend nie geschrieben
wird. Der Test übergibt die Definition deshalb dem echten Taskplaner (`Schedule.Service`,
`NewTask` + `XmlText`), der sie parst, ohne etwas zu registrieren, und liest die Einstellungen
aus dessen eigenem Objekt zurück. Ohne Adminrechte, ohne Nebenwirkung.

## Beenden

Beim Schließen schrieb der Effekt-Renderer den Zustand von **vor** dem Effekt zurück. Innerhalb
einer Sitzung ist das richtig und unsichtbar - ein Moduswechsel stoppt den Effekt, holt sich
eine bekannte Ausgangslage und schreibt seinen neuen Zustand im selben Zug darüber. Beim
Beenden folgt nichts mehr: die Rückstellung war das Letzte, was die Tastatur erreichte, und
damit nahm die App die Beleuchtung beim Gehen mit.

Der Renderer kann jetzt angewiesen werden zu übergeben; beide Stopps, die eine Sitzung beenden,
tun das. Farben und Modus bleiben stehen.

Ebenso hängt das Schließen nicht mehr an der Tastatur. Es hielt das Fenster offen, wenn die
Freigabe fehlschlug - der übliche Grund dafür ist auf diesem Gerät aber, dass die Tastatur vom
USB-Bus gefallen ist (siehe `KEYBOARD-HANG.md`). Ein Laptop, dessen Tastatur gestorben ist,
darf nicht zusätzlich das Schließen seines Kontrollprogramms verweigern. Die Lüfter blockieren
weiterhin: ein festgesetzter Lüfter ist eine Sicherheitsfrage, Beleuchtung nicht.
