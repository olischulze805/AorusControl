using AorusControl.App.Infrastructure;
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

    public BatteryViewModel(
        IAorusBatteryChargeController controller,
        Func<TimeSpan, CancellationToken, Task>? wait = null)
    {
        this.controller = controller;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ApplyStandardCommand = new AsyncRelayCommand(ApplyStandardAsync);
        // Dragging the limit applies by itself once the slider comes to rest. The wait is
        // long enough that a drag across the range is one transaction, not forty.
        _applyLimit = new Debouncer(TimeSpan.FromMilliseconds(700), ApplyPendingLimitAsync, wait);
    }

    private readonly Debouncer _applyLimit;

    /// <summary>Exposed so shutdown can write a value the user set moments earlier
    /// instead of dropping it, and so tests need not wait on a real clock.</summary>
    internal Debouncer PendingLimitWrite => _applyLimit;

    public bool IsBusy { get => _busy; private set { SetProperty(ref _busy, value); OnPropertyChanged(nameof(CanApply)); OnPropertyChanged(nameof(CanRefresh)); OnPropertyChanged(nameof(CanAdjust)); } }
    public bool CanApply => _supported && !IsBusy && !_disposed;

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
    public IReadOnlyList<int> LimitChoices { get; } = Enumerable.Range(60, 41).ToArray();
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string ActivePolicy { get => _activePolicy; private set => SetProperty(ref _activePolicy, value); }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ApplyStandardCommand { get; }

    /// <summary>The module's start: read the device, write nothing.</summary>
    public Task StartAsync() => RefreshAsync();

    public async Task RefreshAsync()
    {
        if (!CanRefresh) return;
        IsBusy = true;
        _supported = false;
        try
        {
            DeviceCompatibility compatibility = await Task.Run(controller.CheckCompatibility);
            if (!compatibility.IsSupported) { ActivePolicy = "Gerät nicht freigegeben"; Status = compatibility.Message; return; }
            ShowState(await controller.ReadAsync());
            Status = "Bereit. Der Regler übernimmt sich kurz nach dem Loslassen von selbst.";
        }
        catch (Exception exception) { ActivePolicy = "Nicht verfügbar"; Status = $"Lesen fehlgeschlagen: {exception.Message}"; }
        finally { IsBusy = false; }
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

    private async Task ChangeAsync(int? limit)
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
            Status = "Einstellung übernommen und rückgelesen. Bleibt nach dem Schließen aktiv.";
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

    private void ShowState(BatteryChargeState state)
    {
        _supported = (state.IsStandardMode || state.IsCustomMode) && state.StoredStopPercent is >= 60 and <= 100;
        // Start the slider where the device actually is. It used to keep its own default,
        // which read as a claim about the hardware next to the large percentage readout.
        if (state.StoredStopPercent is >= 60 and <= 100)
        {
            _applyingDeviceState = true;
            try { SelectedLimit = state.StoredStopPercent; }
            finally { _applyingDeviceState = false; }
        }
        ActivePolicy = state.IsCustomMode
            ? $"Aktiv: Ladelimit {state.StoredStopPercent} %"
            : state.IsStandardMode ? "Aktiv: Standardladen (BIOS-gesteuert)"
            : $"Unbekannter Modus {state.PolicyRaw} · gespeicherter Wert {state.StoredStopPercent}";
        if (!_supported) ActivePolicy += " · Schreiben gesperrt";
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (IsBusy) throw new InvalidOperationException("Laufende Akkuoperation muss vor Dispose beendet werden.");
        _disposed = true;
        controller.Dispose();
    }
}
