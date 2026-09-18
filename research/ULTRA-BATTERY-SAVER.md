# Gigabytes Akkuspar-Modus: was er wirklich tat - und was daraus bei uns werden soll

Stand 2026-09-18. Quelle ist der dekompilierte Code der Notebook-Komponente im Repo, nicht
Gigabytes Marketing:

- `third-party/vendor/GBT_Notebook_26.06.23.01-static/decompiled/ucNotebook/ucNotebook.Helper/CPOWERAPI.cs`
  (`SetEcoDcExtendBattery`, Zeile 980)
- `.../ucNotebook.Views/FanControlNb.cs` (`EcoActiveBox_SelectionChanged`, Zeile 6961;
  `btnEcoClicked`, Zeile 5080)
- `.../ComData/ComHeader/ComData.cs` (`DcEcoFanClick`, Zeile 127)

## Was der Schalter tat - vollständig

`SetEcoDcExtendBattery(true)` macht **genau vier Dinge**, alle über Windows' eigene
Energie-API, keines über Firmware:

| Schritt | Aufruf | Wert |
| --- | --- | --- |
| 1. Alten Wert sichern | Registry `PreProcMax` | der bisherige DC-Wert |
| 2. CPU deckeln | `PowerWriteDCValueIndex(Balanced, PROCESSOR_SETTINGS, PROCESSOR_THROTTLE_MAXIMUM)` | **50 %** |
| 3. Alte Helligkeit sichern | Registry `PreBrightness` | der bisherige DC-Wert |
| 4. Bildschirm dimmen | `PowerWriteDCValueIndex(Balanced, VIDEO_SUBGROUP, VIDEO_BRIGHTNESS)` | **30 %** (oder der gespeicherte `EcoL`) |

Danach `PowerSetActiveScheme(Balanced)`. Ausschalten liest die beiden Registry-Werte zurück,
schreibt sie zurück und löscht die Schlüssel; fehlen sie, fällt es auf 100 % CPU zurück.

Alle benutzten GUIDs sind die dokumentierten Windows-Werte:

```
381B4222-F694-41F0-9685-FF5BB260DF2E   Balanced (GUID_TYPICAL_POWER_SAVINGS)
54533251-82BE-4824-96C1-47B60B740D00   Prozessor-Untergruppe
BC5038F7-23E0-4960-96DA-33ABAF5935EC   Maximaler Prozessorzustand
7516B95F-F776-4464-8C53-06167F40CC99   Bildschirm-Untergruppe
ADED5E82-B909-4619-9949-F5D71DAC0BCB   Bildschirmhelligkeit
```

### Was er *nicht* tat

- **Nichts an der Firmware.** Kein WMI-Aufruf, kein EC, kein Lüfterregister.
- **Nichts an der Grafikkarte.** Die RTX kam in diesem Pfad überhaupt nicht vor - was, wie
  unten gezeigt, den grössten Hebel dieses Geräts ausgelassen hat.
- `ComData.DcEcoFanClick`, das man für einen Lüfterteil halten könnte, ist ein
  Maus-Hover-Flag der Oberfläche (`btnEco_MouseEnter`/`MouseLeave`). Keine Hardware.

### Die Bedingungen

Der Schalter war nur wirksam, wenn **Akkubetrieb** *und* Lüftermodus **Eco** gleichzeitig
galten (`power == 1 && currentMode == FanMode.Eco`). Er war also kein eigener Modus, sondern
ein Zusatzhaken am leisen Lüfterprofil.

## Was das auf diesem Gerät bringt - gerechnet, nicht geraten

Aus unseren eigenen Messungen (`POWER-BREAKDOWN-20260915.md`, 24,4 W im Leerlauf mit
schlafender Grafikkarte):

| Hebel | Gemessener Wert | Im Leerlauf | Unter Last |
| --- | --- | --- | --- |
| Bildschirm 100 % → 30 % | das Panel kostet **5 W** von min bis max | **rund 3 W** | dieselben 3 W |
| CPU-Deckel 100 % → 50 % | Kerne im Leerlauf: 4 W von 24,4 W | **nahe null** | viel, aber auf Kosten der Reaktionszeit |
| *(fehlte bei Gigabyte)* RTX schlafen lassen | am 18.09. hielten 5 Programme sie wach | **bis zu zweistellig** | — |

Das ist die ehrliche Bilanz: **Von Gigabytes zwei Hebeln wirkt im Leerlauf nur einer**, und
der dritte, der am meisten bringt, fehlte dort ganz. Ein Akkusparmodus, der die RTX nicht
anspricht, lässt auf diesem Laptop den grössten Posten liegen.

## Plan für unsere Umsetzung

### Leitgedanke

Nicht Gigabytes Schalter nachbauen, sondern **eine Szene, die sagt, was sie tut und was
jedes Stück davon wert ist**. Wir haben etwas, das Gigabyte nicht hatte: eine Messung des
Gesamtverbrauchs und eine Liste der Programme, die die Karte wachhalten. Ein Sparmodus, der
danebensteht und das Ergebnis zeigt, ist ein anderer Gegenstand als ein Haken.

### Was der Modus setzt

