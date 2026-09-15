using AorusControl.App.Features.Keyboard;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

/// <summary>
/// Right after logon the USB keyboard often enumerates a second or two after this app is
/// already running. The first attempt then fails on a device that simply is not there yet,
/// and the app used to give up at that point - leaving the saved colours unapplied for the
/// rest of the session. That is the "sometimes it does not set itself" with no pattern to it.
/// </summary>
internal static class KeyboardStartRetryTests
{
    public static async Task RunAsync()
    {
        var delays = new List<double>();
        var late = new LateKeyboard { FailuresBeforeSuccess = 2 };
        using (var vm = new KeyboardViewModel(late, resumeReapplyDelay: delay =>
        {
            delays.Add(delay.TotalSeconds);
            return Task.CompletedTask;
        }))
        {
            await vm.StartAsync();
            Check(late.Reads == 3, "a keyboard that arrives late is tried again, not written off");
            Check(vm.ControlsEnabled, "and once it answers the section is usable");
            Check(delays.SequenceEqual([1d, 2d]), "waits get longer between attempts instead of hammering the device");
        }

        // A keyboard that never answers must stop somewhere, and must not leave the section
        // claiming to be usable.
        var absent = new LateKeyboard { FailuresBeforeSuccess = int.MaxValue };
        var absentDelays = new List<double>();
        using (var vm = new KeyboardViewModel(absent, resumeReapplyDelay: delay =>
        {
            absentDelays.Add(delay.TotalSeconds);
            return Task.CompletedTask;
        }))
        {
            await vm.StartAsync();
            Check(absent.Reads == 4, "four attempts, then it stops - the device is genuinely gone");
            Check(absentDelays.SequenceEqual([1d, 2d, 4d]), "about seven seconds in total, not a background loop");
            Check(!vm.ControlsEnabled && vm.Status.Contains("nicht verfügbar"),
                "and it says the keyboard is unavailable rather than pretending otherwise");
        }

        // The keyboard that was there all along must not pay for any of this.
        var present = new LateKeyboard();
        using (var vm = new KeyboardViewModel(present, resumeReapplyDelay: _ =>
            throw new InvalidOperationException("a keyboard that answered must not be waited on")))
        {
            await vm.StartAsync();
            Check(present.Reads == 1, "one attempt is all a working keyboard costs");
        }

        Console.WriteLine("PASS: keyboard start retries a late device and gives up on an absent one");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    /// <summary>A keyboard that is not there yet, and then is.</summary>
    private sealed class LateKeyboard : IAorusKeyboardRgbController
    {
        public int FailuresBeforeSuccess { get; set; }
        public int Reads { get; private set; }

        public KeyboardRgbState ReadState()
        {
            Reads++;
            if (Reads <= FailuresBeforeSuccess)
                throw new InvalidOperationException("Tastatur nicht verf\u00fcgbar \u00b7 GetFeature failed");
            return new KeyboardRgbState([.. Enumerable.Range(1, 3)
                .Select(zone => new KeyboardRgbZoneState(zone, new KeyboardRgbColor(0, 255, 0), 50))]);
        }

        public KeyboardRgbState ApplyState(KeyboardRgbState state) => state;
        public KeyboardRgbState SetLighting(bool enabled) => ReadState();
        public KeyboardRgbState SetBrightness(KeyboardBrightnessLevel level) => ReadState();
        public KeyboardRgbState SetColor(int zone, KeyboardRgbColor color, bool applyToAllZones) => ReadState();
        public Task PlayEffectAsync(KeyboardRgbEffect effect, KeyboardEffectSpeed speed, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public void Dispose() { }
    }
}
