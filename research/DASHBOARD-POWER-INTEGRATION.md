# Dashboard: CPU-Watt und Windows-GPU-Status

Stand: 2026-09-09.

## Starter-Korrektur

Start-AorusControl.ps1 verwendete eine vorhandene Release-EXE vom 5. September und baute nur bei fehlender Datei. Die neue Dashboard-Funktion war zunächst nur im Debug-Build vorhanden. Der Starter baut jetzt vor jedem Start die gesamte Solution inkrementell in Release, einschließlich Worker, und startet bei Buildfehlern keine alte Version. Der erste Release-Versuch nach dieser Korrektur wurde durch die laufende AorusControl.exe (PID 11324) blockiert. Vor erneutem Start die App über das Tray-Menü vollständig beenden; Fenster-X versteckt die App lediglich.

## Implementiert

- CPU-Kachel: CPU-Paketleistung aus Windows Energy Meter (RAPL_Package0_PKG), auf eine Nachkommastelle gerundet. Enthält die integrierte Grafik; keine Summe über überlappende RAPL-Domänen.
- NVIDIA-Kachel: zuletzt von Windows gemeldeter Gerätezustand D0, D1, D2 oder D3. Fehlende/ungültige Daten werden ausdrücklich als unbekannt angezeigt. Mehrere NVIDIA-Adapter ergeben keine geratenen Einzelwerte.
- Der neue DashboardPowerReader nutzt ausschließlich Windows PerformanceCounter und SetupAPI. Kein NVML, NVAPI, nvidia-smi oder GPU-Gerätehandle.
- ReadCategory liest CPU-Energie. SetupDiGetClassDevs mit DIGCF_PRESENT und Display-Klassenguid enumeriert vorhandene Grafikgeräte; Hardware-ID VEN_10DE identifiziert NVIDIA. SPDRP_DEVICE_POWER_DATA enthält die CM_POWER_DATA-Struktur.
- Strukturgröße und Datentyp werden geprüft. DEVICE_POWER_STATE ist einsbasiert: 1=D0, 4=D3. D3 unterscheidet hier nicht D3hot/D3cold und belegt keine physikalischen 0 W.
- Nur bei sichtbarem Dashboard werden die Zusatzwerte abgefragt; Threadpool statt UI-Thread, vorhandenes Intervall zwei Sekunden. Vor Veröffentlichung wird Sichtbarkeit erneut geprüft. Alte Werte werden bei Seitenwechsel, Verstecken, Stoppen oder Telemetriefehler gelöscht.
- Erste CPU-Probe, Abstand über fünf Sekunden, zurückgesetzte Energie, nicht fortschreitende Basiszeit und ungültige Wattwerte werden verworfen. Ein Fehler dieser Zusatzanzeige löst keinen Lüftermoduswechsel aus.
- Vorhandene Gigabyte-Temperatur-/Lüftertelemetrie bleibt unverändert. Der isolierte neue Sensorpfad wurde live geprüft; diese Prüfung ist kein Nachweis des Energieverhaltens sämtlicher bereits bestehender App-Abfragen.

## Warum keine bedingt abgefragten GPU-Watt?

Ein D0-Check unmittelbar vor NVML ist keine Garantie: Die Karte kann zwischen Check und Sensorzugriff einschlafen; außerdem kann Polling ihr Einschlafen verhindern. Die neue Funktion verzichtet daher auch bei D0 auf GPU-Watt. Ein belastbarer Ansatz dafür benötigt weitere treiberspezifische Untersuchungen und einen Akkuvergleich. Der Nutzerwunsch, die Karte durch diese Anzeige nicht aufzuwecken, hat Vorrang.

G-Helper wurde als Referenz gelesen: dort wird GetCurrentPerformanceState mit NVAPI_GPU_NOT_POWERED ausgewertet, bevor GPU-Watt abgefragt werden. Das beweist die Nicht-Aufweckwirkung auf unserem AORUS mit Treiber 616.64 nicht. Kein Fremdcode übernommen.

## Tests und Live-Ergebnis

- Solution-Build erfolgreich, null Warnungen/Fehler.
- Smoke-Suite einschließlich neuer Prüfungen: D0/D3-Abbildung, unbekannt/abgeschnittene Daten, ungültige/reset CPU-Zähler, Sichtbarkeits-Gate, Veröffentlichung und Löschen alter Werte.
- WPF-Layoutprüfung an allen bisherigen Breiten erfolgreich; Dashboard bei 720 Pixeln visuell geprüft.
- Reproduzierbarer nur lesender Check:

```powershell
dotnet run --project tests/AorusControl.HardwareChecks -- --dashboard-power-read-only
```

Bericht: runs/dashboard-power-20260909-183153.md. Acht Proben über rund 14 Sekunden: NVIDIA in allen Proben D3, CPU nach Basisprobe 26,224–48,640 W. Parallel liefen Builds/Tests; kein CPU-Leerlauf-Benchmark. Erste Initialisierung 599,50 ms; Folgeabfragen 0,54–2,69 ms.

Dies spricht dagegen, dass die neue Abfrage die RTX dauerhaft nach D0 versetzt. Sehr kurze unbeobachtete Übergänge werden damit nicht ausgeschlossen. Kein aktueller Akkuvergleich, kein gezielter D0→D3-Übergang provoziert, keine NVIDIA-Sensorabfrage in diesem Umsetzungsschritt. Keine Neuinstallation der zuvor entfernten App.

## Quellen

- Microsoft, Windows setzt PowerData und liefert CM_POWER_DATA: https://learn.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-powerdata
- Microsoft, most recent power state: https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdm/ns-wdm-cm_power_data_s
- Vergleich G-Helper: https://github.com/seerge/g-helper/blob/main/app/Gpu/NVidia/NvidiaGpuControl.cs
- Lokale Einheiten und vorherige CPU-Kontrollrechnung: CPU-GPU-WATTS-20260909.md.
