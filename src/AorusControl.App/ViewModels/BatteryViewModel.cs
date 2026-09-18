using AorusControl.App.Infrastructure;
using AorusControl.Core.Features.Battery;
using AorusControl.Core.Features.Diagnostics;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

namespace AorusControl.App.ViewModels;

/// <summary>UI state only; firmware validation and transactional writes stay in Core.</summary>
public sealed class BatteryViewModel : ObservableObject, IFeatureModule
{
    private readonly IAorusBatteryChargeController controller;
    private bool _busy;
    private bool _supported;
    private bool _disposed;
    private int _selectedLimit = 80;
    private string _status = "Ladelimit wird gelesen …";
    private string _activePolicy = "Noch nicht gelesen";

    private readonly IBatterySettingsStore? _store;
    private readonly bool _watchingResume;
    private bool _restoreDone;

    public BatteryViewModel(
        IAorusBatteryChargeController controller,
        Func<TimeSpan, CancellationToken, Task>? wait = null,
        IBatterySettingsStore? store = null,
        bool watchResume = true)
    {
        this.controller = controller;
        _store = store;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ApplyStandardCommand = new AsyncRelayCommand(ApplyStandardAsync);
        if (watchResume) Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _watchingResume = watchResume;
        // Dragging the limit applies by itself once the slider comes to rest. The wait is
        // long enough that a drag across the range is one transaction, not forty.
        _applyLimit = new Debouncer(TimeSpan.FromMilliseconds(700), ApplyPendingLimitAsync, wait);
    }

    private readonly Debouncer _applyLimit;

