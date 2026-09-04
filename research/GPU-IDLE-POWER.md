# RTX 3070 im Leerlauf abschalten – AORUS 5 SE4

Stand: 2026-09-03. Alle Messungen ausschliesslich lesend.

## Kernaussage

**Abgeschlossen: Die RTX schaltet sich bereits von selbst ab. Es gibt kein
Problem zu lösen.** Nachgewiesen über den Gesamtsystemverbrauch im Akkubetrieb:
Aufwecken kostet rund 22 W, und nach 60 Sekunden ohne Abfrage liegt der
Verbrauch wieder auf der Basislinie. Details im Abschnitt „ERGEBNIS".

Die richtige Frage war nicht „wie schalte ich die GPU aus", sondern „warum
schläft sie nicht von selbst ein" — und die Antwort lautet: sie tut es. Optimus
schaltet die RTX über NVIDIAs Runtime-Power-Management (RTD3) selbständig ab,
sobald nichts sie beansprucht.

Alle Abschnitte zwischen dieser Zusammenfassung und dem Ergebnis beruhen auf
`nvidia-smi`-Werten, die sich als Weckartefakt herausgestellt haben. Sie sind
wegen der Beweiskette erhalten und als korrigiert gekennzeichnet.

**Den Treiber zu deaktivieren ist deshalb kontraproduktiv.** RTD3 steckt im
Treiber. Ohne aktiven Treiber gibt es niemanden, der die Karte in D3 legt — sie
bleibt bestromt. Das erklärt die vom Besitzer beobachteten durchgehenden 25 W im
deaktivierten Zustand vollständig. Ein deaktiviertes Gerät im Geräte-Manager ist
nicht stromlos.

## Gemessener Ausgangszustand

```text
NVIDIA GeForce RTX 3070 Laptop GPU, Treiber 616.56
Performance State : P0
Power Draw        : 27,58 W (instantan)
SM-Takt           : 1560 MHz
Auslastung        : 0 %
Display Active    : Disabled
Power Limit       : 130 W aktuell / 115 W Standard
```

Das ist der **höchste** Leistungszustand bei null Auslastung, mit vollem
Boost-Takt und ohne angeschlossenes Display. Das ist kein RTD3-Leerlauf.

Ergänzend über die Windows-GPU-Leistungsindikatoren:

- `\GPU Adapter Memory(*)\Dedicated Usage`: alle Adapter 0 MB
- `\GPU Process Memory(*)\Dedicated Usage`: **kein einziger Prozess** mit
  dedizierter Nutzung
- `nvidia-smi --query-compute-apps`: leer

Es belegt also nachweislich niemand VRAM, und trotzdem steht die Karte auf P0.

### Wichtiges Messartefakt

`nvidia-smi` **weckt die dGPU selbst**, um sie abzufragen. Ein Wert von 27 W
direkt nach einem `nvidia-smi`-Aufruf ist daher kein Beweis für einen dauerhaft
wachen Zustand. Wiederholtes Pollen mit `nvidia-smi` verhindert RTD3 sogar
aktiv.

Konsequenz für jede künftige Überprüfung: Der Erfolg einer Massnahme darf
**nicht** mit `nvidia-smi` in kurzen Abständen gemessen werden. Brauchbar sind
stattdessen die Windows-Leistungsindikatoren oder die Akku-Entladerate.

## Untersuchte Ursachen

### Registry-Übersteuerung: ausgeschlossen

Der Anzeigeklassenschlüssel
`HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-…}\0000` enthält **keine**
`PowerMizer*`-Werte, also keine erzwungene Leistungsstufe aus der Registry.
Vorhanden sind nur Hardware- und Treiberstandardwerte.

### Prozesse, die VRAM halten: derzeit keine

Siehe oben. Die frühere Inspektion
(`research/runs/gpu-routing-inspection-20260903-140600.md`) listete dagegen über
`nvidia-smi` zwei `AppControl.exe`, Edge WebView, UniGetUI, WhatsApp,
Codex/ChatGPT, zwei Claude-Prozesse und mehrere Windows-Oberflächen auf der RTX.
Solche Electron- und WebView-Anwendungen greifen typischerweise nur kurz zu,
können den Adapter aber immer wieder aufwecken.

### Audio- und Zusatzgeräte auf der dGPU: vorhanden und aktiv

