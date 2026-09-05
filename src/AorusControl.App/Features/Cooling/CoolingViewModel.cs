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
    private readonly Debouncer _applyFixed;

    private bool _busy, _closing, _disposed, _controlsEnabled, _restoreNormalOnExit, _fixedActive, _unsavedCurve;
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
        ApplyCurveCommand = new AsyncRelayCommand(ApplyCurveAsync);
        LoadGigabyteCurveCommand = new RelayCommand(LoadGigabyteCurve);
        // Only ever reschedules an ALREADY active Fixed mode - entering it stays an
        // explicit act, see SetFixedAsync.
        _applyFixed = new Debouncer(TimeSpan.FromMilliseconds(600), ReapplyFixedAsync, debounceWait);
    }

    /// <summary>What the fans are actually doing, for the rotors and the live marker on the
    /// curve. Fed by the shell's telemetry, so adjusting a curve or a fixed value can be
    /// watched happening instead of only being read back as a number.</summary>
    public FanLiveViewModel Live { get; } = new();

    public AsyncRelayCommand<string> SetProfileCommand { get; }
    public AsyncRelayCommand SetFixedCommand { get; }
    public AsyncRelayCommand ReloadCurveCommand { get; }
    public AsyncRelayCommand ApplyCurveCommand { get; }
    public RelayCommand LoadGigabyteCurveCommand { get; }

    /// <summary>
    /// The curve Gigabyte's own Control Center draws for this laptop. It is no longer drawn
    /// beside the user's own - two curves in one chart was more confusing than useful - but it
    /// stays available as a starting point through "Gigabyte-Kurve laden".
    /// </summary>
    public IReadOnlyList<(byte TemperatureCelsius, byte Percent)> GigabyteCurve { get; } =
        GigabyteReferenceCurve.AsGigabyteDrawsIt;

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
        "Fixed" when _fixedRaw == 0 => "Lüfter aus · der Worker stellt bei 65 °C selbsttätig auf Normal zurück.",
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
            OnPropertyChanged(nameof(ConstantPercent));
            OnPropertyChanged(nameof(ShowsActiveLine));
            OnPropertyChanged(nameof(CurveNote));
        }
    }

    /// <summary>
    /// The Fixed slider's value, in percent of the firmware's own 0-229 scale.
    ///
    /// It used to snap to the eight raw steps that had been measured one by one, which made
    /// the slider jump in uneven leaps. The device takes every value in between just as
    /// readily - the curve table has always written arbitrary raw values - and the write is
    /// verified by readback and rolled back if the EC disagrees, so a step table bought
    /// nothing but a jerky control. What actually keeps this safe is unchanged: the worker's
    /// lease, which refuses to hold any fixed value at 65 °C.
    /// </summary>
    public double FixedFanPercent
    {
        get => FanSpeedPercent.ToPercent(_fixedRaw);
        set
        {
            byte raw = FanSpeedPercent.ToRaw((int)Math.Round(value));
            bool changed = raw != _fixedRaw;
            FixedFanRaw = raw;
            OnPropertyChanged(nameof(FixedFanPercent));
            // Following the slider while Fixed is already held is what the user expects;
            // silently ENTERING a mode that pins the fans because a slider was brushed is
            // not, so that still needs the button.
            if (changed && _fixedActive) _applyFixed.Schedule();
        }
    }

    public string FixedFanPercentText => $"{FanSpeedPercent.ToPercent(_fixedRaw)} %";

    /// <summary>
    /// Edits that have not reached the device. Writing a curve is a fifteen-point EC
    /// transaction plus a mode switch and takes noticeable time, so shaping one must not
    /// trigger a write per gesture - the user says when.
    /// </summary>
    public bool HasUnsavedCurve
    {
        get => _unsavedCurve;
        private set
        {
            if (!SetProperty(ref _unsavedCurve, value)) return;
            OnPropertyChanged(nameof(CanApplyCurve));
        }
    }

    public bool CanApplyCurve => IsCurveEditable && HasUnsavedCurve && !_busy;

    /// <summary>
    /// Whether the curve below the profile chips can be dragged. Only "Dynamic" runs the
    /// stored curve; under every other profile the chart is showing something the user does
    /// not control, and a chart that can be dragged but changes nothing is a lie told with a
    /// cursor.
    /// </summary>
    public bool IsCurveEditable => ActiveProfile == "Dynamic";

    /// <summary>The curve the chart draws: always the stored one, because it is always the
    /// curve this device really holds. Whether it is currently regulating anything is said by
    /// its colour, by the note above the chart and by the flat line the constant profiles add -
    /// not by hiding it.</summary>
    public IEnumerable<FanCurveRowViewModel> DisplayedCurve => CurveRows;

    /// <summary>Whether anything in the chart is drawn as being in force right now: the own
    /// curve while Dynamic runs it, or the flat line of Maximum and Fixed. Under the firmware
    /// profiles nothing is, and a legend entry for a colour that is not on screen would be
    /// worse than no legend.</summary>
    public bool ShowsActiveLine => IsCurveEditable || !double.IsNaN(ConstantPercent);

    /// <summary>
    /// A flat line where the profile really is a flat line: Maximum pins the fans at full
    /// speed and Fixed at the chosen step, and both are worth drawing because they are known
    /// exactly. NaN elsewhere, where nothing is known.
    /// </summary>
    public double ConstantPercent => ActiveProfile switch
    {
        "Maximum" => 100,
        "Fixed" => FanSpeedPercent.ToPercent(_fixedRaw),
        _ => double.NaN
    };

    /// <summary>
    /// What the chart is showing, in words, per profile. The uncomfortable case is the middle
    /// one: Leise, Normal and Gaming regulate inside the firmware and publish nothing, so the
    /// honest chart is an empty one with this sentence under it rather than a plausible line.
    /// </summary>
    public string CurveNote => ActiveProfile switch
    {
        "Dynamic" => "Die eigene Kurve regelt die Lüfter. Änderungen gelten erst nach \"Kurve übernehmen\" - das Schreiben dauert ein paar Sekunden und schaltet den Lüftermodus um, deshalb passiert es nicht bei jedem Handgriff.",
        "Maximum" => "Maximal hält die Lüfter unabhängig von jeder Kurve auf voller Stufe.",
        "Fixed" when _fixedRaw == 0 => "Lüfter stehen. Der Hardware-Worker prüft die Temperatur und stellt bei 65 °C von sich aus auf Normal zurück, auch wenn die App abstürzt.",
        "Fixed" => $"Fester Wert: {FanSpeedPercent.ToPercent(_fixedRaw)} %, unabhängig von der Temperatur - die waagerechte Linie. Die eigene Kurve liegt grau darunter: gespeichert, aber außer Kraft.",
        _ => $"{ActiveProfile} regelt in der Firmware und gibt seine Kurve nicht preis. Gezeigt ist deshalb grau die " +
             "eigene, gespeicherte Kurve - sie steht so im Gerät, regelt unter diesem Modus aber nichts. " +
             "Zum Bearbeiten oben auf \"Dynamic (eigene Kurve)\" wechseln."
    };

    /// <summary>
    /// The curve as the user shapes it: two to fifteen handles, not the firmware's fifteen
    /// points. FanCurveShape expands them on the way to the device and collapses them on the
    /// way back, so a curve drawn with four decisions still reads as four handles afterwards.
    /// </summary>
    public ObservableCollection<FanCurveRowViewModel> CurveRows { get; } = new();

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
            OnPropertyChanged(nameof(CanApplyCurve));
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
            OnPropertyChanged(nameof(CanApplyCurve));
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

        IReadOnlyList<FanCurvePoint> points;
        try { points = FanCurveShape.ToFirmwareCurve(ReadHandles()); }
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
            HasUnsavedCurve = false;
            CurveStatus = $"Übernommen, aktiv und gespeichert · {CurveRows.Count} Punkte.";
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
            OnPropertyChanged(nameof(CanApplyCurve));
        }
    }

    /// <summary>
    /// Fills the editor with Gigabyte's own curve, adapted to this firmware's limits (a 25%
    /// floor instead of GCC's 0%, and full speed by 90 °C instead of 99% at 92 °C). Nothing is
    /// written yet - it lands in the editor like a drag would, and the debounce applies it.
    /// </summary>
    private void LoadGigabyteCurve()
    {
        if (_closing || _disposed || !IsCurveEditable) return;
        PopulateCurveRows(GigabyteReferenceCurve.ForThisFirmware());
        HasUnsavedCurve = true;
        CurveStatus = "Gigabytes Kurve geladen, an die Grenzen dieser Firmware angepasst · noch nicht übernommen.";
    }

    /// <summary>
    /// Called by the chart after every edit. It only marks the curve as changed: writing takes
    /// seconds and switches the fan mode, which is far too much to happen behind a drag.
    /// </summary>
    public void NoteCurveEdited()
    {
        if (_closing || _disposed || !IsCurveEditable) return;
        HasUnsavedCurve = true;
        CurveStatus = $"{CurveRows.Count} Punkte · noch nicht übernommen.";
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
            PopulateCurveRows(state.Curve);
            CurveStatus = $"Firmware-Kurve gelesen · {CurveRows.Count} Punkte, unverändert.";
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
            OnPropertyChanged(nameof(CanApplyCurve));
        }
    }

    /// <summary>Writes whatever the user changed a moment ago before the window closes, so a
    /// value they just set is not lost to the timing of the close.</summary>
    public async Task FlushPendingWritesAsync()
    {
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
        int number = 1;
        foreach (FanCurveHandle handle in FanCurveShape.FromFirmwareCurve(curve))
        {
            CurveRows.Add(new FanCurveRowViewModel(number++)
            {
                TemperatureNumber = handle.TemperatureCelsius,
                Percent = handle.Percent
            });
        }
        HasUnsavedCurve = false;
    }

    /// <summary>The handles as the shape code wants them. Reading the numeric views rather than
    /// the text ones: the chart only ever sets numbers, and a half-typed value cannot occur.</summary>
    private IReadOnlyList<FanCurveHandle> ReadHandles() =>
        CurveRows.Select(row => new FanCurveHandle(row.TemperatureNumber, row.Percent)).ToArray();

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
        foreach (string derived in CurveView) OnPropertyChanged(derived);
        Status = $"Aktiv: {profile} · Fixed {state.FixedStatusRaw} · Step {state.StepStatusRaw} · Auto {state.AutoStatusRaw} · Thermal {state.NvidiaThermalTargetRaw}";
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>Everything the chart and its caption read, announced together whenever the
    /// running profile changes - the chart follows the device, like the chips do.</summary>
    private static readonly string[] CurveView =
        [nameof(IsCurveEditable), nameof(DisplayedCurve), nameof(ConstantPercent), nameof(ShowsActiveLine),
         nameof(CurveNote), nameof(CanApplyCurve)];

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