    /// <summary>Exposed so shutdown can write a value the user set moments earlier
    /// instead of dropping it, and so tests need not wait on a real clock.</summary>
    internal Debouncer PendingLimitWrite => _applyLimit;

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            SetProperty(ref _busy, value);
            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(CanRefresh));
            OnPropertyChanged(nameof(CanAdjust));
            OnPropertyChanged(nameof(CanAdjustLimit));
        }
    }
    public bool CanApply => _supported && !IsBusy && !_disposed;

    /// <summary>
    /// Whether a charge limit is in force at all - the switch above the slider.
    ///
    /// Two mutually exclusive states that take effect at once is the definition of a switch.
    /// It used to be a button marked "Standardladen", which could only travel in one
    /// direction: getting the limit back meant nudging the slider until the app noticed, and
    /// which of the two states was active could only be read out of a line of prose.
    /// </summary>
    public bool IsLimitEnabled
    {
        get => _limitEnabled;
        set
        {
            if (!SetProperty(ref _limitEnabled, value)) return;
            OnPropertyChanged(nameof(CanAdjustLimit));
            // Not while a readback is populating the card - that would write back the state
            // the device has just reported.
            if (_applyingDeviceState) return;
            Status = value ? $"Ladelimit {SelectedLimit} % wird gesetzt …" : "Standardladen wird gesetzt …";
            _ = value ? ApplyLimitAsync() : ApplyStandardAsync();
        }
    }

    /// <summary>The slider belongs to the switch above it: with standard charging in force
    /// there is no limit for it to set, and a live control that changes nothing is worse
    /// than one that is visibly unavailable.</summary>
    public bool CanAdjustLimit => CanAdjust && _limitEnabled;

    /// <summary>The slider stays usable while a write is in flight. Since the limit now
    /// applies itself, gating it on <see cref="IsBusy"/> would make it go dead under the
    /// user's own hand every time their drag settles - the debouncer already collapses
    /// what they do in the meantime into a single later write.</summary>
    public bool CanAdjust => _supported && !_disposed;
    public bool CanRefresh => !IsBusy && !_disposed;
    public int SelectedLimit
    {
        get => _selectedLimit;
        set
        {
            if (!SetProperty(ref _selectedLimit, value)) return;
            // Not while the readback is populating the slider - that would write back the
            // value the device just reported.
            if (_applyingDeviceState) return;
            Status = $"{value} % wird übernommen …";
            _applyLimit.Schedule();
        }
    }

    private bool _applyingDeviceState;
    private bool _limitEnabled = true;
    private bool _deviceIsStandard, _deviceIsCustom;
    private int _deviceLimit;
    public IReadOnlyList<int> LimitChoices { get; } = Enumerable.Range(60, 41).ToArray();

    /// <summary>Where the device stops charging, or null when the firmware decides. The
    /// dashboard needs it to say when charging will finish: with a limit of 80 % the pack
    /// stops there, and a figure counted up to 100 % would promise an hour that never comes.</summary>
    public int? StopPercent => _deviceIsCustom && _deviceLimit is >= 60 and <= 100 ? _deviceLimit : null;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string ActivePolicy { get => _activePolicy; private set => SetProperty(ref _activePolicy, value); }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ApplyStandardCommand { get; }

    /// <summary>
    /// The module's start: read the device, and write only if it disagrees with what the user
    /// last chose. That one exception is the whole point - the controller does not reliably
    /// keep the limit across a restart, and the app used to believe whatever it found.
    /// </summary>
    public Task StartAsync() => RefreshAsync(restoreSaved: true);

    public Task RefreshAsync() => RefreshAsync(restoreSaved: false);

    private async Task RefreshAsync(bool restoreSaved)
    {
        if (!CanRefresh) return;
        BatterySettings? restore = null;
        IsBusy = true;
        _supported = false;
        try
        {
            DeviceCompatibility compatibility = await Task.Run(controller.CheckCompatibility);
            if (!compatibility.IsSupported) { ActivePolicy = "Gerät nicht freigegeben"; Status = compatibility.Message; return; }
            ShowState(await controller.ReadAsync());
            Status = "Bereit. Der Regler übernimmt sich kurz nach dem Loslassen von selbst.";
            if (restoreSaved) restore = PendingRestore();
        }
        catch (Exception exception) { ActivePolicy = "Nicht verfügbar"; Status = $"Lesen fehlgeschlagen: {exception.Message}"; }
        finally { IsBusy = false; }

        // Outside the busy block on purpose: the write below goes through the same guard as a
        // user's own change, and would refuse to run while this read still held it.
        if (restore is { } saved) await RestoreAsync(saved);
    }

    /// <summary>
    /// The saved choice, but only when the device is actually somewhere else. Returning null
    /// is the normal case: nothing to do, and nothing written.
    /// </summary>
    private BatterySettings? PendingRestore()
    {
        if (_restoreDone || _store is null || !_supported) return null;
        _restoreDone = true;
        BatterySettings? saved = _store.Load();
        if (saved is null) return null;
        bool alreadyThere = saved.StandardMode ? _deviceIsStandard : _deviceIsCustom && _deviceLimit == saved.Limit;
        return alreadyThere ? null : saved;
    }

    private async Task RestoreAsync(BatterySettings saved)
    {
        string wanted = saved.StandardMode ? "Standardladen" : $"Ladelimit {saved.Limit} %";
        AppLog.Info("battery", $"{wanted} wird nach dem Start wiederhergestellt.");
        await ChangeAsync(saved.StandardMode ? null : saved.Limit, remember: false);
        if (_supported) Status = $"{wanted} war nach dem Neustart nicht mehr gesetzt und wurde wiederhergestellt.";
    }

    public Task ApplyLimitAsync() => ChangeAsync(SelectedLimit);

    /// <summary>The debounced write. Without an apply button there is nobody left to retry
    /// a change that arrives while a read or another write holds the controller, so it
    /// waits its turn instead of being dropped on the floor.</summary>
    private Task ApplyPendingLimitAsync()
    {
        if (_disposed) return Task.CompletedTask;
        if (IsBusy) { _applyLimit.Schedule(); return Task.CompletedTask; }
        return ApplyLimitAsync();
    }
    public Task ApplyStandardAsync() => ChangeAsync(null);

    private async Task ChangeAsync(int? limit, bool remember = true)
    {
        if (!CanApply) return;
        if (limit is < 60 or > 100) { Status = "Bitte ein Limit von 60 bis 100 % wählen."; return; }
        IsBusy = true;
        try
        {
            BatteryChargeChangeResult result = limit is { } value
                ? await controller.SetCustomLimitAsync(value)
                : await controller.SetStandardModeAsync();
            ShowState(result.VerifiedState);
            // Saved only after the read-back confirmed it, so the file always describes a
            // setting that really took effect rather than one that was merely asked for.
            if (remember) Remember(limit);
            Status = "Einstellung übernommen und rückgelesen. Bleibt nach dem Schließen aktiv - auch über einen Neustart.";
        }
        catch (Exception exception)
        {
            _supported = false;
            ActivePolicy = "Nach Fehler noch nicht bestätigt";
            try { ShowState(await controller.ReadAsync()); }
            catch { /* Preserve unknown state and the original error; require refresh. */ }
            Status = $"Änderung fehlgeschlagen: {exception.Message}";
        }
        finally { IsBusy = false; }
    }

    private void Remember(int? limit)
    {
        if (_store is null) return;
        // A choice the user made is also the choice to stop restoring the previous one, so
        // this runs before any failure below can leave the old value in the file.
        _restoreDone = true;
        try { _store.Save(new BatterySettings(limit is null, limit ?? 0)); }
        catch (Exception exception)
        {
            AppLog.Error("battery", "Ladelimit konnte nicht gespeichert werden.", exception);
            Status += " Konnte aber nicht gespeichert werden - nach einem Neustart steht wieder, was das Gerät meldet.";
        }
    }

    private void ShowState(BatteryChargeState state)
    {
        _supported = (state.IsStandardMode || state.IsCustomMode) && state.StoredStopPercent is >= 60 and <= 100;
        OnPropertyChanged(nameof(CanAdjust));
        OnPropertyChanged(nameof(CanAdjustLimit));
        _deviceIsStandard = state.IsStandardMode;
        _deviceIsCustom = state.IsCustomMode;
        _deviceLimit = state.StoredStopPercent;
        OnPropertyChanged(nameof(StopPercent));
        // Start the slider and the switch where the device actually is. The slider used to
        // keep its own default, which read as a claim about the hardware next to the large
        // percentage readout.
        _applyingDeviceState = true;
        try
        {
            if (state.StoredStopPercent is >= 60 and <= 100) SelectedLimit = state.StoredStopPercent;
            if (state.IsStandardMode || state.IsCustomMode) IsLimitEnabled = state.IsCustomMode;
        }
        finally { _applyingDeviceState = false; }
        ActivePolicy = state.IsCustomMode
            ? $"Aktiv: Ladelimit {state.StoredStopPercent} %"
            : state.IsStandardMode ? "Aktiv: Standardladen (BIOS-gesteuert)"
            : $"Unbekannter Modus {state.PolicyRaw} · gespeicherter Wert {state.StoredStopPercent}";
        if (!_supported) ActivePolicy += " · Schreiben gesperrt";
    }

    /// <summary>
    /// Checks the limit again after the machine wakes up.
    ///
    /// The controller does not reliably keep it: that is why the saved value is restored at
    /// start in the first place. Sleep is the same kind of gap - the embedded controller can
    /// come back with the firmware's own threshold - and until now nothing looked again until
    /// the next launch, so a laptop that is suspended rather than shut down could run for
    /// weeks charging to full without anyone noticing.
    ///
    /// The restore itself stays where it was: a write happens only when the device really
    /// disagrees with the saved choice.
    /// </summary>
    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs args)
    {
        if (_disposed || args.Mode != Microsoft.Win32.PowerModes.Resume) return;
        // SystemEvents raises this on its own thread; the refresh touches bound properties.
        _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => ReapplyAfterResumeAsync());
    }

    /// <summary>The resume check itself, separated so it can be run without a real suspend.</summary>
    internal Task ReapplyAfterResumeAsync()
    {
        // The start-up restore runs once per launch on purpose. Waking up is the one other
        // moment the device may have been reset underneath us, so it is allowed once more.
        _restoreDone = false;
        return RefreshAsync(restoreSaved: true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_watchingResume) Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        if (IsBusy) throw new InvalidOperationException("Laufende Akkuoperation muss vor Dispose beendet werden.");
        _disposed = true;
        controller.Dispose();
    }
}
