# Tastatur reagiert plötzlich nicht mehr

Stand: 2026-09-09. Offener Fehler am Gerät, nicht in dieser App.

## Symptom

Alle paar Wochen hört die interne Tastatur mitten im Betrieb auf zu reagieren. Ein Neustart
hilft nicht zuverlässig; was hilft, ist hartes Ausschalten über den Einschaltknopf. Das
Deinstallieren des Treibers im Gerätemanager, das bisher zum Ritual gehörte, ändert am Gerät
selbst nichts - wirksam ist allein die Stromtrennung, weil erst sie den EC zurücksetzt.

## Was das Ereignisprotokoll zeigt

Harte Ausschaltvorgänge (Kernel-Power 41 ohne Bugcheck): 2026-05-04, 06-16, 06-30, 08-05,
08-25 und 09-09. Fünf davon liegen vor dem ersten Commit dieser App am 2026-09-04.

Am 2026-09-09 zusätzlich:

- 18:58:25 Wechsel auf Akku, 19:02:04 zurück ans Netz.
- 19:28:58 die App beim Start: `Tastatur nicht verfügbar · GetFeature failed`. Nicht die
  Tasten - die **RGB-Schnittstelle** derselben Tastatur antwortet nicht mehr.
- 19:30:56 Herunterfahren beginnt, hängt rund viereinhalb Minuten.
- 19:35:35 harte Abschaltung, kein Bluescreen.

Entscheidend ist, was **nicht** dasteht: kein PnP-Entfernen, kein Fehler von `kbdclass`,
`HidUsb` oder `i8042prt`, das Gerät bleibt auf „OK". Windows merkt von diesem Ausfall gar
nichts. Zusammen mit dem RGB-Fehler heißt das: nicht der Windows-Treiber stürzt ab, sondern
der ITE-Controller im EC verstummt - Tasten und Beleuchtung sterben gleichzeitig, weil beides
derselbe Chip ist.

Am 2026-08-27 stehen zwei Bugchecks im Protokoll (`0xCA PNP_DETECTED_FATAL_ERROR`, `0xD1`).
Ob sie zum selben Fehler gehören, ist offen; die Minidumps sind ohne Adminrechte nicht lesbar.

## Zweiter Vorfall am 2026-09-09, 21:03 - anderes Fehlerbild

Diesmal mit Messung (`research/runs/keyboard-hang-20260909-210417.md`), und sie widerlegt die Suspend-Theorie für diesen Fall:

- Alle 16 Geräteknoten stehen auf **Status Unknown**, `LastRemovalDate = 21:03:26`. Das Gerät war also **vom USB-Bus verschwunden**, nicht eingeschlafen - ein anderes Bild als um 19:28, wo es angemeldet blieb und nur nicht antwortete.
- Selektives Energiesparen war zu diesem Zeitpunkt bereits für Netz **und** Akku abgeschaltet (im Bericht bestätigt, beide `0x00000000`). Als Erklärung fällt es damit aus.
- 21:02:30 Wechsel auf Akku, 21:02:44 zurück ans Netz - **42 Sekunden vor dem Verschwinden**. Das ist der dritte Vorfall, der auf einen Wechsel der Stromquelle folgt.
- Windows protokollierte das Entfernen in keinem Ereignis; einzig die Geräteeigenschaft hält es fest. Ohne diese Messung wäre der Unterschied zum ersten Vorfall unbemerkt geblieben.

### Unsere App zwei Sekunden davor

Das App-Protokoll dieser Minute:

```
21:03:22  start      AORUS Control (app) gestartet.
21:03:24  [ERROR] keyboard  Tastatur nicht verfügbar. | IOException: SetFeature failed.
21:03:26  (LastRemovalDate des Geräts)
```

Beim Start liest die App den Zustand und schreibt die gespeicherte Beleuchtung zurück. Das Lesen gelang, das Schreiben scheiterte, zwei Sekunden später war das Gerät weg. Die Reihenfolge spricht dagegen, dass der Schreibversuch der Auslöser war - der Controller nahm ihn bereits nicht mehr an, war also vorher schon gestört. Ausschließen lässt sich aber nicht, dass er einem angeschlagenen Controller den Rest gegeben hat.

### Stromquellen-Wechsel als Muster

97 Wechsel in 30 Tagen. Auffällig sind die schnellen Paare, allen voran am 18.08.: 16 Wechsel innerhalb von zwei Minuten, überwiegend im Abstand von 7 Sekunden. So steckt niemand ein Netzteil um; das sieht nach einem wackeligen Kontakt aus. Auch am 09.09. gab es zwei enge Paare (20:56:45/49 und 21:02:30/44).

Offen und nur vom Nutzer zu beantworten: ob die Wechsel um 21:02 von Hand kamen - an dem Abend wurde die Netz-/Akku-Umschaltung getestet - oder von selbst.

## Dritter Vorfall am 2026-09-10, 18:11 - und der eigentliche Fund

