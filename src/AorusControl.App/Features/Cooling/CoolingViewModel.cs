using System.Collections.ObjectModel;
using AorusControl.App.Infrastructure;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.Cooling;
using AorusControl.Core.Features.Diagnostics;
using AorusControl.Core.Features.Worker;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

namespace AorusControl.App.Features.Cooling;

/// <summary>
/// Fan profiles, the Fixed value and the custom curve.
///
/// The safety-critical part of the app: a Fixed value pins the fans, so it is held through
/// a lease on the out-of-process worker, and every path that can end it - a failed write, a
/// lost telemetry read, closing the window, Windows shutting down - has to hand the fans
/// back. That is why the module exposes those moments explicitly (<see cref="RenewFixedLeaseAsync"/>,
/// <see cref="AbandonFixedAsync"/>, <see cref="RestoreFansToFirmware"/>) instead of hiding
/// them behind a timer of its own: the shell owns the telemetry clock, and the guarantees
/// have to hang off the same one.
/// </summary>
public sealed class CoolingViewModel : ObservableObject, IFeatureModule
{
    private readonly IAorusFanController _fan;
    private readonly IFixedFanLeaseClient _leaseClient;
    private readonly IFanCurveStore _curveStore;
    private readonly Func<Task> _refreshTelemetry;
    private readonly Action _startMonitoring;
    private readonly Debouncer _applyCurve;
    private readonly Debouncer _applyFixed;

    private bool _busy, _closing, _disposed, _controlsEnabled, _restoreNormalOnExit, _fixedActive;
    private Guid? _fixedLease;
    private byte _fixedRaw = 114;
    private string _status = "Lüftersteuerung wird geprüft …";
    private string _curveStatus = "Kurve wird gelesen …";
    private string _activeProfile = "Normal";

    public CoolingViewModel(
        IAorusFanController fan,
        IFixedFanLeaseClient leaseClient,
        IFanCurveStore curveStore,
        Func<Task> refreshTelemetry,
        Action startMonitoring,
        Func<TimeSpan, CancellationToken, Task>? debounceWait = null)
    {
        _fan = fan;
        _leaseClient = leaseClient;
        _curveStore = curveStore;
        _refreshTelemetry = refreshTelemetry;
        _startMonitoring = startMonitoring;
        SetProfileCommand = new AsyncRelayCommand<string>(SetProfileAsync);
        SetFixedCommand = new AsyncRelayCommand(SetFixedAsync);
        ReloadCurveCommand = new AsyncRelayCommand(ReloadCurveFromDeviceAsync);
        FixedFanTicks = new System.Windows.Media.DoubleCollection(
            FixedFanRawChoices.Select(raw => (double)FanSpeedPercent.ToPercent(raw)));
        // Dragging applies by itself once the gesture settles. The curve waits a little
        // longer than a single value would: shaping it means many small drags, and each
        // write is a fifteen-point EC transaction plus a mode switch.
        _applyCurve = new Debouncer(TimeSpan.FromMilliseconds(900), ApplyPendingCurveAsync, debounceWait);
        // Only ever reschedules an ALREADY active Fixed mode - entering it stays an
        // explicit act, see SetFixedAsync.
        _applyFixed = new Debouncer(TimeSpan.FromMilliseconds(600), ReapplyFixedAsync, debounceWait);
    }

    public AsyncRelayCommand<string> SetProfileCommand { get; }
    public AsyncRelayCommand SetFixedCommand { get; }
    public AsyncRelayCommand ReloadCurveCommand { get; }

