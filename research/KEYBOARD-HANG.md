# Tastatur reagiert plötzlich nicht mehr

Stand: 2026-09-17. Offener Fehler am Gerät, nicht in dieser App - aber erstmals ohne hartes
Ausschalten behoben.

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

- **Windows ist jedes Mal sauber hochgekommen.** Kein fehlgeschlagener Boot-Treiber, kein Bugcheck, keine BitLocker-Abfrage. Der einzige regelmäßige Treiberfehler ist `aehd dam` (Android-Emulator, seit jeher, harmlos).
- **Die Startreparatur lief doch** - was zunächst übersehen wurde, weil sie nicht ins Systemprotokoll schreibt, sondern nach `C:\Windows\System32\LogFiles\Srt`. Der Hinweis kam vom Nutzer. Sie lief laut ihren eigenen Zeitstempeln am 10.09. um **18:25:07 bis 18:25:52**, also genau zwischen den Startversuchen 18:22 und 18:26. Die Dateizeiten des Ordners zeigen 14:25, weil die Uhr der Wiederherstellungsumgebung abweicht.

  Ihr Ergebnis ist die zweite, unabhängige Bestätigung:

  ```
  Hora del último arranque correcto: 10/09/2026 21:22:03 (GMT)   = 18:22:03 Ortszeit
  Número de intentos de reparación: 1
  Número de causas principales = 0
  ```

  Alle Prüfungen bestanden - Systemdatenträger, Datenträgerinhalt, interner Zustand, Installationszustand, Bootsektor, Startprotokoll, Fehlerprüfung. Reparieren konnte sie nichts, weil nichts kaputt war. Der einzige Fehlschlag war der optionale Schritt „Netzwerk für die Cloud-Korrektur starten" (0x4c6); `setuperr.log` zeigt dazu nur fehlende WLAN-Zugangsdaten in der Wiederherstellungsumgebung.
- **Was fehlte, war die Tastatur.** Zweiter Abriss des Tages um **18:21:08** - acht Sekunden nach dem Start um 18:21:00. Das Gerät hatte sich angemeldet und war sofort wieder weg. Ohne Tastatur kommt man am Anmeldebildschirm nicht weiter; von außen sieht das aus wie „startet nicht".
- Die harten Abschaltungen sind damit **Folge, nicht Ursache**. Keine davon hat dem Start geschadet.

### Und seit 19:42 ist sie ganz weg

In der Sitzung ab 19:42 taucht die Tastatur **überhaupt nicht mehr auf**: keine Anmeldung im PnP-Protokoll, letzte Anmeldung war 18:26:33, Status „nicht vorhanden", kein Entfernungszeitpunkt - weil sie nie da war, um entfernt zu werden.

Damit ist der Verlauf innerhalb eines Tages: mal läuft sie, mal fällt sie nach Minuten aus, mal nach acht Sekunden, jetzt gar nicht mehr. Über die drei Kaltstarts hinweg ist das kein Firmwarezustand - ein hängender Controller wird durch eine vollständige Stromtrennung zurückgesetzt und muss danach erst einmal funktionieren. Eine Verbindung, die mechanisch oder thermisch nicht mehr trägt, verhält sich genau so.

### Zur Frage Steckverbinder oder Controller

Von außen lässt sich das nicht trennen: Beides sitzt hinter derselben USB-Verbindung, und beides erzeugt dieselben Protokolleinträge. Was die Daten hergeben, ist die Ebene - **physisch, nicht Software** - und die Richtung: Ein Gerät, das sich anmeldet und acht Sekunden später verschwindet, dann gar nicht mehr erscheint, deutet eher auf die Verbindung oder die Stromversorgung des Controllers als auf dessen Programm.

## Die Eingrenzung: Controller lebt, Datenleitung nicht

Zwei Beobachtungen vom 2026-09-10 abends schließen die Diagnose ab.

**Code 43 am selben Anschluss.** Im Gerätemanager steht ein „Unbekanntes USB-Gerät (Fehler beim Anfordern einer Gerätebeschreibung)":

```
USB\VID_0000&PID_0002&7230AE1&0&7     Problem = CM_PROB_FAILED_POST_START (Code 43)
Interne Tastatur, Ort:  Port_#0007.Hub_#0001
```

Beide sitzen auf **Port 7 desselben Hubs**. Der Hub erkennt also, dass am Anschluss etwas hängt - dafür genügt der Abschlusswiderstand auf einer Datenleitung -, aber das Gerät beantwortet nicht einmal die erste Anfrage. Deshalb legt Windows den Platzhalter `VID_0000&PID_0002` an; genau der wurde am 10.09. um 18:11:20 drei Sekunden nach der Tastatur „als fehlerhaft" wieder entfernt.

**Fn+Space regelt die Beleuchtung weiterhin.** Das ist der entscheidende Gegenbeweis zum Controller-Defekt: Fn-Kombinationen verarbeitet der Tastaturcontroller selbst, sie erreichen den Rechner nie. Wenn Helligkeit sich damit noch ändern lässt, dann

- läuft der Controller,
- hat er Strom,
- funktioniert die Tastenmatrix,
- und die LED-Ansteuerung ebenso.

Tot ist allein die **USB-Datenverbindung zum Rechner**.

### Was das bedeutet

| Teil | Zustand |
| --- | --- |
| Tastaturcontroller (ITE) | läuft |
| Stromversorgung der Tastatureinheit | vorhanden |
| Tastenmatrix, Beleuchtung, Fn-Logik | funktionieren |
| USB-Datenleitungen zum Mainboard | defekt |