Diesmal war die Tastatur noch tot, als gemessen wurde. Zwei Dinge fielen sofort weg:

- **Kein Netzteilwechsel.** Die einzigen beiden des Tages lagen um 17:56 und 17:57, vor dem Systemstart (17:57:52) und vierzehn Minuten vor dem Ausfall. Die Vermutung vom Vortag, der Wechsel sei der Auslöser, hält damit nicht.
- **Unsere App lief nicht.** Kein Prozess, keine Logdatei für diesen Tag. Sie kann den Ausfall nicht verursacht haben.

Entscheidend war ein Protokoll, das vorher niemand angesehen hatte:
`Microsoft-Windows-Kernel-PnP/Device Management`.

```
18:11:17  Id1010  Gerät USB\VID_1044&PID_7A41\AP0000000003 wurde überraschend entfernt,
                  da es als fehlend auf dem Bus gemeldet wird.
18:11:20  Id1011  Gerät USB\VID_0000&PID_0002 wurde überraschend entfernt,
                  da es als fehlerhaft gemeldet wurde.
```

„Als fehlend auf dem Bus gemeldet" heißt: Der USB-Hub selbst meldet, am Port hängt nichts mehr. Das ist eine Trennung auf elektrischer Ebene, kein Treiberproblem und kein von Software ausgelöstes Entfernen. Der zweite Eintrag ist der Platzhalter, den Windows für ein nicht mehr identifizierbares Gerät anlegt.

### Die vollständige Vorfallsgeschichte

Dasselbe Protokoll reicht bis August 2025 zurück und enthält jede solche Entfernung:

| Datum | Uhrzeit |
| --- | --- |
| 2026-05-03 | 19:27:26 |
| 2026-05-04 | 18:49:33 |
| 2026-05-09 | 19:15:25 |
| 2026-08-25 | 17:57:10 |
| 2026-09-09 | 19:29:00 |
| 2026-09-09 | 21:03:26 |
| 2026-09-10 | 18:11:17 |

Sieben in sechzehn Monaten - **vier davon in den letzten siebzehn Tagen**. Es kommt in Schüben und die Abstände werden kürzer.

### Was unsere App damit zu tun hat: nichts

Am 09.09. schlug jeweils zwei Sekunden vor der Entfernung ein HID-Zugriff der App fehl (19:28:58 lesend, 21:03:24 schreibend). Das sah nach Mitschuld aus. Am 10.09. lief die App nicht, und es passierte trotzdem - mit demselben Protokolleintrag. Die App war also nur der erste Zeuge: Wer gerade mit dem Gerät spricht, merkt den Abriss sofort, während der Hub ihn erst beim nächsten Port-Status bemerkt. Zwei Sekunden später.

### Der Verlauf, den der Nutzer beschreibt

Vor dem Totalausfall blieben Tasten hängen - die Tastatur meldete eine Taste länger gedrückt, als sie es war. Genau so sieht ein Übertragungsabriss aus: Das Loslass-Ereignis geht verloren, Windows hält die Taste weiter für gedrückt. Erst gehen einzelne Meldungen verloren, dann die Verbindung ganz.

### Bewertung

Das Muster - schleichender Beginn mit verlorenen Meldungen, dann Verschwinden vom Bus, kein Treiberfehler, Rückkehr erst nach vollständiger Stromtrennung, und häufiger werdend - spricht für **Hardware**: die Flachbandleitung zur Tastatur oder ihr Steckverbinder, alternativ der Controller selbst. Eine reine Firmware-Hängung erklärt weder die hängenden Tasten davor noch die Zunahme.

Damit ist es kein Softwarefehler dieser App und mit Software auch nicht zu beheben.

## 2026-09-10 abends: der Fehler wird dauerhaft

Am selben Abend ging es weiter, und der Verlauf beantwortet zwei Fragen auf einmal.

Sechs Startvorgänge zwischen 17:56 und 19:42, dazwischen mehrere harte Abschaltungen. Aus der Ferne sah das aus, als könne der Rechner nicht mehr starten. Die Protokolle sagen etwas anderes:

- **Windows ist jedes Mal sauber hochgekommen.** Keine Startreparatur, kein Wiederherstellungsversuch, kein fehlgeschlagener Boot-Treiber, kein Bugcheck, keine BitLocker-Abfrage. Der einzige regelmäßige Treiberfehler ist `aehd dam` (Android-Emulator, seit jeher, harmlos).
- **Was fehlte, war die Tastatur.** Zweiter Abriss des Tages um **18:21:08** - acht Sekunden nach dem Start um 18:21:00. Das Gerät hatte sich angemeldet und war sofort wieder weg. Ohne Tastatur kommt man am Anmeldebildschirm nicht weiter; von außen sieht das aus wie „startet nicht".
- Die harten Abschaltungen sind damit **Folge, nicht Ursache**. Keine davon hat dem Start geschadet.

### Und seit 19:42 ist sie ganz weg

