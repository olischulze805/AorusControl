using AorusControl.App.Infrastructure;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

namespace AorusControl.App.ViewModels;

/// <summary>UI state only; firmware validation and transactional writes stay in Core.</summary>
public sealed class BatteryViewModel : ObservableObject, IDisposable
{
    private readonly IAorusBatteryChargeController controller;
    private bool _busy;
    private bool _supported;
    private bool _disposed;
    private int _selectedLimit = 80;
    private string _status = "Ladelimit wird gelesen …";
    private string _activePolicy = "Noch nicht gelesen";

    public BatteryViewModel(IAorusBatteryChargeController controller)
    {
        this.controller = controller;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ApplyLimitCommand = new AsyncRelayCommand(ApplyLimitAsync);
        ApplyStandardCommand = new AsyncRelayCommand(ApplyStandardAsync);
    }

    public bool IsBusy { get => _busy; private set { SetProperty(ref _busy, value); OnPropertyChanged(nameof(CanApply)); OnPropertyChanged(nameof(CanRefresh)); } }
    public bool CanApply => _supported && !IsBusy && !_disposed;
    public bool CanRefresh => !IsBusy && !_disposed;
    public int SelectedLimit { get => _selectedLimit; set => SetProperty(ref _selectedLimit, value); }
    public IReadOnlyList<int> LimitChoices { get; } = Enumerable.Range(60, 41).ToArray();
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string ActivePolicy { get => _activePolicy; private set => SetProperty(ref _activePolicy, value); }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ApplyLimitCommand { get; }
    public AsyncRelayCommand ApplyStandardCommand { get; }

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
            Status = "Auswahl ändert noch nichts. Erst Übernehmen schreibt die Einstellung.";
        }
        catch (Exception exception) { ActivePolicy = "Nicht verfügbar"; Status = $"Lesen fehlgeschlagen: {exception.Message}"; }
        finally { IsBusy = false; }
    }

    public Task ApplyLimitAsync() => ChangeAsync(SelectedLimit);
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
        if (state.StoredStopPercent is >= 60 and <= 100) SelectedLimit = state.StoredStopPercent;
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
