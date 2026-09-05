using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows.Input;
using MediaBrush = System.Windows.Media.Brush;
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
    private readonly IAorusKeyboardRgbController _keyboardRgb;
    private readonly KeyboardLightingSession _keyboardSession;
    private readonly IKeyboardSettingsStore? _keyboardSettingsStore;
    private readonly Func<Action<KeyboardBrightnessLevel>, CancellationToken, Task>? _brightnessListener;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private CancellationTokenSource _brightnessCancellation = new();
    private Task? _brightnessListenerTask;
    private Task _brightnessDrainTask = Task.CompletedTask;
    private KeyboardBrightnessLevel? _pendingBrightness;
    private bool _drainingBrightness;
    private string _brightnessEventStatus = "Fn+Space-Ereignisleser nicht gestartet";
    public string BrightnessEventStatus { get => _brightnessEventStatus; private set => SetProperty(ref _brightnessEventStatus, value); }
    private readonly DispatcherTimer _rgbTimer;
    private readonly IAorusFanController _fanController;
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
    private string _lastUpdated = "Noch keine Messung";
    private string _toggleButtonText = "Überwachung starten";
    private bool _keyboardControlsEnabled;
    private bool _keyboardBusy;
    private bool _keyboardInitialized;
    private bool _keyboardPowerOn;
    private KeyboardBrightnessLevel _keyboardBrightness = KeyboardBrightnessLevel.High;
    private KeyboardEffectSpeed _keyboardEffectSpeed = KeyboardEffectSpeed.Normal;
    private bool _keyboardModeIsEffect;
    private string _keyboardPaletteHint = "Gespeicherte manuelle Farben";
    private bool _linkKeyboardZones;
    private string _keyboardStatus = "Tastatur wird geprüft …";
    private KeyboardRgbColor _zone1Color = new(0, 255, 0);
    private KeyboardRgbColor _zone2Color = new(0, 255, 0);
    private KeyboardRgbColor _zone3Color = new(0, 255, 0);
    private bool _keyboardEffectRunning;
    private bool _fanControlsEnabled;
    private bool _fanBusy;
    private bool _restoreNormalFanOnDispose;
    private bool _fixedFanActive;
    private Guid? _fixedFanLease;
    private readonly IFixedFanLeaseClient _fixedFanLeaseClient;
    private bool _closing;
    private bool _starting;
    private byte _fixedFanRaw = 114;
    private string _fanStatus = "Lüftersteuerung wird geprüft …";
    private bool _powerControlsEnabled;
    private bool _powerBusy;
    private string _powerStatus = "Windows-Leistungsmodus wird gelesen …";
    private readonly IFanCurveStore _fanCurveStore;
    private string _fanCurveStatus = "Kurve wird gelesen …";
    private readonly IStartupManager _startupManager;
    private bool _startWithWindows;
    private bool _startupBusy;
    private string _startupStatus = "Autostart wird geprüft …";
    private readonly Func<TimeSpan, Task> _resumeReapplyDelay;
    private readonly Debouncer _applyFanCurve;
    private readonly Debouncer _applyFixedFan;
    private bool _disposed;

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
        // Defaults to the real out-of-process worker client: only that implementation
        // survives this process crashing, which is the entire point of Fixed-mode safety.
        _fixedFanLeaseClient = fixedFanLeaseClient ?? new WorkerFixedFanLeaseClient();
        _fanCurveStore = fanCurveStore ?? new FanCurveStore(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AorusControl", "fan-curve-v1.json"));
        _startupManager = startupManager ?? new StartupManager(Environment.ProcessPath ?? "AorusControl.exe");
        _resumeReapplyDelay = resumeReapplyDelay ?? Task.Delay;
        Battery = new BatteryViewModel(batteryController ?? new GigabyteWmiBatteryChargeController(), debounceWait);
        Updates = new UpdateViewModel();
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _keyboardRgb = keyboardRgb;
        _keyboardSession = new KeyboardLightingSession(keyboardRgb);
        _keyboardSettingsStore = keyboardSettingsStore;
        _brightnessListener = brightnessListener;
        _rgbTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _rgbTimer.Tick += OnRgbTimerTick;
        // 20 Hz is enough for the eye and a third of the renderer's own rate; it only
        // ever runs while the Tastatur section is visible and an effect is playing.
        _previewTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
        _previewTimer.Tick += OnPreviewTimerTick;
        _fanController = fanController;
        _powerOverlay = powerOverlay;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += OnTimerTick;
        ToggleMonitoringCommand = new RelayCommand(ToggleMonitoring);
        SetFanProfileCommand = new AsyncRelayCommand<string>(SetFanProfileAsync);
        SetFixedFanCommand = new AsyncRelayCommand(SetFixedFanAsync);
        SetPowerModeCommand = new AsyncRelayCommand<string>(modeName =>
            Enum.TryParse(modeName, ignoreCase: true, out WindowsPowerOverlayMode mode)
                ? SetWindowsPowerModeAsync(mode)
                : Task.CompletedTask);
        ToggleKeyboardPowerCommand = new AsyncRelayCommand(() => SetKeyboardPowerAsync(!KeyboardPowerOn));
        ReapplyKeyboardCommand = new AsyncRelayCommand(ReapplyKeyboardAsync);
        StartKeyboardEffectCommand = new AsyncRelayCommand(() => StartKeyboardEffectAsync(SelectedKeyboardEffect));
        StopKeyboardEffectCommand = new AsyncRelayCommand(StopKeyboardEffectAsync);
        ApplyFanCurveCommand = new AsyncRelayCommand(ApplyFanCurveAsync);
        ReloadFanCurveFromDeviceCommand = new AsyncRelayCommand(ReloadFanCurveFromDeviceAsync);
        ToggleStartWithWindowsCommand = new AsyncRelayCommand(() => SetStartWithWindowsAsync(!StartWithWindows));
        FixedFanTicks = new System.Windows.Media.DoubleCollection(
            FixedFanRawChoices.Select(raw => (double)FanSpeedPercent.ToPercent(raw)));
        // Dragging applies by itself once the gesture settles. The curve waits a little
        // longer than a single value would: shaping it means many small drags, and each
        // write is a fifteen-point EC transaction plus a mode switch.
        _applyFanCurve = new Debouncer(TimeSpan.FromMilliseconds(900), ApplyPendingFanCurveAsync, debounceWait);
        // Only ever reschedules an ALREADY active Fixed mode - entering it stays an
        // explicit act, see SetFixedFanAsync.
        _applyFixedFan = new Debouncer(TimeSpan.FromMilliseconds(600), ReapplyFixedFanAsync, debounceWait);
        ApplyKeyboardEffectCommand = new AsyncRelayCommand<string>(name =>
            // "Manual" is the tenth tile rather than a separate button: picking it is
            // simply choosing no effect.
            name == "Manual" ? StopKeyboardEffectAsync()
                : Enum.TryParse(name, ignoreCase: true, out KeyboardRgbEffect effect)
                    ? StartKeyboardEffectAsync(effect)
                    : Task.CompletedTask);
    }

    private string _activeFanProfile = "Normal";
    private string? _activePowerMode;
    private KeyboardRgbEffect? _activeKeyboardEffect;
    private readonly DispatcherTimer _previewTimer;
    private readonly System.Diagnostics.Stopwatch _effectClock = new();
    private MediaBrush _previewZone1 = CreateBrush(new KeyboardRgbColor(0, 0, 0));
    private MediaBrush _previewZone2 = CreateBrush(new KeyboardRgbColor(0, 0, 0));
    private MediaBrush _previewZone3 = CreateBrush(new KeyboardRgbColor(0, 0, 0));
    private double _previewOpacity;
    private string _selectedSection = "Dashboard";

    /// <summary>Which navigation section is visible. Pure UI state - no hardware
    /// implication - kept here rather than split into one ViewModel per page, since
    /// every section already shares this same ViewModel and its live device state.</summary>
    public string SelectedSection
    {
        get => _selectedSection;
        set
        {
            // The live keyboard preview only animates while its own section is on
            // screen: an animation nobody is looking at is pure battery drain.
            if (SetProperty(ref _selectedSection, value)) UpdatePreviewTimer();
        }
    }

    public ICommand ToggleMonitoringCommand { get; }
    public AsyncRelayCommand<string> SetFanProfileCommand { get; }
    public AsyncRelayCommand SetFixedFanCommand { get; }
    public AsyncRelayCommand<string> SetPowerModeCommand { get; }
    public AsyncRelayCommand ToggleKeyboardPowerCommand { get; }
    public AsyncRelayCommand ReapplyKeyboardCommand { get; }
    public AsyncRelayCommand StartKeyboardEffectCommand { get; }
    public AsyncRelayCommand StopKeyboardEffectCommand { get; }
    public AsyncRelayCommand ApplyFanCurveCommand { get; }
    public AsyncRelayCommand ReloadFanCurveFromDeviceCommand { get; }
    public AsyncRelayCommand ToggleStartWithWindowsCommand { get; }
    public AsyncRelayCommand<string> ApplyKeyboardEffectCommand { get; }
    public BatteryViewModel Battery { get; }
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
    public bool KeyboardControlsEnabled { get => _keyboardControlsEnabled; private set => SetProperty(ref _keyboardControlsEnabled, value); }
    public bool KeyboardPowerOn { get => _keyboardPowerOn; private set => SetProperty(ref _keyboardPowerOn, value); }
    public string KeyboardPowerButtonText => KeyboardPowerOn ? "Ausschalten" : "Einschalten";

    public KeyboardBrightnessLevel KeyboardBrightness
    {
        get => _keyboardBrightness;
        private set => SetProperty(ref _keyboardBrightness, value);
    }

    /// <summary>
    /// The four steps the firmware accepts. Anything else is either off or full, so no
    /// slider is offered.
    /// </summary>
    public KeyboardEffectSpeed KeyboardEffectSpeed
    {
        get => _keyboardEffectSpeed;
        private set => SetProperty(ref _keyboardEffectSpeed, value);
    }

    public IReadOnlyList<KeyboardEffectSpeedChoice> KeyboardEffectSpeedChoices { get; } =
        KeyboardEffectSpeeds.All
            .Select(speed => new KeyboardEffectSpeedChoice(speed, DescribeSpeed(speed)))
            .ToArray();

    public IReadOnlyList<KeyboardBrightnessChoice> KeyboardBrightnessLevelChoices { get; } =
        KeyboardBrightnessLevels.All
            .Select(level => new KeyboardBrightnessChoice(level, DescribeBrightness(level)))
            .ToArray();

    public IReadOnlyList<KeyboardEffectChoice> KeyboardEffectChoices { get; } = new[]
    {
        (KeyboardRgbEffect.Breathing, "Atmen"),
        (KeyboardRgbEffect.Pulse, "Pulsieren"),
        (KeyboardRgbEffect.ColorCycle, "Farbwechsel"),
        (KeyboardRgbEffect.RainbowMarquee, "Regenbogen-Lauflicht"),
        (KeyboardRgbEffect.Wave, "Welle"),
        (KeyboardRgbEffect.Marquee, "Lauflicht"),
        (KeyboardRgbEffect.Rotate, "Pendel"),
        (KeyboardRgbEffect.Raindrop, "Regentropfen"),
        (KeyboardRgbEffect.FadeSweep, "Ausblendende Welle"),
    }.Select(pair => new KeyboardEffectChoice(pair.Item1, pair.Item2)).ToArray();

    private KeyboardRgbEffect _selectedKeyboardEffect = KeyboardRgbEffect.Breathing;
    public KeyboardRgbEffect SelectedKeyboardEffect
    {
        get => _selectedKeyboardEffect;
        set => SetProperty(ref _selectedKeyboardEffect, value);
    }
    public bool LinkKeyboardZones { get => _linkKeyboardZones; set => SetProperty(ref _linkKeyboardZones, value); }
    public bool KeyboardEffectRunning { get => _keyboardEffectRunning; private set => SetProperty(ref _keyboardEffectRunning, value); }
    public bool KeyboardModeIsEffect { get => _keyboardModeIsEffect; private set => SetProperty(ref _keyboardModeIsEffect, value); }
    public string KeyboardPaletteHint { get => _keyboardPaletteHint; private set => SetProperty(ref _keyboardPaletteHint, value); }
    public string KeyboardStatus { get => _keyboardStatus; private set => SetProperty(ref _keyboardStatus, value); }
    public string Zone1Hex => _zone1Color.Hex;
    public string Zone2Hex => _zone2Color.Hex;
    public string Zone3Hex => _zone3Color.Hex;
    public MediaBrush Zone1Brush => CreateBrush(_zone1Color);
    public MediaBrush Zone2Brush => CreateBrush(_zone2Color);
    public MediaBrush Zone3Brush => CreateBrush(_zone3Color);
    public bool FanControlsEnabled { get => _fanControlsEnabled; private set => SetProperty(ref _fanControlsEnabled, value); }
    public string FanStatus { get => _fanStatus; private set => SetProperty(ref _fanStatus, value); }

    // A note that applies to ActiveFanProfile, ActivePowerMode and
    // ActiveKeyboardEffectName alike: the chips and tiles bind to them ONE-WAY, and a
    // RadioButton sets its own IsChecked locally the moment it is clicked. Only a
    // PropertyChanged pushes the real value back over that local one - so every command
    // that touches them re-announces in its finally block even when the value did not
    // change. Relying on SetProperty's equality gate would leave the clicked chip lit
    // after a write that failed and left the device exactly where it was.

    /// <summary>
    /// Which profile chip is highlighted. Derived from what was actually read back from
    /// the EC, not from what was last clicked, so an externally changed profile (vendor
    /// tool, Fn shortcut, our own safety restore) shows up honestly.
    /// </summary>
    public string ActiveFanProfile { get => _activeFanProfile; private set => SetProperty(ref _activeFanProfile, value); }

    public byte FixedFanRaw
    {
        get => _fixedFanRaw;
        set
        {
            if (!SetProperty(ref _fixedFanRaw, value)) return;
            OnPropertyChanged(nameof(FixedFanPercent));
            OnPropertyChanged(nameof(FixedFanPercentText));
        }
    }

    public IReadOnlyList<byte> FixedFanRawChoices { get; } = [57, 68, 91, 114, 137, 160, 194, 229];

    /// <summary>
    /// The Fixed slider's value. Reads and writes percent, but can only ever land on one
    /// of <see cref="FixedFanRawChoices"/>: the setter snaps to the nearest tested raw
    /// step, so a value the firmware was never measured at is unreachable even if the
    /// slider's own snapping were bypassed.
    /// </summary>
    public double FixedFanPercent
    {
        get => FanSpeedPercent.ToPercent(_fixedFanRaw);
        set
        {
            byte nearest = FixedFanRawChoices
                .OrderBy(raw => Math.Abs(FanSpeedPercent.ToPercent(raw) - value))
                .First();
            bool changed = nearest != _fixedFanRaw;
            FixedFanRaw = nearest;
            OnPropertyChanged(nameof(FixedFanPercent));
            // Following the slider while Fixed is already held is what the user expects;
            // silently ENTERING a mode that pins the fans because a slider was brushed is
            // not, so that still needs the button.
            if (changed && _fixedFanActive) _applyFixedFan.Schedule();
        }
    }

    public string FixedFanPercentText => $"{FanSpeedPercent.ToPercent(_fixedFanRaw)} %";

    /// <summary>Tick positions for the Fixed slider, on the percentages the tested raw
    /// steps really sit at - hence unevenly spaced, which is the honest picture. Derived
    /// from FixedFanRawChoices rather than restated, so the marks cannot come to show
    /// values the slider can no longer reach.</summary>
    public System.Windows.Media.DoubleCollection FixedFanTicks { get; }
    public bool PowerControlsEnabled { get => _powerControlsEnabled; private set => SetProperty(ref _powerControlsEnabled, value); }
    public string PowerStatus { get => _powerStatus; private set => SetProperty(ref _powerStatus, value); }

    /// <summary>Highlighted power-mode chip, again from Windows' own readback rather
    /// than from the last click.</summary>
    public string? ActivePowerMode { get => _activePowerMode; private set => SetProperty(ref _activePowerMode, value); }

    /// <summary>
    /// Brightness as a 0-3 slider position over the four steps the firmware accepts.
    /// The setter goes through the same guarded write path as every other brightness
    /// change, and the getter always reflects the device, so a rejected write makes the
    /// slider snap back rather than lie.
    /// </summary>
    public int KeyboardBrightnessIndex
    {
        get
        {
            int index = KeyboardBrightnessLevels.All.ToList().IndexOf(KeyboardBrightness);
            return index < 0 ? 0 : index;
        }
        set
        {
            IReadOnlyList<KeyboardBrightnessLevel> levels = KeyboardBrightnessLevels.All;
            if (value < 0 || value >= levels.Count || levels[value] == KeyboardBrightness)
            {
                // Push the real position back over whatever the thumb was dragged to.
                OnPropertyChanged(nameof(KeyboardBrightnessIndex));
                return;
            }

            PendingSliderWrite = SetKeyboardBrightnessAsync(levels[value]);
        }
    }

    public string KeyboardBrightnessLabel => DescribeBrightness(KeyboardBrightness);

    /// <summary>
    /// The device write a slider drag started. A two-way bound property setter cannot be
    /// awaited, so the task it launches is published here rather than dropped: that keeps
    /// the write observable (tests await it; a caller can tell when the device has really
    /// been told) instead of being fire-and-forget.
    /// </summary>
    internal Task PendingSliderWrite { get; private set; } = Task.CompletedTask;

    /// <summary>Playback speed as a 0-4 slider position over the five named steps.</summary>
    public int KeyboardEffectSpeedIndex
    {
        get
        {
            int index = KeyboardEffectSpeeds.All.ToList().IndexOf(KeyboardEffectSpeed);
            return index < 0 ? 2 : index;
        }
        set
        {
            IReadOnlyList<KeyboardEffectSpeed> speeds = KeyboardEffectSpeeds.All;
            if (value < 0 || value >= speeds.Count || speeds[value] == KeyboardEffectSpeed)
            {
                OnPropertyChanged(nameof(KeyboardEffectSpeedIndex));
                return;
            }

            PendingSliderWrite = SetKeyboardEffectSpeedAsync(speeds[value]);
        }
    }

    public string KeyboardEffectSpeedLabel => DescribeSpeed(KeyboardEffectSpeed);

    /// <summary>
    /// The effect actually running on the device, or null for manual zone colours -
    /// this is what highlights an effect tile. Deliberately separate from
    /// <see cref="SelectedKeyboardEffect"/>, which is only the last pick: if a write
    /// fails, the tile must keep showing what is really on the keyboard.
    /// </summary>
    public KeyboardRgbEffect? ActiveKeyboardEffect
    {
        get => _activeKeyboardEffect;
        private set
        {
            if (!SetProperty(ref _activeKeyboardEffect, value)) return;
            OnPropertyChanged(nameof(ActiveKeyboardEffectName));
        }
    }

    /// <summary>Tile identity of the running effect; "Manual" when none runs, so the
    /// manual-colours tile is a state among equals rather than a special case.</summary>
    public string ActiveKeyboardEffectName => _activeKeyboardEffect?.ToString() ?? "Manual";

    // ---- Live keyboard preview -------------------------------------------------
    // Rendered from KeyboardEffectFrames, the very function whose output is written to
    // the device, so the preview shows the actual frame rather than a lookalike.
    public MediaBrush PreviewZone1Brush { get => _previewZone1; private set => SetProperty(ref _previewZone1, value); }
    public MediaBrush PreviewZone2Brush { get => _previewZone2; private set => SetProperty(ref _previewZone2, value); }
    public MediaBrush PreviewZone3Brush { get => _previewZone3; private set => SetProperty(ref _previewZone3, value); }

    /// <summary>Approximates the LEDs' perceived brightness. The frames themselves carry
    /// no brightness - the device applies that separately - so this is a rendering of the
    /// chosen step, not a measured luminance.</summary>
    public double PreviewOpacity { get => _previewOpacity; private set => SetProperty(ref _previewOpacity, value); }

    /// <summary>Where the log files are, so "look in the log" is an actionable
    /// instruction rather than a scavenger hunt.</summary>
    public string LogDirectory => AppLog.Directory;

    // ---- Which zone colours the running mode actually reads -------------------
    // Offering a colour picker for an effect that ignores colours is a control that
    // pretends to do something. These drive the swatches so the UI can say plainly which
    // colour is in play - without hiding the others, since they stay stored and come back
    // in manual mode.
    public KeyboardEffectColorUsage KeyboardColorUsage => KeyboardEffectFrames.ColorUsage(_activeKeyboardEffect);

    public bool Zone1AffectsLighting => KeyboardColorUsage is KeyboardEffectColorUsage.AllZones or KeyboardEffectColorUsage.BaseColorOnly;
    public bool Zone2AffectsLighting => KeyboardColorUsage is KeyboardEffectColorUsage.AllZones;
    public bool Zone3AffectsLighting => KeyboardColorUsage is KeyboardEffectColorUsage.AllZones;

    /// <summary>Names zone 1's role, since for Atmen/Pulsieren it is not just "zone 1"
    /// but the colour the whole effect is built from.</summary>
    public string Zone1Label => KeyboardColorUsage == KeyboardEffectColorUsage.BaseColorOnly ? "Zone 1 · Basisfarbe" : "Zone 1";

    public bool HasInactiveZones => KeyboardColorUsage != KeyboardEffectColorUsage.AllZones;

    /// <summary>
    /// Said once under the row rather than three times under the swatches. It has to
    /// carry the reassurance too: dimmed means "no effect right now", never "lost".
    /// </summary>
    public string InactiveZoneNote => KeyboardColorUsage switch
    {
        KeyboardEffectColorUsage.BaseColorOnly =>
            "Die gedimmten Zonen liest dieser Effekt nicht - er baut alles aus der Basisfarbe. Sie bleiben gespeichert und gelten wieder im manuellen Modus.",
        KeyboardEffectColorUsage.None =>
            "Dieser Effekt liest keine der gespeicherten Farben. Sie bleiben erhalten und gelten wieder im manuellen Modus.",
        _ => string.Empty
    };

    public string PreviewCaption => KeyboardPowerOn
        ? $"Läuft: {(_activeKeyboardEffect is { } effect ? GetEffectName(effect) : "Manuelle Zonenfarben")} · Tempo {DescribeSpeed(KeyboardEffectSpeed)} · Helligkeit {DescribeBrightness(KeyboardBrightness)}"
        : "Beleuchtung aus · Auswahl bleibt gespeichert";

    /// <summary>The 15 editable curve points shown in the Cooling section. Text-backed
    /// like FanCurveRowViewModel elsewhere, so invalid/incomplete typing survives until
    /// an explicit Apply, instead of being silently clamped as the user types.</summary>
    public ObservableCollection<FanCurveRowViewModel> FanCurveRows { get; } = new();
    public string FanCurveStatus { get => _fanCurveStatus; private set => SetProperty(ref _fanCurveStatus, value); }

    public bool StartWithWindows { get => _startWithWindows; private set => SetProperty(ref _startWithWindows, value); }
    public string StartWithWindowsButtonText => StartWithWindows ? "Autostart deaktivieren" : "Autostart aktivieren";
    public string StartupStatus { get => _startupStatus; private set => SetProperty(ref _startupStatus, value); }

    public async Task StartAsync()
    {
        if (_closing || _starting) return;
        _starting = true;
        try
        {
            await LoadKeyboardAsync();
            await LoadFanAsync();
            await LoadPowerModeAsync();
            await LoadStartupStateAsync();
            await Battery.RefreshAsync();

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
        try { await _applyFanCurve.FlushAsync(); } catch (Exception error) { AppLog.Error("fan", "Ausstehende Kurve nicht mehr geschrieben.", error); }
        try { await _applyFixedFan.FlushAsync(); } catch (Exception error) { AppLog.Error("fan", "Ausstehender Fixed-Wert nicht mehr geschrieben.", error); }
        try { await Battery.PendingLimitWrite.FlushAsync(); } catch (Exception error) { AppLog.Error("battery", "Ausstehendes Ladelimit nicht mehr geschrieben.", error); }

        _closing = true;
        _brightnessCancellation.Cancel();
        if (_brightnessListenerTask is not null) await _brightnessListenerTask;
        if (_brightnessListenerTask is not null) BrightnessEventStatus = "Fn+Space-Ereignisleser beendet.";
        await _brightnessDrainTask;
        _timer.Stop();
        while (_starting || _fanBusy || _keyboardBusy || _powerBusy || _isReading || Battery.IsBusy)
            await Task.Delay(50);
        _timer.Stop();
        _rgbTimer.Stop();
        _previewTimer.Stop();
        try { await _keyboardSession.SuspendAsync(); }
        catch
        {
            _closing = false;
            RestartBrightnessAfterFailedClose();
            if (_isRunning) _timer.Start();
            throw;
        }
        if (_fixedFanActive && _fixedFanLease is { } closingLease)
        {
            // Best-effort only: never block shutdown on this. The worker's own
            // supervisor keeps the lease's guarantee regardless of whether the app
            // reaches it in time, so a failure here is not a reason to stay open.
            try { await _fixedFanLeaseClient.ReleaseAsync(closingLease); }
            catch { /* Worker's own supervisor remains responsible. */ }
            _fixedFanActive = false;
            _fixedFanLease = null;
        }
        if (_restoreNormalFanOnDispose)
        {
            try
            {
                await _fanController.SetNormalAsync();
                _restoreNormalFanOnDispose = false;
            }
            catch
            {
                _closing = false;
                RestartBrightnessAfterFailedClose();
                if (_isRunning) _timer.Start();
                throw;
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _applyFanCurve.Cancel();
        _applyFixedFan.Cancel();
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _brightnessCancellation.Cancel();
        _previewTimer.Stop();
        _previewTimer.Tick -= OnPreviewTimerTick;
        _rgbTimer.Stop();
        _rgbTimer.Tick -= OnRgbTimerTick;
        _keyboardSession.DisposeAsync().AsTask().GetAwaiter().GetResult();

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _reader.Dispose();
        Battery.Dispose();
        Updates.Dispose();
        _keyboardRgb.Dispose();
        if (_fixedFanActive && _fixedFanLease is { } disposeLease)
        {
            try { _fixedFanLeaseClient.ReleaseAsync(disposeLease).GetAwaiter().GetResult(); }
            catch { /* Worker's own supervisor remains responsible. */ }
        }
        if (_restoreNormalFanOnDispose)
        {
            try
            {
                _fanController.SetNormalAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // The independent Start-FanNormalRestore.ps1 remains available.
            }
        }
        _fanController.Dispose();
    }

    public KeyboardRgbColor GetKeyboardZoneColor(int zone) => zone switch
    {
        1 => _zone1Color,
        2 => _zone2Color,
        3 => _zone3Color,
        _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };

    public Task SetKeyboardPowerAsync(bool enabled) =>
        RunKeyboardChangeAsync(s => s with { Enabled = enabled });

    public Task ReapplyKeyboardAsync() => RunKeyboardChangeAsync(s => s, forceWrite: true);

    public Task SetKeyboardBrightnessAsync(KeyboardBrightnessLevel level) =>
        level == KeyboardBrightness ? Task.CompletedTask : RunKeyboardChangeAsync(s => s.WithBrightness(level));

    public Task SetKeyboardEffectSpeedAsync(KeyboardEffectSpeed speed) =>
        speed == KeyboardEffectSpeed ? Task.CompletedTask : RunKeyboardChangeAsync(s => s with { Speed = speed });

    public static string DescribeSpeed(KeyboardEffectSpeed speed) => speed switch
    {
        KeyboardEffectSpeed.VerySlow => "Sehr langsam",
        KeyboardEffectSpeed.Slow => "Langsam",
        KeyboardEffectSpeed.Fast => "Schnell",
        KeyboardEffectSpeed.VeryFast => "Sehr schnell",
        _ => "Normal"
    };

    public static string DescribeBrightness(KeyboardBrightnessLevel level) => level switch
    {
        KeyboardBrightnessLevel.Off => "Aus",
        KeyboardBrightnessLevel.Low => "Niedrig",
        KeyboardBrightnessLevel.Medium => "Mittel",
        _ => "Hell"
    };

    public Task SetKeyboardColorAsync(int zone, KeyboardRgbColor color) =>
        RunKeyboardChangeAsync(s => s.WithColor(zone, color, LinkKeyboardZones));

    public Task StartKeyboardEffectAsync(KeyboardRgbEffect effect) =>
        RunKeyboardChangeAsync(s => s with { Effect = effect, Enabled = true });

    public Task StopKeyboardEffectAsync() =>
        RunKeyboardChangeAsync(s => s with { Effect = null });

    public async Task SetFanProfileAsync(string profile)
    {
        if (_closing || _fanBusy || !FanControlsEnabled)
        {
            return;
        }

        _fanBusy = true;
        FanControlsEnabled = false;
        FanStatus = $"{profile} wird gesetzt und geprüft …";
        try
        {
            if (_fixedFanActive && _fixedFanLease is { } activeLease)
            {
                // Best effort: releasing already restores Normal through the worker, so
                // switching straight to the Normal preset costs one harmless extra write
                // below rather than needing special-cased logic to skip it.
                try { await _fixedFanLeaseClient.ReleaseAsync(activeLease); }
                catch { /* Worker's own supervisor remains responsible. */ }
                _fixedFanActive = false;
                _fixedFanLease = null;
            }

            FanProfileChangeResult result = profile switch
            {
                "Quiet" => await _fanController.SetQuietAsync(),
                "Gaming" => await _fanController.SetGamingAsync(),
                "Maximum" => await _fanController.SetMaximumAsync(),
                "Dynamic" => await _fanController.SetDynamicAsync(),
                _ => await _fanController.SetNormalAsync()
            };
            _restoreNormalFanOnDispose = profile is "Maximum" or "Dynamic";
            ApplyFanState(result.VerifiedState, profile);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            AppLog.Error("fan", $"Profil {profile} fehlgeschlagen.", exception);
            FanStatus = $"Lüfteränderung fehlgeschlagen: {exception.Message}";
            await TryReloadFanStateAsync();
        }
        finally
        {
            _fanBusy = false;
            FanControlsEnabled = true;
        }
    }

    public async Task SetFixedFanAsync()
    {
        if (_closing || _fanBusy || !FanControlsEnabled)
        {
            return;
        }

        _fanBusy = true;
        FanControlsEnabled = false;
        FanStatus = $"Fixed {FixedFanRaw} wird gesetzt und geprüft …";
        try
        {
            // The lease client validates telemetry itself before writing; Fixed mode is
            // never authorized on stale or unsafe temperatures. Ensuring a backing
            // worker process exists is that client's own concern (WorkerFixedFanLeaseClient
            // does it internally), not something this ViewModel should know about - it
            // must stay agnostic to which IFixedFanLeaseClient implementation is in use.
            _fixedFanLease = await _fixedFanLeaseClient.AcquireAsync(FixedFanRaw);
            _fixedFanActive = true;
            _isRunning = true;
            ToggleButtonText = "Überwachung stoppen";
            _timer.Start();
            FanControlState state = await _fanController.ReadAsync();
            ApplyFanState(state, $"Fixed {FixedFanRaw}");
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            AppLog.Error("fan", $"Fixed {FixedFanRaw} fehlgeschlagen.", exception);
            FanStatus = $"Fixed fehlgeschlagen: {exception.Message}";
            await TryReloadFanStateAsync();
        }
        finally
        {
            _fanBusy = false;
            FanControlsEnabled = true;
            // Unconditional: see ReannounceSelection's note on why equality-gated
            // notifications are not enough for a one-way bound selection.
            OnPropertyChanged(nameof(ActiveFanProfile));
        }
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

    private async void OnTimerTick(object? sender, EventArgs eventArgs) =>
        await RefreshAsync();

    private async void ToggleMonitoring()
    {
        if (_closing || _fanBusy) return;
        if (_isRunning)
        {
            if (_fixedFanActive)
            {
                await AbandonFixedLeaseAsync("Überwachung wird beendet");
                if (_fixedFanActive) return;
            }
            _timer.Stop();
            _isRunning = false;
            ToggleButtonText = "Überwachung starten";
            Status = "Überwachung angehalten";
            return;
        }

        await StartAsync();
    }

    private async Task RefreshAsync()
    {
        if (_closing || _isReading || (!_dashboardVisible && !_fixedFanActive))
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
            CpuDuty = $"Rohwert {snapshot.CpuFanDutyPercent} / 229";
            GpuDuty = $"Rohwert {snapshot.GpuFanDutyPercent} / 229";
            LastUpdated = $"Letzte Messung: {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}";
            Status = "Live-Telemetrie verbunden";
            // The worker's own lease re-validates temperature on every renewal, using its
            // own independent telemetry read; a failure there already means it has
            // restored Normal by itself before this call returns.
            if (_fixedFanActive && _fixedFanLease is { } activeLease)
            {
                try { await _fixedFanLeaseClient.RenewAsync(activeLease); }
                catch (Exception renewalError) { await AbandonFixedLeaseAsync(renewalError.Message); }
            }
        }
        catch (Exception exception)
        {
            if (_fixedFanActive) await AbandonFixedLeaseAsync("Temperaturmessung ausgefallen");
            // Keep retrying the safety restoration if WMI temporarily fails.
            if (!_fixedFanActive) _timer.Stop();
            _isRunning = _fixedFanActive;
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
        UpdatePreviewTimer();
        // Never pause the existing safety sampling for a manually fixed fan.
        if (visible && _isRunning) _timer.Start();
        else if (!visible && !_fixedFanActive) _timer.Stop();
    }

    /// <summary>
    /// Gives up the app's own claim to Fixed mode. Never retries a failed release itself:
    /// once a lease is acquired, the worker's own supervisor is unconditionally
    /// responsible for eventually restoring Normal, independent of this app's state or
    /// even its continued existence. Retrying from here would just race that guarantee.
    /// </summary>
    private async Task AbandonFixedLeaseAsync(string reason)
    {
        if (_fanBusy) return;
        AppLog.Warn("fan", $"Fixed-Freigabe wird aufgegeben: {reason}");
        _fanBusy = true;
        FanControlsEnabled = false;
        try
        {
            string? releaseFailure = null;
            if (_fixedFanLease is { } lease)
            {
                try { await _fixedFanLeaseClient.ReleaseAsync(lease); }
                catch (Exception releaseError) { releaseFailure = releaseError.Message; }
            }

            _fixedFanActive = false;
            _fixedFanLease = null;
            try
            {
                FanControlState state = await _fanController.ReadAsync();
                ApplyFanState(state, DescribeFanState(state));
            }
            catch
            {
                // Display only; the worker's supervisor remains responsible for the
                // actual hardware state regardless of whether this read succeeds.
            }

            FanStatus = releaseFailure is null
                ? $"{FanStatus} · Sicherheitsrückstellung: {reason}"
                : $"{FanStatus} · {releaseFailure}";
        }
        finally
        {
            _fanBusy = false;
            FanControlsEnabled = true;
        }
    }

    private async Task LoadKeyboardAsync()
    {
        if (_keyboardBusy || _keyboardInitialized)
        {
            return;
        }

        _keyboardBusy = true;
        KeyboardControlsEnabled = false;
        try
        {
            KeyboardLightingSettings? saved = null;
            string? warning = null;
            if (_keyboardSettingsStore is not null)
            {
                try { saved = await Task.Run(_keyboardSettingsStore.Load); }
                catch (Exception exception) { warning = $"Gespeicherte RGB-Auswahl nicht geladen: {exception.Message}"; }
            }
            ApplyKeyboardSettings(saved is null
                ? await _keyboardSession.ReadSettingsAsync()
                : await _keyboardSession.ChangeAsync(_ => saved));
            if (warning is not null) KeyboardStatus = warning + " · Aktueller Gerätezustand gelesen, nichts automatisch überschrieben.";
            _keyboardInitialized = true;
            KeyboardControlsEnabled = true;
            if (_brightnessListener is not null && _brightnessListenerTask is null)
                _brightnessListenerTask = ListenForBrightnessAsync();
        }
        catch (Exception exception)
        {
            AppLog.Error("keyboard", "Tastatur nicht verfügbar.", exception);
            KeyboardStatus = $"Tastatur nicht verfügbar: {exception.Message}";
        }
        finally
        {
            _keyboardBusy = false;
        }
    }

    private async Task LoadFanAsync()
    {
        try
        {
            DeviceCompatibility compatibility = _fanController.CheckCompatibility();
            if (!compatibility.IsSupported)
            {
                FanStatus = compatibility.Message;
                FanCurveStatus = compatibility.Message;
                return;
            }
            FanControlState state = await _fanController.ReadAsync();
            ApplyFanState(state, DescribeFanState(state));
            FanControlsEnabled = true;
            LoadFanCurveOnStartup(state.Curve);
        }
        catch (Exception exception)
        {
            AppLog.Error("fan", "Lüftersteuerung nicht verfügbar.", exception);
            FanStatus = $"Lüftersteuerung nicht verfügbar: {exception.Message}";
            FanCurveStatus = FanStatus;
        }
    }

    private void LoadFanCurveOnStartup(IReadOnlyList<FanCurvePoint> liveCurve)
    {
        try
        {
            IReadOnlyList<FanCurvePoint>? saved = _fanCurveStore.Load();
            PopulateFanCurveRows(saved ?? liveCurve);
            FanCurveStatus = saved is null
                ? "Aktuelle Firmware-Kurve geladen. Noch keine eigene Kurve gespeichert."
                : "Gespeicherte eigene Kurve geladen (erst nach Übernehmen aktiv).";
        }
        catch (Exception exception)
        {
            PopulateFanCurveRows(liveCurve);
            FanCurveStatus = $"Gespeicherte Kurve nicht geladen, Firmware-Kurve angezeigt: {exception.Message}";
        }
    }

    private void PopulateFanCurveRows(IReadOnlyList<FanCurvePoint> curve)
    {
        FanCurveRows.Clear();
        foreach (FanCurvePoint point in curve)
        {
            FanCurveRows.Add(new FanCurveRowViewModel(point.Index + 1)
            {
                Temperature = point.Temperature.ToString(),
                Value = point.Value.ToString()
            });
        }
    }

    private FanCurvePoint[] ParseFanCurveRows()
    {
        if (FanCurveRows.Count != 15)
            throw new InvalidOperationException("Es müssen genau 15 Punkte vorhanden sein.");
        var points = new FanCurvePoint[15];
        for (int index = 0; index < 15; index++)
        {
            FanCurveRowViewModel row = FanCurveRows[index];
            if (!byte.TryParse(row.Temperature, out byte temperature))
                throw new FormatException($"Punkt {index + 1}: ungültige Temperatur.");
            if (!byte.TryParse(row.Value, out byte value))
                throw new FormatException($"Punkt {index + 1}: ungültiger Rohwert.");
            points[index] = new FanCurvePoint((byte)index, temperature, value);
        }
        FanCurveValidation.Validate(points);
        return points;
    }

    /// <summary>
    /// Writes the 15 edited points to the EC and switches into Dynamic mode so they take
    /// immediate effect, then persists them locally. Writing and activating are one user
    /// action rather than two, because a written-but-inactive curve is easy to forget
    /// about and mistake for "not working".
    /// </summary>
    public async Task ApplyFanCurveAsync()
    {
        if (_closing || _fanBusy || !FanControlsEnabled)
        {
            return;
        }

        FanCurvePoint[] points;
        try
        {
            points = ParseFanCurveRows();
        }
        catch (Exception exception)
        {
            FanCurveStatus = $"Ungültige Kurve: {exception.Message}";
            return;
        }

        _fanBusy = true;
        FanControlsEnabled = false;
        FanCurveStatus = "Kurve wird geschrieben und aktiviert …";
        try
        {
            if (_fixedFanActive && _fixedFanLease is { } activeLease)
            {
                try { await _fixedFanLeaseClient.ReleaseAsync(activeLease); }
                catch { /* Worker's own supervisor remains responsible. */ }
                _fixedFanActive = false;
                _fixedFanLease = null;
            }

            await _fanController.SetCurveAsync(points);
            FanProfileChangeResult activated = await _fanController.SetDynamicAsync();
            _restoreNormalFanOnDispose = true;
            ApplyFanState(activated.VerifiedState, "Eigene Kurve (Dynamic)");
            _fanCurveStore.Save(points);
            FanCurveStatus = "Eigene Kurve übernommen, aktiv und gespeichert.";
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            AppLog.Error("fan", "Kurve fehlgeschlagen.", exception);
            FanCurveStatus = $"Kurve fehlgeschlagen: {exception.Message}";
            await TryReloadFanStateAsync();
        }
        finally
        {
            _fanBusy = false;
            FanControlsEnabled = true;
        }
    }

    /// <summary>Discards edits and re-reads whatever curve is currently on the EC —
    /// an escape hatch back to known hardware truth, not a guessed default.</summary>
    /// <summary>The waiting curve write, so shutdown and tests can observe it rather than
    /// guess at a clock.</summary>
    internal Debouncer PendingFanCurveWrite => _applyFanCurve;

    /// <summary>The waiting Fixed re-apply, exposed for the same reason.</summary>
    internal Debouncer PendingFixedFanWrite => _applyFixedFan;

    /// <summary>Called by the chart after a drag: the curve writes itself once the user
    /// stops moving points, so there is no apply button to forget.</summary>
    public void ScheduleFanCurveApply()
    {
        if (_closing || _disposed || !FanControlsEnabled) return;
        FanCurveStatus = "Änderung wird gleich übernommen …";
        _applyFanCurve.Schedule();
    }

    /// <summary>The debounced curve write. With the apply button gone there is nobody to
    /// retry a change that lands while the fan controller is busy, so it waits its turn.</summary>
    private Task ApplyPendingFanCurveAsync()
    {
        if (_closing || _disposed) return Task.CompletedTask;
        if (_fanBusy || !FanControlsEnabled) { _applyFanCurve.Schedule(); return Task.CompletedTask; }
        return ApplyFanCurveAsync();
    }

    /// <summary>Re-applies a Fixed value while Fixed mode is already held.</summary>
    private async Task ReapplyFixedFanAsync()
    {
        if (_closing || _disposed || !_fixedFanActive) return;
        await SetFixedFanAsync();
    }

    public async Task ReloadFanCurveFromDeviceAsync()
    {
        if (_closing || _fanBusy)
        {
            return;
        }

        _fanBusy = true;
        try
        {
            FanControlState state = await _fanController.ReadAsync();
            _applyFanCurve.Cancel();
            PopulateFanCurveRows(state.Curve);
            FanCurveStatus = "Aktuelle Firmware-Kurve geladen (noch nicht gespeichert oder aktiviert).";
        }
        catch (Exception exception)
        {
            FanCurveStatus = $"Kurve konnte nicht gelesen werden: {exception.Message}";
        }
        finally
        {
            _fanBusy = false;
        }
    }

    private async Task TryReloadFanStateAsync()
    {
        try
        {
            FanControlState state = await _fanController.ReadAsync();
            FanStatus += $" · Rückgelesen: {DescribeFanState(state)}";
        }
        catch
        {
            // Keep the original, rollback-aware error.
        }
    }

    private void ApplyFanState(FanControlState state, string profile)
    {
        FixedFanRaw = state.FixedSpeedRaw is >= 57 and <= 229
            ? checked((byte)state.FixedSpeedRaw)
            : FixedFanRaw;
        ActiveFanProfile = DescribeFanProfileKey(state);
        FanStatus = $"Aktiv: {profile} · Fixed {state.FixedStatusRaw} · Step {state.StepStatusRaw} · Auto {state.AutoStatusRaw} · Thermal {state.NvidiaThermalTargetRaw}";
    }

    /// <summary>The chip identity for a read-back state. "Fixed" is its own key so no
    /// profile chip lights up while a manual fixed value is held.</summary>
    private static string DescribeFanProfileKey(FanControlState state) =>
        state.FixedStatusRaw == 1
            ? state.FixedSpeedRaw == 229 ? "Maximum" : "Fixed"
            : state.NvidiaThermalTargetRaw == 1
                ? "Quiet"
                : state.AutoStatusRaw == 1
                    ? "Gaming"
                    : state.StepStatusRaw == 1
                        ? "Dynamic"
                        : "Normal";

    private static string DescribeFanState(FanControlState state) =>
        state.FixedStatusRaw == 1
            ? state.FixedSpeedRaw == 229 ? "Maximum" : $"Fixed {state.FixedSpeedRaw}"
            : state.NvidiaThermalTargetRaw == 1
                ? "Quiet"
                : state.AutoStatusRaw == 1
                    ? "Gaming"
                    : state.StepStatusRaw == 1
                        ? "Dynamic"
                        : "Normal";

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

    private async Task RunKeyboardChangeAsync(Func<KeyboardLightingSettings, KeyboardLightingSettings> change, bool forceWrite = false)
    {
        if (_closing || _disposed || _keyboardBusy || !KeyboardControlsEnabled) return;
        _keyboardBusy = true;
        KeyboardControlsEnabled = false;
        KeyboardStatus = "Einstellung wird übernommen …";
        try
        {
            KeyboardLightingSettings state = forceWrite ? await _keyboardSession.ReapplyAsync() : await _keyboardSession.ChangeAsync(change);
            ApplyKeyboardSettings(state);
            if (_keyboardSettingsStore is not null)
            {
                try { await Task.Run(() => _keyboardSettingsStore.Save(state)); }
                catch (Exception exception) { KeyboardStatus += $" · Aktiv, aber nicht gespeichert: {exception.Message}"; }
            }
        }
        catch (Exception exception)
        {
            ApplyKeyboardSettings(await _keyboardSession.ReadSettingsAsync());
            KeyboardEffectRunning = false;
            _rgbTimer.Stop();
            AppLog.Error("keyboard", "RGB-Änderung fehlgeschlagen.", exception);
            KeyboardStatus = $"RGB-Änderung fehlgeschlagen: {exception.Message}. Auswahl erneut anwenden.";
        }
        finally
        {
            _keyboardBusy = false;
            KeyboardControlsEnabled = true;
            OnPropertyChanged(nameof(ActiveKeyboardEffectName));
            OnPropertyChanged(nameof(KeyboardBrightnessIndex));
            OnPropertyChanged(nameof(KeyboardEffectSpeedIndex));
        }
    }

    private async Task ListenForBrightnessAsync()
    {
        BrightnessEventStatus = "Fn+Space-Ereignisleser wird gestartet; noch kein Ereignis empfangen";
        try
        {
            CancellationToken token = _brightnessCancellation.Token;
            await KeyboardNotificationReconnect.RunAsync(_brightnessListener!, level => _dispatcher.BeginInvoke(new Action(() =>
            {
                if (!token.IsCancellationRequested && !_closing && !_disposed) _ = QueueExternalBrightness(level);
            })), (error, pause) => _dispatcher.BeginInvoke(new Action(() =>
            {
                if (!token.IsCancellationRequested && !_disposed)
                    BrightnessEventStatus = $"Fn+Space nicht verbunden; neuer Versuch in {pause.TotalSeconds:0} s: {error.Message}";
            })), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_brightnessCancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            _ = _dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_disposed) BrightnessEventStatus = "Fn+Space-Synchronisierung nicht verfügbar: " + error.Message;
            }));
        }
    }

    private void RestartBrightnessAfterFailedClose()
    {
        if (_brightnessListener is null || !_keyboardInitialized || _disposed) return;
        _brightnessCancellation.Dispose();
        _brightnessCancellation = new();
        _brightnessListenerTask = ListenForBrightnessAsync();
    }

    internal Task QueueExternalBrightness(KeyboardBrightnessLevel level)
    {
        if (_closing || _disposed) return Task.CompletedTask;
        if (!KeyboardBrightnessLevels.All.Contains(level)) throw new ArgumentOutOfRangeException(nameof(level));
        _pendingBrightness = level;
        if (!_drainingBrightness) _brightnessDrainTask = DrainBrightnessAsync();
        return _brightnessDrainTask;
    }

    private async Task DrainBrightnessAsync()
    {
        _drainingBrightness = true;
        try
        {
            while (!_closing && !_disposed && _pendingBrightness is not null)
            {
                // Only wait while an event is pending; no idle polling.
                while (_keyboardBusy && !_closing && !_disposed) await Task.Delay(25);
                if (_closing || _disposed) break;
                if (!KeyboardControlsEnabled) { BrightnessEventStatus = "Fn+Space erkannt; RGB-Steuerung nicht bereit."; break; }
                var level = _pendingBrightness.Value;
                _pendingBrightness = null;
                if (level == KeyboardBrightness)
                {
                    BrightnessEventStatus = "Fn+Space-Ereignis empfangen; Helligkeit bereits aktuell.";
                    continue; // Avoid feedback writes and repeated disk saves for identical reports.
                }
                await RunKeyboardChangeAsync(s => s.WithBrightness(level));
                BrightnessEventStatus = "Fn+Space-Ereignis verarbeitet; RGB-Ergebnis siehe Status oben.";
            }
        }
        catch (Exception error) { BrightnessEventStatus = "Fn+Space-Übernahme fehlgeschlagen: " + error.Message; }
        finally { _pendingBrightness = null; _drainingBrightness = false; }
    }

    /// <summary>
    /// A well-known weak spot in RGB keyboard software: the USB HID lighting controller
    /// often resets to its own power-on default after the laptop sleeps and wakes,
    /// silently discarding whatever the user had set, and most tools never notice because
    /// they only ever write on user action. Reapplying proactively after resume is the
    /// fix; the delay gives the USB device a moment to re-enumerate before the first write
    /// after wake, which otherwise reliably fails on this hardware.
    /// </summary>
    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs eventArgs)
    {
        if (eventArgs.Mode != Microsoft.Win32.PowerModes.Resume) return;
        // SystemEvents raises this on its own dedicated thread, not the UI dispatcher.
        _dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_closing && !_disposed) _ = ReapplyAfterResumeAsync();
        }));
    }

    internal async Task ReapplyAfterResumeAsync()
    {
        if (_closing || _disposed || !_keyboardInitialized) return;
        await _resumeReapplyDelay(TimeSpan.FromSeconds(2));
        if (_closing || _disposed) return;
        await ReapplyKeyboardAsync();
    }

    private void OnPreviewTimerTick(object? sender, EventArgs args) => RenderPreviewFrame();

    /// <summary>
    /// Starts the preview clock only when it can be seen and there is something moving:
    /// the Tastatur section on screen, the window visible, an effect actually running.
    /// A static (manual) selection needs no timer at all - it is painted once.
    /// </summary>
    private void UpdatePreviewTimer()
    {
        bool wanted = !_closing && !_disposed && _dashboardVisible
            && SelectedSection == "Lighting"
            && KeyboardPowerOn
            && _activeKeyboardEffect is not null;
        if (wanted && !_previewTimer.IsEnabled) _previewTimer.Start();
        else if (!wanted && _previewTimer.IsEnabled) _previewTimer.Stop();
        RenderPreviewFrame();
    }

    private void RenderPreviewFrame()
    {
        PreviewOpacity = KeyboardBrightness switch
        {
            KeyboardBrightnessLevel.Off => 0.10,
            KeyboardBrightnessLevel.Low => 0.45,
            KeyboardBrightnessLevel.Medium => 0.72,
            _ => 1.0
        };

        if (!KeyboardPowerOn)
        {
            SetPreviewZones(new KeyboardRgbColor(0, 0, 0), new KeyboardRgbColor(0, 0, 0), new KeyboardRgbColor(0, 0, 0));
            return;
        }

        if (_activeKeyboardEffect is not { } effect)
        {
            SetPreviewZones(_zone1Color, _zone2Color, _zone3Color);
            return;
        }

        // Same call the renderer makes, with the same time scale - the clock started
        // when this effect did, so the phase tracks the device rather than drifting on
        // its own schedule.
        double elapsed = _effectClock.Elapsed.TotalSeconds * KeyboardEffectSpeed.ToTimeScale();
        KeyboardRgbColor[] frame = KeyboardEffectFrames.Create(effect, elapsed, _zone1Color);
        SetPreviewZones(frame[0], frame[1], frame[2]);
    }

    private void SetPreviewZones(KeyboardRgbColor zone1, KeyboardRgbColor zone2, KeyboardRgbColor zone3)
    {
        PreviewZone1Brush = CreateBrush(zone1);
        PreviewZone2Brush = CreateBrush(zone2);
        PreviewZone3Brush = CreateBrush(zone3);
    }

    private async void OnRgbTimerTick(object? sender, EventArgs args)
    {
        if (_closing || _disposed || _keyboardBusy) return;
        _keyboardBusy = true;
        try { await _keyboardSession.CheckEffectAsync(); }
        catch (Exception exception)
        {
            KeyboardEffectRunning = false;
            _rgbTimer.Stop();
            KeyboardStatus = $"Effekt beendet: {exception.Message}. Effekt erneut anwenden oder Manuell wählen.";
        }
        finally { _keyboardBusy = false; }
    }

    private static string GetEffectName(KeyboardRgbEffect effect) => effect switch
    {
        KeyboardRgbEffect.Breathing => "Atmen",
        KeyboardRgbEffect.Pulse => "Pulsieren",
        KeyboardRgbEffect.ColorCycle => "Farbwechsel",
        KeyboardRgbEffect.RainbowMarquee => "Regenbogen-Lauflicht",
        KeyboardRgbEffect.Wave => "Welle",
        KeyboardRgbEffect.Marquee => "Lauflicht",
        KeyboardRgbEffect.Rotate => "Pendel",
        KeyboardRgbEffect.Raindrop => "Regentropfen",
        KeyboardRgbEffect.FadeSweep => "Ausblendende Welle",
        _ => effect.ToString()
    };

    private void ApplyKeyboardSettings(KeyboardLightingSettings state)
    {
        _zone1Color = state.Left;
        _zone2Color = state.Center;
        _zone3Color = state.Right;
        KeyboardPowerOn = state.Enabled;
        OnPropertyChanged(nameof(KeyboardPowerButtonText));
        KeyboardBrightness = state.Enabled ? state.OnBrightness : KeyboardBrightnessLevel.Off;
        KeyboardEffectSpeed = state.Speed;
        KeyboardEffectRunning = state.Enabled && state.Effect is not null;
        KeyboardModeIsEffect = state.Effect is not null;
        OnPropertyChanged(nameof(KeyboardBrightnessIndex));
        OnPropertyChanged(nameof(KeyboardBrightnessLabel));
        OnPropertyChanged(nameof(KeyboardEffectSpeedIndex));
        OnPropertyChanged(nameof(KeyboardEffectSpeedLabel));

        // Restart the preview clock only when the running effect actually changed, so a
        // brightness or colour tweak does not visibly jump the animation's phase.
        KeyboardRgbEffect? running = state.Enabled ? state.Effect : null;
        if (running != _activeKeyboardEffect || (running is not null && !_effectClock.IsRunning))
        {
            _effectClock.Restart();
        }
        if (running is null) _effectClock.Reset();
        ActiveKeyboardEffect = running;
        KeyboardStatus = $"{(state.Enabled ? "Ein" : "Aus · Auswahl gespeichert")} · " +
            (state.Effect is { } effect ? GetEffectName(effect) : "Manuelle Zonenfarben");
        KeyboardPaletteHint = KeyboardEffectFrames.ColorUsage(running) switch
        {
            KeyboardEffectColorUsage.AllZones => "Alle drei gespeicherten Farben werden direkt angezeigt.",
            KeyboardEffectColorUsage.BaseColorOnly =>
                "Dieser Effekt moduliert die Farbe von Zone 1 über alle drei Zonen. Zone 2 und 3 bleiben gespeichert und gelten wieder im manuellen Modus.",
            _ => "Dieser Effekt bringt seine eigene Palette mit; keine der gespeicherten Farben wird gelesen. Sie bleiben erhalten und gelten wieder im manuellen Modus."
        };
        if (KeyboardEffectRunning && !_closing) _rgbTimer.Start(); else _rgbTimer.Stop();
        OnPropertyChanged(nameof(Zone1Hex));
        OnPropertyChanged(nameof(Zone2Hex));
        OnPropertyChanged(nameof(Zone3Hex));
        OnPropertyChanged(nameof(Zone1Brush));
        OnPropertyChanged(nameof(Zone2Brush));
        OnPropertyChanged(nameof(Zone3Brush));
        OnPropertyChanged(nameof(PreviewCaption));
        OnPropertyChanged(nameof(KeyboardColorUsage));
        OnPropertyChanged(nameof(Zone1AffectsLighting));
        OnPropertyChanged(nameof(Zone2AffectsLighting));
        OnPropertyChanged(nameof(Zone3AffectsLighting));
        OnPropertyChanged(nameof(Zone1Label));
        OnPropertyChanged(nameof(InactiveZoneNote));
        OnPropertyChanged(nameof(HasInactiveZones));
        UpdatePreviewTimer();
    }

    private static MediaBrush CreateBrush(KeyboardRgbColor color)
    {
        var brush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(color.Red, color.Green, color.Blue));
        brush.Freeze();
        return brush;
    }
}