```text
NVIDIA High Definition Audio                        Status OK
NVIDIA Virtual Audio Device (Wave Extensible) (WDM) Status OK
NVIDIA Broadcast                                    Status OK
```

Das ist der stärkste vorliegende Verdacht. Ein aktiver Audio-Endpunkt oder ein
Broadcast-Filtertreiber auf der dGPU hält das Gerät in D0, weil das Audiogerät
selbst nicht schlafen darf. `NVIDIA Broadcast` ist installiert und die zugehörige
Geräteklasse gemeldet als OK; der Dienst `NvBroadcast.ContainerLocalSystem` war
zum Messzeitpunkt gestoppt, das Gerät aber aktiv.

### Laufende NVIDIA-Dienste

```text
nvagent                          Running (Manual)
NvContainerLocalSystem           Running (Automatic)
NVDisplay.ContainerLocalSystem   Running (Automatic)
NvBroadcast.ContainerLocalSystem Stopped (Manual)
```

`NVDisplay.ContainerLocalSystem` und `NvContainerLocalSystem` sind für den
Treiberbetrieb normal und sollten nicht abgeschaltet werden.

### Nicht auslesbar

- `DEVPKEY_Device_PowerState` liefert über `Get-PnpDeviceProperty` keinen Wert,
  der aktuelle D-Zustand ist so nicht direkt lesbar.
- `powercfg /requests` benötigt Administratorrechte und wurde noch nicht
  erhoben. Es würde zeigen, ob ein Treiber oder Prozess eine Energieanforderung
  hält.
- Die NVIDIA-3D-Einstellungen liegen binär in
  `%ProgramData%\NVIDIA Corporation\Drs\nvdrsdb0.bin` und sind nicht sinnvoll
  auslesbar. Die Einstellung **Energieverwaltungsmodus** muss deshalb in der
  NVIDIA-Systemsteuerung selbst geprüft werden.

### Installierte NVIDIA-Software: mehrere bekannte Wachhalter

Die Software-Inventur erklaert den Zustand besser als jede einzelne Einstellung.
Installiert sind unter anderem:

| Komponente | Version | Relevanz |
| --- | --- | --- |
| NVIDIA App | 11.0.8.299 | Rahmen fuer Overlay und Aufzeichnung |
| **NVIDIA ShadowPlay** | 11.0.8.0 | Overlay und Instant Replay halten eine Capture-Pipeline auf der dGPU |
| **NVIDIA Broadcast** + 6 Container | 1.4.0.29 | Effektpipeline auf der dGPU |
| NVIDIA Broadcast Voice Driver | 1.0.1.9 | Audiofilter |
| Camera (NVIDIA Broadcast) | 1.4.0.29 | virtuelle Kamera |
| **NVIDIA Virtual Audio** | 4.65.0.12 | Audio-Endpunkt auf der dGPU |
| NVIDIA Canvas | 1.4.311 | eigenstaendige GPU-Anwendung |
| CUDA Toolkit, Nsight Compute/Systems/VS | 13.3 / 2026.x | Entwicklungswerkzeuge |
| FrameView SDK | 1.8.x | Messwerkzeug |
| NVIDIA Platform Controllers and Framework | 615.34 | Systemkomponente |

`ShadowPlay` beziehungsweise das Overlay der NVIDIA App und `NVIDIA Broadcast`
mit virtuellem Audio sind die bekanntesten Gruende dafuer, dass ein
Notebook-dGPU nie in RTD3 geht. Beide sind hier installiert und aktiv.

### Fremdanwendung AppControl

Zwei laufende Instanzen aus `C:\Program Files\AppControl\ui\AppControl.exe`, in
der frueheren Inspektion auf der RTX gelistet. Der Pfad liegt nicht unter
`Program Files\GIGABYTE`, es ist also keine Gigabyte-Komponente dieses
Projekts, sondern eine separat installierte Anwendung.

## Empfohlene Reihenfolge

Vom wahrscheinlichsten und harmlosesten zum aufwendigsten. Nach jedem Schritt
messen, nicht mehrere gleichzeitig ändern.

1. **Treiber aktiviert lassen.** Ohne Treiber kein RTD3. Das ist die Ursache der
   beobachteten 25 W und keine Nebenwirkung.
