using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows.Input;
using MediaBrush = System.Windows.Media.Brush;
using AorusControl.App.Features.Cooling;
using AorusControl.App.Features.Keyboard;
using AorusControl.App.Features.Updates;
using AorusControl.App.Infrastructure;
using AorusControl.Core.Models;
using AorusControl.Core.Services;
using AorusControl.Core.Features.Cooling;
using AorusControl.Core.Features.Diagnostics;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Features.Startup;
using AorusControl.Core.Features.Worker;

namespace AorusControl.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IAorusTelemetryReader _reader;
    private readonly WindowsPowerOverlayController _powerOverlay;
    private readonly DispatcherTimer _timer;
    private bool _isReading;
    private bool _isRunning;
    private bool _dashboardVisible = true;
    private string _cpuTemperature = "-- °C";
    private string _gpuTemperature = "-- °C";
    private string _cpuFan = "-- U/min";
    private string _gpuFan = "-- U/min";
    private string _cpuDuty = "Rohwert --";
    private string _gpuDuty = "Rohwert --";
    private string _status = "Bereit";
    private string _powerSource = "Stromquelle wird gelesen …";
    private string _lastUpdated = "Noch keine Messung";
    private string _toggleButtonText = "Überwachung starten";
    private bool _closing;
    private bool _starting;
    private bool _powerControlsEnabled;
    private bool _powerBusy;
    private string _powerStatus = "Windows-Leistungsmodus wird gelesen …";
    private readonly IStartupManager _startupManager;
    private bool _startWithWindows;
    private bool _startupBusy;
    private string _startupStatus = "Autostart wird geprüft …";

    public MainWindowViewModel()
        : this(
            new GigabyteWmiTelemetryReader(),
            new GigabyteHidKeyboardRgbController(),
            new GigabyteWmiFanController(),
            new WindowsPowerOverlayController(),
            keyboardSettingsStore: new KeyboardSettingsStore(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AorusControl", "keyboard-v1.json")),
            brightnessListener: new KeyboardBrightnessNotifications().RunAsync,
            fanCurveStore: new FanCurveStore(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AorusControl", "fan-curve-v1.json")),
            startupManager: new StartupManager(Environment.ProcessPath ?? throw new InvalidOperationException("Prozesspfad unbekannt.")))
    {
    }

    internal MainWindowViewModel(
        IAorusTelemetryReader reader,
        IAorusKeyboardRgbController keyboardRgb,
        IAorusFanController fanController,
        WindowsPowerOverlayController powerOverlay,
        IAorusBatteryChargeController? batteryController = null,
        IKeyboardSettingsStore? keyboardSettingsStore = null,
        Func<Action<KeyboardBrightnessLevel>, CancellationToken, Task>? brightnessListener = null,
        IFixedFanLeaseClient? fixedFanLeaseClient = null,
        IFanCurveStore? fanCurveStore = null,
        IStartupManager? startupManager = null,
        Func<TimeSpan, Task>? resumeReapplyDelay = null,
        Func<TimeSpan, CancellationToken, Task>? debounceWait = null)
    {
        _reader = reader;
        Keyboard = new KeyboardViewModel(keyboardRgb, keyboardSettingsStore, brightnessListener, resumeReapplyDelay);
        Cooling = new CoolingViewModel(
            fanController,
            // Defaults to the real out-of-process worker client: only that implementation
            // survives this process crashing, which is the entire point of Fixed-mode safety.
            fixedFanLeaseClient ?? new WorkerFixedFanLeaseClient(),
            fanCurveStore ?? new FanCurveStore(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AorusControl", "fan-curve-v1.json")),
            RefreshAsync,
            StartMonitoring,
            debounceWait);
        _startupManager = startupManager ?? new StartupManager(Environment.ProcessPath ?? "AorusControl.exe");
        Battery = new BatteryViewModel(batteryController ?? new GigabyteWmiBatteryChargeController(), debounceWait);
        Updates = new UpdateViewModel();
        _powerOverlay = powerOverlay;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += OnTimerTick;
        ToggleMonitoringCommand = new RelayCommand(ToggleMonitoring);
        SetPowerModeCommand = new AsyncRelayCommand<string>(modeName =>
            Enum.TryParse(modeName, ignoreCase: true, out WindowsPowerOverlayMode mode)
                ? SetWindowsPowerModeAsync(mode)
                : Task.CompletedTask);
        ToggleStartWithWindowsCommand = new AsyncRelayCommand(() => SetStartWithWindowsAsync(!StartWithWindows));
    }

    private string? _activePowerMode;
    private string _selectedSection = "Dashboard";

    /// <summary>Which navigation section is visible. Pure UI state - no hardware
    /// implication - kept here rather than split into one ViewModel per page, since
    /// every section already shares this same ViewModel and its live device state.</summary>
    public string SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value)) UpdateModuleVisibility();
        }
    }

    public ICommand ToggleMonitoringCommand { get; }
    public AsyncRelayCommand<string> SetPowerModeCommand { get; }
    public AsyncRelayCommand ToggleStartWithWindowsCommand { get; }
    /// <summary>The attached feature modules. Everything the shell does to all of them -
    /// start them, wait for them, release them - goes through this list, so a new feature
    /// is a class plus one entry rather than another branch in three methods.</summary>
    private IReadOnlyList<IFeatureModule> Modules => [Keyboard, Cooling, Battery];

    public KeyboardViewModel Keyboard { get; }
    public CoolingViewModel Cooling { get; }
    public BatteryViewModel Battery { get; }
    public UpdateViewModel Updates { get; }

    public string CpuTemperature { get => _cpuTemperature; private set => SetProperty(ref _cpuTemperature, value); }
    public string GpuTemperature { get => _gpuTemperature; private set => SetProperty(ref _gpuTemperature, value); }
    public string CpuFan { get => _cpuFan; private set => SetProperty(ref _cpuFan, value); }
    public string GpuFan { get => _gpuFan; private set => SetProperty(ref _gpuFan, value); }
    public string CpuDuty { get => _cpuDuty; private set => SetProperty(ref _cpuDuty, value); }
    public string GpuDuty { get => _gpuDuty; private set => SetProperty(ref _gpuDuty, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>Netz- oder Akkubetrieb. On the dashboard because it explains the rest: the
    /// power modes are only verified on AC, and the charge limit only matters off it.</summary>
    public string PowerSource { get => _powerSource; private set => SetProperty(ref _powerSource, value); }
    public string LastUpdated { get => _lastUpdated; private set => SetProperty(ref _lastUpdated, value); }
    public string ToggleButtonText { get => _toggleButtonText; private set => SetProperty(ref _toggleButtonText, value); }

    public bool PowerControlsEnabled { get => _powerControlsEnabled; private set => SetProperty(ref _powerControlsEnabled, value); }
    public string PowerStatus { get => _powerStatus; private set => SetProperty(ref _powerStatus, value); }

    /// <summary>Highlighted power-mode chip, again from Windows' own readback rather
    /// than from the last click.</summary>
    public string? ActivePowerMode
    {
        get => _activePowerMode;
        private set
        {
            if (!SetProperty(ref _activePowerMode, value)) return;
            OnPropertyChanged(nameof(PowerModeEffect));
            OnPropertyChanged(nameof(ActivePowerModeLabel));
        }
    }

    /// <summary>
    /// Spells out what the running mode actually changes. Tools in this category routinely
    /// imply that a "performance mode" also drives the fans; here it does not - the fan
    /// curve is ours, on the EC - and saying so plainly is worth more than a louder label.
    /// </summary>
    /// <summary>The running mode in the same words as its chip, for the dashboard tile.</summary>
    public string ActivePowerModeLabel => _activePowerMode switch
    {
        "BestEfficiency" => "Energieeffizienz",
        "BestPerformance" => "Beste Leistung",
        "Balanced" => "Ausbalanciert",
        _ => "Noch nicht gelesen"
    };

    public string PowerModeEffect => _activePowerMode switch
    {
        "BestEfficiency" =>
            "Windows hält Takt und Boost niedrig und schiebt Last bevorzugt auf sparsame Kerne. Weniger Abwärme, dadurch drehen die Lüfter meist niedriger - die Kurve selbst bleibt unverändert.",
        "BestPerformance" =>
            "Windows lässt Boost länger zu und hält die Taktziele hoch. Mehr Abwärme, dadurch erreicht die Kurve ihre höheren Stufen früher - die Kurve selbst bleibt unverändert.",
        "Balanced" =>
            "Windows regelt Takt und Boost nach Last. Die Lüfterkurve bleibt unverändert; sie reagiert nur auf die Temperatur, die sich dadurch ergibt.",
        _ => "Noch kein Modus gelesen."
    } + " Nur für den Netzbetrieb getestet; GPU-Limits und EC-Einstellungen fasst Windows dabei nicht an.";



    /// <summary>Where the log files are, so "look in the log" is an actionable
    /// instruction rather than a scavenger hunt.</summary>
    public string LogDirectory => AppLog.Directory;



    public bool StartWithWindows { get => _startWithWindows; private set => SetProperty(ref _startWithWindows, value); }
    public string StartWithWindowsButtonText => StartWithWindows ? "Autostart deaktivieren" : "Autostart aktivieren";
    public string StartupStatus { get => _startupStatus; private set => SetProperty(ref _startupStatus, value); }

    public async Task StartAsync()
    {
        if (_closing || _starting) return;
        _starting = true;
        try
        {
            foreach (IFeatureModule module in Modules) await module.StartAsync();
            await LoadPowerModeAsync();
            await LoadStartupStateAsync();

            if (_isRunning)
            {
                return;
            }

            DeviceCompatibility compatibility = _reader.CheckCompatibility();
            if (!compatibility.IsSupported)
            {
                Status = compatibility.Message;
                return;
            }

            _isRunning = true;
            ToggleButtonText = "Überwachung stoppen";
            Status = "Live-Telemetrie verbunden";
            await RefreshAsync();
            if (_isRunning) _timer.Start();
        }
        finally { _starting = false; }
    }

    public async Task PrepareToCloseAsync()
    {
        // Flush BEFORE _closing goes up: a value the user set a moment ago must reach the
        // device rather than vanish because the window happened to close right after.
        await Cooling.FlushPendingWritesAsync();
        try { await Battery.PendingLimitWrite.FlushAsync(); } catch (Exception error) { AppLog.Error("battery", "Ausstehendes Ladelimit nicht mehr geschrieben.", error); }

        _closing = true;
        await Keyboard.StopListeningAsync();
        _timer.Stop();
        Cooling.BeginClose();
        while (_starting || _powerBusy || _isReading || Modules.Any(module => module.IsBusy))
            await Task.Delay(50);
        _timer.Stop();
        try { await Keyboard.SuspendAsync(); }
        catch
        {
            // The lighting stayed with us, so the window stays open and everything that was
            // stopped for the close has to come back up.
            _closing = false;
            Cooling.CancelClose();
            Keyboard.ResumeAfterFailedClose();
            if (_isRunning) _timer.Start();
            throw;
        }
        try { await Cooling.HandBackAsync(); }
        catch
        {
            // The fans stayed where they were, so the window stays open and says so rather
            // than closing over a machine left running pinned.
            _closing = false;
            Cooling.CancelClose();
            Keyboard.ResumeAfterFailedClose();
            if (_isRunning) _timer.Start();
            throw;
        }
    }

    /// <summary>Best-effort hardware handback for a Windows shutdown or logoff, where there
    /// is no time for the normal close sequence.</summary>
    public void RestoreFansToFirmware() => Cooling.RestoreFansToFirmware();

    public void Dispose()
    {
        _closing = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _reader.Dispose();
        foreach (IFeatureModule module in Modules) module.Dispose();
    }


    public async Task SetWindowsPowerModeAsync(WindowsPowerOverlayMode mode)
    {
        if (_closing || _powerBusy || !PowerControlsEnabled)
        {
            return;
        }

        _powerBusy = true;
        PowerControlsEnabled = false;
        PowerStatus = $"{DescribePowerMode(mode)} wird gesetzt …";
        try
        {
            if (!_powerOverlay.IsOnAcPower())
                throw new InvalidOperationException("Bitte zuerst das Netzteil anschließen.");
            await Task.Run(() => _powerOverlay.Set(mode));
            if (!_powerOverlay.IsOnAcPower())
                throw new InvalidOperationException("Stromquelle während der Änderung gewechselt. Aktuellen Modus erneut prüfen.");
            Guid actual = await Task.Run(_powerOverlay.ReadActiveForCurrentPowerSource);
            Guid expected = GetPowerModeGuid(mode);
            if (actual != expected)
            {
                throw new InvalidOperationException($"Windows-Rücklesen stimmt nicht überein: {actual}.");
            }
            ActivePowerMode = mode.ToString();
            PowerStatus = $"Aktiv: {DescribePowerMode(mode)} (Netzbetrieb)";
        }
        catch (Exception exception)
        {
            AppLog.Error("power", $"Leistungsmodus {mode} fehlgeschlagen.", exception);
            PowerStatus = $"Leistungsmodus fehlgeschlagen: {exception.Message}";
            try
            {
                // Without this the chip keeps showing the mode that was clicked rather
                // than the one Windows is actually in.
                ActivePowerMode = DescribePowerModeKey(await Task.Run(_powerOverlay.ReadActiveForCurrentPowerSource));
            }
            catch (Exception readError)
            {
                AppLog.Warn("power", "Aktiver Modus nach Fehler nicht lesbar: " + readError.Message);
            }
        }
        finally
        {
            _powerBusy = false;
            PowerControlsEnabled = true;
            OnPropertyChanged(nameof(ActivePowerMode));
        }
    }

    /// <summary>Starts the telemetry clock on a module's behalf. Holding a pinned fan
    /// without supervision is exactly what this app exists to avoid.</summary>
    private void StartMonitoring()
    {
        _isRunning = true;
        ToggleButtonText = "Überwachung stoppen";
        _timer.Start();
    }

    private async void OnTimerTick(object? sender, EventArgs eventArgs) =>
        await RefreshAsync();

    private async void ToggleMonitoring()
    {
        if (_closing || Cooling.IsBusy) return;
        if (_isRunning)
        {
            if (Cooling.IsFixedActive)
            {
                // Stopping the clock while a fixed value is held would remove the very
                // supervision that makes holding it safe, so the value goes first.
                await Cooling.AbandonFixedAsync("Überwachung wird beendet");
                if (Cooling.IsFixedActive) return;
            }
            _timer.Stop();
            _isRunning = false;
            ToggleButtonText = "Überwachung starten";
            Status = "Überwachung angehalten";
            return;
        }

        await StartAsync();
    }

    /// <summary>Duty as a share of the firmware's own maximum, with the raw byte kept as
    /// the smaller half - the percentage is what answers "how hard is it working".</summary>
    private static string DescribeDuty(ushort raw) =>
        $"{FanSpeedPercent.ToPercent((byte)Math.Min(raw, (ushort)255))} % Leistung · Rohwert {raw}";

    private async Task RefreshAsync()
    {
        if (_closing || _isReading || (!_dashboardVisible && !Cooling.IsFixedActive))
        {
            return;
        }

        _isReading = true;
        try
        {
            TelemetrySnapshot snapshot = await _reader.ReadAsync();
            CpuTemperature = $"{snapshot.CpuTemperatureCelsius} °C";
            GpuTemperature = $"{snapshot.GpuTemperatureCelsius} °C";
            CpuFan = $"{snapshot.CpuFanRpm:N0} U/min";
            GpuFan = $"{snapshot.GpuFanRpm:N0} U/min";
            CpuDuty = DescribeDuty(snapshot.CpuFanDutyPercent);
            GpuDuty = DescribeDuty(snapshot.GpuFanDutyPercent);
            PowerSource = _powerOverlay.IsOnAcPower() ? "Netzbetrieb" : "Akkubetrieb";
            LastUpdated = $"Letzte Messung: {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}";
            Status = "Live-Telemetrie verbunden";
            // The worker's own lease re-validates temperature on every renewal, using its
            // own independent telemetry read; a failure there already means it has
            // restored Normal by itself before this call returns.
            await Cooling.RenewFixedLeaseAsync();
        }
        catch (Exception exception)
        {
            if (Cooling.IsFixedActive) await Cooling.AbandonFixedAsync("Temperaturmessung ausgefallen");
            // Keep retrying the safety restoration if WMI temporarily fails.
            if (!Cooling.IsFixedActive) _timer.Stop();
            _isRunning = Cooling.IsFixedActive;
            ToggleButtonText = "Erneut versuchen";
            Status = $"Messfehler: {exception.Message}";
        }
        finally
        {
            _isReading = false;
        }
    }

    public void SetDashboardVisible(bool visible)
    {
        _dashboardVisible = visible;
        UpdateModuleVisibility();
        // Never pause the existing safety sampling for a manually fixed fan.
        if (visible && _isRunning) _timer.Start();
        else if (!visible && !Cooling.IsFixedActive) _timer.Stop();
    }

    /// <summary>Tells each module whether its own section is actually on screen, so nothing
    /// animates or polls for a view nobody is looking at.</summary>
    private void UpdateModuleVisibility() =>
        Keyboard.IsVisible = _dashboardVisible && SelectedSection == "Lighting";

    private async Task LoadPowerModeAsync()
    {
        try
        {
            if (!_powerOverlay.IsOnAcPower())
            {
                PowerStatus = "Akkubetrieb · Umschalten ist vorerst nur im getesteten Netzbetrieb freigegeben";
                return;
            }
            Guid current = await Task.Run(_powerOverlay.ReadActiveForCurrentPowerSource);
            ActivePowerMode = DescribePowerModeKey(current);
            PowerStatus = $"Aktiv: {DescribePowerGuid(current)} (Netzbetrieb)";
            PowerControlsEnabled = true;
        }
        catch (Exception exception)
        {
            AppLog.Error("power", "Windows-Leistungsmodus nicht verfügbar.", exception);
            PowerStatus = $"Windows-Leistungsmodus nicht verfügbar: {exception.Message}";
        }
    }

    private async Task LoadStartupStateAsync()
    {
        try
        {
            StartWithWindows = await _startupManager.IsEnabledAsync();
            OnPropertyChanged(nameof(StartWithWindowsButtonText));
            StartupStatus = StartWithWindows
                ? "Startet automatisch mit Windows (angemeldet, erhöhte Rechte, kein Bestätigungsdialog nötig)."
                : "Startet aktuell nicht automatisch mit Windows.";
        }
        catch (Exception exception)
        {
            StartupStatus = $"Autostart-Status nicht lesbar: {exception.Message}";
        }
    }

    /// <summary>Uses a Scheduled Task, not the registry Run key, specifically so this does
    /// not show a fresh UAC prompt at every single login - see StartupManager's own doc
    /// comment for why that distinction matters for an admin-required app like this one.</summary>
    public async Task SetStartWithWindowsAsync(bool enabled)
    {
        if (_startupBusy) return;
        _startupBusy = true;
        StartupStatus = enabled ? "Autostart wird eingerichtet …" : "Autostart wird entfernt …";
        try
        {
            if (enabled) await _startupManager.EnableAsync();
            else await _startupManager.DisableAsync();
            StartWithWindows = await _startupManager.IsEnabledAsync();
            OnPropertyChanged(nameof(StartWithWindowsButtonText));
            StartupStatus = StartWithWindows
                ? "Startet automatisch mit Windows (angemeldet, erhöhte Rechte, kein Bestätigungsdialog nötig)."
                : "Startet aktuell nicht automatisch mit Windows.";
        }
        catch (Exception exception)
        {
            AppLog.Error("startup", "Autostart-Änderung fehlgeschlagen.", exception);
            StartupStatus = $"Autostart-Änderung fehlgeschlagen: {exception.Message}";
        }
        finally
        {
            _startupBusy = false;
        }
    }

    private static Guid GetPowerModeGuid(WindowsPowerOverlayMode mode) => mode switch
    {
        WindowsPowerOverlayMode.BestEfficiency => WindowsPowerOverlayController.BestEfficiencyGuid,
        WindowsPowerOverlayMode.BestPerformance => WindowsPowerOverlayController.BestPerformanceGuid,
        _ => WindowsPowerOverlayController.BalancedGuid
    };

    private static string DescribePowerMode(WindowsPowerOverlayMode mode) => mode switch
    {
        WindowsPowerOverlayMode.BestEfficiency => "Beste Energieeffizienz",
        WindowsPowerOverlayMode.BestPerformance => "Beste Leistung",
        _ => "Ausbalanciert"
    };

    private static string? DescribePowerModeKey(Guid guid) =>
        guid == WindowsPowerOverlayController.BestEfficiencyGuid
            ? nameof(WindowsPowerOverlayMode.BestEfficiency)
            : guid == WindowsPowerOverlayController.BestPerformanceGuid
                ? nameof(WindowsPowerOverlayMode.BestPerformance)
                : guid == WindowsPowerOverlayController.BalancedGuid
                    ? nameof(WindowsPowerOverlayMode.Balanced)
                    : null;

    private static string DescribePowerGuid(Guid guid) =>
        guid == WindowsPowerOverlayController.BestEfficiencyGuid
            ? "Beste Energieeffizienz"
            : guid == WindowsPowerOverlayController.BestPerformanceGuid
                ? "Beste Leistung"
                : guid == WindowsPowerOverlayController.BalancedGuid
                    ? "Ausbalanciert"
                    : $"Unbekannt ({guid})";


}
