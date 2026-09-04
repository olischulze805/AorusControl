using AorusControl.App.ViewModels;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

internal static class BatteryTests
{
    public static async Task RunAsync()
    {
        var controller = new FakeBattery();
        using var vm = new BatteryViewModel(controller);
        await vm.RefreshAsync();
        Check(controller.Writes == 0 && vm.ActivePolicy.Contains("Standard"), "initial read must not apply stored stop");
        Check(!vm.ActivePolicy.Contains("97"), "inactive threshold must not look active");
        vm.SelectedLimit = 80;
        Check(controller.Writes == 0, "selection must not write");
        await vm.ApplyLimitAsync();
        Check(controller.State == new BatteryChargeState(4, 80) && vm.ActivePolicy.Contains("80"), "confirmed custom state");
        vm.SelectedLimit = 59;
        await vm.ApplyLimitAsync();
        Check(controller.Writes == 1, "out-of-range value must not write");
        await vm.ApplyStandardAsync();
        Check(controller.State == new BatteryChargeState(0, 100), "standard is a distinct policy");

        controller.FailWrite = true;
        vm.SelectedLimit = 60;
        await vm.ApplyLimitAsync();
        Check(vm.Status.Contains("fehlgeschlagen") && vm.ActivePolicy.Contains("Standard"), "error survives rollback readback");
        controller.FailRead = true;
        await vm.ApplyLimitAsync();
        Check(!vm.CanApply && vm.ActivePolicy.Contains("nicht bestätigt"), "unknown state disables writes");
        controller.FailWrite = controller.FailRead = false;
        controller.State = new(9, 80);
        await vm.RefreshAsync();
        Check(!vm.CanApply, "unknown policy disabled");
        controller.Supported = false;
        await vm.RefreshAsync();
        Check(!vm.CanApply && vm.ActivePolicy.Contains("nicht freigegeben"), "unsupported device disabled");
        controller.Supported = true;
        controller.State = new(4, 80);
        await vm.RefreshAsync();
        controller.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task pending = vm.ApplyLimitAsync();
        Check(vm.IsBusy && !vm.CanApply && !vm.CanRefresh, "busy disables overlapping operations");
        int writes = controller.Writes;
        await vm.ApplyStandardAsync();
        Check(controller.Writes == writes, "concurrent click ignored");
        controller.Pending.SetResult(new(new(4, 80), new(4, 60)));
        await pending;
        Check(vm.CanApply && vm.ActivePolicy.Contains("60"), "completion restores controls");
        vm.Dispose();
        Check(controller.Disposed && !vm.CanApply && !vm.CanRefresh, "shutdown releases controller without reverting policy");
        Console.WriteLine("PASS: battery read/selection, custom/standard, range, failure, unknown device/state, concurrency and disposal");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class FakeBattery : IAorusBatteryChargeController
    {
        public BatteryChargeState State { get; set; } = new(0, 97);
        public bool Supported { get; set; } = true;
        public bool FailRead { get; set; }
        public bool FailWrite { get; set; }
        public bool Disposed { get; private set; }
        public int Writes { get; private set; }
        public TaskCompletionSource<BatteryChargeChangeResult>? Pending { get; set; }
        public DeviceCompatibility CheckCompatibility() => new(Supported, "Fake", "Fake", "Fake", "Testfreigabe");
        public Task<BatteryChargeState> ReadAsync(CancellationToken cancellationToken = default) => FailRead
            ? Task.FromException<BatteryChargeState>(new InvalidOperationException("read failure")) : Task.FromResult(State);
        public Task<BatteryChargeChangeResult> SetCustomLimitAsync(int limitPercent, CancellationToken cancellationToken = default) => Write(new(4, checked((byte)limitPercent)));
        public Task<BatteryChargeChangeResult> SetStandardModeAsync(CancellationToken cancellationToken = default) => Write(new(0, 100));
        private Task<BatteryChargeChangeResult> Write(BatteryChargeState next)
        {
            Writes++;
            if (FailWrite) throw new InvalidOperationException("simulated write failure");
            if (Pending is { } pending) return pending.Task;
            var result = new BatteryChargeChangeResult(State, next);
            State = next;
            return Task.FromResult(result);
        }
        public void Dispose() => Disposed = true;
    }
}
