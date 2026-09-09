# GPU-Präferenzen abhängig von Netz/Akku: Machbarkeit

Stand 2026-09-09. Nur Code-/Registry-Lesen und Webrecherche; keine Einstellungen geändert.

## Ergebnis

Automatische Umschaltung der bevorzugten GPU für ausgewählte Programme ist technisch plausibel und implementierbar. Eine funktionierende End-to-End-Umschaltung wurde noch nicht getestet. Keine harte RTX-Sperre, kein Wechsel bereits laufender Grafikgeräte garantiert.

## Umgesetzt am 2026-09-09

Karte „Grafikkarte je nach Stromquelle" unter Leistung & Akku. Ausgewählte Programme laufen am Netz auf der RTX und im Akkubetrieb auf der Intel-Grafik, damit die RTX in ihrem Schlafzustand bleibt.

- Geschrieben wird ausschließlich Windows' eigener Wert unter `HKCU\Software\Microsoft\DirectX\UserGpuPreferences` - derselbe, den die Windows-Einstellungen setzen. Keine NVIDIA-Schnittstelle, kein Treibereingriff, kein Abschalten der Karte.
- Der Eintrag wird tokenweise geändert: nur `GpuPreference`, alles andere (`AutoHDREnable`, `SwapEffectUpgradeEnable`, unbekannte Tokens) bleibt unverändert und in seiner Reihenfolge stehen. Ein Eintrag mit `SpecificAdapter` wird angezeigt, aber nie geschrieben.
- Umgeschaltet wird bei Stromquellenwechsel über `SystemEvents.PowerModeChanged`, also auch bei geschlossenem Fenster aus dem Infobereich heraus. Unbekannte Stromquelle ändert nichts.
- Was der Nutzer selbst in Windows umstellt, wird erkannt (Vergleich mit dem zuletzt selbst geschriebenen Wert) und nicht überschrieben; die Karte zeigt es an. Beim Entfernen aus der Liste wird die ursprüngliche Einstellung zurückgegeben, sofern sie noch die unsrige ist.
- Grenzen, die die Oberfläche auch benennt: Eine Präferenz wirkt erst beim nächsten Start des Programms und bewegt kein laufendes. Programme, die ihre Grafikkarte selbst wählen (CUDA, expliziter Adapter), ignorieren sie.
- Die App läuft erhöht; HKCU zeigt dabei auf dasselbe Benutzerprofil, solange die Erhöhung mit demselben Konto erfolgt. Würde sie mit einem anderen Administratorkonto erhöht, landeten die Einträge in dessen Profil.
- Tests: Tokenbehandlung inklusive der echten VLC-Zeile dieses Rechners, Registry-Rundlauf in einem eigenen Testschlüssel, Netz-/Akku-Plan, manuelle Änderung, unbekannte Quelle, Automatik aus, Entfernen. Die Smoke-Tests fassen die echten Zuordnungen des Benutzers nicht an - dafür gibt es Attrappen.

Noch offen: der End-to-End-Nachweis, dass ein umgeschaltetes Programm tatsächlich auf dem anderen Chip startet. Der unten vorgeschlagene DXGI-Test steht weiterhin aus.

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