In der Sitzung ab 19:42 taucht die Tastatur **überhaupt nicht mehr auf**: keine Anmeldung im PnP-Protokoll, letzte Anmeldung war 18:26:33, Status „nicht vorhanden", kein Entfernungszeitpunkt - weil sie nie da war, um entfernt zu werden.

Damit ist der Verlauf innerhalb eines Tages: mal läuft sie, mal fällt sie nach Minuten aus, mal nach acht Sekunden, jetzt gar nicht mehr. Über die drei Kaltstarts hinweg ist das kein Firmwarezustand - ein hängender Controller wird durch eine vollständige Stromtrennung zurückgesetzt und muss danach erst einmal funktionieren. Eine Verbindung, die mechanisch oder thermisch nicht mehr trägt, verhält sich genau so.

### Zur Frage Steckverbinder oder Controller

Von außen lässt sich das nicht trennen: Beides sitzt hinter derselben USB-Verbindung, und beides erzeugt dieselben Protokolleinträge. Was die Daten hergeben, ist die Ebene - **physisch, nicht Software** - und die Richtung: Ein Gerät, das sich anmeldet und acht Sekunden später verschwindet, dann gar nicht mehr erscheint, deutet eher auf die Verbindung oder die Stromversorgung des Controllers als auf dessen Programm.

## Was ausgeschlossen ist

- Keine Herstellersoftware, die um dasselbe Gerät konkurriert: GCC ist deinstalliert, kein
  ITE-Dienst läuft.
- Keine Klassenfilter außer dem Windows-eigenen `kbdclass`.
- Schnellstart ist aus (`HiberbootEnabled = 0`), das Problem liegt also nicht an einem
  Herunterfahren, das den EC gar nicht stromlos macht.

Diese App ist nicht die Ursache - fünf der sechs Vorfälle liegen vor ihrer Entstehung. Ganz
freisprechen lässt sie sich nicht: bei laufendem Effekt fragt sie die RGB-Schnittstelle alle
zwei Sekunden über HID ab, und Polling kann einen Firmwarefehler leichter treffen. Gegenprobe
wäre eine längere Zeit ohne Effekt beziehungsweise mit geschlossener App.

## Vorgenommene Änderung

USB-Selektiv-Suspend war im Netzbetrieb aus, im Akkubetrieb aber an, und alle vier
Tastaturschnittstellen erlauben selektives Energiesparen. Am 2026-09-09 auf Wunsch des
Nutzers auch für den Akkubetrieb abgeschaltet:

```
powercfg /setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0
powercfg /setactive SCHEME_CURRENT
```

Ob das den Ausfall verhindert, zeigt erst die Zeit: der Abstand zwischen zwei Vorfällen liegt
bei Wochen. Rückgängig mit `1` statt `0`.

## Messung im nächsten Ausfall

`tools/Start-KeyboardHangDiagnose.cmd` - rein lesend, ohne Adminrechte, **vor** dem
Ausschalten per Doppelklick ausführen. Danach ist der Zustand weg.

Es ist bewusst nichts einzutippen und nichts zu bestätigen: Ein Skript, das bei defekter
Tastatur auf die Eingabetaste wartet, ist genau dann nutzlos, wenn man es braucht. Stattdessen
misst es selbst zweimal - einmal wie vorgefunden, dann nach zehn Sekunden, in denen auf der
internen Tastatur getippt werden soll. Auch das Fenster schließt sich von allein.

Referenzwerte einer funktionierenden Tastatur, beide am 2026-09-09 direkt nach einem
Tastendruck gemessen (`research/runs/keyboard-hang-20260909-2001*.md`, einmal Netz, einmal
Akku - kein Unterschied):

| Schnittstelle | Leerlauf | nach Tippen |
| --- | --- | --- |
| `HID\…&MI_00` (Tasten) | D2 | **D0** |
| `HID\…&MI_03` (RGB) | D2 | D2 |
| `USB\…&MI_01`, `MI_03` | D3 | D3 |

Daraus die Entscheidungsregel für den Ausfall:

- **Das Gerät ist gar nicht vorhanden** (Status Unknown) → es ist vom Bus verschwunden, siehe
  LastRemovalDate. Energiesparen scheidet aus. So lag der Fall am 2026-09-09 um 21:03.
- **MI_00 bleibt trotz Tastendruck auf D2/D3** → das Gerät ist da, wacht aber nicht auf; dann
  bleibt das selektive Energiesparen Hauptverdächtiger.
- **MI_00 steht auf D0 und trotzdem passiert nichts** → der Controller hängt im wachen
  Zustand; dann liegt es nicht am Energiesparen und die Suche geht bei Firmware und EC weiter.

## Noch nicht geprüft

- Ob es ein BIOS/EC neuer als FB0F (22.03.2026) gibt.
- Ob der Ausfall auch ohne laufende App auftritt.
- Ob die beiden Bugchecks vom 27.08. denselben Ursprung haben.