2. **Overlay und Instant Replay abschalten.** In der NVIDIA App das
   In-Game-Overlay und die Aufzeichnung deaktivieren. Reversibel, ohne
   Deinstallation, und der häufigste Grund für einen dauerhaft wachen
   Notebook-dGPU. Zuerst probieren.
3. **NVIDIA-Systemsteuerung → 3D-Einstellungen verwalten:**
   - *Energieverwaltungsmodus* auf **Optimale Leistung**. Steht dort „Maximale
     Leistung bevorzugen", erklärt das P0 bei 0 % Auslastung unmittelbar. Diese
     Einstellung liess sich nicht programmatisch auslesen und muss von Hand
     geprüft werden.
   - *Bevorzugter Grafikprozessor* global auf **Integrierte Grafik**.
4. **NVIDIA Broadcast deinstallieren**, falls unbenutzt — samt Voice Driver,
   Camera und Containern. Ebenso **NVIDIA Virtual Audio**, wenn kein HDMI-Ton
   gebraucht wird. Beide bringen Geräte auf die dGPU, die deren Abschalten
   verhindern können.
5. **Windows-Einstellungen → System → Bildschirm → Grafik:** für die
   Electron-Anwendungen (Claude, WhatsApp, Codex/ChatGPT, Edge WebView,
   UniGetUI) *Energiesparen* wählen. Greift erst beim nächsten Start des
   Programms; ein laufender Prozess bleibt auf der RTX.
6. **Hardwarebeschleunigung in diesen Anwendungen abschalten**, wo vorhanden.
   Wirksamer als die Windows-Präferenz, weil die Anwendung dann gar keinen
   Adapter anfordert.
7. **`AppControl.exe` beenden**, falls entbehrlich.
8. **Nicht** im Geräte-Manager deaktivieren. Siehe oben.

## Wie der Erfolg gemessen wird

Nicht mit wiederholtem `nvidia-smi`. Zwei brauchbare Verfahren:

1. **Akku-Entladerate**, im Akkubetrieb und bei Leerlauf:

   ```powershell
   (Get-CimInstance -Namespace root\wmi -ClassName BatteryStatus).DischargeRate
   ```

   Der Wert ist in Milliwatt. Vor und nach einer Massnahme unter gleichen
   Bedingungen vergleichen. Ein schlafender dGPU sollte sich mit deutlich mehr
   als 20 W Unterschied bemerkbar machen.

2. **Windows-Task-Manager**, Reiter Leistung: verschwindet die dedizierte GPU
   aus der Aktivitätsanzeige beziehungsweise bleibt sie dauerhaft bei 0 %, ohne
   dass ein Prozess sie listet, ist RTD3 aktiv.

Ein einzelner `nvidia-smi`-Aufruf nach mehreren Minuten Leerlauf ist zulässig,
solange man weiss, dass er den Zustand selbst verändert.

## Offene Punkte

- `powercfg /requests` erhöht ausführen und prüfen, ob eine Energieanforderung
  auf die GPU oder ein NVIDIA-Audiogerät verweist.
- `AppControl.exe` ist keine Gigabyte-Komponente; ob es entbehrlich ist, weiss
  nur der Besitzer.
- Den Energieverwaltungsmodus in der NVIDIA-Systemsteuerung ablesen; er ist die
  einzige der wahrscheinlichen Ursachen, die sich programmatisch nicht
  bestätigen liess.

## Nachmessung 2026-09-03, 15:0x — und eine Korrektur meiner Diagnose

### Was der Besitzer geändert hat

`NVIDIA Broadcast` ist vollständig entfernt: Hauptpaket, alle sechs Container,
Voice Driver und die virtuelle Kamera sind aus der Installationsliste
verschwunden, ebenso der Dienst `NvBroadcast.ContainerLocalSystem`.

Weiterhin installiert: `NVIDIA Virtual Audio 4.65.0.12` mit aktivem Gerät
`NVIDIA Virtual Audio Device (Wave Extensible) (WDM)`, sowie
`NVIDIA ShadowPlay 11.0.8.0`. Ob das Overlay in der NVIDIA App abgeschaltet
wurde, ist von aussen nicht feststellbar; es laufen fünf `NVIDIA App`-Prozesse
und drei `nvcontainer`.

