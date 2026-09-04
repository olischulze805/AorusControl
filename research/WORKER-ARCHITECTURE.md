# Hardware-Worker und Absturzsicherheit

Stand: 2026-09-04. Beschreibt, wie `AorusControl.Worker` und `AorusControl.App`
zusammenspielen, damit ein abgestürzter oder getöteter App-Prozess niemals eine
nicht-adaptive Lüftereinstellung dauerhaft hinterlässt.

## Fehler: „Der Hardware-Worker hat nicht rechtzeitig geantwortet" beim ersten Fixed-Versuch

Vom Besitzer im echten Betrieb gefunden. Festgehalten, weil genau der Teil, der gesund
aussah, die Ursache war.

`WorkerClient.IsRunning()` hat den Worker geprüft, indem es sich **tatsächlich verbunden**
und die Verbindung ohne zu senden verworfen hat. Der Worker bedient genau eine Verbindung
gleichzeitig (`maxNumberOfServerInstances: 1`), nahm diese Sondierung also für einen echten
Client: Er ging in seinen Lesevorgang und wartete auf eine Anfrage von einer Gegenstelle,
die längst weg war. Die echte Anfrage direkt danach stand dann hinter einem Server, der so
lange belegt war wie das Timeout des Aufrufers — die App meldete einen Worker, der „nicht
rechtzeitig antwortet", während er vollkommen gesund war und die Sondierung, die die
Anfrage vorbereiten sollte, ihr genau den Platz nahm.

Drei Dinge waren falsch, alle drei sind behoben:

