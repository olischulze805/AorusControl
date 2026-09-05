using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows.Input;
using MediaBrush = System.Windows.Media.Brush;
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
    private string _powerSource = "Stromquelle wird gelesen …";
    private string _lastUpdated = "Noch keine Messung";
    private string _toggleButtonText = "Überwachung starten";
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
        Keyboard = new KeyboardViewModel(keyboardRgb, keyboardSettingsStore, brightnessListener, resumeReapplyDelay);
        // Defaults to the real out-of-process worker client: only that implementation
        // survives this process crashing, which is the entire point of Fixed-mode safety.
        _fixedFanLeaseClient = fixedFanLeaseClient ?? new WorkerFixedFanLeaseClient();
        _fanCurveStore = fanCurveStore ?? new FanCurveStore(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AorusControl", "fan-curve-v1.json"));
        _startupManager = startupManager ?? new StartupManager(Environment.ProcessPath ?? "AorusControl.exe");
        Battery = new BatteryViewModel(batteryController ?? new GigabyteWmiBatteryChargeController(), debounceWait);
        Updates = new UpdateViewModel();
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
    }

    private string _activeFanProfile = "Normal";
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
    public AsyncRelayCommand<string> SetFanProfileCommand { get; }
    public AsyncRelayCommand SetFixedFanCommand { get; }
    public AsyncRelayCommand<string> SetPowerModeCommand { get; }
    public AsyncRelayCommand ApplyFanCurveCommand { get; }
    public AsyncRelayCommand ReloadFanCurveFromDeviceCommand { get; }
    public AsyncRelayCommand ToggleStartWithWindowsCommand { get; }
    /// <summary>The attached feature modules. Everything the shell does to all of them -
    /// start them, wait for them, release them - goes through this list, so a new feature
    /// is a class plus one entry rather than another branch in three methods.</summary>
    private IReadOnlyList<IFeatureModule> Modules => [Keyboard, Battery];

    public KeyboardViewModel Keyboard { get; }
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

    /// <summary>The cooling that is actually in force, next to the mode - so "was habe ich
    /// gerade geändert?" has an answer that includes the part Windows does not control.</summary>
    public string CoolingSummary => ActiveFanProfile switch
    {
        "Fixed" => $"Fester Wert {FanSpeedPercent.ToPercent(_fixedFanRaw)} % · die Kurve unten ist gespeichert, aber gerade außer Kraft.",
        "Maximum" => "Maximum · Lüfter laufen unabhängig von der Kurve auf voller Stufe.",
        "Dynamic" => "Dynamic · die Kurve unten regelt die Lüfter.",
        "Quiet" => "Quiet · Firmware-Regelung, leiser als die Kurve unten.",
        "Gaming" => "Gaming · Firmware-Regelung, aggressiver als die Kurve unten.",
        _ => "Normal · Firmware-Standardregelung, nicht die Kurve unten."
    };

    /// <summary>Where the log files are, so "look in the log" is an actionable
    /// instruction rather than a scavenger hunt.</summary>
    public string LogDirectory => AppLog.Directory;


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
            foreach (IFeatureModule module in Modules) await module.StartAsync();
            await LoadFanAsync();
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
        try { await _applyFanCurve.FlushAsync(); } catch (Exception error) { AppLog.Error("fan", "Ausstehende Kurve nicht mehr geschrieben.", error); }
        try { await _applyFixedFan.FlushAsync(); } catch (Exception error) { AppLog.Error("fan", "Ausstehender Fixed-Wert nicht mehr geschrieben.", error); }
        try { await Battery.PendingLimitWrite.FlushAsync(); } catch (Exception error) { AppLog.Error("battery", "Ausstehendes Ladelimit nicht mehr geschrieben.", error); }

        _closing = true;
        await Keyboard.StopListeningAsync();
        _timer.Stop();
        while (_starting || _fanBusy || _powerBusy || _isReading || Modules.Any(module => module.IsBusy))
            await Task.Delay(50);
        _timer.Stop();
        try { await Keyboard.SuspendAsync(); }
        catch
        {
            // The lighting stayed with us, so the window stays open and everything that was
            // stopped for the close has to come back up.
            _closing = false;
            Keyboard.ResumeAfterFailedClose();
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
                Keyboard.ResumeAfterFailedClose();
                if (_isRunning) _timer.Start();
                throw;
            }
        }
    }

    /// <summary>
    /// Hands the fans back to the firmware, synchronously and best-effort.
    ///
    /// Called both on dispose and when Windows is shutting down or logging off: without the
    /// second case a machine that shut down while Fixed or Maximum was held would come back
    /// up with the fans still pinned there, with nothing running that knows why. Windows
    /// gives a process a few seconds at SessionEnding, which is enough for one EC write.
    /// </summary>
    public void RestoreFansToFirmware()
    {
        if (_fixedFanActive && _fixedFanLease is { } lease)
        {
            try { _fixedFanLeaseClient.ReleaseAsync(lease).GetAwaiter().GetResult(); }
            catch { /* Worker's own supervisor remains responsible. */ }
            _fixedFanActive = false;
            _fixedFanLease = null;
        }

        if (!_restoreNormalFanOnDispose) return;
        try
        {
            _fanController.SetNormalAsync().GetAwaiter().GetResult();
            _restoreNormalFanOnDispose = false;
        }
        catch (Exception error)
        {
            // The independent Start-FanNormalRestore.ps1 remains available.
            AppLog.Error("fan", "Lüfter konnten nicht auf Normal zurückgestellt werden.", error);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _applyFanCurve.Cancel();
        _applyFixedFan.Cancel();
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _reader.Dispose();
        foreach (IFeatureModule module in Modules) module.Dispose();
        RestoreFansToFirmware();
        _fanController.Dispose();
    }


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

    /// <summary>Duty as a share of the firmware's own maximum, with the raw byte kept as
    /// the smaller half - the percentage is what answers "how hard is it working".</summary>
    private static string DescribeDuty(ushort raw) =>
        $"{FanSpeedPercent.ToPercent((byte)Math.Min(raw, (ushort)255))} % Leistung · Rohwert {raw}";

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
            CpuDuty = DescribeDuty(snapshot.CpuFanDutyPercent);
            GpuDuty = DescribeDuty(snapshot.GpuFanDutyPercent);
            PowerSource = _powerOverlay.IsOnAcPower() ? "Netzbetrieb" : "Akkubetrieb";
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
        UpdateModuleVisibility();
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
    /// <summary>Tells each module whether its own section is actually on screen, so
    /// nothing animates or polls for a view nobody is looking at.</summary>
    private void UpdateModuleVisibility() =>
        Keyboard.IsVisible = _dashboardVisible && SelectedSection == "Lighting";

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
        OnPropertyChanged(nameof(CoolingSummary));
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


}