Neu aufgefallen: ein Audio-Endpunkt `MSI MAG401QR (NVIDIA High Definition Audio)`
mit Status `Unknown`. Der externe Monitor ist also über einen Anschluss
angebunden, der an der NVIDIA-GPU hängt. Solange er angeschlossen ist, **kann**
die dGPU grundsätzlich nicht schlafen, weil sie diesen Ausgang treibt. Zum
Messzeitpunkt war er nicht verbunden.

Prozesse mit dediziertem GPU-Speicher: weiterhin keiner.

### Die Messwerte sind unverändert — aber sie taugen nicht als Beweis

```text
P0, 27,39 W, SM 1560 MHz, Speicher 7001 MHz, 0 % Auslastung, Display inaktiv
zweite Probe nach 6 s: P0, 29,72 W, SM 1560 MHz, 0 %
```

Der volle Speichertakt von 7001 MHz statt der üblichen rund 405 MHz im Leerlauf
sah nach einer Fixierung auf Maximalleistung aus. Die Abfrage der
Taktbegrenzungsgründe widerlegt diese Deutung:

```text
clocks_event_reasons.active   = 0x0000000000000001
clocks_event_reasons.gpu_idle = Active
```

Bit `0x1` ist `GpuIdle` und bedeutet laut NVIDIA: auf der GPU läuft nichts und
die Takte sinken in den Leerlaufzustand. Der Treiber betrachtet die Karte also
selbst als im Leerlauf.

**Korrektur:** Die Werte P0/1560/7001/27 W sind mit hoher Wahrscheinlichkeit das
bereits im Abschnitt „Messartefakt" beschriebene Weckverhalten von
`nvidia-smi` — das Werkzeug weckt die Karte und taktet sie für die Abfrage hoch.
Meine Schlussfolgerung „auf P0 festgenagelt" war deshalb nicht belegt, und die
Suche nach einer Ursache dafür war voreilig. Ich habe das Artefakt vorher selbst
dokumentiert und bei der Auswertung dann ignoriert.

Was daraus folgt: Über den tatsächlichen Leerlaufverbrauch der dGPU ist mit
`nvidia-smi` **keine** Aussage möglich. Weder vorher noch nachher.

### Die verbleibende gültige Messung

Am Netz liefert `BatteryStatus.DischargeRate` konstant `0`, weil nicht entladen
wird. Die Messung ist deshalb nur im Akkubetrieb möglich:

```powershell
powershell -Command "(Get-CimInstance -Namespace root\wmi -ClassName BatteryStatus).DischargeRate"
```

Vorgehen: Netzteil abziehen, Bildschirmhelligkeit fest einstellen, eine Minute
Leerlauf, dann mehrere Proben. Der Wert ist die **Gesamt**-Systemleistung in
Milliwatt, nicht die der GPU allein.

Grobe Einordnung für dieses Gerät: Panel und CPU im Leerlauf liegen zusammen
typischerweise bei etwa 10 bis 15 W. Ein Wert in dieser Grössenordnung bedeutet,
dass die dGPU schläft. Ein Wert um 35 bis 45 W bedeutet, dass sie wach ist.
Zwischen zwei Zuständen vergleicht man am besten dieselbe Prozedur zweimal.

## ERGEBNIS: RTD3 funktioniert, es gibt kein Problem

Gemessen im Akkubetrieb über `BatteryStatus.DischargeRate`, also am
Gesamtsystemverbrauch und nicht mit `nvidia-smi`. Der Weckreiz wurde bewusst
erzeugt, indem `nvidia-smi` einmal aufgerufen wurde — das Artefakt wird damit
vom Störfaktor zum Messinstrument.

| Phase | Proben (mW) | Mittel |
| --- | --- | --- |
| A Basislinie, Leerlauf | 19755, 21102, 21102, 21102, 21102 | **20,8 W** |
| B direkt nach `nvidia-smi` | 21102, 21102, 43026, 43026, 43026 | **34,3 W** |
| C nach 60 s Ruhe ohne GPU-Abfrage | 21503 ×5 | **21,5 W** |

Deltas: `B − A = +13,4 W` im Mittel, in der Spitze `43,0 − 21,1 = +21,9 W`.
`C − A = +0,7 W`, also innerhalb der Messstreuung.

Daraus folgt dreierlei:

1. **Das Aufwecken der dGPU kostet rund 22 W.** Der Sprung von 21,1 auf 43,0 W
   ist unmittelbar und ausschliesslich durch den `nvidia-smi`-Aufruf verursacht.
