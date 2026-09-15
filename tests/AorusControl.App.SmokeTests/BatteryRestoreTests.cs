using System.IO;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.Battery;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

/// <summary>
/// The charge limit has to survive a restart. It lives on the embedded controller, and after
/// a reboot this machine was found charging to 97 % with the policy back at 100 % - so the
/// app keeps its own copy and puts it back when the device disagrees.
///
/// The interesting cases are the ones where it must NOT write: nothing saved, device already
/// correct, device not writable, and a limit the user has since changed by hand.
/// </summary>
internal static class BatteryRestoreTests
{
    public static async Task RunAsync()
    {
        // --- the file itself -------------------------------------------------------------
        string path = Path.Combine(Path.GetTempPath(), $"aorus-battery-{Guid.NewGuid():N}.json");
        try
        {
            var store = new BatterySettingsStore(path);
            Check(store.Load() is null, "no file means no opinion, not a default");
            store.Save(new BatterySettings(false, 80));
            Check(store.Load() == new BatterySettings(false, 80), "a saved limit comes back unchanged");
            store.Save(new BatterySettings(true, 0));
            Check(store.Load() is { StandardMode: true }, "standard charging is a choice too, not an absent one");
            Throws(() => store.Save(new BatterySettings(false, 42)), "a limit outside the firmware range is refused");
            File.WriteAllText(path, "{ nonsense");
            Check(store.Load() is null, "a damaged file is no opinion either - the device still decides");
        }
        finally { foreach (string leftover in new[] { path, path + ".bak" }) if (File.Exists(leftover)) File.Delete(leftover); }

        // --- restoring after a restart ---------------------------------------------------
        var saved = new FakeStore { Settings = new BatterySettings(false, 80) };
        var device = new FakeBattery { State = new BatteryChargeState(0, 100) };
        using (var vm = new BatteryViewModel(device, store: saved))
        {
            await vm.StartAsync();
            Check(device.Writes == 1, "a device that lost the limit gets it back, exactly once");
            Check(device.State == new BatteryChargeState(4, 80), "and it is the saved limit, not some default");
            Check(vm.Status.Contains("wiederhergestellt"), "and the card says so instead of doing it behind the reader's back");
        }

        // --- the cases that must stay quiet ----------------------------------------------
        var matching = new FakeBattery { State = new BatteryChargeState(4, 80) };
        using (var vm = new BatteryViewModel(matching, store: new FakeStore { Settings = new BatterySettings(false, 80) }))
        {
            await vm.StartAsync();
            Check(matching.Writes == 0, "a device that already agrees is left alone");
        }

        var nothingSaved = new FakeBattery { State = new BatteryChargeState(0, 100) };
        using (var vm = new BatteryViewModel(nothingSaved, store: new FakeStore()))
        {
            await vm.StartAsync();
            Check(nothingSaved.Writes == 0, "with nothing saved the device keeps whatever it has");
        }

        var unsupported = new FakeBattery { Supported = false };
        using (var vm = new BatteryViewModel(unsupported, store: new FakeStore { Settings = new BatterySettings(false, 80) }))
        {
            await vm.StartAsync();
            Check(unsupported.Writes == 0, "a device that is not cleared for writing is never written to");
        }

        // --- saving what the user chose --------------------------------------------------
        var writing = new FakeStore();
        var controller = new FakeBattery { State = new BatteryChargeState(0, 100) };
        using (var vm = new BatteryViewModel(controller, store: writing))
        {
            await vm.RefreshAsync();
            vm.SelectedLimit = 70;
            await vm.ApplyLimitAsync();
            Check(writing.Settings == new BatterySettings(false, 70), "a confirmed change is what gets remembered");
            await vm.ApplyStandardAsync();
            Check(writing.Settings is { StandardMode: true }, "switching back to standard is remembered as well");

            controller.FailWrite = true;
            vm.SelectedLimit = 65;
            await vm.ApplyLimitAsync();
            Check(writing.Settings is { StandardMode: true }, "a change that failed is not remembered as if it had worked");
        }

        Console.WriteLine("PASS: charge limit survives a restart, and stays quiet when there is nothing to restore");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Throws(Action action, string message)
    {
        try { action(); }
        catch { return; }
        throw new InvalidOperationException(message);
    }

    private sealed class FakeStore : IBatterySettingsStore
    {
        public BatterySettings? Settings { get; set; }
        public BatterySettings? Load() => Settings;
        public void Save(BatterySettings settings) => Settings = settings;
    }

    private sealed class FakeBattery : IAorusBatteryChargeController
    {
        public BatteryChargeState State { get; set; } = new(0, 100);
        public int Writes { get; private set; }
        public bool Supported { get; set; } = true;
        public bool FailWrite { get; set; }

        public DeviceCompatibility CheckCompatibility() =>
            new(Supported, "Test", "Test", "Test", Supported ? "Test" : "Gerät nicht freigegeben");

        public Task<BatteryChargeState> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(State);

        public Task<BatteryChargeChangeResult> SetCustomLimitAsync(int percent, CancellationToken cancellationToken = default) =>
            Write(new BatteryChargeState(4, (byte)percent));

        public Task<BatteryChargeChangeResult> SetStandardModeAsync(CancellationToken cancellationToken = default) =>
            Write(new BatteryChargeState(0, 100));

        private Task<BatteryChargeChangeResult> Write(BatteryChargeState next)
        {
            if (FailWrite) throw new InvalidOperationException("Simulierter Schreibfehler");
            Writes++;
            BatteryChargeState previous = State;
            State = next;
            return Task.FromResult(new BatteryChargeChangeResult(previous, next));
        }

        public void Dispose() { }
    }
}
