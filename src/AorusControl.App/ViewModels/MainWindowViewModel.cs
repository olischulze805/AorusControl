using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows.Input;
using MediaBrush = System.Windows.Media.Brush;
using AorusControl.App.Features.Cooling;
using AorusControl.App.Features.Keyboard;
using AorusControl.App.Features.Platform;
using AorusControl.App.Features.Updates;
using AorusControl.App.Infrastructure;
using AorusControl.App.Localization;
using AorusControl.Core.Models;
using AorusControl.Core.Services;
using AorusControl.Core.Features.Battery;
using AorusControl.Core.Features.Cooling;
using AorusControl.Core.Features.Diagnostics;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Features.Startup;
using AorusControl.Core.Features.Worker;
using AorusControl.App.Features.GpuPreferences;
using AorusControl.Core.Features.GpuPreferences;
using AorusControl.Core.Features.PowerMonitoring;
using AorusControl.Core.Features.PowerProfiles;

namespace AorusControl.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IAorusTelemetryReader _reader;
    private readonly DispatcherTimer _timer;
    private bool _isReading;
    private bool _isRunning;
    private bool _dashboardVisible = true;
    private string _status = "Bereit";
    private string _lastUpdated = Strings.Current["Shell_NoReadingYet"];
    private string _toggleButtonText = Strings.Current["Shell_StartMonitoring"];
    private bool _closing;
    private bool _starting;
    private readonly Func<DashboardPowerReading>? _readPower;
    private readonly Action? _releasePower;
    // One dashboard, one reader. It is held statically only so that the parameterless
    // constructor can hand the same instance to both parameters below; tests never reach
    // this path, since building it would go to the real hardware.
    private static readonly DashboardPowerReader SharedPowerReader = new();
    private string _cpuPower = "– W";
    private string _gpuPower = "– W";
    private string _flowValue = "– W";
    private string _flowLabel = Strings.Current["Common_NotReadYet"];
    private string _flowNote = string.Empty;
    private double _batteryCharge;
    private bool _flowIsCharging;
    private bool _flowIsLive;
    private double[] _flowHistory = [];
    private string _flowRuntime = string.Empty;
    private readonly SampleHistory _flow = new();
    // Three minutes: long enough that opening a page does not halve the estimate, short
    // enough that putting the machine to work is visible in it within a page of reading.
    private readonly MovingAverage _flowAverage = new(TimeSpan.FromMinutes(3));
    private BatteryFlowDirection _flowDirection = BatteryFlowDirection.Unknown;
    private string _gpuPowerStatus = Strings.Current["Gpu_StateUnknown"];
    public string CpuPower { get => _cpuPower; private set => SetProperty(ref _cpuPower, value); }
    public string GpuPower { get => _gpuPower; private set => SetProperty(ref _gpuPower, value); }

    /// <summary>
    /// What the battery is doing, in the three words that matter: the figure, what it means,
    /// and how full the pack is.
    ///
    /// On battery the figure is the whole machine's draw - processor, graphics, screen and
    /// all - which is the only total this laptop can measure at all. On mains it is what
    /// goes into the battery, and the card says so rather than letting it pass for a system
    /// figure: the power the adapter delivers is not readable anywhere on this machine.
    /// </summary>
    public string PowerFlowValue { get => _flowValue; private set => SetProperty(ref _flowValue, value); }
    public string PowerFlowLabel { get => _flowLabel; private set => SetProperty(ref _flowLabel, value); }
    public string PowerFlowNote { get => _flowNote; private set => SetProperty(ref _flowNote, value); }
    public double BatteryCharge { get => _batteryCharge; private set => SetProperty(ref _batteryCharge, value); }
    public bool PowerFlowIsCharging { get => _flowIsCharging; private set => SetProperty(ref _flowIsCharging, value); }
    public bool PowerFlowIsLive { get => _flowIsLive; private set => SetProperty(ref _flowIsLive, value); }
    public double[] PowerFlowHistory { get => _flowHistory; private set => SetProperty(ref _flowHistory, value); }

    /// <summary>The same figure as time: how long the pack lasts, or how long until charging
    /// stops. Empty whenever the rate does not support one - a resting battery has no
    /// remaining time, and neither does a machine that has just been unplugged.</summary>
    public string PowerFlowRuntime { get => _flowRuntime; private set => SetProperty(ref _flowRuntime, value); }
    public string GpuPowerStatus { get => _gpuPowerStatus; private set => SetProperty(ref _gpuPowerStatus, value); }

    /// <summary>
    /// What the tray icon says when the pointer rests on it. Built from state the app already
    /// holds - no reading, no clock - so it stays honest while the window is hidden and the
    /// telemetry is stopped.
    /// </summary>
    public string TrayText => TrayStatus.Build(Cooling.ActiveProfile, Battery.StopPercent, Battery.IsSupported);

    public MainWindowViewModel()
        : this(
            new GigabyteWmiTelemetryReader(),
            new GigabyteHidKeyboardRgbController(),
            new GigabyteWmiFanController(),
            new WindowsPowerOverlayController(),
            keyboardSettingsStore: new KeyboardSettingsStore(AppData.File("keyboard-v1.json")),
            brightnessListener: new KeyboardBrightnessNotifications().RunAsync,
            fanCurveStore: new FanCurveStore(AppData.File("fan-curve-v1.json")),
            startupManager: new StartupManager(Environment.ProcessPath ?? throw new InvalidOperationException("Prozesspfad unbekannt.")),
            readPower: SharedPowerReader.Read,
            releasePower: SharedPowerReader.ReleaseGpu)
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
        IBatterySettingsStore? batterySettings = null,
        Func<TimeSpan, Task>? resumeReapplyDelay = null,
        Func<TimeSpan, CancellationToken, Task>? debounceWait = null,
        Func<DashboardPowerReading>? readPower = null,
        Action? releasePower = null,
        IGpuPreferenceStore? gpuPreferenceStore = null,
        IGpuPreferenceSettingsStore? gpuPreferenceSettings = null,
        Func<LaptopPowerSource>? readPowerSource = null,
        IGpuActivityReader? gpuActivity = null)
    {
        _reader = reader;
        _readPower = readPower;
        _releasePower = releasePower;
        Keyboard = new KeyboardViewModel(keyboardRgb, keyboardSettingsStore, brightnessListener, resumeReapplyDelay);
        Cooling = new CoolingViewModel(
            fanController,
            // Defaults to the real out-of-process worker client: only that implementation
            // survives this process crashing, which is the entire point of Fixed-mode safety.
            fixedFanLeaseClient ?? new WorkerFixedFanLeaseClient(),
            fanCurveStore ?? new FanCurveStore(AppData.File("fan-curve-v1.json")),
            RefreshAsync,
            StartMonitoring,
            debounceWait);
        Windows = new WindowsSettingsViewModel(
            powerOverlay,
            startupManager ?? new StartupManager(Environment.ProcessPath ?? "AorusControl.exe"));
        Battery = new BatteryViewModel(batteryController ?? new GigabyteWmiBatteryChargeController(), debounceWait,
            batterySettings ?? new BatterySettingsStore(AppData.File("battery-v1.json")));
        Graphics = new GpuPreferenceViewModel(
            gpuPreferenceStore ?? new WindowsGpuPreferenceStore(),
            gpuPreferenceSettings ?? new GpuPreferenceSettingsStore(AppData.File("gpu-preferences-v1.json")),
            readPowerSource ?? powerOverlay.ReadPowerSource,
            gpuActivity);
        Updates = new UpdateViewModel();
        Language = new LanguageViewModel();
        // The tray text is derived, so it has to be told when either half moves. Both
        // modules already raise these; nothing new is polled for it.
        // A language change reaches bound text by itself; the sentences these modules compute
        // have to be asked to say themselves again. One subscription for all of them, from the
        // object that owns them and outlives none of them.
        Localization.Strings.Current.PropertyChanged += (_, _) =>
        {
            RefreshAllProperties();
            foreach (IFeatureModule module in Modules)
                if (module is ObservableObject observable) observable.RefreshAllProperties();
        };
        Cooling.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(CoolingViewModel.ActiveProfile)) OnPropertyChanged(nameof(TrayText));
        };
        Battery.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(BatteryViewModel.StopPercent) or nameof(BatteryViewModel.CanApply))
                OnPropertyChanged(nameof(TrayText));
        };
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
    private IReadOnlyList<IFeatureModule> Modules => [Graphics, Keyboard, Cooling, Windows, Battery];

    public KeyboardViewModel Keyboard { get; }
    public CoolingViewModel Cooling { get; }
    public BatteryViewModel Battery { get; }

    /// <summary>Which chip chosen programs start on, following the power source.</summary>
    public GpuPreferenceViewModel Graphics { get; }

    /// <summary>The Windows-side settings. Named for what it controls, not for the OS.</summary>
    public WindowsSettingsViewModel Windows { get; }
    public UpdateViewModel Updates { get; }

    /// <summary>The language picker. Constructed early on purpose: it applies the stored
    /// choice, and everything drawn afterwards reads from the table it selected.</summary>
    public LanguageViewModel Language { get; }

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
            // Not awaited: looking for a newer version is nobody's reason to wait for the
            // window, and it says nothing unless it finds one.
            _ = Updates.CheckOnStartupAsync();

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
            ToggleButtonText = Strings.Current["Shell_StopMonitoring"];
            Status = Strings.Current["Shell_LiveConnected"];
            await RefreshAsync();
            if (_isRunning && (_dashboardVisible || Cooling.IsFixedActive)) _timer.Start();
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
        Updates.CancelStartupCheck();
        await Keyboard.StopListeningAsync();
        _timer.Stop();
        Cooling.BeginClose();
        while (_starting || _isReading || Modules.Any(module => module.IsBusy))
            await Task.Delay(50);
        _timer.Stop();
        // Letting go of the lighting is allowed to fail. It used to keep the window open,
        // on the reasoning that the app still held the keyboard - but since the lighting is
        // now handed over as it stands, there is nothing left to hand back, and the usual
        // reason for the failure is that the keyboard has fallen off the USB bus. A machine
        // whose keyboard has died must not also refuse to close its control panel.
        try { await Keyboard.SuspendAsync(); }
        catch (Exception error) { AppLog.Error("keyboard", "Beleuchtung beim Schließen nicht sauber freigegeben.", error); }

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
        _releasePower?.Invoke();
        foreach (IFeatureModule module in Modules) module.Dispose();
    }

    /// <summary>Starts the telemetry clock on a module's behalf. Holding a pinned fan
    /// without supervision is exactly what this app exists to avoid.</summary>
    private void StartMonitoring()
    {
        _isRunning = true;
        ToggleButtonText = Strings.Current["Shell_StopMonitoring"];
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
                await Cooling.AbandonFixedAsync(Strings.Current["Shell_MonitoringEnding"]);
                if (Cooling.IsFixedActive) return;
            }
            _timer.Stop();
            _isRunning = false;
            Cooling.Live.MarkStale();
            ClearPowerDisplay();
            ToggleButtonText = Strings.Current["Shell_StartMonitoring"];
            Status = Strings.Current["Shell_MonitoringPaused"];
            return;
        }

        await StartAsync();
    }

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
            // The dashboard and the cooling page both read these numbers from Cooling.Live,
            // which is also the only thing that knows whether they are still current.
            Cooling.Live.Update(snapshot);
            Windows.RefreshPowerSource();
            LastUpdated = Strings.Current.Format("Shell_LastReading", snapshot.CapturedAt.ToLocalTime().ToString("HH:mm:ss"));
            Status = Strings.Current["Shell_LiveConnected"];
            // The worker's own lease re-validates temperature on every renewal, using its
            // own independent telemetry read; a failure there already means it has
            // restored Normal by itself before this call returns.
            await Cooling.RenewFixedLeaseAsync();
            if (_readPower is not null && _dashboardVisible && SelectedSection == "Dashboard")
            {
                try
                {
                    DashboardPowerReading power = await Task.Run(_readPower);
                    if (!_closing && _dashboardVisible && SelectedSection == "Dashboard")
                    {
                        CpuPower = power.CpuPackageWatts is { } watts ? $"{watts:F1} W" : "– W";
                        GpuPower = power.GpuWatts is { } gpuWatts ? $"{gpuWatts:F1} W" : "– W";
                        GpuPowerStatus = power.GpuStatus;
                        PublishBatteryFlow(power.Battery ?? BatteryFlow.Unknown);
                    }
                }
                catch { ClearPowerDisplay(); }
            }
        }
        catch (Exception exception)
        {
            // The rotors stop and the numbers stop claiming to be current the moment a read
            // fails - a fan drawn turning on stale data would be the one lie this page cannot
            // afford.
            Cooling.Live.MarkStale();
            ClearPowerDisplay();
            if (Cooling.IsFixedActive) await Cooling.AbandonFixedAsync(Strings.Current["Shell_TelemetryLost"]);
            // Keep retrying the safety restoration if WMI temporarily fails.
            if (!Cooling.IsFixedActive) _timer.Stop();
            _isRunning = Cooling.IsFixedActive;
            ToggleButtonText = Strings.Current["Shell_TryAgain"];
            Status = Strings.Current.Format("Shell_ReadingError", exception.Message);
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
    /// animates or polls for a view nobody is looking at - and reads a little more often while
    /// the cooling page is open, where the rotors and the live marker are the whole point and
    /// two seconds between readings is visibly coarse.</summary>
    private void UpdateModuleVisibility()
    {
        ClearPowerDisplay();
        Keyboard.IsVisible = _dashboardVisible && SelectedSection == "Lighting";
        Graphics.IsVisible = _dashboardVisible && SelectedSection == "Graphics";
        _timer.Interval = TimeSpan.FromSeconds(_dashboardVisible && SelectedSection == "Cooling" ? 1 : 2);
    }

    /// <summary>
    /// Turns one battery reading into the card's three lines, and keeps the trend.
    ///
    /// The series is thrown away whenever the direction changes: a line running from "40 W
    /// out of the battery" straight into "60 W into it" draws two different measurements as
    /// one shape, and the reader has no way to see the seam.
    /// </summary>
    private void PublishBatteryFlow(BatteryFlow battery)
    {
        if (battery.Direction != _flowDirection)
        {
            _flowDirection = battery.Direction;
            PowerFlowHistory = _flow.Reset();
            _flowAverage.Reset();
        }

        PowerFlowIsCharging = battery.Direction == BatteryFlowDirection.Charging;
        PowerFlowIsLive = battery.Direction is BatteryFlowDirection.Charging or BatteryFlowDirection.Discharging;
        if (battery.Watts is { } watts)
        {
            PowerFlowHistory = _flow.Add(watts);
            PowerFlowRuntime = Remaining(battery, _flowAverage.Add(watts));
        }
        else PowerFlowRuntime = string.Empty;

        PowerFlowValue = battery.Watts is { } value ? $"{value:F1} W" : battery.Direction switch
        {
            BatteryFlowDirection.Resting => Strings.Current["Flow_OnMains"],
            _ => "– W"
        };
        PowerFlowLabel = battery.Direction switch
        {
            BatteryFlowDirection.Discharging => Strings.Current["Flow_Discharging"],
            BatteryFlowDirection.Charging => Strings.Current["Flow_Charging"],
            BatteryFlowDirection.Resting => Strings.Current["Flow_Resting"],
            _ => Strings.Current["Flow_NoReading"]
        };
        BatteryCharge = battery.Percent ?? 0;
        PowerFlowNote = battery.Percent is { } percent && battery.RemainingWattHours is { } remaining && battery.FullWattHours is { } full
            ? Strings.Current.Format("Flow_Note", percent.ToString("F0"), remaining.ToString("F1"), full.ToString("F1"))
            : battery.Percent is { } onlyPercent ? Strings.Current.Format("Flow_NoteShort", onlyPercent.ToString("F0")) : string.Empty;
    }

    /// <summary>
    /// The estimate in words, from the smoothed rate rather than this second's.
    ///
    /// While charging it names the limit it is counting up to. Saying "voll in 40 Minuten"
    /// on a machine that stops at 80 % would be a promise the charge limit breaks.
    /// </summary>
    private string Remaining(BatteryFlow battery, double averageWatts) =>
        BatteryRuntimeMath.From(battery, averageWatts, Battery.StopPercent) switch
        {
            null => string.Empty,
            { UntilCharged: false } left => Strings.Current.Format("Flow_RemainingApprox", left.Text),
            { } until when Battery.StopPercent is { } stop => Strings.Current.Format("Flow_ToLimitApprox", stop, until.Text),
            { } until => Strings.Current.Format("Flow_ToFullApprox", until.Text)
        };

    /// <summary>
    /// Blanks the dashboard's power lines - and gives the NVIDIA library back while doing it.
    /// Every path that lands here means no one is looking at the dashboard any more, and the
    /// app then sits in the tray for hours; holding a driver handle through that would undo
    /// the point of letting the card sleep.
    /// </summary>
    private void ClearPowerDisplay()
    {
        CpuPower = "– W";
        GpuPower = "– W";
        GpuPowerStatus = Strings.Current["Gpu_StateUnknown"];
        PowerFlowValue = "– W";
        PowerFlowLabel = Strings.Current["Common_NotReadYet"];
        PowerFlowNote = string.Empty;
        PowerFlowRuntime = string.Empty;
        PowerFlowIsLive = false;
        _releasePower?.Invoke();
    }
}