Damit ist der Fehler auf zwei Leitungen und deren Kontakte eingegrenzt: das Datenpaar im Flachbandkabel, die Kontakte im ZIF-Stecker, oder eine gerissene Leiterbahn im Kabel. Ein Steckverbinder, der neu gesetzt und gereinigt wird, hat hier eine echte Chance - und wenn eine Leiterbahn gebrochen ist, ist es das Kabel beziehungsweise die Tastatureinheit, nicht der Controller.

## 2026-09-14, 19:50:32 - der zehnte Abriss, und der sauberste

Der Steckverbinder wurde am 10.09. gezogen und neu gesetzt (nichts Sichtbares am Kontakt).
Danach lief die Tastatur vier Tage. Am 14.09. fiel sie wieder aus. **Das Neusetzen war also
keine Reparatur** - es hat höchstens einen Kontakt vorübergehend wieder tragen lassen.

Dieser Vorfall ist der aussagekräftigste von allen, weil diesmal nichts passiert ist, was man
verantwortlich machen könnte:

| | 19:50:32 am 2026-09-14 |
| --- | --- |
| Stromwechsel Netz/Akku | keiner - letzter um 17:43 |
| Standby | keiner - die letzte S3-Phase war am 05.09.; am Netz steht die Standbyzeit auf 0 |
| Deckel | geschlossen, aber nur der interne Bildschirm ging aus; die Maschine lief weiter |
| USB-Selektiv-Suspend | **aus**, auf Netz wie Akku (die Änderung vom 09.09.) |
| Benutzereingabe | keine, der Nutzer war nicht am Gerät |
| Diese App | lief seit 19:37:07, hat in dieser Minute **nichts** protokolliert und keinen HID-Zugriff gemacht |

Die Ereignisfolge selbst ist die bekannte:

```
19:50:28  SWD\DAFUPnPProvider\uuid:d080a978-…   überraschend entfernt   (UPnP, Netzwerk)
19:50:32  USB\VID_1044&PID_7A41\AP0000000003     überraschend entfernt   (die Tastatur)
19:50:34  USB\VID_0000&PID_0002\5&7230ae1&0&7    als fehlerhaft entfernt  (Port 7, derselbe Hub)
```

Zwei Sekunden nach dem Abriss meldet sich an Port 7 wieder das Platzhaltergerät
`VID_0000&PID_0002` - der Hub sieht etwas hängen, bekommt aber keine Gerätebeschreibung. Das
ist Zeichen für Zeichen dasselbe Bild wie am 10.09. um 18:11:20. Bestätigt: Die Tastatur sitzt
auf Port 7 von `USB\ROOT_HUB30\4&2ed29309&0&0`, und das Geistergerät steht bis heute als
„Unknown" auf demselben Port.

