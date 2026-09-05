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
await Run("Fan curve loads from live device on startup when nothing is saved", async (vm, reader, fan) =>
{
    await Task.CompletedTask;
    Check(vm.Cooling.CurveRows.Count == 15, "all 15 points must load");
    Check(vm.Cooling.CurveRows[0].Temperature == "40" && vm.Cooling.CurveRows[0].Value == "57", "first point reflects live device state");
    Check(vm.Cooling.CurveRows[14].Value == "229", "last point reflects live device state");
});
await Run("Applying an edited curve writes it, activates Dynamic and persists it", async (vm, reader, fan) =>
{
    vm.Cooling.CurveRows[3].Value = (byte.Parse(vm.Cooling.CurveRows[3].Value) + 1).ToString();
    for (int i = 4; i < 15; i++)
    {
        byte current = byte.Parse(vm.Cooling.CurveRows[i].Value);
        byte previous = byte.Parse(vm.Cooling.CurveRows[i - 1].Value);
        if (current < previous) vm.Cooling.CurveRows[i].Value = previous.ToString();
    }
    await vm.Cooling.ApplyCurveAsync();
    Check(fan.CurveWrites == 1, "editing and applying must write the curve exactly once");
    Check(fan.DynamicWrites == 1, "applying a curve must activate Dynamic mode");
    Check(vm.Cooling.CurveStatus.Contains("übernommen"), "success must be visible");
});
await Run("Invalid curve edits are rejected before any hardware write", async (vm, reader, fan) =>
{
    vm.Cooling.CurveRows[7].Value = "nicht-numerisch";
    await vm.Cooling.ApplyCurveAsync();
    Check(fan.CurveWrites == 0, "malformed input must never reach the device");
    Check(vm.Cooling.CurveStatus.Contains("Ungültige Kurve"), "validation failure must be visible");
});
await Run("A curve that violates monotonic/last-point rules is rejected before any write", async (vm, reader, fan) =>
{
    vm.Cooling.CurveRows[^1].Value = "200"; // Last point must force 229; this violates FanCurveValidation.
    await vm.Cooling.ApplyCurveAsync();
    Check(fan.CurveWrites == 0, "a curve failing hardware-safety validation must never reach the device");
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
    vm.Cooling.CurveRows[0].Temperature = "99";
    await vm.Cooling.ReloadCurveFromDeviceAsync();
    Check(vm.Cooling.CurveRows[0].Temperature == "40", "reload must reflect the device, not the discarded edit");
});
Console.WriteLine("PASS: fan curve startup load, apply/activate/persist, invalid input, hardware-safety rejection, write failure, and reload-from-device");

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
await Run("The Fixed slider can only land on tested steps", async (vm, reader, fan) =>
{
    await Task.CompletedTask;
    // Every raw step reads back as its own percentage...
    foreach (byte raw in vm.Cooling.FixedFanRawChoices)
    {
        vm.Cooling.FixedFanRaw = raw;
        Check(Math.Abs(vm.Cooling.FixedFanPercent - FanSpeedPercent.ToPercent(raw)) < 0.001,
            $"raw {raw} must report its own percent");
    }
    // ...and a value between two steps snaps to the nearer tested one instead of
    // reaching the firmware as an unverified duty.
    vm.Cooling.FixedFanPercent = 63;
    Check(vm.Cooling.FixedFanRaw == 137, $"63 % must snap to the 60 % step (raw 137), got {vm.Cooling.FixedFanRaw}");
    vm.Cooling.FixedFanPercent = 78;
    Check(vm.Cooling.FixedFanRaw == 194, $"78 % must snap to the 85 % step (raw 194), got {vm.Cooling.FixedFanRaw}");
    vm.Cooling.FixedFanPercent = 0;
    Check(vm.Cooling.FixedFanRaw == 57, "below the floor must snap up to the lowest tested step");
    vm.Cooling.FixedFanPercent = 500;
    Check(vm.Cooling.FixedFanRaw == 229, "above the ceiling must snap down to the highest tested step");
    Check(vm.Cooling.FixedFanTicks.Count == vm.Cooling.FixedFanRawChoices.Count, "one tick per tested step");
});
Console.WriteLine("PASS: profile chip tracks device state; Fixed slider snaps to tested steps only");

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
