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
