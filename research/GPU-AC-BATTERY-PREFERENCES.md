# GPU-Präferenzen abhängig von Netz/Akku: Machbarkeit

Stand 2026-09-09. Nur Code-/Registry-Lesen und Webrecherche; keine Einstellungen geändert.

## Ergebnis

Automatische Umschaltung der bevorzugten GPU für ausgewählte Programme ist technisch plausibel und implementierbar. Eine funktionierende End-to-End-Umschaltung wurde noch nicht getestet. Keine harte RTX-Sperre, kein Wechsel bereits laufender Grafikgeräte garantiert.

## Autostart-Befund vom 2026-09-16

Die Behauptung unten, die GPU-Automatik laufe im Infobereich auch ohne je geöffnetes Fenster, gilt für den aktuellen Startpfad **nicht**. Der Autostart startet `AorusControl.App` mit `--background` und erzeugt `MainWindow`, ruft aber `ShowWindow()` nicht auf. `MainWindowViewModel.StartAsync()` hängt derzeit an `MainWindow.Loaded`, das bei diesem unsichtbaren Fenster noch nicht ausgelöst wird. Dadurch werden `GpuPreferenceViewModel.StartAsync()`, das erste Anwenden der gespeicherten Programme und das Abonnement von `SystemEvents.PowerModeChanged` bis zum ersten Öffnen des Fensters nicht ausgeführt. Nach dem Öffnen läuft die Automatik auch bei wieder verstecktem Fenster weiter. Die Auswahl des Tabs „Leistung & Akku“ ist technisch nicht erforderlich; sie macht nur Status und Programme sichtbar.

Der separat gestartete `AorusControl.Worker.exe` übernimmt ausschließlich Hardware-/Lüfteraufgaben und keine GPU-Präferenzwechsel. Die Autostart-Aufgabe selbst erlaubt inzwischen Akku-Betrieb (`DisallowStartIfOnBatteries=false`, `StopIfGoingOnBatteries=false`); die fehlende Initialisierung der Oberfläche ist hier die Ursache. Erforderliche Korrektur: Module beim App-Start unabhängig vom `Loaded`-Ereignis einmalig starten und Mehrfachstarts verhindern. Danach Autostart ohne Öffnen des Fensters und einen echten Netz-/Akkuwechsel prüfen.

### Korrektur im Quellcode

`App.OnStartup` ruft nun `MainWindowViewModel.StartAsync()` auch bei `--background` auf. Der `Loaded`-Handler navigiert nur noch zur Startseite und startet die Module nicht erneut. `Graphics` steht in der Startreihenfolge vor den Hardware-Modulen, damit die gespeicherten GPU-Präferenzen schon vor einer möglichen Tastatur-Retry-Verzögerung angewendet werden. Das GPU-Modul meldet den Stromquellen-Handler nur einmal an, auch wenn die Überwachung später erneut gestartet wird. Bei verstecktem Autostart wird das Dashboard als unsichtbar markiert; dessen Telemetrie wird deshalb nicht abgefragt. Debug- und Release-Build ohne Warnungen; alle Smoke-Tests bestanden (keine echten Hardware-Setter). Ein echter Windows-Neustart mit dem neuen Release/Installer und Netz-/Akkuwechsel muss noch live verifiziert werden.

## Umgesetzt am 2026-09-09

Karte „Grafikkarte je nach Stromquelle" unter Leistung & Akku. Ausgewählte Programme laufen am Netz auf der RTX und im Akkubetrieb auf der Intel-Grafik, damit die RTX in ihrem Schlafzustand bleibt.

- Geschrieben wird ausschließlich Windows' eigener Wert unter `HKCU\Software\Microsoft\DirectX\UserGpuPreferences` - derselbe, den die Windows-Einstellungen setzen. Keine NVIDIA-Schnittstelle, kein Treibereingriff, kein Abschalten der Karte.
- Der Eintrag wird tokenweise geändert: nur `GpuPreference`, alles andere (`AutoHDREnable`, `SwapEffectUpgradeEnable`, unbekannte Tokens) bleibt unverändert und in seiner Reihenfolge stehen. Ein Eintrag mit `SpecificAdapter` wird angezeigt, aber nie geschrieben.
- Umgeschaltet wird bei Stromquellenwechsel über `SystemEvents.PowerModeChanged`, also auch bei geschlossenem Fenster aus dem Infobereich heraus. Unbekannte Stromquelle ändert nichts.
- Was der Nutzer selbst in Windows umstellt, wird erkannt (Vergleich mit dem zuletzt selbst geschriebenen Wert) und nicht überschrieben; die Karte zeigt es an. Beim Entfernen aus der Liste wird die ursprüngliche Einstellung zurückgegeben, sofern sie noch die unsrige ist.
- Grenzen, die die Oberfläche auch benennt: Eine Präferenz wirkt erst beim nächsten Start des Programms und bewegt kein laufendes. Programme, die ihre Grafikkarte selbst wählen (CUDA, expliziter Adapter), ignorieren sie.
- Die App läuft erhöht; HKCU zeigt dabei auf dasselbe Benutzerprofil, solange die Erhöhung mit demselben Konto erfolgt. Würde sie mit einem anderen Administratorkonto erhöht, landeten die Einträge in dessen Profil.
- Tests: Tokenbehandlung inklusive der echten VLC-Zeile dieses Rechners, Registry-Rundlauf in einem eigenen Testschlüssel, Netz-/Akku-Plan, manuelle Änderung, unbekannte Quelle, Automatik aus, Entfernen. Die Smoke-Tests fassen die echten Zuordnungen des Benutzers nicht an - dafür gibt es Attrappen.