    public bool IsBusy => _busy;
    public bool IsFixedActive => _fixedActive;
    public bool ControlsEnabled { get => _controlsEnabled; private set => SetProperty(ref _controlsEnabled, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string CurveStatus { get => _curveStatus; private set => SetProperty(ref _curveStatus, value); }

    // A note that applies to ActiveProfile as much as to the power-mode and effect chips
    // elsewhere: the chips bind to it ONE-WAY, and a RadioButton sets its own IsChecked
    // locally the moment it is clicked. Only a PropertyChanged pushes the real value back
    // over that local one - so every command that touches it re-announces in its finally
    // block even when the value did not change. Relying on SetProperty's equality gate
    // would leave the clicked chip lit after a write that failed and changed nothing.

    /// <summary>Which profile chip is highlighted. Derived from what was actually read back
    /// from the EC, not from what was last clicked, so an externally changed profile (vendor
    /// tool, Fn shortcut, our own safety restore) shows up honestly.</summary>
    public string ActiveProfile
    {
        get => _activeProfile;
        private set { if (SetProperty(ref _activeProfile, value)) OnPropertyChanged(nameof(Summary)); }
    }

    /// <summary>The cooling that is really in force, in one line - shown next to the Windows
    /// power modes, where the honest answer to "what did I just change?" has to include the
    /// part Windows does not control.</summary>
    public string Summary => ActiveProfile switch
    {
        "Fixed" => $"Fester Wert {FanSpeedPercent.ToPercent(_fixedRaw)} % · die Kurve unten ist gespeichert, aber gerade außer Kraft.",
        "Maximum" => "Maximum · Lüfter laufen unabhängig von der Kurve auf voller Stufe.",
        "Dynamic" => "Dynamic · die Kurve unten regelt die Lüfter.",
        "Quiet" => "Quiet · Firmware-Regelung, leiser als die Kurve unten.",
        "Gaming" => "Gaming · Firmware-Regelung, aggressiver als die Kurve unten.",
        _ => "Normal · Firmware-Standardregelung, nicht die Kurve unten."
    };

    public byte FixedFanRaw
    {
        get => _fixedRaw;
        set
        {
            if (!SetProperty(ref _fixedRaw, value)) return;
            OnPropertyChanged(nameof(FixedFanPercent));
            OnPropertyChanged(nameof(FixedFanPercentText));
            OnPropertyChanged(nameof(Summary));
        }
    }

    public IReadOnlyList<byte> FixedFanRawChoices { get; } = [57, 68, 91, 114, 137, 160, 194, 229];

    /// <summary>
    /// The Fixed slider's value. Reads and writes percent, but can only ever land on one of
    /// <see cref="FixedFanRawChoices"/>: the setter snaps to the nearest tested raw step, so
    /// a value the firmware was never measured at is unreachable even if the slider's own
    /// snapping were bypassed.
    /// </summary>
    public double FixedFanPercent
    {
        get => FanSpeedPercent.ToPercent(_fixedRaw);
        set
        {
            byte nearest = FixedFanRawChoices
                .OrderBy(raw => Math.Abs(FanSpeedPercent.ToPercent(raw) - value))
                .First();
            bool changed = nearest != _fixedRaw;
            FixedFanRaw = nearest;
            OnPropertyChanged(nameof(FixedFanPercent));
            // Following the slider while Fixed is already held is what the user expects;
            // silently ENTERING a mode that pins the fans because a slider was brushed is
            // not, so that still needs the button.
            if (changed && _fixedActive) _applyFixed.Schedule();
        }
    }

    public string FixedFanPercentText => $"{FanSpeedPercent.ToPercent(_fixedRaw)} %";

    /// <summary>Tick positions for the Fixed slider, on the percentages the tested raw steps
    /// really sit at - hence unevenly spaced, which is the honest picture. Derived from
    /// FixedFanRawChoices rather than restated, so the marks cannot come to show values the
    /// slider can no longer reach.</summary>
    public System.Windows.Media.DoubleCollection FixedFanTicks { get; }

    /// <summary>The 15 editable curve points. Text-backed like FanCurveRowViewModel
    /// elsewhere, so invalid or incomplete typing survives until it is validated, instead of
    /// being silently clamped as the user types.</summary>
    public ObservableCollection<FanCurveRowViewModel> CurveRows { get; } = new();

    /// <summary>The waiting curve write, so shutdown and tests can observe it rather than
    /// guess at a clock.</summary>
    internal Debouncer PendingCurveWrite => _applyCurve;

    /// <summary>The waiting Fixed re-apply, exposed for the same reason.</summary>
    internal Debouncer PendingFixedWrite => _applyFixed;

    public async Task StartAsync()
    {
        try
        {
            DeviceCompatibility compatibility = _fan.CheckCompatibility();
            if (!compatibility.IsSupported)
            {
                Status = compatibility.Message;
                CurveStatus = compatibility.Message;
                return;
            }
            FanControlState state = await _fan.ReadAsync();
            Show(state, DescribeFanState(state));
            ControlsEnabled = true;
            LoadCurveOnStartup(state.Curve);
        }
        catch (Exception exception)
        {
            AppLog.Error("fan", "Lüftersteuerung nicht verfügbar.", exception);
            Status = $"Lüftersteuerung nicht verfügbar: {exception.Message}";
            CurveStatus = Status;
        }
    }

    public async Task SetProfileAsync(string profile)
    {
        if (_closing || _busy || !ControlsEnabled) return;

        _busy = true;
        ControlsEnabled = false;
        Status = $"{profile} wird gesetzt und geprüft …";
        try
        {
            // Best effort: releasing already restores Normal through the worker, so
            // switching straight to the Normal preset costs one harmless extra write below
            // rather than needing special-cased logic to skip it.
            await ReleaseLeaseAsync();

            FanProfileChangeResult result = profile switch
            {
                "Quiet" => await _fan.SetQuietAsync(),
                "Gaming" => await _fan.SetGamingAsync(),
                "Maximum" => await _fan.SetMaximumAsync(),
                "Dynamic" => await _fan.SetDynamicAsync(),
                _ => await _fan.SetNormalAsync()
            };
            _restoreNormalOnExit = profile is "Maximum" or "Dynamic";
            Show(result.VerifiedState, profile);
            await _refreshTelemetry();
        }
        catch (Exception exception)
        {
            AppLog.Error("fan", $"Profil {profile} fehlgeschlagen.", exception);
            Status = $"Lüfteränderung fehlgeschlagen: {exception.Message}";
            await AppendReadbackAsync();
        }
        finally
        {
            _busy = false;
            ControlsEnabled = true;
            OnPropertyChanged(nameof(ActiveProfile));
        }
    }

    public async Task SetFixedAsync()
    {
        if (_closing || _busy || !ControlsEnabled) return;

        _busy = true;
        ControlsEnabled = false;
        Status = $"Fixed {FixedFanRaw} wird gesetzt und geprüft …";
        try
        {
            // The lease client validates telemetry itself before writing; Fixed mode is
            // never authorized on stale or unsafe temperatures. Ensuring a backing worker
            // process exists is that client's own concern (WorkerFixedFanLeaseClient does it
            // internally), not something this module should know about - it must stay
            // agnostic to which IFixedFanLeaseClient implementation is in use.
            _fixedLease = await _leaseClient.AcquireAsync(FixedFanRaw);
            _fixedActive = true;
            // A pinned fan must be watched, so holding one starts the telemetry clock even
            // if the user had stopped it.
            _startMonitoring();
            Show(await _fan.ReadAsync(), $"Fixed {FixedFanRaw}");
            await _refreshTelemetry();
        }
        catch (Exception exception)
        {
            AppLog.Error("fan", $"Fixed {FixedFanRaw} fehlgeschlagen.", exception);
            Status = $"Fixed fehlgeschlagen: {exception.Message}";
            await AppendReadbackAsync();
        }
        finally
        {
            _busy = false;
            ControlsEnabled = true;
            OnPropertyChanged(nameof(ActiveProfile));
        }
    }

    /// <summary>
    /// Writes the 15 edited points to the EC and switches into Dynamic mode so they take
    /// immediate effect, then persists them locally. Writing and activating are one action
    /// rather than two, because a written-but-inactive curve is easy to forget about and
    /// mistake for "not working".
    /// </summary>
    public async Task ApplyCurveAsync()
    {
        if (_closing || _busy || !ControlsEnabled) return;

        FanCurvePoint[] points;
        try { points = ParseCurveRows(); }
        catch (Exception exception)
        {
            CurveStatus = $"Ungültige Kurve: {exception.Message}";
            return;
        }

        _busy = true;
        ControlsEnabled = false;
        CurveStatus = "Kurve wird geschrieben und aktiviert …";
        try
        {
            await ReleaseLeaseAsync();
            await _fan.SetCurveAsync(points);
            FanProfileChangeResult activated = await _fan.SetDynamicAsync();
            _restoreNormalOnExit = true;
            Show(activated.VerifiedState, "Eigene Kurve (Dynamic)");
            _curveStore.Save(points);
            CurveStatus = "Eigene Kurve übernommen, aktiv und gespeichert.";
            await _refreshTelemetry();
        }
        catch (Exception exception)
        {
            AppLog.Error("fan", "Kurve fehlgeschlagen.", exception);
            CurveStatus = $"Kurve fehlgeschlagen: {exception.Message}";
            await AppendReadbackAsync();
        }
        finally
        {
            _busy = false;
            ControlsEnabled = true;
        }
    }

    /// <summary>Called by the chart after a drag: the curve writes itself once the user stops
    /// moving points, so there is no apply button to forget.</summary>
    public void ScheduleCurveApply()
    {
        if (_closing || _disposed || !ControlsEnabled) return;
        CurveStatus = "Änderung wird gleich übernommen …";
        _applyCurve.Schedule();
    }

    /// <summary>Discards edits and re-reads whatever curve is on the EC - an escape hatch
    /// back to known hardware truth, not a guessed default.</summary>
    public async Task ReloadCurveFromDeviceAsync()
    {
        if (_closing || _busy) return;
        _busy = true;
        try
        {
            FanControlState state = await _fan.ReadAsync();
            // A write scheduled a moment ago must not land afterwards and undo the reload.
            _applyCurve.Cancel();
            PopulateCurveRows(state.Curve);
            CurveStatus = "Aktuelle Firmware-Kurve geladen (noch nicht gespeichert oder aktiviert).";
        }
        catch (Exception exception)
        {
            CurveStatus = $"Kurve konnte nicht gelesen werden: {exception.Message}";
        }
        finally { _busy = false; }
    }

    /// <summary>
    /// Renews the Fixed lease on the shell's telemetry tick. The worker re-validates the
    /// temperature itself on every renewal from its own independent read, so a failure here
    /// means it has already restored Normal before this returns.
    /// </summary>
    public async Task RenewFixedLeaseAsync()
    {
        if (!_fixedActive || _fixedLease is not { } lease) return;
        try { await _leaseClient.RenewAsync(lease); }
        catch (Exception error) { await AbandonFixedAsync(error.Message); }
    }

    /// <summary>
    /// Gives up the app's own claim to Fixed mode. Never retries a failed release: once a
    /// lease is acquired, the worker's supervisor is unconditionally responsible for
    /// eventually restoring Normal, independent of this app's state or even its continued
    /// existence. Retrying from here would only race that guarantee.
    /// </summary>
    public async Task AbandonFixedAsync(string reason)
    {
        if (_busy) return;
        AppLog.Warn("fan", $"Fixed-Freigabe wird aufgegeben: {reason}");
        _busy = true;
        ControlsEnabled = false;
        try
        {
            string? releaseFailure = null;
            if (_fixedLease is { } lease)
            {
                try { await _leaseClient.ReleaseAsync(lease); }
                catch (Exception releaseError) { releaseFailure = releaseError.Message; }
            }

            _fixedActive = false;
            _fixedLease = null;
            try
            {
                FanControlState state = await _fan.ReadAsync();
                Show(state, DescribeFanState(state));
            }
            catch
            {
                // Display only; the worker's supervisor remains responsible for the actual
                // hardware state regardless of whether this read succeeds.
            }

            Status = releaseFailure is null
                ? $"{Status} · Sicherheitsrückstellung: {reason}"
                : $"{Status} · {releaseFailure}";
        }
        finally
        {
            _busy = false;
            ControlsEnabled = true;
        }
    }

    /// <summary>Writes whatever the user changed a moment ago before the window closes, so a
    /// value they just set is not lost to the timing of the close.</summary>
    public async Task FlushPendingWritesAsync()
    {
        try { await _applyCurve.FlushAsync(); } catch (Exception error) { AppLog.Error("fan", "Ausstehende Kurve nicht mehr geschrieben.", error); }
        try { await _applyFixed.FlushAsync(); } catch (Exception error) { AppLog.Error("fan", "Ausstehender Fixed-Wert nicht mehr geschrieben.", error); }
    }

    public void BeginClose() => _closing = true;

    public void CancelClose() => _closing = false;

    /// <summary>Hands the fans back as part of a normal close. Throws if the device refuses,
    /// which keeps the window open rather than leaving the fans pinned silently.</summary>
    public async Task HandBackAsync()
    {
        await ReleaseLeaseAsync();
        if (!_restoreNormalOnExit) return;
        await _fan.SetNormalAsync();
        _restoreNormalOnExit = false;
    }

    /// <summary>
    /// The same handback, synchronous and best-effort, for dispose and for Windows shutting
    /// down: without it a machine that shut down while Fixed or Maximum was held would come
    /// back up with the fans still pinned and nothing running that knows why. Windows allows
    /// a process a few seconds at SessionEnding, which is enough for one EC write.
    /// </summary>
    public void RestoreFansToFirmware()
    {
        if (_fixedActive && _fixedLease is { } lease)
        {
            try { _leaseClient.ReleaseAsync(lease).GetAwaiter().GetResult(); }
            catch { /* Worker's own supervisor remains responsible. */ }
            _fixedActive = false;
            _fixedLease = null;
        }

        if (!_restoreNormalOnExit) return;
        try
        {
            _fan.SetNormalAsync().GetAwaiter().GetResult();
            _restoreNormalOnExit = false;
        }
        catch (Exception error)
        {
            // The independent fan-restore entry point remains available.
            AppLog.Error("fan", "Lüfter konnten nicht auf Normal zurückgestellt werden.", error);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _applyCurve.Cancel();
        _applyFixed.Cancel();
        RestoreFansToFirmware();
        _fan.Dispose();
    }

    // ---- internals -----------------------------------------------------------------
    private async Task ReleaseLeaseAsync()
    {
        if (!_fixedActive || _fixedLease is not { } lease) return;
        try { await _leaseClient.ReleaseAsync(lease); }
        catch { /* Worker's own supervisor remains responsible. */ }
        _fixedActive = false;
        _fixedLease = null;
    }

    private Task ApplyPendingCurveAsync()
    {
        if (_closing || _disposed) return Task.CompletedTask;
        // With no apply button there is nobody left to retry a write that lands while the
        // controller is busy, so it waits its turn instead of being dropped.
        if (_busy || !ControlsEnabled) { _applyCurve.Schedule(); return Task.CompletedTask; }
        return ApplyCurveAsync();
    }

    private async Task ReapplyFixedAsync()
    {
        if (_closing || _disposed || !_fixedActive) return;
        await SetFixedAsync();
    }

    private void LoadCurveOnStartup(IReadOnlyList<FanCurvePoint> liveCurve)
    {
        try
        {
            IReadOnlyList<FanCurvePoint>? saved = _curveStore.Load();
            PopulateCurveRows(saved ?? liveCurve);
            CurveStatus = saved is null
                ? "Aktuelle Firmware-Kurve geladen. Noch keine eigene Kurve gespeichert."
                : "Gespeicherte eigene Kurve geladen (erst nach Übernehmen aktiv).";
        }
        catch (Exception exception)
        {
            PopulateCurveRows(liveCurve);
            CurveStatus = $"Gespeicherte Kurve nicht geladen, Firmware-Kurve angezeigt: {exception.Message}";
        }
    }

    private void PopulateCurveRows(IReadOnlyList<FanCurvePoint> curve)
    {
        CurveRows.Clear();
        foreach (FanCurvePoint point in curve)
        {
            CurveRows.Add(new FanCurveRowViewModel(point.Index + 1)
            {
                Temperature = point.Temperature.ToString(),
                Value = point.Value.ToString()
            });
        }
    }

    private FanCurvePoint[] ParseCurveRows()
    {
        if (CurveRows.Count != 15) throw new InvalidOperationException("Es müssen genau 15 Punkte vorhanden sein.");
        var points = new FanCurvePoint[15];
        for (int index = 0; index < 15; index++)
        {
            FanCurveRowViewModel row = CurveRows[index];
            if (!byte.TryParse(row.Temperature, out byte temperature))
                throw new FormatException($"Punkt {index + 1}: ungültige Temperatur.");
            if (!byte.TryParse(row.Value, out byte value))
                throw new FormatException($"Punkt {index + 1}: ungültiger Rohwert.");
            points[index] = new FanCurvePoint((byte)index, temperature, value);
        }
        FanCurveValidation.Validate(points);
        return points;
    }

    private async Task AppendReadbackAsync()
    {
        try
        {
            FanControlState state = await _fan.ReadAsync();
            Status += $" · Rückgelesen: {DescribeFanState(state)}";
        }
        catch
        {
            // Keep the original, rollback-aware error.
        }
    }

    private void Show(FanControlState state, string profile)
    {
        FixedFanRaw = state.FixedSpeedRaw is >= 57 and <= 229 ? checked((byte)state.FixedSpeedRaw) : FixedFanRaw;
        ActiveProfile = DescribeFanProfileKey(state);
        Status = $"Aktiv: {profile} · Fixed {state.FixedStatusRaw} · Step {state.StepStatusRaw} · Auto {state.AutoStatusRaw} · Thermal {state.NvidiaThermalTargetRaw}";
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>The chip identity for a read-back state. "Fixed" is its own key so no profile
    /// chip lights up while a manual fixed value is held.</summary>
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
}