1. **Die Sondierung verbindet sich nicht mehr.** `IsRunning` durchsucht `\\.\pipe\` und
   schaut nur; die Erkennung hat keine Nebenwirkung auf den Accept-Slot des Servers.
   `WorkerDiscoveryTests` prüft exakt das: nach drei `IsRunning`-Aufrufen muss die nächste
   Anfrage noch das Erste sein, was der Server überhaupt sieht.
2. **Eine abgelehnte Anfrage wird beantwortet.** Die Serve-Schleife hat bei ungültigen oder
   unlesbaren Anfragen nur geloggt und getrennt — von einem Hänger auf der anderen Seite
   nicht zu unterscheiden. Ein Protokoll- oder Feldfehler erschien damit als Timeout ohne
   jeden Hinweis. Jetzt geht eine Fehlerantwort zurück, die das Problem benennt, und der
   Lesevorgang hat eine eigene kurze Frist, damit eine Verbindung ohne Inhalt den Slot
   nicht für das gesamte Aufrufer-Timeout blockieren kann.
3. **Im Entwicklungsbaum wird keine veraltete Binärdatei mehr bevorzugt.**
   `WorkerLauncher.FindExecutable` hat bedingungslos `Release` vorgezogen, sodass ein Tage
   alter Release-Worker gegen frisch gebauten App-Code gestartet wurde. Jetzt gewinnt die
   zuletzt gebaute Konfiguration. (In einer Installation existiert nur ein Kandidat — das
   betraf also ausschließlich die Entwicklung, und genau dort hat es zugeschlagen.)

Das von außen zu diagnostizieren war schwerer als nötig. Deshalb schreiben App und Worker
jetzt beide nach `%LocalAppData%\AorusControl\logs`
(`AorusControl.Core.Features.Diagnostics.AppLog`): Der Worker läuft erhöht und ohne
sichtbares Fenster, seine Seite eines gescheiterten Austauschs hinterließ vorher überhaupt
keine Spur. Der Launcher protokolliert, welche Datei er startet und wie lange die Pipe
brauchte; der Lease-Client protokolliert jede Operation und ihr Ergebnis. In der App führt
„Info & Updates → Protokoll" direkt zum Ordner.

## Kernidee

`FanSafetySupervisor` (in `AorusControl.Core.Features.Cooling`) existierte
bereits vollständig implementiert und getestet, wurde aber nirgends
instanziiert. Die App schrieb Fixed-Lüfterwerte bisher direkt über
`GigabyteWmiFanController`, mit einer eigenen, einfacheren Absicherung im
WPF-Dispatcher-Timer der Oberfläche. Diese Absicherung lebte im selben Prozess
wie die Bedienoberfläche — killt man `AorusControl.exe`, stirbt auch die
Absicherung, und keine Instanz stellt Normal wieder her.

Die Lösung verlagert **ausschliesslich den Fixed-Modus** (der einzige
nicht-adaptive, potenziell "hängenbleibende" Zustand) in den unabhängigen
Hintergrundprozess `AorusControl.Worker`. Alle anderen Profile (Normal, Quiet,
Gaming, Maximum, Dynamic) bleiben adaptive Firmwaremodi und werden weiterhin
direkt aus der App geschrieben — sie benötigen keine Lease, weil sie sich
selbst nicht "verabschieden" können.

## Ablauf

1. Die App ruft `SetFixedFanAsync()` auf. Zuerst `WorkerLauncher.EnsureRunningAsync()`:
   prüft per Named-Pipe-Verbindungsversuch, ob ein Worker läuft; falls nicht,
   startet sie `AorusControl.Worker.exe --serve` als **losgelösten** Prozess
   (kein Job-Object, keine Eltern-Kind-Bindung) und wartet bis zu 3 Sekunden auf
   Erreichbarkeit.
2. `WorkerFixedFanLeaseClient.AcquireAsync(rawValue)` sendet
   `WorkerOperation.AcquireFixedFan` über die Pipe. Der Worker validiert
   Temperatur/Kompatibilität, schreibt den Festwert über seine eigene
   `GigabyteWmiFanController`-Instanz und gibt eine `Guid`-Freigabe (Lease)
   zurück.
3. Solange Fixed aktiv ist, verlängert die App die Freigabe bei jedem
   2-Sekunden-Timer-Tick (`RenewFixedFan`). Der Worker validiert bei jeder
   Verlängerung die Temperatur erneut, mit **seiner eigenen** Telemetrie —
   unabhängig davon, ob die App noch korrekt liest.
4. **Stirbt die App**, bleibt der Worker unberührt am Leben. Seine eigene
   `FanSafetySupervisor.RunAsync`-Schleife (2-Sekunden-`PeriodicTimer`) prüft
   unabhängig von jeder Client-Verbindung, ob die 10-Sekunden-Freigabe
   abgelaufen ist. Läuft niemand mehr zur Verlängerung ein, stellt der Worker
   spätestens nach ~12 Sekunden **von sich aus** Normal wieder her — ganz ohne
   Mitwirkung der toten App.
5. Bei sauberem Beenden (Stop-Knopf, Profilwechsel, Fenster schliessen)
   versucht die App zusätzlich ein explizites `ReleaseFixedFan`, damit die
   Wiederherstellung sofort statt erst nach Ablauf der Freigabe erfolgt. Das
   ist reine Bequemlichkeit — schlägt es fehl, greift trotzdem Punkt 4.

## Wichtige Entwurfsentscheidung: keine Wiederholung durch die App

Frühere Version: Bei einem fehlgeschlagenen Rückstellversuch blieb die App im
Zustand "ACHTUNG" hängen und wiederholte den Schreibversuch bei jedem
Timer-Tick selbst.

Neue Version: Die App unternimmt **nie** einen zweiten Rettungsversuch. Schlägt
`ReleaseAsync`/`RenewAsync` fehl, gibt die App ihren eigenen Anspruch auf Fixed
sofort auf (`_fixedFanActive = false`), zeigt die Fehlermeldung an und verlässt
sich vollständig darauf, dass der Worker selbst über `TickAsync` unbegrenzt oft
erneut versucht, sobald `_requiresRestoration` gesetzt ist. Diese Trennung ist
sauberer: Wiederherstellung ist ausschliesslich Aufgabe des Prozesses, der die
Garantie tatsächlich halten kann.

## Testarchitektur

`FanSafetySupervisor`s eigene Regeln (Lease-Ablauf, Temperaturprüfung,
fehlgeschlagene-Wiederherstellung-wird-wiederholt) sind bereits erschöpfend in
`tests/AorusControl.App.SmokeTests/FanSupervisorTests.cs` mit einem virtuellen
`TimeProvider` abgedeckt.

Die `MainWindowViewModel`-Tests verwenden einen `InProcessFixedFanLeaseClient`,
der denselben `FanSafetySupervisor` **im selben Prozess** um dieselben
Test-Doubles (`FakeFan`, `FakeReader`) wickelt. Das hält die Tests schnell und
deterministisch, ohne echte Named Pipes, und testet auf dieser Ebene bewusst
nur die **Orchestrierung** (ruft die ViewModel die richtigen Methoden zur
richtigen Zeit auf, spiegelt sie Fehler korrekt wider) statt die bereits
anderswo getesteten Kernregeln zu wiederholen. Die tatsächliche
Absturzsicherheit — dass der Worker einen echten Prozessabsturz übersteht —
ist strukturell nur über einen echten Prozesstest nachweisbar, siehe unten.

## Zwei echte Fehler, gefunden beim Testen

**Fehler 1 — WorkerLauncher gehoerte nicht ins ViewModel.** Der erste Entwurf
rief `WorkerLauncher.EnsureRunningAsync()` direkt in
`MainWindowViewModel.SetFixedFanAsync()` auf, unabhaengig davon, welcher
`IFixedFanLeaseClient` injiziert war. In den Unit-Tests (die einen
In-Process-Fake verwenden, gerade um echte Prozesse zu vermeiden) fuehrte das
dazu, dass tatsaechlich versucht wurde, einen echten `AorusControl.Worker.exe`
zu suchen und zu starten — mit Seiteneffekt: ein echter, herrenloser
Worker-Prozess blieb nach dem Testlauf zurueck, unelevated, auf dem
Produktions-Pipenamen. Schlug der Start fehl oder dauerte die Verbindung zu
lang, brach der GANZE Aufruf ab, **bevor** der injizierte Fake ueberhaupt
erreicht wurde — der Fake-Client wurde faktisch nie benutzt.

Behoben durch Verschieben der Verantwortung: `WorkerFixedFanLeaseClient.AcquireAsync`
ruft `WorkerLauncher` jetzt selbst auf, nicht das ViewModel. Das ViewModel
kennt nur noch `IFixedFanLeaseClient` und weiss nichts von "Workern". Eine
Lehre fuers Gesamtprojekt: Eine Implementierungsdetail-Abhaengigkeit (hier:
"es gibt einen Hintergrundprozess") darf nie ueber die Abstraktionsgrenze nach
aussen dringen, sonst wird jeder Test-Double zur Farce.

**Fehler 2 — echte Wanduhr in Tests.** Der zweite Entwurf startete
`FanSafetySupervisor.RunAsync()` in jedem Test als echten Hintergrund-Task mit
`TimeProvider.System`, nur um die private `_running`-Sperre zu erfuellen. Unter
Systemlast (parallele Builds) kollidierte der echte 2-Sekunden-Timer
gelegentlich mit dem Testablauf und erzeugte einen einzigen, nicht
reproduzierbaren Fehlschlag. Nach Beheben von Fehler 1 trat derselbe Test
ploetzlich **deterministisch** fehlgeschlagen auf — der Timing-Fehler hatte den
eigentlichen, in Fehler 1 liegenden Bug bisher nur zufaellig manchmal verdeckt.

Behoben durch Verzicht auf den echten Hintergrund-Task: `_running` wird in den
Tests direkt per Reflection gesetzt (passend zum bereits vorhandenen
`Invoke()`-Muster fuer private Methoden in dieser Testdatei). Acht
aufeinanderfolgende Laeufe bestaetigen Determinismus.

## Bekannter Altlast-Prozess

Waehrend der Fehlersuche blieb durch Fehler 1 ein echter, unelevated
`AorusControl.Worker.exe`-Prozess auf dem Produktions-Pipenamen zurueck, der
sich aus dieser Automatisierungsumgebung nicht beenden liess
(Zugriff-verweigert-Fehler, auch mit `taskkill /F`). Wegen
`PipeOptions.FirstPipeInstance` blockiert er jeden spaeteren echten, elevated
Worker auf demselben Pipenamen. **Vor dem ersten echten Test von Fixed-Modus
muss dieser Prozess ueber den Task-Manager beendet werden**, sonst schlaegt
jede Verbindung mit einer verwirrenden Fehlermeldung fehl, die nichts mit dem
eigentlichen Code zu tun hat.

## Manueller Abnahmetest

1. Worker und App sauber beenden, sicherstellen, dass kein Prozess läuft.
2. App starten, Fixed-Lüfter über die Oberfläche aktivieren.
3. Prüfen: `AorusControl.Worker.exe --fan-status` liefert
   `FanRequiresRestoration: true` und eine Lease-ID.
4. `AorusControl.exe` **hart** beenden (Task-Manager oder `taskkill /F`), ohne
   über die Oberfläche zu schliessen.
5. Mindestens 12 Sekunden warten.
6. Erneut `--fan-status` abfragen: erwartet `FanRequiresRestoration: false`.
7. Unabhängig verifizieren: App neu starten, `LoadFanAsync()`s eigene,
   unprivilegierte Anzeige sollte "Normal" zeigen — ein von der Worker-eigenen
   Zustandsvermutung unabhängiger, echter Hardware-Rücklese-Pfad.

## Was absichtlich unverändert blieb

- Normal/Quiet/Gaming/Maximum/Dynamic bleiben direkte, ungeleaste Schreibungen
  aus der App. Sie sind adaptive Firmwaremodi ohne "hängenbleiben"-Risiko.
- `_restoreNormalFanOnDispose` (Best-Effort-Aufräumen für Maximum/Dynamic beim
  Schliessen) bleibt bestehen, unverändert von der neuen Lease-Logik.
- Der Worker ist weiterhin **kein installierter Windows-Dienst und kein
  Autostart-Eintrag** — er wird aktuell erst bei Bedarf (erste Fixed-Anfrage)
  von der App losgelöst gestartet. Für "läuft schon beim Windows-Start"
  bräuchte es zusätzlich einen Task-Scheduler-Eintrag oder eine echte
  Dienstregistrierung — bewusst nicht Teil dieser Änderung.

## Bekannte Grenzen

- Der Worker selbst hat noch keinen eigenen Watchdog. Stürzt der Worker
  *während* Fixed aktiv ist (statt die App), bleibt die Firmware im
  Fixed-Zustand hängen, bis jemand `Start-FanNormalRestore.cmd` ausführt oder
  die App neu startet und selbst einen neuen Worker hochfährt. Ein
  produktionsreifer Aufbau bräuchte einen zweiten, unabhängigen
  Watchdog-Prozess oder einen echten Windows-Dienst mit
  Neustart-bei-Absturz-Richtlinie (`sc.exe failure`).
- `WorkerLauncher.FindExecutable()` sucht im Entwicklungsbaum nach
  `AorusControl.slnx` und dann `src/AorusControl.Worker/bin/<Release|Debug>/...`.
  Für eine echte Installation muss der Worker neben der App oder in einem
  bekannten Unterordner mitausgeliefert werden.