### Vorschläge statt automatischer Übernahme

Die Karte schlägt Programme vor, statt sie selbst zu übernehmen. Quelle sind ausschließlich die Einträge, die Windows schon führt: vorgeschlagen wird, was auf Höchstleistung steht, noch nicht verwaltet wird, keinen fest gewählten Adapter hat und dessen Datei es noch gibt.

Warum nicht alles Installierte durchsuchen: Dieser Rechner hat 234 Startmenü-Verknüpfungen und 303 Deinstallationseinträge - das ist keine Vorschlagsliste, das sind Hausaufgaben. Die interessante Menge ist winzig und braucht kein Raten. Am 2026-09-09 ergab die Regel hier genau zwei Treffer: theHunter und Netflix. Zehn Programme stehen bereits auf Energiesparen und tun im Akkubetrieb ohnehin das Richtige, eines hat einen festen Adapter, fünf Einträge sind Leichen deinstallierter Programme - letztere werden gezählt und einmal erwähnt, nicht vorgeschlagen.

Der Nebeneffekt ist der wichtigere: Netflix ist eine Store-App ohne erreichbare EXE (`C:\Program Files\WindowsApps\4DF9E0F8.Netflix_...`, adressiert über die App-ID `4DF9E0F8.Netflix_mcm4njqhnhss8!Netflix.App`). Über den Dateidialog ist sie nicht hinzuzufügen - von 272 Startmenü-Einträgen dieses Rechners sind 169 solche Apps. Die Vorschlagsliste kommt aus der Registry und kennt diese Namen deshalb von selbst.

### Programmsuche

Statt des Dateidialogs gibt es einen Suchdialog: tippen, auswählen, hinzufügen. Er führt zwei Quellen zusammen, weil Windows die beiden Programmarten getrennt verwaltet:

- Store-Apps aus dem AppsFolder (`shell:::{4234d49b-0245-4df3-b780-3893943456e1}`), erkannt am `!` in der Kennung - 42 auf diesem Rechner. Die Kennung ist genau die, die auch in der Registry steht.
- Klassische Programme über die Startmenü-Verknüpfungen, aufgelöst auf ihre EXE.

Beim Auflösen gab es zwei Fallstricke, beide erst im Lauf am echten System aufgefallen:

1. `Directory.EnumerateFiles(..., AllDirectories)` wirft *während* des Durchlaufens, nicht beim Aufruf. Ein Startmenü-Ordner dieses Rechners verweigert den Zugriff und riss damit die ganze Liste ab; jetzt mit `EnumerationOptions.IgnoreInaccessible`.
2. `Folder.GetLink.Path` ist bei 157 der 234 Verknüpfungen leer - darunter Google Chrome und der Epic-Launcher. Was funktioniert, ist die erweiterte Eigenschaft `System.Link.TargetParsingPath`.

Ergebnis: 199 Programme, davon 42 aus dem Store, Dauer rund 2 Sekunden - deshalb im Hintergrund mit Ladehinweis. Gesucht wird über Name und Pfad, jedes Wort muss vorkommen, Namenstreffer stehen vor reinen Pfadtreffern. Der Dateidialog bleibt als Knopf daneben, für Programme ohne Startmenü-Eintrag wie Spiele aus einer Launcher-Bibliothek (theHunter etwa steht nur deshalb schon im Vorschlag, weil Windows für ihn bereits eine Präferenz führt).

Noch offen: der End-to-End-Nachweis, dass ein umgeschaltetes Programm tatsächlich auf dem anderen Chip startet. Der unten vorgeschlagene DXGI-Test steht weiterhin aus.