| # | Hebel | Womit | Bereits im Code? |
| --- | --- | --- | --- |
| 1 | Windows-Leistungsmodus auf Energieeffizienz | `PowerSetActiveOverlayScheme` | **ja**, `WindowsPowerOverlayController` |
| 2 | Lüfterprofil auf Leise | Gigabyte-WMI | **ja**, `CoolingViewModel` |
| 3 | Bildschirm auf einen einstellbaren Wert | `PowerWriteDCValueIndex(VIDEO_BRIGHTNESS)` | nein |
| 4 | CPU-Deckel auf einen einstellbaren Wert | `PowerWriteDCValueIndex(PROCESSOR_THROTTLE_MAXIMUM)` | nein |
| 5 | Verwaltete Programme auf die Intel-Grafik | Registry, vorhanden | **ja**, `GpuSwitchPlan` |
| 6 | Hinweis auf Programme, die die RTX gerade wachhalten | vorhanden | **ja**, `GpuActivityReader` |

Drei von sechs Hebeln sind schon gebaut. Neu sind nur 3 und 4 - und die sind zwei Aufrufe
derselben dokumentierten Funktion.

### Der Punkt, an dem Gigabytes Lösung schlecht war

`SetEcoDcExtendBattery` schreibt **in den Energiesparplan des Nutzers** und sichert die alten
Werte in der Registry. Stürzt das Programm zwischen Setzen und Zurückstellen ab, bleibt der
Rechner gedeckelt, und niemand sagt einem, warum er plötzlich langsam ist. Genau dieses
Problem haben wir bei den festgesetzten Lüftern schon gelöst.

Zwei mögliche Wege:

**A. Eigener Energiesparplan.** Einmalig `PowerDuplicateScheme(Balanced)` unter dem Namen
„AORUS Control · Akku sparen", dort hineinschreiben, beim Einschalten aktivieren, beim
Ausschalten auf den vorher aktiven Plan zurückschalten. Am Plan des Nutzers wird **nie**
etwas geändert; ein Absturz lässt höchstens einen zusätzlichen Plan in der Liste stehen, der
nichts tut, sobald man ihn nicht aktiviert.

**B. Wie Gigabyte, aber mit unserem Rückstellmechanismus.** In den aktiven Plan schreiben,
die alten Werte versioniert in `AppData` sichern (wie `battery-v1.json`), und beim Start
prüfen und zurückstellen, so wie das Ladelimit es heute schon tut.

**Empfehlung: A.** Der Plan des Nutzers ist seiner. Ein Modus, der ihn verändert, muss sich
darauf verlassen, dass er ihn auch wieder zurückbekommt - und genau diese Verlässlichkeit
ist der teure Teil. Ein eigener Plan hat sie geschenkt.

### Schritte

1. **`PowerSchemeWriter` in Core** - `PowerDuplicateScheme`, `PowerWriteDCValueIndex`,
   `PowerSetActiveScheme`, `PowerGetActiveScheme`, `PowerDeleteScheme`. Reine P/Invoke-Schicht
   mit Rücklesen nach jedem Schreibvorgang, wie überall sonst in diesem Projekt.
   *Prüfbar ohne Hardware:* Plan anlegen, Werte schreiben, zurücklesen, Plan löschen - alles
   im Benutzerkontext, nichts Bleibendes.
2. **`BatterySaverScene` in Core** - welche Hebel in welcher Reihenfolge, was beim Ausschalten
   zurückgeht, und was passiert, wenn ein Hebel scheitert (die anderen laufen weiter, der
   Bericht nennt den gescheiterten). Reine Logik, vollständig testbar.
3. **Karte unter „Leistung & Akku"** - ein Schalter, darunter die Liste der Hebel mit dem
   jeweils gemessenen Gewinn, und zwei Regler für Helligkeit und CPU-Deckel. Wer den Modus
   einschaltet, soll lesen können, was er dafür aufgibt.
4. **Die Messung daneben.** Der Verbrauch vorher und nachher aus `BatteryFlow`, und die neue
   Restlaufzeit. Das ist der Teil, den Gigabyte nicht hatte, und der eigentliche Grund, das
   überhaupt zu bauen: Man sieht, ob es etwas gebracht hat.
5. **Nur im Akkubetrieb aktiv**, wie bei Gigabyte - aber als Automatik formuliert: beim
   Abziehen ein, beim Anstecken aus, abschaltbar. Die Stromquellen-Ereignisse hören wir für
   die Grafikumschaltung ohnehin schon ab.

### Was bewusst nicht hineingehört

- **Kein Deckel unter 30 %.** Ein Laptop, der sich nicht mehr bedienen lässt, spart keine
  Laufzeit, er verschiebt nur Arbeit nach hinten.
- **Kein Abschalten der RTX.** Wir haben keinen belegten Weg dorthin, und der Umweg über
  Präferenzen wirkt erst beim nächsten Programmstart - das gehört gesagt, nicht umgangen.
- **Keine Zahl ohne Messung.** Wenn ein Hebel auf diesem Gerät nichts bringt, steht das an
  ihm dran. Der CPU-Deckel bringt im Leerlauf nichts, und das ist der häufigste Zustand.

## Offen

- `PowerWriteDCValueIndex` auf einem duplizierten Plan ist noch nicht an diesem Gerät
  ausprobiert. Schritt 1 ist genau dieser Versuch.
- Ob die Bildschirmhelligkeit über den Energieplan mit der Helligkeitstaste des Geräts
  kollidiert, ist ungeprüft. Gigabyte hat es so gemacht, was ein Indiz ist, kein Beleg.
- Wie viel der CPU-Deckel unter realer Last bringt, ist auf diesem Gerät nicht gemessen. Vor
  dem Bau einer Anzeige, die Watt verspricht, gehört eine Messung.
