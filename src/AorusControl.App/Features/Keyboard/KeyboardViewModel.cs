using System.Windows.Threading;
using MediaBrush = System.Windows.Media.Brush;
using AorusControl.App.Infrastructure;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.Diagnostics;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

namespace AorusControl.App.Features.Keyboard;

/// <summary>
/// Everything the Tastatur section owns: lighting state, effects, the Fn+Space listener
/// and the live preview. Self-contained on purpose - it talks to one device interface and
/// exposes the module lifecycle, so the shell composes it rather than containing it.
/// </summary>
public sealed class KeyboardViewModel : ObservableObject, IFeatureModule
{
    private readonly IAorusKeyboardRgbController _controller;
    private readonly KeyboardLightingSession _session;
    private readonly IKeyboardSettingsStore? _settingsStore;
    private readonly Func<Action<KeyboardBrightnessLevel>, CancellationToken, Task>? _brightnessListener;
    private readonly Func<TimeSpan, Task> _resumeReapplyDelay;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _rgbTimer;
    private readonly DispatcherTimer _previewTimer;
    private readonly System.Diagnostics.Stopwatch _effectClock = new();

    private CancellationTokenSource _brightnessCancellation = new();
    private Task? _brightnessListenerTask;
    private Task _brightnessDrainTask = Task.CompletedTask;
    private KeyboardBrightnessLevel? _pendingBrightness;
    private bool _drainingBrightness;
    private bool _busy, _initialized, _closing, _disposed, _visible;
    private string _brightnessEventStatus = "Fn+Space-Ereignisleser nicht gestartet";
    private bool _controlsEnabled, _powerOn, _modeIsEffect, _linkZones, _effectRunning;
    private KeyboardBrightnessLevel _brightness = KeyboardBrightnessLevel.High;
    private KeyboardEffectSpeed _speed = KeyboardEffectSpeed.Normal;
    private KeyboardRgbEffect? _activeEffect;
    private KeyboardRgbEffect _selectedEffect = KeyboardRgbEffect.Breathing;
    private KeyboardRgbColor _zone1 = new(0, 255, 0), _zone2 = new(0, 255, 0), _zone3 = new(0, 255, 0);
    private string _paletteHint = "Gespeicherte manuelle Farben";
    private string _status = "Tastatur wird geprüft …";
    private MediaBrush _previewZone1 = CreateBrush(new(0, 0, 0));
    private MediaBrush _previewZone2 = CreateBrush(new(0, 0, 0));
    private MediaBrush _previewZone3 = CreateBrush(new(0, 0, 0));
    private double _previewOpacity;