## Wer benutzt gerade welchen Chip (2026-09-18)

Bis hierher zeigte die Karte nur **gespeicherte Präferenzen** - also Wünsche. Welches Programm
tatsächlich auf welchem Chip rechnet, stand nirgends, und damit war auch nicht zu erkennen,
welche Programme überhaupt interessant sind.

### Woher die Antwort kommt

Windows führt pro Prozess, Adapter und Engine eine Leistungsindikator-Instanz:

```
pid_1692_luid_0x00000000_0x0001232e_phys_0_eng_0_engtype_3d
```

Der **Name allein** trägt Prozess-ID und Adapter-LUID. Es muss also kein einziger Zählerwert
abgetastet werden - und das ist wichtig: Ein Programm, das mit null Prozent Last auf der RTX
sitzt, hält sie trotzdem wach. Die Anwesenheit ist das Signal, nicht die Auslastung.

Die LUIDs werden über **D3DKMT** (`D3DKMTEnumAdapters2` + `KMTQAITYPE_ADAPTERREGISTRYINFO`)
zu Namen aufgelöst - dieselben Aufrufe, die hinter der Grafikspalte des Task-Managers stehen.
Es wird kein Gerät erzeugt und der NVIDIA-Treiber nicht angefasst. Ergebnis auf diesem Gerät:

| LUID | Name |
| --- | --- |
| `0x1232e` | NVIDIA GeForce RTX 3070 Laptop GPU |
| `0x12066` | Intel(R) Iris(R) Xe Graphics |

**Noch offen:** Ob die D3DKMT-Abfrage eine *schlafende* Karte weckt, ist nicht belegt - beim
Test war sie bereits in D0. Die Aufrufe lesen eine Kernel-Liste und erzeugen kein Gerät, was
dagegen spricht; ein Beleg ist das nicht. Zu prüfen bei schlafender Karte: Gerätezustand vor
und nach `GraphicsAdapters.Enumerate()` vergleichen. Die Adapterliste wird ohnehin nur einmal
je Programmlauf abgefragt und zwischengespeichert.

### Warum gefiltert wird

Der Rohbestand war unbrauchbar: **29 Zeilen**, davon 21 Windows' eigene Shell und Kernel -
`csrss`, `dwm`, `System`, der Sperrbildschirm, der Texteingabe-Host. Keines davon lässt sich
einer Grafikkarte zuweisen, und keines ist der Grund, warum die Karte wach ist.

`GpuActivitySummary.From` fasst deshalb zusammen und wirft weg:

1. Einträge ohne lesbaren Pfad (geschützte Prozesse),
2. alles unterhalb des Windows-Verzeichnisses,
3. mehrere Prozesse eines Programms werden zu einer Zeile (Electron-Apps, Browser),
4. ein Programm auf **beiden** Chips gilt als auf der RTX - das ist der Zustand, der zählt.

Übrig blieben 6 Zeilen, davon 3 auf der RTX: `Claude`, `IGCC` und `WindowsTerminal`. Genau
das war die Antwort auf die Frage, warum die Karte an diesem Nachmittag wach war.

### Grenze: Store-Apps

Windows führt die Grafikpräferenz einer Store-App unter ihrer Paketkennung, nicht unter dem
Pfad unter `WindowsApps`. Für solche Einträge wird deshalb **kein** Übernehmen-Knopf angeboten,
sondern auf die Programmsuche verwiesen, die die Kennungen kennt. Ein Schreiben des Pfades
würde einen Eintrag hinterlassen, den nie jemand liest.

## Regeln pro Programm statt einer festen Kopplung (2026-09-18)

`GpuSwitchPlan.For(source)` war fest verdrahtet: am Netz die RTX, am Akku die Intel-Grafik,
für alles auf der Liste. Das ist die richtige Voreinstellung und das falsche Gesetz - ein
Browser gehört auch am Netz auf die Intel-Grafik, ein Spiel auch am Akku auf die RTX.

`ManagedProgram` trägt die beiden Werte jetzt selbst (`OnAc`, `OnBattery`), voreingestellt
genau auf die bisherige Regel. In der Oberfläche sind das zwei Auswahlfelder je Zeile.

Die Einstellungsdatei steht damit auf **Version 2**. Eine Datei der Version 1 wird weiterhin
gelesen; ihre Einträge bekommen die Voreinstellungen, also exakt das Verhalten dieser Version.
Ein Update, das die Liste stillschweigend leert, wäre schlimmer als eines, das sich beschwert.

