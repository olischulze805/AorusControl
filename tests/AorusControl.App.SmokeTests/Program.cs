using System.IO;
using System.Reflection;
using AorusControl.App.ViewModels;
using AorusControl.Core.Models;
using AorusControl.Core.Services;
using AorusControl.Core.Features.Cooling;
using AorusControl.Core.Features.PowerMonitoring;
using AorusControl.Core.Features.Startup;
using AorusControl.Core.Features.Worker;
using AorusControl.App.Infrastructure;

string instanceName = @"Local\AorusControl.Test." + Guid.NewGuid().ToString("N");
using (var primary = new SingleInstanceGate(instanceName))
using (var secondary = new SingleInstanceGate(instanceName))
{
    Check(primary.IsPrimary && !secondary.IsPrimary, "only one primary per session/name");
    secondary.RequestActivation();
    Check(primary.Activation.WaitOne(1000), "second launch signals first instance");
}
using (var replacement = new SingleInstanceGate(instanceName))
    Check(replacement.IsPrimary, "instance ownership released after close");
Console.WriteLine("PASS: single-instance ownership, activation and release");

await KeyboardSessionTests.RunAsync();
await KeyboardReconnectTests.RunAsync();
KeyboardStorageTests.Run();
KeyboardFrameTests.Run();
await WorkerProtocolTests.RunAsync();
PowerProfileSelectionTests.Run();
LaptopProfileTests.Run();
ProfileCatalogTests.Run();
await ProfileEditorTests.RunAsync();
await BatteryTests.RunAsync();
await FanSupervisorTests.RunAsync();
FanCurveStoreTests.Run();
HsvColorTests.Run();
RecentColorsStoreTests.Run();
StartupManagerTests.Run();
FanSpeedPercentTests.Run();
KeyboardEffectFrameTests.Run();
KeyboardLayoutTests.Run();
AppDataTests.Run();
GigabyteCurveTests.Run();
FanCurveShapeTests.Run();
SliderGeometryTests.Run();
UpdateRestartTests.Run();
await UpdateRestartTests.RunStartupCheckAsync();
await DebouncerTests.RunAsync();
await AutoApplyTests.RunAsync();
await WorkerDiscoveryTests.RunAsync();

Check(PowerSampleMath.CpuUsage(new(100, 200, 100), new(150, 300, 200)) == 75, "CPU kernel includes idle");
Check(PowerSampleMath.CpuUsage(new(100, 200, 100), new(100, 200, 100)) is null, "empty interval is unavailable");
Check(PowerSampleMath.CpuUsage(new(100, 200, 100), new(90, 300, 200)) is null, "counter reset is unavailable");
Check(PowerSampleMath.CpuUsage(new(0, 0, 0), new(300, 100, 100)) is null, "invalid idle delta is unavailable");
Check(PowerSampleMath.DischargeWatts(false, true, 21503) == 21.503, "milliwatts conversion");
Check(PowerSampleMath.DischargeWatts(true, false, 21503) is null, "AC does not prove battery draw");
Check(PowerSampleMath.DischargeWatts(false, true, uint.MaxValue) is null, "unknown rate sentinel");
Check(PowerSampleMath.DischargeWatts(false, true, 0) is null, "missing rate is not measured zero");
Console.WriteLine("PASS: 8 power sample calculation checks");

