# Was einen Neustart überlebt - und was nicht

Stand: 2026-09-15. Drei Beschwerden, drei verschiedene Ursachen.

> Das Ladelimit setzt sich nach einem Neustart immer auf 97 %, manchmal stellt sich die
> Tastatur nicht ein, und die App öffnet sich erst nach einer Weile.

## 1. Der Autostart lief im Akkubetrieb gar nicht

Die Aufgabe wurde mit `schtasks /Create /SC ONLOGON` angelegt. Das sieht nach dem einfachen
Weg aus und bringt stillschweigend Microsofts Voreinstellungen für einen *Wartungsauftrag*
mit. Die Aufgabe auf diesem Gerät sagte wörtlich:

```xml
<DisallowStartIfOnBatteries>true</DisallowStartIfOnBatteries>
<StopIfGoingOnBatteries>true</StopIfGoingOnBatteries>
```

- **Im Akkubetrieb startete sie nicht.** Windows prüft die Bedingung später erneut - daher
  „öffnet sich erst nach einer Weile": nämlich dann, wenn das Netzteil wieder dran ist.
- **Beim Abziehen des Netzteils beendete Windows die laufende App.** Für ein Programm, das
  die Lüfter überwacht, ist das die falsche Richtung: Im Akkubetrieb wird es am ehesten
  gebraucht.

Zwei weitere Vorgaben stehen nicht in der Datei und gelten trotzdem:

| Einstellung | schtasks-Vorgabe | Folge |
| --- | --- | --- |
| `ExecutionTimeLimit` | PT72H | Nach drei Tagen Laufzeit beendet Windows die App |
| `Priority` | 7 (unter normal) | Langsamerer Start - genau das, was hier stören sollte |

Eine Startverzögerung war **nicht** gesetzt; die Langsamkeit kam aus den Bedingungen und der
Priorität, nicht aus einem Timer.

### Die Aufgabe kommt jetzt aus einer eigenen Definition

`StartupTaskDefinition` schreibt die vollständige XML, `StartupManager` registriert sie mit
`/Create /XML`. Alle vier Punkte sind korrigiert, und die Reihenfolge der Elemente ist die,
die Windows selbst ausgibt - das Schema ist verbindlich, eine falsch sortierte Datei wird
abgewiesen.

Geprüft wird das nicht nur auf dem Papier: `--autostart-definition-check` legt die echte
Definition unter einem Wegwerfnamen an, liest sie aus Windows zurück, vergleicht und löscht
sie wieder. Einziger Unterschied zur echten Aufgabe ist `LeastPrivilege` statt
`HighestAvailable`, weil das Anlegen einer erhöhten Aufgabe selbst Adminrechte braucht - und
genau dieses Element trug die alte, funktionierende Definition bereits.

```
anlegen: Code 0
OK    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
OK    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
OK    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
OK    <Priority>5</Priority>
OK    <LogonTrigger>
OK    <Arguments>--background</Arguments>
OK    Matches erkennt die eigene Aufgabe wieder
```

### Bestehende Aufgaben werden repariert, nicht liegengelassen

Die Beschreibung der Aufgabe trägt eine Kennung (`AORUS Control Autostart v2`). Beim Start
prüft `RepairAsync`, ob die vorhandene Aufgabe diese Kennung trägt **und** auf die aktuelle
EXE zeigt; andernfalls wird sie einmal neu geschrieben. Zwei Dinge dabei bewusst so:

- **Sie schaltet den Autostart nie ein.** Wer ihn aus hat, behält ihn aus. Ihn nach einem
  Update wiederzufinden wäre die unangenehmere Überraschung.
- **Der Pfad zählt mit.** Ein Update verschiebt das Programm; eine Aufgabe, die auf einen
  alten Pfad zeigt, scheitert lautlos.

Beim Vergleich werden beide Seiten auf druckbares ASCII reduziert, weil `schtasks` seine
Ausgabe in der Konsolen-Codepage liefert - ein Profilordner mit Umlaut käme verändert zurück
und würde die Aufgabe sonst bei jedem Start neu schreiben, für immer, ohne je zu sagen warum.

## 2. Das Ladelimit wurde nirgends gespeichert

Kein Fehler, eine Lücke: Die App **las** das Limit beim Start vom EC und glaubte, was sie
vorfand. Eine eigene Kopie gab es nicht - im Einstellungsordner lagen Dateien für Lüfterkurve,
Grafikzuordnung, Tastatur und zuletzt benutzte Farben, für den Akku keine. Verliert der EC die
Schwelle über einen Neustart, war sie weg.

`BatterySettingsStore` schreibt sie jetzt mit - **erst nachdem die Änderung zurückgelesen und
bestätigt wurde**, damit in der Datei nie eine Einstellung steht, die gar nicht angekommen
ist. Beim Start vergleicht `BatteryViewModel` Gerät und Datei und schreibt nur bei einer
Abweichung, genau einmal, sichtbar in der Statuszeile.

Die interessanten Fälle sind die, in denen nichts geschehen darf, und jeder hat eine
Testzeile: nichts gespeichert, Gerät stimmt schon überein, Gerät nicht freigegeben, und eine
fehlgeschlagene Änderung, die nicht als erfolgreich gemerkt werden darf.

## 3. Die Tastatur versuchte es genau einmal

`KeyboardViewModel.StartAsync` schrieb die gespeicherte Auswahl einmal auf das Gerät.
Scheiterte das - und direkt nach dem Anmelden meldet sich die USB-Tastatur oft ein, zwei
Sekunden später an -, landete es im `catch` und wurde nie wieder versucht. Daher das
„manchmal": Es ist ein Wettlauf, kein Zufall.

Jetzt vier Versuche mit 1, 2 und 4 Sekunden Abstand, also rund sieben Sekunden. Danach ist
Schluss - eine Tastatur, die dann noch nicht da ist, ist wirklich weg, und die Kachel sagt das
auch, statt Bedienelemente anzubieten, die ins Leere greifen. Eine Tastatur, die sofort
antwortet, zahlt für all das nichts: ein Versuch, keine Wartezeit.

## Was ausdrücklich nicht wiederhergestellt wird

Ein fester Lüfterwert. `PRODUCT-DECISIONS.md` Punkt 4 hält fest, dass es keine pauschale
Freigabe unüberwachter Fixed-Werte nach einem Neustart gibt. Ein Lüfter, der nach dem
Hochfahren auf einem festen Wert steht, ohne dass jemand die Temperatur beobachtet, ist genau
das Risiko, dem diese App aus dem Weg geht. Lüfterprofil und Kurve sind davon nicht betroffen
- die liegen ohnehin auf dem EC.
