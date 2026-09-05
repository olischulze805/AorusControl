using System.IO;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Models;
using AorusControl.Core.Services;
using AorusControl.App.Features.Keyboard;
using AorusControl.App.ViewModels;
using System.Reflection;

internal static class KeyboardSessionTests
{
    public static async Task RunAsync()
    {
        var transport = new FakeTransport();
        await using var session = new KeyboardLightingSession(transport);
        KeyboardLightingSettings initial = await session.ReadSettingsAsync();
        Assert(transport.Writes == 0, "initialization must only read");
        transport.SimulateReset();
        await session.ReapplyAsync();
        Assert(transport.Hardware.GetZone(1).Color == initial.Left && transport.Writes == 1, "explicit reconciliation must write unchanged app intent after device reset");
        await session.ChangeAsync(s => s.WithBrightness(KeyboardBrightnessLevel.Low));
        await session.ChangeAsync(s => s with { Enabled = false });
        KeyboardLightingSettings restored = await session.ChangeAsync(s => s with { Enabled = true });
        Assert(restored.OnBrightness == KeyboardBrightnessLevel.Low, "power cycle retains brightness");
        await session.ChangeAsync(s => s with { Effect = KeyboardRgbEffect.Breathing });
        int startsBeforeBrightness = transport.Starts;
        KeyboardLightingSettings dimmed = await session.ChangeAsync(s => s.WithBrightness(KeyboardBrightnessLevel.Medium));
        Assert(transport.Starts == startsBeforeBrightness, "live brightness must not restart animation");
        await session.ReapplyAsync();
        Assert(transport.Starts == startsBeforeBrightness + 1 && transport.Active == 1 && !transport.WriteDuringEffect, "reapply stops old renderer before restarting exactly one");
        Assert(dimmed.Effect == KeyboardRgbEffect.Breathing && transport.Active == 1, "brightness retains effect");
        var color = new KeyboardRgbColor(2, 44, 9);
        KeyboardLightingSettings colored = await session.ChangeAsync(s => s.WithColor(2, color, false));
        Assert(colored.Center == color && colored.Left == initial.Left && colored.Effect == KeyboardRgbEffect.Breathing,
            "stored colors are independent of animation");
        await session.ChangeAsync(s => s with { Enabled = false });
        Assert(transport.Active == 0 && transport.Hardware.Brightness == KeyboardBrightnessLevel.Off, "off stops worker");
        Assert(transport.LastRestoredBrightness == KeyboardBrightnessLevel.Off, "stop cannot restore old brightness before switching off");
        KeyboardLightingSettings resumed = await session.ChangeAsync(s => s with { Enabled = true });
        Assert(resumed.Effect == KeyboardRgbEffect.Breathing && resumed.OnBrightness == KeyboardBrightnessLevel.Medium,
            "on resumes mode and brightness");

        Task<KeyboardLightingSettings> first = session.ChangeAsync(s => s with { Speed = KeyboardEffectSpeed.Fast });
        Task<KeyboardLightingSettings> last = session.ChangeAsync(s => s.WithBrightness(KeyboardBrightnessLevel.Low));
        await Task.WhenAll(first, last);
        KeyboardLightingSettings final = await session.ReadSettingsAsync();
        Assert(final.Speed == KeyboardEffectSpeed.Fast && final.OnBrightness == KeyboardBrightnessLevel.Low,
            "queued updates are based on latest state");
        Assert(transport.MaxActive == 1 && !transport.WriteDuringEffect, "never overlap workers or overwrite old restoration");

        await session.ChangeAsync(s => s with { Effect = null });
        Assert(transport.Hardware.GetZone(2).Color == color, "manual mode restores latest manual colors");
        transport.FailWrite = true;
        bool failed = false;
        try { await session.ChangeAsync(s => s.WithColor(1, color, false)); }
        catch (InvalidOperationException) { failed = true; }
        Assert(failed && (await session.ReadSettingsAsync()).Left == initial.Left, "failed write is not committed");
        transport.FailWrite = false;
        await session.ChangeAsync(s => s with { Effect = KeyboardRgbEffect.Pulse });
        await session.DisposeAsync();
        Assert(transport.Active == 0, "dispose awaits renderer stop");
        bool disposed = false;
        try { await session.ChangeAsync(s => s); } catch (ObjectDisposedException) { disposed = true; }
        Assert(disposed, "disposed session rejects changes");
        Console.WriteLine("PASS: RGB initialization, power memory, brightness/effect, colors, off/resume, concurrent changes, failure and shutdown");

        var uiTransport = new FakeTransport();
        using var vm = new MainWindowViewModel(new FakeReader(), uiTransport, new FakeFan(), new WindowsPowerOverlayController(),
            observationPath: Path.Combine(Path.GetTempPath(), "AorusControlTests", $"fan-observations-{Guid.NewGuid():N}.json"));
        await vm.Keyboard.StartAsync();
        await vm.Keyboard.SetBrightnessAsync(KeyboardBrightnessLevel.Low);
        await vm.Keyboard.StartEffectAsync(KeyboardRgbEffect.Breathing);
        await vm.Keyboard.SetBrightnessAsync(KeyboardBrightnessLevel.Medium);
        Assert(vm.Keyboard.EffectRunning && vm.Keyboard.Brightness == KeyboardBrightnessLevel.Medium, "UI brightness retains animation");
        await vm.Keyboard.SetColorAsync(2, color);
        Assert(vm.Keyboard.EffectRunning && vm.Keyboard.GetZoneColor(2) == color, "UI colors preserve effect");
        await vm.Keyboard.SetPowerAsync(false);
        Assert(!vm.Keyboard.PowerOn && !vm.Keyboard.EffectRunning && vm.Keyboard.ModeIsEffect, "UI off keeps mode");
        await vm.Keyboard.SetPowerAsync(true);
        Assert(vm.Keyboard.EffectRunning && vm.Keyboard.Brightness == KeyboardBrightnessLevel.Medium, "UI resumes state");
        typeof(KeyboardViewModel).GetField("_busy", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm.Keyboard, true);
        Task pendingLow = vm.Keyboard.QueueExternalBrightness(KeyboardBrightnessLevel.Low);
        Task pendingMedium = vm.Keyboard.QueueExternalBrightness(KeyboardBrightnessLevel.Medium);
        Task pendingOff = vm.Keyboard.QueueExternalBrightness(KeyboardBrightnessLevel.Off);
        typeof(KeyboardViewModel).GetField("_busy", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm.Keyboard, false);
        await Task.WhenAll(pendingLow, pendingMedium, pendingOff);
        Assert(!vm.Keyboard.PowerOn && uiTransport.LastRestoredBrightness == KeyboardBrightnessLevel.Off, "latest external brightness wins after busy UI");
        await vm.Keyboard.QueueExternalBrightness(KeyboardBrightnessLevel.Low);
        Assert(vm.Keyboard.PowerOn && vm.Keyboard.EffectRunning && vm.Keyboard.Brightness == KeyboardBrightnessLevel.Low, "external on resumes selected effect");
        int repeatedWrites = uiTransport.Writes;
        int repeatedStarts = uiTransport.Starts;
        for (int i = 0; i < 20; i++) await vm.Keyboard.QueueExternalBrightness(KeyboardBrightnessLevel.Low);
        Assert(uiTransport.Writes == repeatedWrites && uiTransport.Starts == repeatedStarts, "repeated identical notifications do not resend or restart");
        await vm.Keyboard.StopEffectAsync();
        Assert(!vm.Keyboard.ModeIsEffect && uiTransport.Hardware.GetZone(2).Color == color, "UI manual applies saved colors");
        await vm.PrepareToCloseAsync();
        Console.WriteLine("PASS: WPF ViewModel RGB session integration");
        bool listenerStarted = false, listenerStopped = false;
        using var listenerVm = new MainWindowViewModel(new FakeReader(), new FakeTransport(), new FakeFan(), new WindowsPowerOverlayController(),
            brightnessListener: async (_, token) =>
            {
                listenerStarted = true;
                try { await Task.Delay(Timeout.Infinite, token); }
                finally { listenerStopped = true; }
            });
        await listenerVm.Keyboard.StartAsync();
        Assert(listenerStarted, "listener starts after keyboard initialization");
        await listenerVm.PrepareToCloseAsync();
        Assert(listenerStopped, "shutdown waits for event listener");
        Console.WriteLine("PASS: external brightness coalescing, effect resume and listener shutdown");

        var savedStore = new MemoryStore { Settings = initial with { Enabled = false, OnBrightness = KeyboardBrightnessLevel.Low } };
        using var restoredVm = new MainWindowViewModel(new FakeReader(), new FakeTransport(), new FakeFan(),
            new WindowsPowerOverlayController(), keyboardSettingsStore: savedStore,
            observationPath: Path.Combine(Path.GetTempPath(), "AorusControlTests", $"fan-observations-{Guid.NewGuid():N}.json"));
        await restoredVm.Keyboard.StartAsync();
        Assert(!restoredVm.Keyboard.PowerOn, "stored off restored on initialization");
        await restoredVm.Keyboard.SetPowerAsync(true);
        Assert(restoredVm.Keyboard.Brightness == KeyboardBrightnessLevel.Low && savedStore.Settings!.Enabled, "saved brightness resumed and persisted");
        savedStore.FailSave = true;
        await restoredVm.Keyboard.SetBrightnessAsync(KeyboardBrightnessLevel.Medium);
        Assert(restoredVm.Keyboard.Brightness == KeyboardBrightnessLevel.Medium && restoredVm.Keyboard.Status.Contains("nicht gespeichert"),
            "storage failure must not falsely claim hardware failure");
        await restoredVm.PrepareToCloseAsync();
        Console.WriteLine("PASS: RGB startup restore, saving UI changes and visible persistence failure");

        var resumeTransport = new FakeTransport();
        using var resumeVm = new MainWindowViewModel(new FakeReader(), resumeTransport, new FakeFan(), new WindowsPowerOverlayController(),
            resumeReapplyDelay: _ => Task.CompletedTask,
            observationPath: Path.Combine(Path.GetTempPath(), "AorusControlTests", $"fan-observations-{Guid.NewGuid():N}.json"));
        await resumeVm.Keyboard.ReapplyAfterResumeAsync();
        Assert(resumeTransport.Writes == 0, "resume before the keyboard has ever initialized must not write");
        await resumeVm.Keyboard.StartAsync();
        resumeTransport.SimulateReset();
        int writesBeforeResume = resumeTransport.Writes;
        await resumeVm.Keyboard.ReapplyAfterResumeAsync();
        Assert(resumeTransport.Writes > writesBeforeResume, "resume after device reset must reapply the last known lighting");
        await resumeVm.PrepareToCloseAsync();
        int writesAfterClose = resumeTransport.Writes;
        await resumeVm.Keyboard.ReapplyAfterResumeAsync();
        Assert(resumeTransport.Writes == writesAfterClose, "resume after closing must not reach for a disposed session");
        Console.WriteLine("PASS: resume-from-sleep reapplies lighting once initialized, skips before init and after close");

        var uiVm2Transport = new FakeTransport();
        using var uiVm2 = new MainWindowViewModel(new FakeReader(), uiVm2Transport, new FakeFan(), new WindowsPowerOverlayController(),
            observationPath: Path.Combine(Path.GetTempPath(), "AorusControlTests", $"fan-observations-{Guid.NewGuid():N}.json"));
        await uiVm2.Keyboard.StartAsync();

        // The tile highlight follows what runs on the device, so manual colours are the
        // "Manual" tile rather than an absence of selection.
        Assert(uiVm2.Keyboard.ActiveEffectName == "Manual", "no effect running means the manual tile is the active one");
        await uiVm2.Keyboard.StartEffectAsync(KeyboardRgbEffect.Wave);
        Assert(uiVm2.Keyboard.ActiveEffect == KeyboardRgbEffect.Wave && uiVm2.Keyboard.ActiveEffectName == "Wave",
            "a running effect must be the highlighted tile");
        await uiVm2.Keyboard.StopEffectAsync();
        Assert(uiVm2.Keyboard.ActiveEffect is null && uiVm2.Keyboard.ActiveEffectName == "Manual",
            "stopping returns the highlight to the manual tile");

        // Brightness and speed sliders address the firmware's own steps by index.
        await uiVm2.Keyboard.SetBrightnessAsync(KeyboardBrightnessLevel.Low);
        Assert(uiVm2.Keyboard.BrightnessIndex == KeyboardBrightnessLevels.All.ToList().IndexOf(KeyboardBrightnessLevel.Low),
            "the brightness slider position must match the level actually set");
        Assert(uiVm2.Keyboard.BrightnessLabel == "Niedrig", "the readout names the step in German");
        uiVm2.Keyboard.BrightnessIndex = KeyboardBrightnessLevels.All.ToList().IndexOf(KeyboardBrightnessLevel.Medium);
        await uiVm2.Keyboard.PendingSliderWrite;
        Assert(uiVm2.Keyboard.Brightness == KeyboardBrightnessLevel.Medium, "dragging the slider writes the level through");
        uiVm2.Keyboard.BrightnessIndex = 99;
        Assert(uiVm2.Keyboard.Brightness == KeyboardBrightnessLevel.Medium, "an out-of-range slider index changes nothing");

        await uiVm2.Keyboard.SetSpeedAsync(KeyboardEffectSpeed.Fast);
        Assert(uiVm2.Keyboard.SpeedIndex == 3 && uiVm2.Keyboard.SpeedLabel == "Schnell",
            "the tempo slider position and readout follow the speed actually set");
        uiVm2.Keyboard.SpeedIndex = 0;
        await uiVm2.Keyboard.PendingSliderWrite;
        Assert(uiVm2.Keyboard.Speed == KeyboardEffectSpeed.VerySlow, "dragging the tempo slider writes it through");
        uiVm2.Keyboard.SpeedIndex = -1;
        Assert(uiVm2.Keyboard.Speed == KeyboardEffectSpeed.VerySlow, "an out-of-range tempo index changes nothing");

        // The preview shows the same frame the renderer produces, not a lookalike.
        await uiVm2.Keyboard.StartEffectAsync(KeyboardRgbEffect.RainbowMarquee);
        await (Task)Task.Run(() => { });
        typeof(KeyboardViewModel).GetMethod("RenderPreviewFrame", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(uiVm2.Keyboard, null);
        Assert(uiVm2.Keyboard.PreviewZone1Brush is System.Windows.Media.SolidColorBrush,
            "the preview publishes a concrete brush per zone");
        Assert(uiVm2.Keyboard.PreviewOpacity > 0, "a lit keyboard is previewed with visible lighting");
        Assert(uiVm2.Keyboard.PreviewCaption.Contains("Regenbogen"), "the caption names the running effect");

        await uiVm2.Keyboard.SetPowerAsync(false);
        typeof(KeyboardViewModel).GetMethod("RenderPreviewFrame", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(uiVm2.Keyboard, null);
        Assert(((System.Windows.Media.SolidColorBrush)uiVm2.Keyboard.PreviewZone1Brush).Color
                == System.Windows.Media.Color.FromRgb(0, 0, 0),
            "an off keyboard previews as unlit rather than keeping the last frame");
        Assert(uiVm2.Keyboard.PreviewCaption.Contains("aus"), "the caption says the lighting is off");
        // Which zone swatches are meaningful depends on what is running: offering a colour
        // choice that changes nothing is a control that lies about what it does.
        await uiVm2.Keyboard.SetPowerAsync(true);
        await uiVm2.Keyboard.StopEffectAsync();
        Assert(uiVm2.Keyboard.Zone1AffectsLighting && uiVm2.Keyboard.Zone2AffectsLighting && uiVm2.Keyboard.Zone3AffectsLighting,
            "manual mode reads all three stored colours");
        Assert(uiVm2.Keyboard.Zone1Label == "Zone 1", "no effect means zone 1 is just zone 1");
        Assert(uiVm2.Keyboard.InactiveZoneNote.Length == 0, "nothing is inactive in manual mode");

        await uiVm2.Keyboard.StartEffectAsync(KeyboardRgbEffect.Breathing);
        Assert(uiVm2.Keyboard.Zone1AffectsLighting && !uiVm2.Keyboard.Zone2AffectsLighting && !uiVm2.Keyboard.Zone3AffectsLighting,
            "breathing is built from zone 1 only");
        Assert(uiVm2.Keyboard.Zone1Label.Contains("Basisfarbe"), "zone 1 must be named as the effect's base colour");
        Assert(uiVm2.Keyboard.PaletteHint.Contains("Zone 1"), "the hint must say which colour is in play");

        await uiVm2.Keyboard.StartEffectAsync(KeyboardRgbEffect.RainbowMarquee);
        Assert(!uiVm2.Keyboard.Zone1AffectsLighting && !uiVm2.Keyboard.Zone2AffectsLighting && !uiVm2.Keyboard.Zone3AffectsLighting,
            "the rainbow marquee reads no stored colour at all");
        Assert(uiVm2.Keyboard.InactiveZoneNote.Length > 0, "the swatches must say they have no effect right now");
        Assert(uiVm2.Keyboard.PaletteHint.Contains("gespeichert"),
            "and the hint must promise the colours are kept, not lost");

        await uiVm2.PrepareToCloseAsync();
        Console.WriteLine("PASS: effect tiles, slider indices, live preview and per-zone colour relevance follow real device state");
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class MemoryStore : IKeyboardSettingsStore
    {
        public KeyboardLightingSettings? Settings { get; set; }
        public bool FailSave { get; set; }
        public KeyboardLightingSettings? Load() => Settings;
        public void Save(KeyboardLightingSettings settings)
        {
            if (FailSave) throw new IOException("Simulated save failure");
            Settings = settings;
        }
    }

    private sealed class FakeTransport : IAorusKeyboardRgbController, ILiveEffectBrightness
    {
        private KeyboardBrightnessLevel? _liveBrightness;
        public int Starts { get; private set; }
        public KeyboardBrightnessLevel? LastRestoredBrightness { get; private set; }
        public void UpdateEffectBrightness(KeyboardBrightnessLevel level)
        {
            _liveBrightness = level;
            Hardware = new(Hardware.Zones.Select(z => z with { Brightness = (byte)level }).ToArray());
        }
        public void Dispose() { }
        public KeyboardRgbState SetLighting(bool enabled) => throw new InvalidOperationException("Legacy writer must not be used");
        public KeyboardRgbState SetBrightness(KeyboardBrightnessLevel level) => throw new InvalidOperationException("Legacy writer must not be used");
        public KeyboardRgbState SetColor(int zone, KeyboardRgbColor color, bool applyToAllZones) => throw new InvalidOperationException("Legacy writer must not be used");
        public KeyboardRgbState Hardware { get; private set; } = new(new KeyboardRgbZoneState[]
        {
            new(1, new(10, 20, 30), 50), new(2, new(40, 50, 60), 50), new(3, new(70, 80, 90), 50)
        });
        public int Writes { get; private set; }
        public int Active { get; private set; }
        public int MaxActive { get; private set; }
        public bool WriteDuringEffect { get; private set; }
        public bool FailWrite { get; set; }
        public KeyboardRgbState ReadState() => Hardware;
        public void SimulateReset() => Hardware = new(Hardware.Zones.Select(z => z with { Color = new KeyboardRgbColor(0, 0, 255), Brightness = 0 }).ToArray());
        public KeyboardRgbState ApplyState(KeyboardRgbState state)
        {
            if (Active > 0) WriteDuringEffect = true;
            if (FailWrite) throw new InvalidOperationException("Simulated write failure");
            Writes++;
            return Hardware = state;
        }
        public async Task PlayEffectAsync(KeyboardRgbEffect effect, KeyboardEffectSpeed speed, CancellationToken cancellationToken)
        {
            Starts++;
            _liveBrightness = null;
            KeyboardRgbState original = Hardware;
            Active++;
            MaxActive = Math.Max(MaxActive, Active);
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            finally
            {
                // Delayed restoration simulates the real transport's shutdown sequence.
                await Task.Delay(10);
                Hardware = _liveBrightness is { } level ? new(original.Zones.Select(z => z with { Brightness = (byte)level }).ToArray()) : original;
                LastRestoredBrightness = Hardware.Brightness;
                Active--;
            }
        }
    }
}