// No vendor hardware writes: both hardware dependencies are test doubles.
await Run("Hidden dashboard skips telemetry but preserves fixed-fan safety", async (vm, reader, fan) =>
{
    vm.SetDashboardVisible(false);
    reader.Fail = true;
    await Invoke(vm, "RefreshAsync");
    Check(!vm.Status.Contains("Messfehler"), "hidden automatic mode should not poll");
    reader.Fail = false;
    await vm.Cooling.SetFixedAsync();
    reader.Temperature = 65;
    await Invoke(vm, "RefreshAsync");
    Check(fan.NormalWrites == 1, "hidden fixed mode still needs safety polling");
});
await Run("Fixed rejected at 65 C", async (vm, reader, fan) =>
{
    reader.Temperature = 65;
    await vm.Cooling.SetFixedAsync();
    Check(fan.FixedWrites == 0, "hot fixed write must be blocked");
    Check(vm.Cooling.Status.Contains("65"), "original error must remain visible");
});
await Run("Fixed rejects zero and stale temperatures", async (vm, reader, fan) =>
{
    reader.Temperature = 0;
    await vm.Cooling.SetFixedAsync();
    Check(fan.FixedWrites == 0, "zero is not a safe temperature");
    reader.Temperature = 50;
    reader.Timestamp = () => DateTimeOffset.Now.AddSeconds(-6);
    await vm.Cooling.SetFixedAsync();
    Check(fan.FixedWrites == 0, "old cool sample cannot authorize fixed");
});
await Run("Stale telemetry restores active fixed mode", async (vm, reader, fan) =>
{
    await vm.Cooling.SetFixedAsync();
    reader.Timestamp = () => DateTimeOffset.Now.AddSeconds(-6);
    await Invoke(vm, "RefreshAsync");
    Check(fan.NormalWrites == 1, "stale sample restores firmware control");
});
await Run("Fixed accepted and restored on heat", async (vm, reader, fan) =>
{
    await vm.Cooling.SetFixedAsync();
    Check(fan.FixedWrites == 1, "fixed write missing");
    reader.Temperature = 65;
    await Invoke(vm, "RefreshAsync");
    Check(fan.NormalWrites == 1, "heat must restore normal");
});
await Run("Missing telemetry restores normal", async (vm, reader, fan) =>
{
    await vm.Cooling.SetFixedAsync();
    reader.Fail = true;
    await Invoke(vm, "RefreshAsync");
    Check(fan.NormalWrites == 1, "missing telemetry must restore normal");
});
await Run("Restoration failure is surfaced, not retried by the app itself", async (vm, reader, fan) =>
{
    // Since Fixed mode now lives behind a worker-hosted lease, retrying a failed
    // restore is the worker's own supervisor's job (already covered by
    // FanSupervisorTests's "next tick retries restoration"), not the app's. The app
    // only needs to surface the failure and stop claiming Fixed mode itself.
    await vm.Cooling.SetFixedAsync();
    reader.Fail = true;
    fan.FailNormal = true;
    await Invoke(vm, "RefreshAsync");
    Check(fan.NormalWrites == 1, "exactly one restoration attempt, even though it fails");
    Check(vm.Cooling.Status.Contains("ACHTUNG"), "restore failure must be visible");
    fan.FailNormal = false;
    await Invoke(vm, "RefreshAsync");
    Check(fan.NormalWrites == 1, "abandoned fixed mode must not write again on its own");
});
await Run("Closing restores fixed", async (vm, reader, fan) =>
{
    await vm.Cooling.SetFixedAsync();
    await vm.PrepareToCloseAsync();
    Check(fan.NormalWrites == 1, "closing must restore normal");
});
await Run("Closing failure is reported and retryable", async (vm, reader, fan) =>
{
    await vm.Cooling.SetProfileAsync("Maximum");
    fan.FailNormal = true;
    bool failed = false;
    try { await vm.PrepareToCloseAsync(); } catch (InvalidOperationException) { failed = true; }
    Check(failed, "closing must not silently ignore restoration failure");
    fan.FailNormal = false;
    await vm.PrepareToCloseAsync();
    Check(fan.NormalWrites == 2, "close restore must be retryable");
});
await Run("Normal cancels manual close restoration", async (vm, reader, fan) =>
{
    await vm.Cooling.SetFixedAsync();
    // Switching to Normal releases the lease (one write via the worker's own restore)
    // and then applies the Normal preset directly (a second, harmless, idempotent
    // write); closing afterward must not add a third.
    await vm.Cooling.SetProfileAsync("Normal");
    await vm.PrepareToCloseAsync();
    Check(fan.NormalWrites == 2, "closing must not write normal a third time");
});
await Run("A device curve is read back as handles, not as fifteen dots", async (vm, reader, fan) =>
{
    await Task.CompletedTask;
    // The stub's curve is a straight line, and a straight line is two decisions however many
    // points the firmware stores it in. Handing the user fifteen dots to express it would be
    // handing them thirteen that do nothing.
    Check(vm.Cooling.CurveRows.Count >= 2, "a curve always has at least its two ends");
    Check(vm.Cooling.CurveRows.Count < 15, $"a straight line must not read back as fifteen handles, got {vm.Cooling.CurveRows.Count}");
    Check(vm.Cooling.CurveRows[0].TemperatureNumber == 40, "the first handle sits where the device says it does");
    Check(vm.Cooling.CurveRows[^1].Percent == 100, "and the last is the firmware's full-speed point");
});
await Run("Applying an edited curve writes it, activates Dynamic and persists it", async (vm, reader, fan) =>
{
    await vm.Cooling.SetProfileAsync("Dynamic");
    vm.Cooling.CurveRows[0].Percent += 5;
    vm.Cooling.NoteCurveEdited();
    await vm.Cooling.ApplyCurveAsync();
    Check(fan.CurveWrites == 1, "editing and applying must write the curve exactly once");
    Check(fan.DynamicWrites >= 1, "applying a curve must activate Dynamic mode");
    Check(fan.LastWrittenCurve!.Count == 15, "whatever was drawn, the firmware gets its fifteen points");
    FanCurveValidation.Validate(fan.LastWrittenCurve!);
    Check(vm.Cooling.CurveStatus.Contains("Übernommen"), "success must be visible");
    Check(!vm.Cooling.HasUnsavedCurve, "and nothing is left pending");
});
await Run("A curve too small to be a curve is refused before any hardware write", async (vm, reader, fan) =>
{
    while (vm.Cooling.CurveRows.Count > 1) vm.Cooling.CurveRows.RemoveAt(vm.Cooling.CurveRows.Count - 1);
    await vm.Cooling.ApplyCurveAsync();
    Check(fan.CurveWrites == 0, "a single point is not a curve and must never reach the device");
    Check(vm.Cooling.CurveStatus.Contains("Ungültige Kurve"), "and the refusal must be visible");
});
await Run("A shape the firmware would reject is corrected on the way out, not sent and refused", async (vm, reader, fan) =>
{
    // Out of order, below the tested floor, and falling in the middle: all three are things
    // the editor clamps live, but nothing stops a stored file or a future caller from holding
    // them, and the device must never be the thing that finds out.
    vm.Cooling.CurveRows.Clear();
    vm.Cooling.CurveRows.Add(new FanCurveRowViewModel(1) { TemperatureNumber = 80, Percent = 90 });
    vm.Cooling.CurveRows.Add(new FanCurveRowViewModel(2) { TemperatureNumber = 40, Percent = 5 });
    vm.Cooling.CurveRows.Add(new FanCurveRowViewModel(3) { TemperatureNumber = 60, Percent = 70 });
    vm.Cooling.CurveRows.Add(new FanCurveRowViewModel(4) { TemperatureNumber = 70, Percent = 30 });
    await vm.Cooling.ApplyCurveAsync();
    Check(fan.CurveWrites == 1, "the corrected curve is written rather than the write being refused");
    FanCurveValidation.Validate(fan.LastWrittenCurve!);
});
await Run("A failed curve write leaves the fan controller queryable and is surfaced", async (vm, reader, fan) =>
{
    fan.FailCurve = true;
    await vm.Cooling.ApplyCurveAsync();
    Check(fan.CurveWrites == 1, "the attempt itself must still be counted");
    Check(fan.DynamicWrites == 0, "must not activate Dynamic mode after a failed curve write");
    Check(vm.Cooling.CurveStatus.Contains("fehlgeschlagen"), "failure must be visible, not silently swallowed");
});
await Run("Reloading from device discards edits and shows current firmware state", async (vm, reader, fan) =>
{
    await vm.Cooling.SetProfileAsync("Dynamic");
    vm.Cooling.CurveRows[0].TemperatureNumber = 99;
    vm.Cooling.NoteCurveEdited();
    await vm.Cooling.ReloadCurveFromDeviceAsync();
    Check(vm.Cooling.CurveRows[0].TemperatureNumber == 40, "reload must reflect the device, not the discarded edit");
    Check(!vm.Cooling.HasUnsavedCurve, "and nothing may still look unsaved afterwards");
});
Console.WriteLine("PASS: fan curve read back as handles, apply/activate/persist, refusal, correction, write failure, and reload-from-device");

