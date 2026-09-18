using AorusControl.App.Infrastructure;
using AorusControl.App.Features.Platform;
using AorusControl.Core.Features.PowerProfiles;

internal static class BatterySaverTests
{
    public static async Task RunAsync()
    {
        Plan();
        await Module();
        Console.WriteLine("PASS: battery saver floors, stored choice, leftover cleanup, failure, and that it survives closing");
    }

    private static void Plan()
    {
        IReadOnlyList<SaverLever> levers = BatterySaverPlan.Levers(50, 30);
        Check(levers.Count == 2, "brightness and the processor cap, and nothing invented beside them");
        SaverLever screen = levers.Single(lever => lever.Setting == PowerSetting.DisplayBrightness);
        SaverLever cpu = levers.Single(lever => lever.Setting == PowerSetting.MaximumProcessorState);
        Check(screen.MeasuredWatts is 3.0, "the screen carries the figure this project measured");
        Check(cpu.MeasuredWatts is null,
            "and the processor cap carries none, because at idle it saves nothing and an invented number would be worse than silence");

        // Below these the machine stops being usable, which does not save runtime - it moves
        // the work to later.
        Check(BatterySaverPlan.Levers(0, 0).Single(l => l.Setting == PowerSetting.MaximumProcessorState).Percent
            == BatterySaverPlan.LowestProcessorCap, "the processor cap has a floor");
        Check(BatterySaverPlan.Levers(0, 0).Single(l => l.Setting == PowerSetting.DisplayBrightness).Percent
            == BatterySaverPlan.LowestBrightness, "and so does the screen");
        Check(BatterySaverPlan.Clamp(140, 30) == 100, "and neither goes over 100");
    }

    private static async Task Module()
    {
        var saver = new FakeSaver();
        var store = new FakeSaverStore();
        var clock = new ManualWait();
        using (var vm = new BatterySaverViewModel(saver, store, clock.Wait))
        {
            await vm.StartAsync();
            Check(!saver.On && saver.Discarded.Count == 0, "nothing happens on a first start with nothing stored");

            vm.IsOn = true;
            await Task.Delay(50);
            Check(saver.On && saver.Cap == 50 && saver.Brightness == 30, "switching on applies the stored values");
            Check(store.Saved!.Enabled && store.Saved.LeftoverScheme == saver.Scheme,
                "and the scheme it created is written down, so a crash does not leave it in the user's plan list for ever");

            // A drag is one rewrite, not one per value.
            vm.Brightness = 55;
            vm.Brightness = 60;
            Check(saver.Applications == 1, "moving the slider does not rewrite the scheme mid-drag");
            await clock.ElapseAsync(Pending(vm));
            await Task.Delay(50);
            Check(saver.Applications == 2 && saver.Brightness == 60, "and the settled value is what reaches it");

            vm.IsOn = false;
            await Task.Delay(50);
            Check(!saver.On, "switching off goes back");
            Check(store.Saved!.LeftoverScheme is null, "and there is no leftover to clean up any more");
        }

        // Closing the window deliberately leaves it on: this is a setting, not a lease, and
        // quietly putting the machine back on a thirstier plan would be a surprise.
        var stillOn = new FakeSaver();
        using (var vm = new BatterySaverViewModel(stillOn, new FakeSaverStore(new BatterySaverSettings(true, 50, 30)), new ManualWait().Wait))
        {
            await vm.StartAsync();
            Check(stillOn.On, "a stored choice is put back in force at the next start");
            vm.Dispose();
            Check(stillOn.On, "and closing the window does not undo it");
        }

        // A scheme left behind by a killed run is removed at the next start.
        Guid orphan = Guid.NewGuid();
        var cleaner = new FakeSaver();
        var cleanerStore = new FakeSaverStore(new BatterySaverSettings(false, 50, 30, orphan));
        using (var vm = new BatterySaverViewModel(cleaner, cleanerStore, new ManualWait().Wait))
        {
            await vm.StartAsync();
            Check(cleaner.Discarded.Contains(orphan), "a scheme from a killed run is discarded");
            Check(cleanerStore.Saved!.LeftoverScheme is null, "and not discarded a second time at the start after that");
        }

        // A refusal has to leave the switch showing the machine, not the request.
        var failing = new FakeSaver { Fail = true };
        using (var vm = new BatterySaverViewModel(failing, new FakeSaverStore(), new ManualWait().Wait))
        {
            vm.IsOn = true;
            await Task.Delay(50);
            Check(!vm.IsOn, "a failed switch falls back to off rather than claiming to be on");
            Check(vm.Status.Contains("fehlgeschlagen") || vm.Status.Contains("failed"), "and says why");
        }
    }

    /// <summary>A clock the test moves by hand, so "one write per drag" can be checked
    /// without waiting out a real 700 ms.</summary>
    private sealed class ManualWait
    {
        private TaskCompletionSource _current = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Wait(TimeSpan delay, CancellationToken cancellationToken) =>
            _current.Task.WaitAsync(cancellationToken);

        public async Task ElapseAsync(Debouncer debouncer)
        {
            TaskCompletionSource elapsed = _current;
            _current = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            elapsed.SetResult();
            await debouncer.FlushAsync();
        }
    }

    private static Debouncer Pending(BatterySaverViewModel vm) =>
        (Debouncer)typeof(BatterySaverViewModel)
            .GetField("_apply", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(vm)!;

    private sealed class FakeSaver : IBatterySaver
    {
        public bool Fail { get; set; }
        public bool On { get; private set; }
        public uint Cap { get; private set; }
        public uint Brightness { get; private set; }
        public int Applications { get; private set; }
        public List<Guid> Discarded { get; } = [];

        public bool IsOn => On;
        public Guid Scheme { get; private set; }

        public void DiscardLeftover(Guid scheme) => Discarded.Add(scheme);

        public IReadOnlyList<SaverLever> TurnOn(uint processorCap, uint brightness)
        {
            if (Fail) throw new InvalidOperationException("Simulated refusal");
            On = true;
            Cap = processorCap;
            Brightness = brightness;
            Scheme = Scheme == Guid.Empty ? Guid.NewGuid() : Scheme;
            Applications++;
            return BatterySaverPlan.Levers(processorCap, brightness);
        }

        public void TurnOff()
        {
            On = false;
            Scheme = Guid.Empty;
        }
    }

    private sealed class FakeSaverStore(BatterySaverSettings? initial = null) : IBatterySaverSettingsStore
    {
        public BatterySaverSettings? Saved { get; private set; }
        public BatterySaverSettings Load() => initial ?? BatterySaverSettings.Default;
        public void Save(BatterySaverSettings settings) => Saved = settings;
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
