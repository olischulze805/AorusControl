using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows.Input;
using MediaBrush = System.Windows.Media.Brush;
using AorusControl.App.Features.Cooling;
using AorusControl.App.Features.Keyboard;
using AorusControl.App.Features.Platform;
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
    private string _lastUpdated = "Noch keine Messung";
    private string _toggleButtonText = "Überwachung starten";
    private bool _closing;
    private bool _starting;

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
        Windows = new WindowsSettingsViewModel(
            powerOverlay,
            startupManager ?? new StartupManager(Environment.ProcessPath ?? "AorusControl.exe"));
        Battery = new BatteryViewModel(batteryController ?? new GigabyteWmiBatteryChargeController(), debounceWait);
        Updates = new UpdateViewModel();
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += OnTimerTick;
        ToggleMonitoringCommand = new RelayCommand(ToggleMonitoring);
    }

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
    /// <summary>The attached feature modules. Everything the shell does to all of them -
    /// start them, wait for them, release them - goes through this list, so a new feature
    /// is a class plus one entry rather than another branch in three methods.</summary>
    private IReadOnlyList<IFeatureModule> Modules => [Keyboard, Cooling, Windows, Battery];

    public KeyboardViewModel Keyboard { get; }
    public CoolingViewModel Cooling { get; }
    public BatteryViewModel Battery { get; }

    /// <summary>The Windows-side settings. Named for what it controls, not for the OS.</summary>
    public WindowsSettingsViewModel Windows { get; }
    public UpdateViewModel Updates { get; }

    public string CpuTemperature { get => _cpuTemperature; private set => SetProperty(ref _cpuTemperature, value); }
    public string GpuTemperature { get => _gpuTemperature; private set => SetProperty(ref _gpuTemperature, value); }
    public string CpuFan { get => _cpuFan; private set => SetProperty(ref _cpuFan, value); }
    public string GpuFan { get => _gpuFan; private set => SetProperty(ref _gpuFan, value); }
    public string CpuDuty { get => _cpuDuty; private set => SetProperty(ref _cpuDuty, value); }
    public string GpuDuty { get => _gpuDuty; private set => SetProperty(ref _gpuDuty, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public string LastUpdated { get => _lastUpdated; private set => SetProperty(ref _lastUpdated, value); }
    public string ToggleButtonText { get => _toggleButtonText; private set => SetProperty(ref _toggleButtonText, value); }





    public async Task StartAsync()
    {
        if (_closing || _starting) return;
        _starting = true;
        try
        {
            foreach (IFeatureModule module in Modules) await module.StartAsync();

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
        while (_starting || _isReading || Modules.Any(module => module.IsBusy))
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
            Windows.RefreshPowerSource();
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



}