await RunWithStartup("Startup state is read on launch and toggling enables/disables it", async (vm, reader, fan, startup) =>
{
    await Task.CompletedTask;
    Check(!vm.Windows.StartWithWindows, "must reflect the fake's initial disabled state, not assume enabled");
    await vm.Windows.SetStartWithWindowsAsync(true);
    Check(vm.Windows.StartWithWindows && startup.Enabled, "enabling must reach the manager and be reflected back");
    await vm.Windows.SetStartWithWindowsAsync(false);
    Check(!vm.Windows.StartWithWindows && !startup.Enabled, "disabling must reach the manager and be reflected back");
});
await RunWithStartup("A failed startup change is surfaced, not silently ignored", async (vm, reader, fan, startup) =>
{
    startup.FailEnable = true;
    await vm.Windows.SetStartWithWindowsAsync(true);
    Check(!vm.Windows.StartWithWindows, "a failed enable must not report itself as enabled");
    Check(vm.Windows.StartupStatus.Contains("fehlgeschlagen"), "the failure must be visible to the user");
});
Console.WriteLine("PASS: startup toggle reflects manager state and surfaces failures");

await Run("Fan profile chip follows the readback, not the click", async (vm, reader, fan) =>
{
    Check(vm.Cooling.ActiveProfile == "Normal", "a freshly read Normal state must highlight Normal");
    await vm.Cooling.SetProfileAsync("Gaming");
    Check(vm.Cooling.ActiveProfile == "Gaming", "a successful change must move the highlight");
    await vm.Cooling.SetFixedAsync();
    Check(vm.Cooling.ActiveProfile == "Fixed", "a held fixed value is its own state, not one of the profiles");
});
await Run("A Windows shutdown hands the fans back to the firmware", async (vm, reader, fan) =>
{
    // Shutdown and logoff never reach the window's close path. Without a handback there,
    // the machine boots with the fans still pinned and nothing running that knows why.
    await vm.Cooling.SetProfileAsync("Maximum");
    int before = fan.NormalWrites;
    vm.RestoreFansToFirmware();
    Check(fan.NormalWrites == before + 1, "a held Maximum must be handed back on shutdown");
    vm.RestoreFansToFirmware();
    Check(fan.NormalWrites == before + 1, "a second shutdown notification must not write again");
});
await Run("The curve below the profiles shows that profile, and only Dynamic can be edited", async (vm, reader, fan) =>
{
    // Dragging points that change nothing would be a lie told with a cursor, so the chart is
    // editable exactly when the stored curve is the thing in force.
    await vm.Cooling.SetProfileAsync("Gaming");
    Check(!vm.Cooling.IsCurveEditable, "a firmware-regulated profile must not offer an editable curve");
    // The stored curve is still shown - it is really in the device - but nothing claims it is
    // in force: no active line, and the note says so.
    Check(vm.Cooling.DisplayedCurve.SequenceEqual(vm.Cooling.CurveRows), "the stored curve stays on show");
    Check(!vm.Cooling.ShowsActiveLine, "but nothing in the chart is drawn as being in force");
    Check(double.IsNaN(vm.Cooling.ConstantPercent), "nothing is known about its shape, so no flat line either");
    Check(vm.Cooling.CurveNote.Contains("gespeichert"), "and the note says the curve is only stored");
    Check(vm.Cooling.CurveNote.Contains("Gaming"), "the note names the profile it is talking about");

    // Two profiles are known exactly, and a straight line is the honest picture of both.
    await vm.Cooling.SetProfileAsync("Maximum");
    Check(vm.Cooling.ConstantPercent == 100, "Maximum holds full speed at every temperature");
    Check(vm.Cooling.ShowsActiveLine, "which is a line that really is in force");

    await vm.Cooling.SetProfileAsync("Dynamic");
    Check(vm.Cooling.IsCurveEditable, "the stored curve is editable exactly when it regulates the fans");
    Check(vm.Cooling.DisplayedCurve.SequenceEqual(vm.Cooling.CurveRows), "and is exactly the curve on show");

    reader.Temperature = 50;
    await vm.Cooling.SetFixedAsync();
    Check(vm.Cooling.IsFixedActive, "the fixed value must be held for this to test anything");
    Check(Math.Abs(vm.Cooling.ConstantPercent - vm.Cooling.FixedFanPercent) < 0.001,
        "a held fixed value is drawn as the flat line it is");
    Check(!vm.Cooling.IsCurveEditable, "and the stored curve is out of force, so it cannot be edited");
});
await Run("The power section says what the mode does and what the fans are doing", async (vm, reader, fan) =>
{
    // The point of this text is that it never claims the power mode drives the fans, and
    // that it describes the cooling that is really in force - both follow the readback.
    Check(vm.Cooling.Summary.Contains("Normal"), "a freshly read Normal state must be summarised as Normal");
    await vm.Cooling.SetProfileAsync("Gaming");
    Check(vm.Cooling.Summary.Contains("Gaming"), "the summary must follow the profile that was read back");
    await vm.Cooling.SetFixedAsync();
    Check(vm.Cooling.Summary.Contains("%"), "a held fixed value must be summarised with its percentage");
    Check(vm.Windows.PowerModeEffect.Contains("Netzbetrieb"), "the effect text must name the tested power source");
});
await Run("The Fixed slider moves smoothly across the firmware's whole range", async (vm, reader, fan) =>
{
    await Task.CompletedTask;
    // Every raw value reads back as its own percentage, and every percentage reaches the raw
    // value it names - no step table in between, so the control moves as smoothly as the
    // firmware's own scale allows.
    for (byte raw = 0; raw < FanSpeedPercent.MaxRaw; raw++)
    {
        vm.Cooling.FixedFanRaw = raw;
        Check(Math.Abs(vm.Cooling.FixedFanPercent - FanSpeedPercent.ToPercent(raw)) < 0.001,
            $"raw {raw} must report its own percent");
    }
    for (int percent = 0; percent <= 100; percent++)
    {
        vm.Cooling.FixedFanPercent = percent;
        Check(vm.Cooling.FixedFanRaw == FanSpeedPercent.ToRaw(percent),
            $"{percent} % must reach the firmware as its own raw value, got {vm.Cooling.FixedFanRaw}");
    }
    vm.Cooling.FixedFanPercent = 0;
    Check(vm.Cooling.FixedFanRaw == 0, "the left end of the slider is fans off");
    vm.Cooling.FixedFanPercent = 500;
    Check(vm.Cooling.FixedFanRaw == 229, "above the ceiling it clamps to full speed");
    vm.Cooling.FixedFanPercent = -20;
    Check(vm.Cooling.FixedFanRaw == 0, "and below it to off");
});
await Run("The live fan readings never claim to be current when they are not", async (vm, reader, fan) =>
{
    await Task.CompletedTask;
    // Before anything was read, nothing may look like a measurement - a rotor drawn turning
    // on invented numbers would undo the entire point of live feedback.
    Check(!vm.Cooling.Live.IsLive, "an unread device is not live");
    Check(vm.Cooling.Live.CpuRpmText.Contains('–'), "and shows a dash rather than a speed");
    Check(double.IsNaN(vm.Cooling.Live.MarkerTemperature), "so the curve gets no live marker either");

    vm.Cooling.Live.Update(new TelemetrySnapshot(DateTimeOffset.Now, 61, 55, 3200, 0, 137, 0));
    Check(vm.Cooling.Live.IsLive, "a reading makes it live");
    Check(vm.Cooling.Live.CpuRpm == 3200 && vm.Cooling.Live.CpuRpmText.Contains("U/min"), "the CPU fan reports its speed");
    // A fan at rest and a fan nobody read are different things, and have to read differently.
    Check(vm.Cooling.Live.GpuRpmText == "steht", "a stopped fan says so instead of showing 0");
    Check(vm.Cooling.Live.MarkerTemperature == 61, "the marker follows the hotter of the two");
    Check(Math.Abs(vm.Cooling.Live.MarkerPercent - FanSpeedPercent.ToPercent(137)) < 0.001,
        "and the harder-working fan's duty");

    vm.Cooling.Live.MarkStale();
    Check(!vm.Cooling.Live.IsLive, "a failed read stops the numbers claiming to be current");
    Check(double.IsNaN(vm.Cooling.Live.MarkerTemperature), "and takes the marker off the curve");
});
Console.WriteLine("PASS: profile chip tracks device state; Fixed slider spans the whole firmware range");

