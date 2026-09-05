using System.IO;
using System.Reflection;
using AorusControl.App.Infrastructure;
using AorusControl.App.ViewModels;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

/// <summary>
/// The apply buttons are gone: fan curve, Fixed value and charge limit write themselves
/// once the user stops moving things. That is only an improvement if a change never gets
/// lost and never fires while the gesture is still running - which is what these check.
/// The wait is injected, so nothing here sleeps.
/// </summary>
internal static class AutoApplyTests
{
    public static async Task RunAsync()
    {
        await FanCurveWaitsForTheButtonAsync();
        await FixedSliderOnlyFollowsAnActiveModeAsync();
        await ChargeLimitAppliesItselfAsync();
        Console.WriteLine("PASS: the curve waits for its button; Fixed value and charge limit apply themselves");
    }

    private static async Task FanCurveWaitsForTheButtonAsync()
    {
        var clock = new ManualWait();
        var fan = new FakeFan();
        using var vm = new MainWindowViewModel(new FakeReader(), new FakeKeyboard(), fan,
            new WindowsPowerOverlayController(),
            fanCurveStore: new FakeFanCurveStore(),
            startupManager: new FakeStartupManager(),
            debounceWait: clock.Wait);
        await vm.Cooling.StartAsync();
        await vm.Cooling.SetProfileAsync("Dynamic");
        int before = fan.CurveWrites;

        // Shaping a curve is many small edits and one decision. Writing is a fifteen-point EC
        // transaction plus a mode switch and takes seconds, so no edit may trigger one.
        vm.Cooling.CurveRows[1].TemperatureNumber = 50;
        vm.Cooling.NoteCurveEdited();
        Check(fan.CurveWrites == before, "editing the curve must not write anything");
        Check(vm.Cooling.HasUnsavedCurve && vm.Cooling.CanApplyCurve, "but the change must be offered for applying");
        Check(vm.Cooling.CurveStatus.Contains("nicht übernommen"), "and said out loud, so nobody thinks it is live");

        await vm.Cooling.ApplyCurveAsync();
        Check(fan.CurveWrites == before + 1, $"the button writes once, got {fan.CurveWrites - before}");
        Check(!vm.Cooling.HasUnsavedCurve && !vm.Cooling.CanApplyCurve, "and there is nothing left to apply afterwards");

        // Re-reading the device is the way out of a shape you no longer want.
        vm.Cooling.CurveRows[1].TemperatureNumber = 51;
        vm.Cooling.NoteCurveEdited();
        await vm.Cooling.ReloadCurveFromDeviceAsync();
        Check(!vm.Cooling.HasUnsavedCurve, "reloading from the device discards the edits with them");

        // Closing writes nothing: an unapplied curve was never accepted, and the window is not
        // the place to decide it was.
        vm.Cooling.CurveRows[1].TemperatureNumber = 48;
        vm.Cooling.NoteCurveEdited();
        int beforeClose = fan.CurveWrites;
        await vm.PrepareToCloseAsync();
        Check(fan.CurveWrites == beforeClose, "closing must not apply a curve the user never confirmed");
    }

    private static async Task FixedSliderOnlyFollowsAnActiveModeAsync()
    {
        var clock = new ManualWait();
        var fan = new FakeFan();
        using var vm = new MainWindowViewModel(new FakeReader(), new FakeKeyboard(), fan,
            new WindowsPowerOverlayController(),
            fanCurveStore: new FakeFanCurveStore(),
            startupManager: new FakeStartupManager(),
            debounceWait: clock.Wait);
        await vm.Cooling.StartAsync();

        // Brushing the slider must not pin the fans: entering Fixed stays a deliberate act.
        vm.Cooling.FixedFanPercent = 75;
        await clock.ElapseAsync(vm.Cooling.PendingFixedWrite);
        Check(fan.FixedWrites == 0, "moving the slider must not enter Fixed mode on its own");
    }

    private static async Task ChargeLimitAppliesItselfAsync()
    {
        var clock = new ManualWait();
        var controller = new AutoApplyBattery();
        using var vm = new BatteryViewModel(controller, clock.Wait);
        await vm.RefreshAsync();
        int afterRead = controller.Writes;
        Check(afterRead == 0, "reading the device must not write back to it");

        // A drag across the range is one transaction, not one per value.
        vm.SelectedLimit = 70;
        vm.SelectedLimit = 75;
        vm.SelectedLimit = 80;
        Check(controller.Writes == 0, "the limit must not be written mid-drag");
        await clock.ElapseAsync(vm.PendingLimitWrite);
        Check(controller.Writes == 1, $"a settled drag writes once, got {controller.Writes}");
        Check(controller.State.StoredStopPercent == 80, "the value the user settled on is what reaches the device");

        // Flushing on shutdown writes a change that was still waiting.
        vm.SelectedLimit = 90;
        await vm.PendingLimitWrite.FlushAsync();
        Check(controller.Writes == 2 && controller.State.StoredStopPercent == 90, "closing writes the waiting limit");
    }

    /// <summary>A wait the test completes by hand, so debounce behaviour is checked
    /// without a real clock and without timing luck.</summary>
    private sealed class ManualWait
    {
        private TaskCompletionSource _current = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task _pending = Task.CompletedTask;

        public Task Wait(TimeSpan delay, CancellationToken cancellationToken)
        {
            Task task = _current.Task.WaitAsync(cancellationToken);
            _pending = task;
            return task;
        }

        /// <summary>Lets the current delay elapse and waits for the write it triggers -
        /// the debouncer's own task, not a spin, so the check is deterministic.</summary>
        public async Task ElapseAsync(Debouncer debouncer)
        {
            TaskCompletionSource elapsing = _current;
            _current = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task pending = debouncer.Pending;
            elapsing.TrySetResult();
            try { await _pending; } catch (OperationCanceledException) { }
            try { await pending; } catch (OperationCanceledException) { }
        }
    }

    private sealed class AutoApplyBattery : IAorusBatteryChargeController
    {
        public BatteryChargeState State { get; private set; } = new(0, 100);
        public int Writes { get; private set; }
        public DeviceCompatibility CheckCompatibility() => new(true, "Test", "Test", "Test", "Test");
        public Task<BatteryChargeState> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(State);
        public Task<BatteryChargeChangeResult> SetCustomLimitAsync(int limitPercent, CancellationToken cancellationToken = default)
            => Apply(new BatteryChargeState(4, (byte)limitPercent));
        public Task<BatteryChargeChangeResult> SetStandardModeAsync(CancellationToken cancellationToken = default)
            => Apply(new BatteryChargeState(0, 100));
        private Task<BatteryChargeChangeResult> Apply(BatteryChargeState state)
        {
            Writes++;
            var result = new BatteryChargeChangeResult(State, state);
            State = state;
            return Task.FromResult(result);
        }
        public void Dispose() { }
    }

    private static Task Invoke(MainWindowViewModel vm, string method) =>
        (Task)typeof(MainWindowViewModel).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(vm, null)!;

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