Beim Ändern einer Regel wird das Programm aus `lastWritten` entfernt: Der Wert in der
Registry ist dann kein Beleg mehr dafür, dass ihn jemand von Hand gesetzt hat - die Frage,
die er beantwortet hat, ist eine andere geworden.

## Lokale Belege

HKCU\Software\Microsoft\DirectX\UserGpuPreferences existiert. Beispielsweise theHunter: GpuPreference=2; mehrere Anwendungen: GpuPreference=1; Chrome: GpuPreference=0. VLC nutzt bereits eine explizite Adapterzuordnung: SpecificAdapter=10DE&249D&15461458 zusammen mit GpuPreference=1073741824 und weiteren Grafikoptionen. Netflix verwendet eine Paket-/App-ID statt eines EXE-Pfads.

Folge: Ein simpler vollständiger String-Ersatz durch GpuPreference=1/2 wäre fehlerhaft. HDR-/Swapchain-Optionen müssen erhalten bleiben, explizite Adapterauswahl muss gesondert behandelt werden. Unbekannte Formate zunächst ablehnen. Gespeicherte Einträge beweisen nicht die aktuelle GPU-Nutzung oder dass alle EXE-Pfade noch existieren.

## Vorgeschlagene Umsetzung

- Kleine Liste ausgewählter Programme mit Netz-/Akku-Präferenz und aktivierter Automatik.
- GetSystemPowerStatus beim Start/Resume; RegisterPowerSettingNotification mit GUID_ACDC_POWER_SOURCE für Wechsel auch im Tray. Unbekannte Versorgung führt zu keiner Änderung; kurze Entprellung verhindert unnötige Schreibfolgen.
- Im angemeldeten Benutzerkontext nur die zugeordneten Einträge aktualisieren. Vorherige Werte sichern; nur selbst verwaltete Teilwerte ändern. Manuelle Änderungen erkennen, keine dauernde Überschreibschleife. Beim Abschalten Originalpräferenz nur wiederherstellen, wenn sie noch unserem zuletzt geschriebenen Zustand entspricht.
- Rücklesen bestätigt die gespeicherte Präferenz, nicht die tatsächlich verwendete GPU. Anzeige trennt beides und weist bei laufenden Programmen auf Neustart hin.
- Keine NVML/NVIDIA-Sensorabfrage für diese Steuerlogik. Stromquellenereignisse/Registry benötigen keine Initialisierung der RTX. Ein danach auf der RTX gestartetes Programm darf sie natürlich aufwecken.
- Keine automatische Prozessbeendigung, Treiberdeaktivierung oder BIOS/EC-Abschaltung.

## Grenzen / Prüfung vor Integration

Für das programmgesteuerte Setzen fremder App-Präferenzen wurde keine öffentliche dokumentierte stabile Setter-API gefunden. Der Registry-Weg wird von Open-Source-Installern wie 3D Slicer verwendet, bleibt aber eine Windows-Implementierungsabhängigkeit. DXGI EnumAdapterByGpuPreference dient der Adapterwahl innerhalb einer Anwendung, nicht als globaler Setter für andere Programme.

Nächster kontrollierter Test: eigene kleine DXGI-Probe als separat gestartetes Testprogramm; beide Präferenzen ausschließlich für dessen EXE setzen, Prozess neu starten, tatsächlichen gewählten Adapter prüfen, Originalzustand wiederherstellen. Das Starten auf der RTX weckt sie hierbei absichtlich. Danach echter Netz-/Akkuwechsel, Tray/Resume und Konflikte mit manuellen Windows-Einstellungen prüfen. Ohne diese Tests keine Zuverlässigkeitsgarantie.

Externe an NVIDIA angeschlossene Anzeigen und explizite GPU-/CUDA-Auswahl können eine Intel-Präferenz übergehen. Ein bereits laufendes Spiel wandert beim Abziehen des Netzteils nicht automatisch zu Intel.

## Primärquellen

- Windows-Vorrang: https://www.nvidia.com/content/Control-Panel-Help/vLatest/en-us/mergedProjects/3D%20Settings/Setting_the_Preferred_Graphics_Processor.htm
- Optimus-Ausnahmen: https://www.nvidia.com/content/Control-Panel-Help/vLatest/en-us/mergedProjects/nvcpl/Using_Optimus_Hybrid.htm
- Windows-Stromquellenereignisse: https://learn.microsoft.com/en-us/windows/win32/power/power-setting-guids
- DXGI-GPU-Präferenz: https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_6/nf-dxgi1_6-idxgifactory6-enumadapterbygpupreference
- Praxisbeispiel Registry im 3D-Slicer-Installer: https://github.com/Slicer/Slicer/blob/main/CMake/SlicerCPack.cmake