Console.WriteLine("All smoke tests passed. No hardware setters invoked.");

static async Task Run(string name, Func<MainWindowViewModel, FakeReader, FakeFan, Task> test) =>
    await RunWithStartup(name, (vm, reader, fan, _) => test(vm, reader, fan));

static async Task RunWithStartup(string name, Func<MainWindowViewModel, FakeReader, FakeFan, FakeStartupManager, Task> test)
{
    var reader = new FakeReader();
    var fan = new FakeFan();
    // The same FanSafetySupervisor class the real worker hosts, wrapping the same fakes
    // the ViewModel uses directly elsewhere, so write counts stay meaningful. Its own
    // lease/temperature/expiry rules are exhaustively covered by FanSupervisorTests;
    // these tests only check that the ViewModel calls it correctly and reflects failure.
    //
    // Deliberately not calling RunAsync(): it schedules a real 2-second PeriodicTimer on
    // the real system clock (this supervisor has no virtual TimeProvider here), which
    // raced with these tests under load and produced a flaky, non-deterministic extra
    // tick. AcquireFixedAsync only checks the private _running flag, never actually
    // touched otherwise in these tests, so setting it directly is sufficient and fully
    // deterministic - the App itself drives every Renew/Release call explicitly here.
    var supervisor = new FanSafetySupervisor(fan, reader);
    typeof(FanSafetySupervisor).GetField("_running", BindingFlags.NonPublic | BindingFlags.Instance)!
        .SetValue(supervisor, 1);
    var startupManager = new FakeStartupManager();
    using var vm = new MainWindowViewModel(reader, new FakeKeyboard(), fan, new WindowsPowerOverlayController(),
        fixedFanLeaseClient: new InProcessFixedFanLeaseClient(supervisor),
        fanCurveStore: new FakeFanCurveStore(),
        startupManager: startupManager);
    await vm.Cooling.StartAsync();
    await test(vm, reader, fan, startupManager);
    Console.WriteLine($"PASS: {name}");
}
static Task Invoke(MainWindowViewModel vm, string method) =>
    (Task)typeof(MainWindowViewModel).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(vm, null)!;
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FakeReader : IAorusTelemetryReader
{
    public Func<DateTimeOffset> Timestamp { get; set; } = () => DateTimeOffset.Now;
    public ushort Temperature { get; set; } = 50;
    public bool Fail { get; set; }
    public DeviceCompatibility CheckCompatibility() => new(true, "Test", "Test", "Test", "Test");
    public Task<TelemetrySnapshot> ReadAsync(CancellationToken cancellationToken = default) => Fail
        ? Task.FromException<TelemetrySnapshot>(new InvalidOperationException("Simulated telemetry failure"))
        : Task.FromResult(new TelemetrySnapshot(Timestamp(), Temperature, Temperature, 1900, 2000, 66, 66));
    public void Dispose() { }
}
sealed class FakeFan : IAorusFanController
{
    private static readonly FanCurvePoint[] InitialCurve = Enumerable.Range(0, 15)
        .Select(index => new FanCurvePoint((byte)index, (byte)(40 + index * 3), (byte)(57 + index * 12)))
        .ToArray();
    private FanControlState _state;
    public int FixedWrites { get; private set; }
    public int NormalWrites { get; private set; }
    public int CurveWrites { get; private set; }
    public int DynamicWrites { get; private set; }
    public IReadOnlyList<FanCurvePoint>? LastWrittenCurve { get; private set; }
    public bool FailNormal { get; set; }
    public bool FailCurve { get; set; }