2. **Die dGPU schläft von selbst wieder ein.** Nach 60 Sekunden ohne Abfrage
   liegt der Verbrauch wieder auf der Basislinie.
3. **RTD3 arbeitet auf diesem Gerät korrekt.** Es gibt nichts zu reparieren.

### Damit sind alle vorherigen Messwerte erklärt

Die durchgehend beobachteten „27 bis 30 W bei P0" waren vollständig das
Weckartefakt. Jeder `nvidia-smi`-Aufruf hat genau den Zustand erzeugt, den er
zu messen vorgab. Die Suche nach einer Ursache für eine Fixierung auf P0 war
gegenstandslos.

Ebenfalls entwertet: `power.draw` ist auf diesem Gerät unbrauchbar. Ein Lauf
meldete für die GPU allein 26,16 W, während der **Gesamtverbrauch** des Systems
zeitgleich 26,4 W betrug. Der Momentanwert kann also nicht stimmen; die
Durchschnittsanzeige lieferte 0,00 W.

### Bewertung der durchgeführten Änderungen

Die Deinstallation von `NVIDIA Broadcast` war nach dieser Messung
**wahrscheinlich unnötig**. Das lässt sich nicht mehr rückwirkend prüfen, weil
vor der Deinstallation keine gültige Messung vorliegt — nur artefaktbehaftete
`nvidia-smi`-Werte. Wer Broadcast braucht, kann es ohne Bedenken wieder
installieren und die Messung anschliessend wiederholen.

### Der einzige reale Fall, in dem die dGPU wach bleibt

Der Audio-Endpunkt `MSI MAG401QR (NVIDIA High Definition Audio)` zeigt, dass der
externe Monitor an einem Anschluss hängt, der auf die NVIDIA-GPU verdrahtet ist.
Ist dieser Monitor angeschlossen, treibt die dGPU dessen Ausgang und kann
konstruktionsbedingt nicht schlafen. Die Kosten dafür entsprechen der gemessenen
Weckdifferenz, also etwa 22 W. Das ist keine Fehlfunktion und mit Software nicht
behebbar.

### Praktische Folgerungen

- Treiber aktiviert lassen. Die 25 W im deaktivierten Zustand waren der
  Fehlerfall, nicht der Normalzustand.
- **Nicht mit `nvidia-smi` überwachen.** Jeder Aufruf kostet rund 22 W und
  verhindert RTD3, solange man pollt. Für ein Monitoring-Werkzeug heisst das:
  die dGPU-Leistung darf nicht zyklisch abgefragt werden.
- Die verbleibende Basislinie von etwa 21 W ist **nicht** die GPU. Sie besteht
  aus Panel, CPU und laufenden Hintergrundanwendungen. Wer sie senken will,
  muss dort ansetzen, nicht an der Grafikkarte.

### Konsequenz für die Anwendung

Eine GPU-Seite in `AorusControl` darf die dGPU nicht periodisch abfragen, weil
sie damit genau den Verbrauch verursacht, den sie anzeigen soll. Zulässig sind
nur nicht weckende Quellen: die Windows-Leistungsindikatoren
`\GPU Process Memory(*)\Dedicated Usage` und
`\GPU Adapter Memory(*)\Dedicated Usage`, die Geräte- und Anzeigeinventur und
optional `BatteryStatus.DischargeRate`. Eine Anzeige von P-State oder
GPU-Leistungsaufnahme über `nvidia-smi` ist bewusst zu unterlassen.

## Bezug zum übrigen Projekt

Der herstellerseitige Weg über Gigabytes WMI ist auf FB0F versperrt:
`GetNvPowerConfig`, `GetPEG2orSG2` und `getAiPowerCtlCapability` werden
abgewiesen, und `GetPEGorSG` spiegelt nur den Lüfter-Duty-Wert. Es gibt keinen
lesbaren GPU-Power-Zustand und damit keine Grundlage, `SetNvPowerConfig` zu
schreiben. Details in `FAN-POWER-GPU-CONTROL.md`.

Für die Anwendung folgt daraus: eine **Diagnoseseite** statt eines Schalters —
anzeigen, welche Prozesse und Geräte die RTX wachhalten, plus P-State und
Leistungsaufnahme. Das ist reines Lesen und adressiert die tatsächliche Ursache.