    public KeyboardViewModel(
        IAorusKeyboardRgbController controller,
        IKeyboardSettingsStore? settingsStore = null,
        Func<Action<KeyboardBrightnessLevel>, CancellationToken, Task>? brightnessListener = null,
        Func<TimeSpan, Task>? resumeReapplyDelay = null)
    {
        _controller = controller;
        _session = new KeyboardLightingSession(controller);
        _settingsStore = settingsStore;
        _brightnessListener = brightnessListener;
        _resumeReapplyDelay = resumeReapplyDelay ?? Task.Delay;
        _rgbTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _rgbTimer.Tick += OnRgbTimerTick;
        // 20 Hz is enough for the eye and a third of the renderer's own rate; it only ever
        // runs while the Tastatur section is visible and an effect is playing.
        _previewTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
        _previewTimer.Tick += (_, _) => RenderPreviewFrame();
        TogglePowerCommand = new AsyncRelayCommand(() => SetPowerAsync(!PowerOn));
        ReapplyCommand = new AsyncRelayCommand(ReapplyAsync);
        StartEffectCommand = new AsyncRelayCommand(() => StartEffectAsync(SelectedEffect));
        StopEffectCommand = new AsyncRelayCommand(StopEffectAsync);
        ApplyEffectCommand = new AsyncRelayCommand<string>(name =>
            // "Manual" is the tenth tile rather than a separate button: picking it is
            // simply choosing no effect.
            name == "Manual" ? StopEffectAsync()
                : Enum.TryParse(name, ignoreCase: true, out KeyboardRgbEffect effect)
                    ? StartEffectAsync(effect)
                    : Task.CompletedTask);
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public AsyncRelayCommand TogglePowerCommand { get; }
    public AsyncRelayCommand ReapplyCommand { get; }
    public AsyncRelayCommand StartEffectCommand { get; }
    public AsyncRelayCommand StopEffectCommand { get; }
    public AsyncRelayCommand<string> ApplyEffectCommand { get; }

    public bool IsBusy => _busy;

    /// <summary>Set by the shell: the preview animates only while its own section is on
    /// screen and the window is visible. An animation nobody looks at is battery drain.</summary>
    public bool IsVisible { set { _visible = value; UpdatePreviewTimer(); } }

    public string BrightnessEventStatus { get => _brightnessEventStatus; private set => SetProperty(ref _brightnessEventStatus, value); }
    public bool ControlsEnabled { get => _controlsEnabled; private set => SetProperty(ref _controlsEnabled, value); }
    public bool PowerOn { get => _powerOn; private set => SetProperty(ref _powerOn, value); }
    public string PowerButtonText => PowerOn ? "Ausschalten" : "Einschalten";
    public KeyboardBrightnessLevel Brightness { get => _brightness; private set => SetProperty(ref _brightness, value); }
    public KeyboardEffectSpeed Speed { get => _speed; private set => SetProperty(ref _speed, value); }
    public bool LinkZones { get => _linkZones; set => SetProperty(ref _linkZones, value); }
    public bool EffectRunning { get => _effectRunning; private set => SetProperty(ref _effectRunning, value); }
    public bool ModeIsEffect { get => _modeIsEffect; private set => SetProperty(ref _modeIsEffect, value); }
    public string PaletteHint { get => _paletteHint; private set => SetProperty(ref _paletteHint, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public KeyboardRgbEffect SelectedEffect { get => _selectedEffect; set => SetProperty(ref _selectedEffect, value); }

    public IReadOnlyList<KeyboardEffectChoice> EffectChoices { get; } = new[]
    {
        (KeyboardRgbEffect.Breathing, "Atmen"),
        (KeyboardRgbEffect.Pulse, "Pulsieren"),
        (KeyboardRgbEffect.ColorCycle, "Farbwechsel"),
        (KeyboardRgbEffect.RainbowMarquee, "Regenbogen-Lauflicht"),
        (KeyboardRgbEffect.Wave, "Welle"),
        (KeyboardRgbEffect.Marquee, "Lauflicht"),
        (KeyboardRgbEffect.Rotate, "Pendel"),
        (KeyboardRgbEffect.Raindrop, "Regentropfen"),
        (KeyboardRgbEffect.FadeSweep, "Ausblendende Welle"),
    }.Select(pair => new KeyboardEffectChoice(pair.Item1, pair.Item2)).ToArray();

    public string Zone1Hex => _zone1.Hex;
    public string Zone2Hex => _zone2.Hex;
    public string Zone3Hex => _zone3.Hex;
    public MediaBrush Zone1Brush => CreateBrush(_zone1);
    public MediaBrush Zone2Brush => CreateBrush(_zone2);
    public MediaBrush Zone3Brush => CreateBrush(_zone3);

    /// <summary>
    /// Brightness as a 0-3 slider position over the four steps the firmware accepts. The
    /// setter goes through the same guarded write path as every other brightness change,
    /// and the getter always reflects the device, so a rejected write makes the slider snap
    /// back rather than lie.
    /// </summary>
    public int BrightnessIndex
    {
        get => Math.Max(0, KeyboardBrightnessLevels.All.ToList().IndexOf(Brightness));
        set => Step(KeyboardBrightnessLevels.All, value, Brightness, nameof(BrightnessIndex), SetBrightnessAsync);
    }

    /// <summary>Playback speed as a 0-4 slider position over the five named steps.</summary>
    public int SpeedIndex
    {
        get
        {
            int index = KeyboardEffectSpeeds.All.ToList().IndexOf(Speed);
            return index < 0 ? 2 : index;
        }
        set => Step(KeyboardEffectSpeeds.All, value, Speed, nameof(SpeedIndex), SetSpeedAsync);
    }

    /// <summary>Shared by both index sliders: an out-of-range or no-op position pushes the
    /// real value back over the dragged one, anything else starts the write.</summary>
    private void Step<T>(IReadOnlyList<T> steps, int index, T current, string property, Func<T, Task> write)
    {
        if (index < 0 || index >= steps.Count || EqualityComparer<T>.Default.Equals(steps[index], current))
        {
            OnPropertyChanged(property);
            return;
        }

        PendingSliderWrite = write(steps[index]);
    }

    public string BrightnessLabel => DescribeBrightness(Brightness);
    public string SpeedLabel => DescribeSpeed(Speed);

    /// <summary>
    /// The device write a slider drag started. A two-way bound property setter cannot be
    /// awaited, so the task it launches is published here rather than dropped: that keeps
    /// the write observable (tests await it; a caller can tell when the device has really
    /// been told) instead of being fire-and-forget.
    /// </summary>
    internal Task PendingSliderWrite { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// The effect actually running on the device, or null for manual zone colours - this is
    /// what highlights an effect tile. Deliberately separate from <see cref="SelectedEffect"/>,
    /// which is only the last pick: if a write fails, the tile must keep showing what is
    /// really on the keyboard.
    /// </summary>
    public KeyboardRgbEffect? ActiveEffect
    {
        get => _activeEffect;
        private set { if (SetProperty(ref _activeEffect, value)) OnPropertyChanged(nameof(ActiveEffectName)); }
    }

    /// <summary>Tile identity of the running effect; "Manual" when none runs, so the
    /// manual-colours tile is a state among equals rather than a special case.</summary>
    public string ActiveEffectName => _activeEffect?.ToString() ?? "Manual";

    // ---- Live preview ----------------------------------------------------------
    // Rendered from KeyboardEffectFrames, the very function whose output is written to the
    // device, so the preview shows the actual frame rather than a lookalike.
    public MediaBrush PreviewZone1Brush { get => _previewZone1; private set => SetProperty(ref _previewZone1, value); }
    public MediaBrush PreviewZone2Brush { get => _previewZone2; private set => SetProperty(ref _previewZone2, value); }
    public MediaBrush PreviewZone3Brush { get => _previewZone3; private set => SetProperty(ref _previewZone3, value); }

    /// <summary>Approximates the LEDs' perceived brightness. The frames themselves carry no
    /// brightness - the device applies that separately - so this is a rendering of the
    /// chosen step, not a measured luminance.</summary>
    public double PreviewOpacity { get => _previewOpacity; private set => SetProperty(ref _previewOpacity, value); }

    // ---- Which zone colours the running mode actually reads ---------------------
    // Offering a colour picker for an effect that ignores colours is a control that
    // pretends to do something. These drive the swatches so the UI can say plainly which
    // colour is in play - without hiding the others, since they stay stored and come back
    // in manual mode.
    public KeyboardEffectColorUsage ColorUsage => KeyboardEffectFrames.ColorUsage(_activeEffect);
    public bool Zone1AffectsLighting => ColorUsage is KeyboardEffectColorUsage.AllZones or KeyboardEffectColorUsage.BaseColorOnly;
    public bool Zone2AffectsLighting => ColorUsage is KeyboardEffectColorUsage.AllZones;
    public bool Zone3AffectsLighting => ColorUsage is KeyboardEffectColorUsage.AllZones;

    /// <summary>Names zone 1's role, since for Atmen/Pulsieren it is not just "zone 1" but
    /// the colour the whole effect is built from.</summary>
    public string Zone1Label => ColorUsage == KeyboardEffectColorUsage.BaseColorOnly ? "Zone 1 · Basisfarbe" : "Zone 1";
    public bool HasInactiveZones => ColorUsage != KeyboardEffectColorUsage.AllZones;

    /// <summary>Said once under the row rather than three times under the swatches. It has
    /// to carry the reassurance too: dimmed means "no effect right now", never "lost".</summary>
    public string InactiveZoneNote => ColorUsage switch
    {
        KeyboardEffectColorUsage.BaseColorOnly =>
            "Die gedimmten Zonen liest dieser Effekt nicht - er baut alles aus der Basisfarbe. Sie bleiben gespeichert und gelten wieder im manuellen Modus.",
        KeyboardEffectColorUsage.None =>
            "Dieser Effekt liest keine der gespeicherten Farben. Sie bleiben erhalten und gelten wieder im manuellen Modus.",
        _ => string.Empty
    };

    public string PreviewCaption => PowerOn
        ? $"Läuft: {(_activeEffect is { } effect ? GetEffectName(effect) : "Manuelle Zonenfarben")} · Tempo {DescribeSpeed(Speed)} · Helligkeit {DescribeBrightness(Brightness)}"
        : "Beleuchtung aus · Auswahl bleibt gespeichert";

    public KeyboardRgbColor GetZoneColor(int zone) => zone switch
    {
        1 => _zone1,
        2 => _zone2,
        3 => _zone3,
        _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };

    public Task SetPowerAsync(bool enabled) => ChangeAsync(s => s with { Enabled = enabled });
    public Task ReapplyAsync() => ChangeAsync(s => s, forceWrite: true);

    public Task SetBrightnessAsync(KeyboardBrightnessLevel level) =>
        level == Brightness ? Task.CompletedTask : ChangeAsync(s => s.WithBrightness(level));

    public Task SetSpeedAsync(KeyboardEffectSpeed speed) =>
        speed == Speed ? Task.CompletedTask : ChangeAsync(s => s with { Speed = speed });

    public Task SetColorAsync(int zone, KeyboardRgbColor color) =>
        ChangeAsync(s => s.WithColor(zone, color, LinkZones));

    public Task StartEffectAsync(KeyboardRgbEffect effect) =>
        ChangeAsync(s => s with { Effect = effect, Enabled = true });

    public Task StopEffectAsync() => ChangeAsync(s => s with { Effect = null });

    public static string DescribeSpeed(KeyboardEffectSpeed speed) => speed switch
    {
        KeyboardEffectSpeed.VerySlow => "Sehr langsam",
        KeyboardEffectSpeed.Slow => "Langsam",
        KeyboardEffectSpeed.Fast => "Schnell",
        KeyboardEffectSpeed.VeryFast => "Sehr schnell",
        _ => "Normal"
    };

    public static string DescribeBrightness(KeyboardBrightnessLevel level) => level switch
    {
        KeyboardBrightnessLevel.Off => "Aus",
        KeyboardBrightnessLevel.Low => "Niedrig",
        KeyboardBrightnessLevel.Medium => "Mittel",
        _ => "Hell"
    };

    private static string GetEffectName(KeyboardRgbEffect effect) => effect switch
    {
        KeyboardRgbEffect.Breathing => "Atmen",
        KeyboardRgbEffect.Pulse => "Pulsieren",
        KeyboardRgbEffect.ColorCycle => "Farbwechsel",
        KeyboardRgbEffect.RainbowMarquee => "Regenbogen-Lauflicht",
        KeyboardRgbEffect.Wave => "Welle",
        KeyboardRgbEffect.Marquee => "Lauflicht",
        KeyboardRgbEffect.Rotate => "Pendel",
        KeyboardRgbEffect.Raindrop => "Regentropfen",
        KeyboardRgbEffect.FadeSweep => "Ausblendende Welle",
        _ => effect.ToString()
    };

    public async Task StartAsync()
    {
        if (_busy || _initialized) return;
        _busy = true;
        ControlsEnabled = false;
        try
        {
            KeyboardLightingSettings? saved = null;
            string? warning = null;
            if (_settingsStore is not null)
            {
                try { saved = await Task.Run(_settingsStore.Load); }
                catch (Exception exception) { warning = $"Gespeicherte RGB-Auswahl nicht geladen: {exception.Message}"; }
            }
            Show(saved is null ? await _session.ReadSettingsAsync() : await _session.ChangeAsync(_ => saved));
            if (warning is not null) Status = warning + " · Aktueller Gerätezustand gelesen, nichts automatisch überschrieben.";
            _initialized = true;
            ControlsEnabled = true;
            if (_brightnessListener is not null && _brightnessListenerTask is null)
                _brightnessListenerTask = ListenForBrightnessAsync();
        }
        catch (Exception exception)
        {
            AppLog.Error("keyboard", "Tastatur nicht verfügbar.", exception);
            Status = $"Tastatur nicht verfügbar: {exception.Message}";
        }
        finally { _busy = false; }
    }

    /// <summary>Stops accepting Fn+Space events and lets in-flight ones finish. Split from
    /// <see cref="SuspendAsync"/> because the shell has to wait for every feature to go idle
    /// in between.</summary>
    public async Task StopListeningAsync()
    {
        _closing = true;
        _brightnessCancellation.Cancel();
        if (_brightnessListenerTask is not null)
        {
            await _brightnessListenerTask;
            BrightnessEventStatus = "Fn+Space-Ereignisleser beendet.";
        }
        await _brightnessDrainTask;
    }

    /// <summary>Hands the lighting back to the firmware. Throws if the device refuses - the
    /// shell then keeps the window open and calls <see cref="ResumeAfterFailedClose"/>.</summary>
    public async Task SuspendAsync()
    {
        _rgbTimer.Stop();
        _previewTimer.Stop();
        await _session.SuspendAsync();
    }

    public void ResumeAfterFailedClose()
    {
        _closing = false;
        if (_brightnessListener is null || !_initialized || _disposed) return;
        _brightnessCancellation.Dispose();
        _brightnessCancellation = new();
        _brightnessListenerTask = ListenForBrightnessAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _brightnessCancellation.Cancel();
        _previewTimer.Stop();
        _rgbTimer.Stop();
        _rgbTimer.Tick -= OnRgbTimerTick;
        _session.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _controller.Dispose();
    }

    private async Task ChangeAsync(Func<KeyboardLightingSettings, KeyboardLightingSettings> change, bool forceWrite = false)
    {
        if (_closing || _disposed || _busy || !ControlsEnabled) return;
        _busy = true;
        ControlsEnabled = false;
        Status = "Einstellung wird übernommen …";
        try
        {
            KeyboardLightingSettings state = forceWrite ? await _session.ReapplyAsync() : await _session.ChangeAsync(change);
            Show(state);
            if (_settingsStore is not null)
            {
                try { await Task.Run(() => _settingsStore.Save(state)); }
                catch (Exception exception) { Status += $" · Aktiv, aber nicht gespeichert: {exception.Message}"; }
            }
        }
        catch (Exception exception)
        {
            Show(await _session.ReadSettingsAsync());
            EffectRunning = false;
            _rgbTimer.Stop();
            AppLog.Error("keyboard", "RGB-Änderung fehlgeschlagen.", exception);
            Status = $"RGB-Änderung fehlgeschlagen: {exception.Message}. Auswahl erneut anwenden.";
        }
        finally
        {
            _busy = false;
            ControlsEnabled = true;
            // Unconditional: the tiles bind one-way and a RadioButton lights itself on
            // click, so only a notification pushes the real value back over a failed write.
            OnPropertyChanged(nameof(ActiveEffectName));
            OnPropertyChanged(nameof(BrightnessIndex));
            OnPropertyChanged(nameof(SpeedIndex));
        }
    }

    private async Task ListenForBrightnessAsync()
    {
        BrightnessEventStatus = "Fn+Space-Ereignisleser wird gestartet; noch kein Ereignis empfangen";
        try
        {
            CancellationToken token = _brightnessCancellation.Token;
            await KeyboardNotificationReconnect.RunAsync(_brightnessListener!, level => _dispatcher.BeginInvoke(new Action(() =>
            {
                if (!token.IsCancellationRequested && !_closing && !_disposed) _ = QueueExternalBrightness(level);
            })), (error, pause) => _dispatcher.BeginInvoke(new Action(() =>
            {
                if (!token.IsCancellationRequested && !_disposed)
                    BrightnessEventStatus = $"Fn+Space nicht verbunden; neuer Versuch in {pause.TotalSeconds:0} s: {error.Message}";
            })), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_brightnessCancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            _ = _dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_disposed) BrightnessEventStatus = "Fn+Space-Synchronisierung nicht verfügbar: " + error.Message;
            }));
        }
    }

    internal Task QueueExternalBrightness(KeyboardBrightnessLevel level)
    {
        if (_closing || _disposed) return Task.CompletedTask;
        if (!KeyboardBrightnessLevels.All.Contains(level)) throw new ArgumentOutOfRangeException(nameof(level));
        _pendingBrightness = level;
        if (!_drainingBrightness) _brightnessDrainTask = DrainBrightnessAsync();
        return _brightnessDrainTask;
    }

    private async Task DrainBrightnessAsync()
    {
        _drainingBrightness = true;
        try
        {
            while (!_closing && !_disposed && _pendingBrightness is not null)
            {
                // Only wait while an event is pending; no idle polling.
                while (_busy && !_closing && !_disposed) await Task.Delay(25);
                if (_closing || _disposed) break;
                if (!ControlsEnabled) { BrightnessEventStatus = "Fn+Space erkannt; RGB-Steuerung nicht bereit."; break; }
                var level = _pendingBrightness.Value;
                _pendingBrightness = null;
                if (level == Brightness)
                {
                    BrightnessEventStatus = "Fn+Space-Ereignis empfangen; Helligkeit bereits aktuell.";
                    continue; // Avoid feedback writes and repeated disk saves for identical reports.
                }
                await ChangeAsync(s => s.WithBrightness(level));
                BrightnessEventStatus = "Fn+Space-Ereignis verarbeitet; RGB-Ergebnis siehe Status oben.";
            }
        }
        catch (Exception error) { BrightnessEventStatus = "Fn+Space-Übernahme fehlgeschlagen: " + error.Message; }
        finally { _pendingBrightness = null; _drainingBrightness = false; }
    }

    /// <summary>
    /// A well-known weak spot in RGB keyboard software: the USB HID lighting controller
    /// often resets to its own power-on default after the laptop sleeps and wakes, silently
    /// discarding whatever the user had set, and most tools never notice because they only
    /// ever write on user action. Reapplying proactively after resume is the fix; the delay
    /// gives the USB device a moment to re-enumerate before the first write after wake,
    /// which otherwise reliably fails on this hardware.
    /// </summary>
    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs eventArgs)
    {
        if (eventArgs.Mode != Microsoft.Win32.PowerModes.Resume) return;
        // SystemEvents raises this on its own dedicated thread, not the UI dispatcher.
        _dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_closing && !_disposed) _ = ReapplyAfterResumeAsync();
        }));
    }

    internal async Task ReapplyAfterResumeAsync()
    {
        if (_closing || _disposed || !_initialized) return;
        await _resumeReapplyDelay(TimeSpan.FromSeconds(2));
        if (_closing || _disposed) return;
        await ReapplyAsync();
    }

    /// <summary>
    /// Starts the preview clock only when it can be seen and there is something moving: the
    /// Tastatur section on screen, the window visible, an effect actually running. A static
    /// (manual) selection needs no timer at all - it is painted once.
    /// </summary>
    private void UpdatePreviewTimer()
    {
        bool wanted = !_closing && !_disposed && _visible && PowerOn && _activeEffect is not null;
        if (wanted && !_previewTimer.IsEnabled) _previewTimer.Start();
        else if (!wanted && _previewTimer.IsEnabled) _previewTimer.Stop();
        RenderPreviewFrame();
    }

    private void RenderPreviewFrame()
    {
        PreviewOpacity = Brightness switch
        {
            KeyboardBrightnessLevel.Off => 0.10,
            KeyboardBrightnessLevel.Low => 0.45,
            KeyboardBrightnessLevel.Medium => 0.72,
            _ => 1.0
        };

        if (!PowerOn) { SetPreviewZones(new(0, 0, 0), new(0, 0, 0), new(0, 0, 0)); return; }
        if (_activeEffect is not { } effect) { SetPreviewZones(_zone1, _zone2, _zone3); return; }

        // Same call the renderer makes, with the same time scale - the clock started when
        // this effect did, so the phase tracks the device rather than drifting on its own.
        double elapsed = _effectClock.Elapsed.TotalSeconds * Speed.ToTimeScale();
        KeyboardRgbColor[] frame = KeyboardEffectFrames.Create(effect, elapsed, _zone1);
        SetPreviewZones(frame[0], frame[1], frame[2]);
    }

    private void SetPreviewZones(KeyboardRgbColor zone1, KeyboardRgbColor zone2, KeyboardRgbColor zone3)
    {
        PreviewZone1Brush = CreateBrush(zone1);
        PreviewZone2Brush = CreateBrush(zone2);
        PreviewZone3Brush = CreateBrush(zone3);
    }

    private async void OnRgbTimerTick(object? sender, EventArgs args)
    {
        if (_closing || _disposed || _busy) return;
        _busy = true;
        try { await _session.CheckEffectAsync(); }
        catch (Exception exception)
        {
            EffectRunning = false;
            _rgbTimer.Stop();
            Status = $"Effekt beendet: {exception.Message}. Effekt erneut anwenden oder Manuell wählen.";
        }
        finally { _busy = false; }
    }

    /// <summary>Pushes a device state into every property that describes it. One place, so a
    /// readback can never update half the UI.</summary>
    private void Show(KeyboardLightingSettings state)
    {
        _zone1 = state.Left;
        _zone2 = state.Center;
        _zone3 = state.Right;
        PowerOn = state.Enabled;
        Brightness = state.Enabled ? state.OnBrightness : KeyboardBrightnessLevel.Off;
        Speed = state.Speed;
        EffectRunning = state.Enabled && state.Effect is not null;
        ModeIsEffect = state.Effect is not null;

        // Restart the preview clock only when the running effect actually changed, so a
        // brightness or colour tweak does not visibly jump the animation's phase.
        KeyboardRgbEffect? running = state.Enabled ? state.Effect : null;
        if (running != _activeEffect || (running is not null && !_effectClock.IsRunning)) _effectClock.Restart();
        if (running is null) _effectClock.Reset();
        ActiveEffect = running;
        Status = $"{(state.Enabled ? "Ein" : "Aus · Auswahl gespeichert")} · " +
            (state.Effect is { } effect ? GetEffectName(effect) : "Manuelle Zonenfarben");
        PaletteHint = KeyboardEffectFrames.ColorUsage(running) switch
        {
            KeyboardEffectColorUsage.AllZones => "Alle drei gespeicherten Farben werden direkt angezeigt.",
            KeyboardEffectColorUsage.BaseColorOnly =>
                "Dieser Effekt moduliert die Farbe von Zone 1 über alle drei Zonen. Zone 2 und 3 bleiben gespeichert und gelten wieder im manuellen Modus.",
            _ => "Dieser Effekt bringt seine eigene Palette mit; keine der gespeicherten Farben wird gelesen. Sie bleiben erhalten und gelten wieder im manuellen Modus."
        };
        if (EffectRunning && !_closing) _rgbTimer.Start(); else _rgbTimer.Stop();
        foreach (string property in Derived) OnPropertyChanged(property);
        UpdatePreviewTimer();
    }

    /// <summary>Everything computed from the state above. Listed once instead of a wall of
    /// OnPropertyChanged calls, so a new derived property costs one line, not two.</summary>
    private static readonly string[] Derived =
    [
        nameof(PowerButtonText), nameof(BrightnessIndex), nameof(BrightnessLabel), nameof(SpeedIndex),
        nameof(SpeedLabel), nameof(Zone1Hex), nameof(Zone2Hex), nameof(Zone3Hex), nameof(Zone1Brush),
        nameof(Zone2Brush), nameof(Zone3Brush), nameof(PreviewCaption), nameof(ColorUsage),
        nameof(Zone1AffectsLighting), nameof(Zone2AffectsLighting), nameof(Zone3AffectsLighting),
        nameof(Zone1Label), nameof(InactiveZoneNote), nameof(HasInactiveZones)
    ];

    private static MediaBrush CreateBrush(KeyboardRgbColor color)
    {
        var brush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(color.Red, color.Green, color.Blue));
        brush.Freeze();
        return brush;
    }
}