    public FakeFan()
    {
        FanCurvePoint[] curve = (FanCurvePoint[])InitialCurve.Clone();
        curve[^1] = new FanCurvePoint(14, 90, 229); // Satisfies FanCurveValidation's "last point forces 229" rule.
        _state = new(0, 0, 0, 0, 57, 66, curve);
    }
    public DeviceCompatibility CheckCompatibility() => new(true, "Test", "Test", "Test", "Test");
    public Task<FanControlState> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_state);
    private Task<FanProfileChangeResult> Change(FanControlState next)
    {
        var result = new FanProfileChangeResult(_state, next);
        _state = next;
        return Task.FromResult(result);
    }
    public Task<FanProfileChangeResult> SetNormalAsync(CancellationToken cancellationToken = default)
    {
        NormalWrites++;
        if (FailNormal) throw new InvalidOperationException("Simulated WMI failure");
        return Change(new(0, 0, 0, 0, 57, 66, []));
    }
    public Task<FanProfileChangeResult> SetFixedAsync(byte rawValue, CancellationToken cancellationToken = default)
    {
        FixedWrites++;
        return Change(new(1, 1, 0, 0, rawValue, rawValue, []));
    }
    public Task<FanProfileChangeResult> SetQuietAsync(CancellationToken cancellationToken = default) => Change(new(0, 0, 0, 1, 57, 66, []));
    public Task<FanProfileChangeResult> SetGamingAsync(CancellationToken cancellationToken = default) => Change(new(0, 0, 1, 0, 57, 66, []));
    public Task<FanProfileChangeResult> SetMaximumAsync(CancellationToken cancellationToken = default) => Change(new(1, 1, 0, 0, 229, 229, []));
    public Task<FanProfileChangeResult> SetDynamicAsync(CancellationToken cancellationToken = default)
    {
        DynamicWrites++;
        return Change(_state with { FixedStatusRaw = 0, StepStatusRaw = 1, AutoStatusRaw = 0, NvidiaThermalTargetRaw = 0 });
    }
    public Task<FanProfileChangeResult> SetCurveAsync(IReadOnlyList<FanCurvePoint> curve, CancellationToken cancellationToken = default)
    {
        FanCurveValidation.Validate(curve);
        CurveWrites++;
        if (FailCurve) throw new InvalidOperationException("Simulated curve write failure");
        LastWrittenCurve = curve;
        return Change(_state with { Curve = curve });
    }
    public Task<FanProfileChangeResult> RestoreAsync(FanControlState state, CancellationToken cancellationToken = default) => Change(state);
    public void Dispose() { }
}
/// <summary>Test double standing in for the out-of-process worker: same supervisor
/// class, same fakes, in-process so tests stay fast and deterministic. Production
/// code must use <see cref="WorkerFixedFanLeaseClient"/> instead, since only that
/// implementation survives the caller's own crash.</summary>
sealed class InProcessFixedFanLeaseClient(FanSafetySupervisor supervisor) : IFixedFanLeaseClient
{
    public Task<Guid> AcquireAsync(byte rawValue, CancellationToken cancellationToken = default) =>
        supervisor.AcquireFixedAsync(rawValue);
    public Task RenewAsync(Guid lease, CancellationToken cancellationToken = default) =>
        supervisor.RenewAsync(lease);
    public Task ReleaseAsync(Guid lease, CancellationToken cancellationToken = default) =>
        supervisor.ReleaseAsync(lease);
}
/// <summary>In-memory test double so curve tests never touch the real per-user
/// LocalAppData file that the actual app writes; a real leftover file there (from
/// someone manually testing the app on this machine) would otherwise silently make
/// these tests non-deterministic.</summary>
sealed class FakeFanCurveStore : IFanCurveStore
{
    public IReadOnlyList<FanCurvePoint>? Saved { get; private set; }
    public IReadOnlyList<FanCurvePoint>? Load() => Saved;
    public void Save(IReadOnlyList<FanCurvePoint> curve)
    {
        FanCurveValidation.Validate(curve);
        Saved = curve;
    }
}
sealed class FakeStartupManager : IStartupManager
{
    public bool Enabled { get; private set; }
    public bool FailEnable { get; set; }
    public bool FailDisable { get; set; }
    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enabled);
    public Task EnableAsync(CancellationToken cancellationToken = default)
    {
        if (FailEnable) throw new InvalidOperationException("Simulated schtasks failure");
        Enabled = true;
        return Task.CompletedTask;
    }
    public Task DisableAsync(CancellationToken cancellationToken = default)
    {
        if (FailDisable) throw new InvalidOperationException("Simulated schtasks failure");
        Enabled = false;
        return Task.CompletedTask;
    }
}
sealed class FakeKeyboard : IAorusKeyboardRgbController
{
    public KeyboardRgbState ApplyState(KeyboardRgbState state) => throw new NotSupportedException();
    public KeyboardRgbState ReadState() => throw new NotSupportedException();
    public KeyboardRgbState SetLighting(bool enabled) => throw new NotSupportedException();
    public KeyboardRgbState SetBrightness(KeyboardBrightnessLevel level) => throw new NotSupportedException();
    public KeyboardRgbState SetColor(int zone, KeyboardRgbColor color, bool applyToAllZones) => throw new NotSupportedException();
    public Task PlayEffectAsync(KeyboardRgbEffect effect, KeyboardEffectSpeed speed, CancellationToken cancellationToken) => throw new NotSupportedException();
    public void Dispose() { }
}