Der Nutzer kam zurück, weckte den Bildschirm - die Tastatur war schon tot - und schaltete hart
aus (Kernel-Power 41 um 20:15:00, „letztes Herunterfahren erfolgreich: false"). Nach dem
Neustart um 20:14:58 war sie sofort wieder da.

### Was dieser Vorfall ausschließt

- **Energiesparen endgültig.** Das war der letzte verbliebene Softwareverdacht, und er ist
  erledigt: Der Abriss passierte bei abgeschaltetem Selektiv-Suspend und ohne Standby.
- **Den Stromwechsel.** Die Vermutung nach dem 09.09., die Umschaltung Netz/Akku sei der
  Auslöser, trägt nicht mehr - hier gab es keine.
- **Diese App als Auslöser, jetzt ohne Einschränkung.** Am 09.09. lag zweimal ein
  fehlgeschlagener HID-Zugriff zwei Sekunden vor dem Abriss, was nach Mitschuld aussah. Hier
  lief die App, hat das Gerät in dieser Minute aber nicht angefasst und den Ausfall auch nicht
  bemerkt. Zusammen mit dem Vorfall vom 10.09. ohne laufende App bleibt kein Rest.

Was übrig bleibt, ist unverändert die Verbindung: ein Gerät, das im Leerlauf, ohne Anlass und
ohne Zutun von außen von einer Sekunde auf die nächste vom Bus fällt, während derselbe Port
danach noch Widerstand, aber keine Antwort zeigt.

## Vorfallsliste

Alle überraschenden Entfernungen dieser Tastatur aus dem Kernel-PnP-Protokoll:

| Datum | Uhrzeit |
| --- | --- |
| 2026-05-03 | 19:27:26 |
| 2026-05-04 | 18:49:33 |
| 2026-05-09 | 19:15:25 |
| 2026-08-25 | 17:57:10 |
| 2026-09-09 | 19:29:00, 21:03:26 |
| 2026-09-10 | 18:11:17, 18:21:08, 20:43:27 |
| 2026-09-14 | 19:50:32 |
| 2026-09-16 | 19:09:26, 20:05:09 |
| 2026-09-17 | 17:53:53, 18:00:42, 18:37:19 |

Vierzehn in viereinhalb Monaten, zehn davon in den letzten neun Tagen. Der Abstand wird
kürzer: Der Abriss vom 17.09. kam drei Minuten nach dem Hochfahren.

## 2026-09-17: zum ersten Mal ohne hartes Ausschalten zurückgeholt

Vier weitere Abrisse in zwei Tagen (16.09. zweimal, 17.09. zweimal), der letzte **drei Minuten
und elf Sekunden nach dem Hochfahren**. Insgesamt 14.

Dabei kam der erste Erfolg - versehentlich. Die Reihenfolge ist es wert, festgehalten zu
werden, weil sie drei Dinge auf einmal beantwortet.

### Was nicht half

Aus dem Bericht `runs/keyboard-reset-20260917-181738.md`, dem ersten Lauf, der überhaupt
zustande kam:

| Stufe | Ergebnis |
| --- | --- |
| Bus neu einlesen | nichts |
| Platzhalter aus/ein | mechanisch erfolgreich, Tastatur blieb weg |
| **Geräteeinträge entfernen + neu einlesen** | beide entfernt, Tastatur blieb weg |
| Root-Hub abschalten | "Nicht unterstützt" |
| Controller abschalten | "Nicht unterstützt" |

Die dritte Zeile ist die praktisch wichtigste: **Das Deinstallieren des Treibers im
Geräte-Manager bringt für sich genommen nichts.** Es war jahrelang Teil des Rituals, und es
war der wirkungslose Teil.

### Was half

Windows meldete beim Root-Hub "Nicht unterstützt" - **und setzte das Deaktiviert-Kennzeichen
trotzdem.** Das Skript glaubte der Meldung, sparte sich das Wiedereinschalten, und damit waren
Maus und interne Tastatur weg. Der Nutzer hat den Hub von Hand wieder aktiviert.

Danach war die Tastatur **da**: alle 16 Schnittstellen auf OK, mit frischen Instanz-IDs
(`7&26820964` → `7&2388714C`), und der Platzhalter auf Port 7 zur Karteileiche geworden, weil
das echte Gerät den Anschluss zurückhatte. Ohne hartes Ausschalten, zum ersten Mal seit Mai.

Gehalten hat es allerdings nur Minuten: Der Beobachter meldete um 18:37:19 den nächsten Abriss,
26 Sekunden nachdem er gestartet worden war. Der Hub-Zyklus ist also eine Wiederbelebung, keine
Reparatur - was zum übrigen Bild passt.

**Ein Aus-und-wieder-Ein des Root-Hubs holt die Tastatur also zurück.** Das widerlegt meine
eigene Einschätzung von derselben Stunde, ein Neu-Aufzählen könne ein schweigendes Gerät nicht
zum Antworten bringen: Das Abschalten des Hubs ist mehr als ein Neu-Aufzählen, es nimmt dem
Anschluss die Initialisierung und baut sie neu auf.

### Was daraus im Werkzeug wurde

Der Schritt ist der wirksamste und zugleich der gefährlichste - an diesem Hub hängen alle
Eingabegeräte, und **ein deaktiviertes Gerät bleibt deaktiviert, auch über einen Neustart
hinweg.** Zwei Lehren stehen jetzt im Code:

- **Nachsehen statt glauben.** Der zurückgelesene Zustand entscheidet, nicht die
  Rückmeldung - dieselbe Regel, die für jeden Hardwareschreibvorgang der App ohnehin gilt und
  die in diesem Werkzeug gefehlt hat.
- **Ein Wächter, kein `finally`.** `finally` schützt gegen eine Ausnahme, nicht gegen ein
  geschlossenes Fenster oder einen beendeten Prozess. Vor dem Abschalten startet jetzt ein
  eigener Prozess, der nach 30 Sekunden bedingungslos wieder einschaltet - dasselbe Muster wie
  die Lüfterleine der App.

Für den Notfall gibt es zusätzlich `tools\Enable-DisabledUsb.cmd`: Es sucht jedes
abgeschaltete USB-Gerät und schaltet es wieder ein, bedienbar allein mit dem Touchpad.

## Die Laufzeit bricht zusammen

Der Abstand zwischen Systemstart und Abriss, aus Kernel-Boot und Kernel-PnP gegeneinander
gerechnet:

| Abriss | Minuten nach dem Start |
| --- | ---: |
| 2026-09-16 19:09 | 18,5 |
| 2026-09-16 20:05 | 49,9 |
| 2026-09-17 17:53 | **2,2** |
| 2026-09-17 18:00 | **3,3** |
| 2026-09-17 18:37 | **5,9** |

Innerhalb eines Tages von Dutzenden Minuten auf wenige. Dazu die Beobachtungen des Nutzers vom
selben Abend:

- **Der Ruhezustand holt sie nicht zurück.**
- **Vollständiges Ausschalten und wieder Einschalten** brachte sie zurück, aber nur für
  Minuten.

Damit ist auch der letzte verlässliche Behelf weg. Von Mai bis August lagen Wochen zwischen den
Vorfällen, vorige Woche Stunden, jetzt Minuten. Das ist keine sporadische Störung mehr, das ist
eine Verbindung am Ende ihrer Lebensdauer.

Der Hub-Zyklus (siehe oben) holt sie ebenfalls zurück - und hält genauso kurz. Als Weg, das
Gerät im Alltag zu benutzen, taugt bei zwei bis sechs Minuten Laufzeit keiner von beiden.

### Was jetzt noch bleibt

- **Externe USB-Tastatur.** Sofort, billig, und am Schreibtisch mit externem Monitor ohnehin
  die bequemere Lösung. Die naheliegende Antwort.
- **Tastatureinheit tauschen.** Bei diesem Gehäuse hängt sie an der oberen Abdeckung, also
  teuer. Garantie seit Mai 2026 abgelaufen.
- **Noch einmal öffnen.** Das Neusetzen am 10.09. hielt vier Tage. Sinnvoll wäre diesmal nur
  noch, die Kontakte zu reinigen und das Flachbandkabel an der Knickstelle vor dem Stecker auf
  einen Riss anzusehen - ein Riss dort erklärt jede einzelne Beobachtung: dass Druck und
  Stromtrennung kurzzeitig helfen, dass es mit der Zeit schlechter wird, und dass der
  Controller dabei durchgehend lebt.

Software hat hier nichts mehr beizutragen. Die Werkzeuge bleiben im Baum, weil sie die
Diagnose belegen und weil der Hub-Zyklus im Einzelfall noch ein paar Minuten kauft.

## Kann Software den Tastaturcontroller verändert haben?

Der Nutzer erinnert sich, dass es anfing, nachdem er in der Gigabyte-Software Updates gemacht
hatte. Die Frage ist berechtigt, und die Protokolle beantworten die eine Hälfte klar.

**Ein Treiber war es nicht.** Die interne Tastatur läuft ausschließlich auf Microsofts
eigenen Treibern:

```
HID\VID_1044&PID_7A41&MI_00   INF=keyboard.inf  Anbieter=Microsoft
USB\VID_1044&PID_7A41&MI_00   INF=input.inf     Anbieter=Microsoft
```

Im Treiberspeicher liegt kein einziges ITE- oder Gigabyte-Paket für eine Tastatur. GCC hat
hier also nie einen Treiber installiert, und ein Treiber-Update kann die Ursache nicht sein.

**Die Tastatur hat ihre eigene Firmware - und sie ist unverändert.** Das ist der Kern, und
eine frühere Notiz hier war ungenau: Der Tastaturcontroller ist nicht der System-EC aus dem
BIOS, sondern ein eigener Chip mit eigenem, getrennt veröffentlichtem Firmwareabbild.

Das Update-Paket liegt im Projekt (`third-party/downloads/nb-driver-64bit-aorus5-ve-
keyboardfirmware-9.0.4.zip`). Ausgepackt enthält es ITEs Serial-HID-Updater und das Abbild:

```
SHFU.ini      HWID=USB\VID_1044&PID_7A41     <- genau diese Tastatur
              BOOTDEVICECHIPID=8298          <- ITE IT8298
              UPDATEFILESIZEKB=120
docking_b.bin 122.880 Bytes, bei 0x2010:
              "Gigabyte Fusion_8298:1.9.0.4"
```

Und das Gerät meldet sich mit:

```
USB\VID_1044&PID_7A41&REV_1904&MI_03
```

`REV_1904` ist bcdDevice 0x1904, also **1.9.0.4** - Zeichen für Zeichen die Version im
Abbild. **Die Tastatur läuft bereits auf der neuesten veröffentlichten Firmware, und die
stammt vom 13.09.2023.**

Damit ist die Frage beantwortet, ob im Mai 2026 ein Firmware-Update die Tastatur verändert
hat: **Es gab nichts zu aktualisieren.** Seit 2023 ist keine neuere Fassung erschienen, und
die installierte ist genau die aus dem Paket.

Bleibt der System-EC (F00B im BIOS FB0F) - ein anderer Chip, der Lüfter, Akku und Fn-Logik
macht. Zeitlich wäre das noch denkbar:

| | |
| --- | --- |
| BIOS FB0F, Freigabedatum | **2026-03-22** |
| Erster Abriss der Tastatur | **2026-05-03** |

Wann FB0F tatsächlich eingespielt wurde, lässt sich nicht mehr feststellen: Das Systemprotokoll
reicht nur bis 2026-05-17 zurück, das Setup-Protokoll bis 2026-07-19. Rund um den ersten
Abriss zeigt das PnP-Protokoll keinerlei Treiber- oder Geräteaktivität an der Tastatur.

**Dagegen spricht zweierlei**, und das wiegt schwer:

- Es wird **schlechter** - Wochen, dann Stunden, dann Minuten. Firmware altert nicht.
- Das **Neusetzen des Steckers** änderte das Verhalten für vier Tage. Firmware interessiert
  sich nicht für Steckkontakte.

Beides zusammen ergibt trotzdem ein stimmiges Bild: eine grenzwertige Verbindung und eine
Firmware, die weniger Toleranz dafür hat als die vorherige. Die Firmware wäre dann nicht die
Ursache, aber der Punkt, ab dem der Kontakt nicht mehr reichte.

### Was auf der Platte steht

Der Installationsordner der Gigabyte-Software trägt ein Datum, und es liegt **nach** den
ersten beiden Abrissen:

| | |
| --- | --- |
| Erster Abriss | 2026-05-03 19:27 |
| Zweiter Abriss | 2026-05-04 18:49 |
| `Control Center\Lib\MBStorage` | 2026-05-04 **19:18** |
| `GIGABYTE Control Center_2026_Mar_release_All_Setup` | 2026-05-04 **21:06** |

Die März-Fassung von GCC wurde also am 4. Mai eingespielt - einen Tag nach dem ersten Ausfall
und einige Stunden nach dem zweiten. **Diese Installation kann die ersten beiden Abrisse nicht
verursacht haben.** (Ein GCC-Protokoll von 2025-01-15 zeigt, dass die Software schon vorher da
war; frühere Update-Läufe lassen sich nicht mehr datieren.)

Das entkräftet den Zusammenhang nicht vollständig - ein BIOS-Update kann Wochen vorher
gelaufen sein -, aber es nimmt ihm den unmittelbaren Beleg.

### Warum ein Downgrade nicht der erste Schritt ist

Naheliegende Frage, und die Antwort ist trotzdem nein:

- **Gleiche Fassung neu flashen kostet nichts** und prüft dieselbe Hypothese. Wenn die
  EC-Firmware in einem schlechten Zustand ist, schreibt das sie neu. Kein Rückrollrisiko,
  keine Versionssperre.
- **Ein Downgrade hat echtes Risiko.** Auf dieser Plattform laufen Boot Guard und BIOS Guard;
  manche Fassungen verweigern eine ältere Version oder brechen mittendrin ab. Ein halb
  geflashtes Laptop-BIOS ist ein größerer Schaden als eine defekte Tastatur.
- **Die ältere Fassung ist oft gar nicht mehr zu bekommen**; Gigabyte führt auf der
  Produktseite in der Regel nur die neueste.
- **Und die Beweislage trägt es nicht:** sechs Wochen zwischen Freigabe von FB0F und erstem
  Ausfall, die GCC-Installation erst danach, und vor allem ein Fehler, der sich
  verschlimmert. Eine Firmwareversion wird nicht mit der Zeit schlechter.

### FB0E gegen FB0F verglichen

Beide Pakete liegen im Projekt und wurden ausgepackt und gegeneinander gehalten.

**Die Manifeste der Hersteller-Flasher sagen es selbst:**

```
FB0E  info.xml    <BIOS1>FB0E</BIOS1>  <EC>F00B</EC>  <BIN1>RX5ME4FB0E.rom</BIN1>
FB0F  info1.xml   <BIOS>FB0F</BIOS>    <EC>F00B</EC>  <BIN>RX5ME4FB0F.rom</BIN>
```

**Dieselbe EC-Fassung F00B in beiden.** Die BIOS-Nummer wechselt, die EC-Nummer nicht.

Die ROMs selbst:

| | FB0E | FB0F |
| --- | --- | --- |
| Datei | `RX5ME4FB0E.rom` | `RX5ME4FB0F.rom` |
| Größe | 38.797.312 Bytes | 38.797.312 Bytes |
| Datum der Paketdatei | 2023-02-06 | 2026-04-28 |
| SHA256 (Anfang) | `4810857caa667f96…` | `49c5fb7ee8e4a40a…` |

Blockweiser Vergleich über 64 KB: **80 von 592 Blöcken unterscheiden sich**, rund 5 MB, verteilt
auf zwanzig zusammenhängende Bereiche zwischen `0x01090000` und `0x024F0000`. FB0F ist also ein
umfangreiches BIOS-Update - aber eben eines, das die EC-Kennung nicht anfasst.

Eine Disassemblierung der EC-Firmware war nicht möglich: Das Abbild liegt nicht als
zusammenhängender Klartextbereich vor - weder `F00B` noch `IT8xxx` noch eine Versionszeichenkette
ist im ROM zu finden -, sondern komprimiert in einem der Firmwarevolumes.

### Was das für ein Downgrade bedeutet

Damit ist die Frage entschieden, und zwar nicht über eine Abwägung, sondern über die Daten:

1. **Der EC bleibt gleich** - F00B hier wie dort.
2. **Die Tastatur-Firmware ist ohnehin ein dritter Chip** (IT8298, 1.9.0.4 von 2023) und in
   keinem der beiden BIOS-Pakete enthalten.

Ein Downgrade auf FB0E ändert also UEFI-Code, aber weder die EC-Fassung noch irgendetwas am
Tastaturcontroller. Für dieses Problem kann es nichts ausrichten - bei vollem Flash-Risiko.

### Was ein Firmware-Bug im FB0F angeht

Danach war ausdrücklich gefragt. Ehrliche Antwort: **Aus dem Abbild ist das nicht zu
belegen.** Die EC-Firmware ist ein 8051-Binary in der BIOS-Kapsel; einen Defekt in der
USB-Behandlung dort statisch zu finden, bräuchte Disassemblierung, eine Referenz zum
ITE-Baustein und vor allem einen reproduzierbaren Auslöser. Nichts davon ist hier vorhanden,
und eine Vermutung als Befund auszugeben wäre schlimmer als die offene Frage.

Was die Untersuchung stattdessen ergeben hat, ist mehr wert: Die Tastatur hängt gar nicht an
der EC-Firmware aus dem BIOS, sondern an ihrem eigenen, unveränderten Abbild.

### Der Test, der es entscheidet

**Die Tastatur-Firmware neu schreiben**, nicht das BIOS. Der Updater erlaubt das
ausdrücklich auch für dieselbe Version (`DONOTUPDATETHESAMEVERSION=0`), und er trifft genau
den Chip, der ausfällt - ohne BIOS, ohne Rückrollrisiko, ohne Boot Guard.

Ein Vorbehalt, der zählt: Geflasht wird über genau die USB-Verbindung, die zurzeit nach
Minuten abreißt. Reisst sie mitten im Schreiben ab, bleibt der Controller halb programmiert.
Der Updater kennt dafür einen Bootloader-Modus (`USB\VID_048D&PID_89DB`), ein zweiter Versuch
ist also möglich - garantiert ist nichts. Nur direkt nach einem Kaltstart versuchen, solange
die Tastatur nachweislich angemeldet ist.

- Hält die Tastatur danach wieder Tage: Es war die Firmware.
- Stirbt sie weiter nach Minuten: Es ist die Verbindung, endgültig und ohne Restzweifel.

Am Netz durchführen, nicht unterbrechen. Mehr Aufwand als ein Neustart, aber es beendet die
Frage in einem Durchgang - und billiger als eine neue Tastatureinheit ist es allemal.

## Der Bus im Moment des Abrisses (2026-09-17, 19:32:55)

63 Sekunden USB-Mitschnitt, 42.422 Ereignisse, und der Abriss liegt mittendrin
(`runs/usb-trace-20260917-193154.etl`). Zum ersten Mal ist zu sehen, was in den Sekunden
passiert, die Windows im Ereignisprotokoll gar nicht aufführt.

### Der Verlauf

| Zeit | Was geschieht |
| --- | --- |
| bis 19:32:53.70 | **630 Ereignisse pro Sekunde, völlig gleichmäßig** - normaler Betrieb |
| 19:32:53.705 | **erster fehlgeschlagener Transfer** (`URB_Hdr_Status 0xC0000011`, `NTSTATUS 0xC0000001`) |
| 19:32:53.728-729 | vier weitere Fehlschläge, dann vier abgebrochene Transfers (`STATUS_CANCELLED`) |
| 19:32:54 | Verkehr bricht von 630 auf 100 Ereignisse ein |
| 19:32:55.595 | Windows protokolliert die überraschende Entfernung |
| 19:32:55.65-57.66 | **vier Anläufe, das Gerät neu aufzuzählen** - jedes Mal Geräteobjekt anlegen, Steuerendpunkt anlegen, keine Antwort, alles wieder abbauen |
| 19:32:56.68 | die alte Tastatur wird abgebaut: sechs Endpunkte (0x81, 0x83, 0x04, 0x82, 0x85, 0x06) einzeln entfernt |

### Was dadurch feststeht

- **Kein allmählicher Verfall.** Bis zur letzten Millisekunde läuft der Verkehr mit voller
  Rate. Keine CRC-Fehler, keine Wiederholungen, keine Transfers, die erst im zweiten Anlauf
  gelingen - und dann von einem Transfer auf den nächsten scheitert alles.
- **Kein Abziehen und kein Stromproblem.** Der Anschluss meldet **kein** Disconnect, keine
  Überstromabschaltung. Der Hub sieht das Gerät weiterhin als angeschlossen und versucht
  viermal, es anzusprechen.
- **Das Gerät antwortet nicht mehr auf dem Steuerendpunkt.** Jeder Neuanlauf scheitert an der
  ersten Anfrage über Endpunkt 0 - genau das Bild, das der Geräte-Manager als Code 43
  („Fehler bei einer Anforderung des USB-Gerätedeskriptors") zeigt.

### Was es nicht entscheidet - und was es einengt

Beide Erklärungen bleiben möglich, aber die Beschreibung wird schärfer:

- **Gegen einen langsam schlechter werdenden Kontakt.** Ein Kontakt, der zunehmend schlechter
  leitet, meldet sich vorher an: Fehler, die sich häufen, Transfers, die nach Wiederholung
  doch gelingen. Davon ist nichts zu sehen. Der Übergang ist binär und sofortig.
- **Für eine unterbrochene Datenleitung.** Dass der Hub das Gerät weiter als angeschlossen
  führt, heißt: die Leitung mit dem Anschlusswiderstand (D+) hat noch Durchgang. Bricht nur
  **D-**, bleibt das Gerät genau so sichtbar und kann trotzdem kein einziges Bit übertragen.
  Ein aussetzender Kontakt auf einer der beiden Datenleitungen passt exakt.
- **Für einen hängenden Controller.** Ebenso: Der Chip behält seinen Anschlusswiderstand, hält
  die Beleuchtung auf dem letzten Bild stehen (der eingefrorene Regenbogen) und verarbeitet
  Fn-Tasten weiter - nur seine USB-Einheit antwortet nicht mehr.

Die Aufnahme trennt diese beiden nicht. Sie schließt aber aus, was man von außen bisher nicht
ausschließen konnte: ein schleichend schlechter werdendes Signal, ein Wackelkontakt mit
Fehlerhäufung, ein Stromproblem am Anschluss und einen Fehler im Windows-Treiberstapel. Der
Stapel verhält sich vom ersten bis zum letzten Ereignis korrekt.

## Im Ständer hält sie (2026-09-17, abends)

Der Laptop steht jetzt zugeklappt und hochkant in einem Ständer am externen Monitor, mit
externer Tastatur. Die interne wird nicht benutzt - und sie läuft.

| | flach, aufgeklappt, in Benutzung | zugeklappt, hochkant im Ständer |
| --- | ---: | ---: |
| 17.09. 17:53 | 2,2 Minuten | |
| 17.09. 18:00 | 3,3 Minuten | |
| 17.09. 18:37 | 5,9 Minuten | |
| 17.09. ab 21:18 | | **24 Minuten und zählt, kein einziger Abriss** |

Das ist die erste Beobachtung, die zwischen den beiden verbliebenen Erklärungen wirklich
unterscheidet. Zwischen den Zeilen dieser Tabelle liegen vier Änderungen, und alle vier sind
mechanisch:

- **Auf der Tastatur wird nicht mehr getippt.** Die Tastenplatte sitzt unmittelbar über dem
  Flachbandkabel und seinem ZIF-Stecker; jeder Anschlag drückt darauf.
- **Die Lage ist senkrecht statt waagerecht** - die Schwerkraft zieht anders am Gehäuse.
- **Der Deckel ist zu**, das Gehäuse also anders verspannt.
- **Er liegt nicht mehr auf dem Boden**, wo sich die Bodenplatte beim Tippen durchbiegt.

**Eine Firmware interessiert sich für nichts davon.** Ein grenzwertiger Steckkontakt für alles
davon.

Damit fügt sich der Rest zusammen:

- Das Neusetzen des Steckers am 10.09. half vier Tage - ein mechanischer Eingriff mit
  mechanischer Wirkung.
- Der USB-Mitschnitt zeigt einen von einem Transfer auf den nächsten vollständigen Ausfall
  ohne vorherige Fehlerhäufung - so fällt ein Kontakt aus, nicht ein Signal, das schlechter
  wird.
- Fn+Space und die Beleuchtung liefen weiter - die Geräteseite bleibt versorgt, nur der
  Datenweg stirbt.

**Vorsicht bei der Freude:** 24 Minuten sind besser als jede Sitzung dieses Tages, aber am
16.09. hielt sie auch aufgeklappt einmal 50 Minuten. Ein Beweis ist es erst, wenn es Stunden
hält.

### Der Test, der die Stelle findet

`tools\Start-KeyboardWatch.cmd` laufen lassen - es piept bei jeder Änderung und schreibt seit
dem 17.09. mit. Dann der Reihe nach und jeweils ein paar Sekunden halten:

1. auf die Handballenauflage drücken,
2. auf die Tastenmitte etwa unterhalb der F-Tasten drücken (dort liegt üblicherweise der
   Stecker),
3. das Gehäuse vorsichtig verwinden,
4. den Deckel langsam öffnen und schließen.

Piept es reproduzierbar nach einer bestimmten Stelle, ist die Stelle gefunden - und damit auch
die Antwort, ob sich ein erneutes Öffnen lohnt.

## Warum sie nie von selbst zurückkommt - und was das verrät

Der Einwand des Nutzers: Bei einem Wackelkontakt müsste sie doch auch mal von allein
wiederkommen. Sie ist aber in vierzehn Vorfällen **kein einziges Mal** im laufenden Betrieb
zurückgekehrt.

Der Einwand stimmt, und die Prüfung bestätigt ihn: Jede Wiederkehr im Protokoll war ausgelöst -
durch einen Systemstart oder durch ein erzwungenes Neu-Aufzählen. Die Ankunft am 17.09. um
18:36:12, die zunächst spontan aussah, trägt die Instanzkennungen aus dem Hub-Zyklus
(`6&6986dbd&3&...`), stammt also vom Wiedereinschalten des Root-Hubs.

### Die Auflösung steckt im USB-Mitschnitt

Als sie ausfiel, meldete der Anschluss **kein Disconnect**. Das Gerät blieb für den Hub
angeschlossen, es antwortete nur nicht mehr. Windows versuchte viermal, es aufzuzählen, legte
danach den Platzhalter mit Code 43 an - und hörte auf.

Ab da gibt es **keinen Anlass mehr, es noch einmal zu versuchen**. Eine Neu-Aufzählung wird
durch ein Anstecken ausgelöst. Da das Gerät elektrisch nie abgesteckt war, kommt nie ein
Anstecken - auch dann nicht, wenn der Kontakt eine Millisekunde später wieder trägt. Es fragt
schlicht niemand mehr.

Das erklärt alle drei Beobachtungen auf einmal:

- **Von selbst kommt sie nicht zurück** - kein Auslöser, nicht etwa ein dauerhaft offener
  Kontakt.
- **Ein Neustart hilft** - die Aufzählung beginnt von vorn.
- **Der Hub-Zyklus hilft** - er erzwingt genau diese Aufzählung.

Dass ein Ausschalten überhaupt hilft, beweist zugleich: Der Kontakt trägt danach wieder. Der
Fehler ist also vorübergehend - nur fragt Windows nicht nach.

### Und damit lässt sich die Leitung benennen

Bei voller Geschwindigkeit hängt der Anschlusswiderstand, an dem der Hub ein Gerät überhaupt
erkennt, an **D+**. Daraus folgt:

| Bricht | Hub sieht | Verhalten |
| --- | --- | --- |
| **D+** | Gerät weg | Disconnect, und beim nächsten Kontakt automatisch neu aufgezählt - **sie käme von selbst zurück** |
| **D-** | Gerät weiterhin da | keine Übertragung mehr möglich, kein Disconnect, **kein erneuter Versuch** |

Beobachtet wird die zweite Zeile, in jedem einzelnen Vorfall. Der Einwand des Nutzers ist damit
kein Gegenargument, sondern das genaueste Indiz, das diese Untersuchung hervorgebracht hat:
Der Fehler sitzt auf **D-** beziehungsweise in der Signalintegrität des Datenpaars, nicht auf
der Leitung mit dem Anschlusswiderstand.

## Der Aufbau im Gerät - und wo der Fehler danach sitzen muss

Ein Foto des geöffneten Geräts (2026-09-17) zeigt den Aufbau, und er passt genau zur Diagnose:

```
Tastatur ──[breites Flachband, viele Adern]──► eigene kleine Platine mit Bauteilen
                                                         │
                                                         ├──[dünnes Kabel]──► Mainboard
                                                         └──[dünnes Kabel]──► Mainboard
```

Die Tastatur hängt also **nicht** direkt am Mainboard. Dazwischen sitzt eine eigene Platine -
dort lebt der IT8298. Das breite Band trägt die Tastenmatrix und die LED-Leitungen zu diesem
Controller; zum Mainboard gehen nur zwei dünne Kabel.

### Was daraus folgt

Der USB-Anschluss des Geräts - also **D+ und D-** - kann nur in einem dieser **zwei dünnen
Kabel** liegen. Damit ist der Fehlerort ein anderer, als bisher angenommen:

| Teil | Kann das beobachtete Bild erzeugen? |
| --- | --- |
| Breites Tastaturband (Matrix) | **Nein.** Fällt es aus, gehen Tasten nicht mehr - aber das Gerät bliebe am USB angemeldet und die Beleuchtung liefe weiter. Kein Verschwinden vom Bus. |
| Platine mit dem Controller | Ja, wenn der Chip selbst hängt - aber Fn+Space und die stehende Beleuchtung zeigen, dass er läuft. |
| **Dünnes Kabel mit D+/D- und seine beiden Stecker** | **Ja, genau dieses Bild.** |

Und es erklärt das Fortbestehen von Beleuchtung und Fn-Logik zwanglos: Beides passiert
**diesseits** der gestörten Strecke, auf der Controllerplatine, die ihren Strom weiterhin
bekommt.

### Wichtig für einen nächsten Eingriff

Am 2026-09-10 wurde ein Stecker neu gesetzt, und es hielt vier Tage. Falls das das **breite**
Tastaturband war, war es der falsche - dieses Band liegt hinter dem Controller und kann das
Symptom gar nicht verursachen.

Zu prüfen sind stattdessen:

1. die **zwei dünnen Kabel** zwischen Controllerplatine und Mainboard,
2. ihre Stecker an **beiden** Enden,
3. die Knickstellen dieser Kabel.

Welches der beiden das USB-Kabel ist, lässt sich an den Adern abzählen: Der USB-Strang braucht
**vier** (VBUS, GND, D+, D-). Das andere Kabel ist dann die Versorgung der Beleuchtung, meist
mit weniger, dafür breiteren Leiterbahnen.

Ein vierpoliges FFC ist zudem ein Normteil und für wenige Euro zu ersetzen - ungleich billiger
als die Tastatureinheit, an der bisher die Überlegungen hängen.

## Was ausgeschlossen ist

- Keine Herstellersoftware, die um dasselbe Gerät konkurriert: GCC ist deinstalliert, kein
  ITE-Dienst läuft.
- Keine Klassenfilter außer dem Windows-eigenen `kbdclass`.
- Schnellstart ist aus (`HiberbootEnabled = 0`), das Problem liegt also nicht an einem
  Herunterfahren, das den EC gar nicht stromlos macht.

Diese App ist nicht die Ursache - fünf der sechs harten Abschaltungen liegen vor ihrer
Entstehung. Der Rest des Verdachts ist seit dem 14.09. erledigt: Damals lief sie, hat das
Gerät in der fraglichen Minute nicht angefasst, und der Abriss kam trotzdem. Der Vorfall vom
10.09. ohne laufende App sagt dasselbe von der anderen Seite.

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

## Lässt sich der Strom per Software wegnehmen?

Die Frage ist die richtige: Was bei diesem Fehler hilft, ist nicht der Neustart, sondern die
Stromtrennung. Die Antwort ist trotzdem nein - jedenfalls nicht wirklich.

- **VBUS abschalten geht nicht.** Dafür gäbe es `IOCTL_USB_HUB_CYCLE_PORT`, das den Strom an
  einem Anschluss aus- und wieder einschaltet. Es kennt aber nur der USB-2-Hubtreiber
  (`usbhub.sys`); die Tastatur hängt an einem USB-3-Root-Hub unter `usbhub3.sys`. Dazu kommt:
  Intern verlötete Anschlüsse haben meist gar keine schaltbare Stromversorgung, weil niemand
  einen Schalter für etwas einbaut, das nie abgezogen wird.
- **Was geht, ist ein USB-Reset**: Gerät, Root-Hub oder Host-Controller abschalten und wieder
  einschalten. Das setzt die Anmeldung zurück, nicht die Stromversorgung des Controllers in
  der Tastatur.
- **Am nächsten am Stromstoss** ist der Neustart des xHCI-Controllers: Er geht kurz in den
  PCI-Schlafzustand. Ob dabei an den Anschlüssen die Spannung wegfällt, entscheidet die
  Hardware; bei einem intern versorgten Anschluss eher nicht.

Und noch ein Punkt, der erklärt, warum hier ausgerechnet das harte Ausschalten hilft: Die
Tastatur ist zum Aufwecken berechtigt (`powercfg /devicequery wake_armed` führt
„HID-Tastatur (002)" bis „(004)"). Ein Gerät, das wecken darf, behält seine Spannung auch
im Standby und im ausgeschalteten Zustand - sonst könnte es nicht wecken. Erst das Halten
des Einschaltknopfes nimmt sie ihm weg.

Daraus ergibt sich die Reihenfolge für den nächsten Ausfall, von harmlos nach einschneidend:

```
tools\Reset-Keyboard.cmd
```

Das Skript fordert selbst Administratorrechte an, geht die vier Stufen durch - Bus neu
einlesen, Tastatur zurücksetzen, Root-Hub neu starten, Controller neu starten - und prüft
nach jeder, ob die Tastenschnittstelle `MI_00` wieder auf OK steht. Beim ersten Erfolg hört
es auf. Die Kette Tastatur → Hub → Controller wird über `DEVPKEY_Device_Parent` gelaufen
statt fest eingetragen, und das Wiedereinschalten steht in `finally`: Ein abgeschaltet
zurückgebliebener Controller nähme auch die Maus mit.

Hilft keine Stufe, bleiben nur die beiden Wege, die wirklich Strom wegnehmen - und beide sind
freundlicher als der Einschaltknopf:

```
shutdown /h        Ruhezustand, kommt ohne Kaltstart zurück
shutdown /s /t 0   Herunterfahren - ausdrücklich NICHT Neustart
```

Der Unterschied ist wesentlich: Ein **Neustart** lässt die Anschlüsse unter Spannung, ein
**Herunterfahren** nicht (Schnellstart ist auf diesem Gerät aus). Wer bisher neu gestartet hat
und dachte, es helfe nichts, hat vielleicht nur das Falsche probiert.

Ob eine der Stufen tatsächlich reicht, ist **ungeprüft** - beim letzten Ausfall gab es das
Skript noch nicht. Welche Stufe wirkt, gehört beim nächsten Mal hierher.

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

- Ob es ein BIOS/EC neuer als FB0F (22.03.2026) gibt - und ob ein Neuflashen der
  vorhandenen Fassung das Verhalten ändert. Siehe oben; das ist der nächste sinnvolle
  Schritt.
- Ob die beiden Bugchecks vom 27.08. denselben Ursprung haben.
- Ob eine externe USB-Tastatur als dauerhafter Ersatz taugt, oder ob die Tastatureinheit
  getauscht werden soll. Die Garantie ist seit Mai 2026 abgelaufen.
