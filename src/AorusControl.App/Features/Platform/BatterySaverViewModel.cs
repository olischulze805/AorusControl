using AorusControl.App.Infrastructure;
using AorusControl.App.Localization;
using AorusControl.Core.Features.Diagnostics;
using AorusControl.Core.Features.PowerProfiles;

namespace AorusControl.App.Features.Platform;

/// <summary>
/// The battery saver card.
///
/// Only the battery side of a power scheme is written, which has a pleasant consequence: with
/// the saver on and the mains plugged in, the machine behaves exactly as it did before. So
/// there is no automatic to build and no "only on battery" condition to explain - it is a
/// switch you turn on once and leave on, and it bites only where it was meant to.
///
/// What it is worth is written next to it rather than implied. On this machine the screen is
/// the lever that pays at idle and the processor cap is not, and a card that said "saves
/// power" without that distinction would be the same kind of claim this project exists to
/// avoid. See research/ULTRA-BATTERY-SAVER.md.
/// </summary>
public sealed class BatterySaverViewModel : ObservableObject, IFeatureModule
{
    private readonly IBatterySaver _saver;
    private readonly IBatterySaverSettingsStore _store;
    private readonly Debouncer _apply;
    private BatterySaverSettings _settings;
    private bool _busy, _disposed, _applyingStored;
    private ApplyState _applyState = ApplyState.Idle;
    private string _status = string.Empty;

    public BatterySaverViewModel(
        IBatterySaver? saver = null,
        IBatterySaverSettingsStore? store = null,
        Func<TimeSpan, CancellationToken, Task>? wait = null)
    {
        _saver = saver ?? new BatterySaver();
        _store = store ?? new BatterySaverSettingsStore(AppData.File("battery-saver-v1.json"));
        _settings = _store.Load();
        // Dragging a slider while the saver is on rewrites the scheme; the wait turns a drag
        // across the range into one rewrite rather than forty.
        _apply = new Debouncer(TimeSpan.FromMilliseconds(700), ReapplyAsync, wait);
    }

    public bool IsBusy => _busy;

    /// <summary>The two values, as the sliders hold them.</summary>
    public double ProcessorCap
    {
        get => _settings.ProcessorCap;
        set => Change(_settings with { ProcessorCap = Round(value, BatterySaverPlan.LowestProcessorCap) });
    }

    public double Brightness
    {
        get => _settings.Brightness;
        set => Change(_settings with { Brightness = Round(value, BatterySaverPlan.LowestBrightness) });
    }

    public string ProcessorCapText => Strings.Current.Format("Saver_Percent", _settings.ProcessorCap);
    public string BrightnessText => Strings.Current.Format("Saver_Percent", _settings.Brightness);

    public bool IsOn
    {
        get => _settings.Enabled;
        set
        {
            if (value == _settings.Enabled || _applyingStored) return;
            if (_busy || _disposed)
            {
                OnPropertyChanged();
                return;
            }
            _settings = _settings with { Enabled = value };
            Apply = ApplyState.Applying;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanAdjust));
            _ = SwitchAsync(value);
        }
    }

    /// <summary>The sliders stay usable while it is off: the values are what the saver will
    /// apply, not a setting with nothing to act on.</summary>
    public bool CanAdjust => !_disposed && !_busy;
    public bool CanSwitch => !_disposed && !_busy;

    /// <summary>Where the pending change stands, for the mark beside the switch. The switch
    /// already says on or off; a line of text repeating it was saying nothing twice.</summary>
    public ApplyState Apply { get => _applyState; private set => SetProperty(ref _applyState, value); }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>What the two levers are worth here, from this project's own measurements.</summary>
    public string Savings => Strings.Current.Format("Saver_Savings", _settings.Brightness, _settings.ProcessorCap);

    public async Task StartAsync()
    {
        if (_settings.LeftoverScheme is { } leftover)
        {
            if (_settings.PreviousScheme is { } previous && _saver.Resume(leftover, previous))
            {
                if (_settings.Enabled)
                {
                    return;
                }

                await SwitchAsync(false);
                return;
            }

            await Task.Run(() => _saver.DiscardLeftover(leftover));
            Remember(_settings with { LeftoverScheme = null, PreviousScheme = null });
        }
        if (_settings.Enabled) await SwitchAsync(true);
    }

    private async Task SwitchAsync(bool on)
    {
        if (_busy || _disposed) return;
        _busy = true;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanAdjust));
        OnPropertyChanged(nameof(CanSwitch));
        try
        {
            if (on)
            {
                IReadOnlyList<SaverLever> levers = await Task.Run(() =>
                    _saver.TurnOn(_settings.ProcessorCap, _settings.Brightness));
                Remember(_settings with
                {
                    Enabled = true,
                    LeftoverScheme = _saver.Scheme,
                    PreviousScheme = _saver.PreviousScheme
                });
                Status = string.Empty;
                Apply = ApplyState.Confirmed;
            }
            else
            {
                await Task.Run(_saver.TurnOff);
                Remember(_settings with { Enabled = false, LeftoverScheme = null, PreviousScheme = null });
                Status = string.Empty;
                Apply = ApplyState.Confirmed;
            }
        }
        catch (Exception exception)
        {
            AppLog.Error("saver", "Akkusparmodus konnte nicht umgeschaltet werden.", exception);
            Status = Strings.Current.Format("Saver_Failed", exception.Message);
            Apply = ApplyState.Failed;
            // The switch has to show what the machine is doing, not what was asked for.
            _applyingStored = true;
            try
            {
                _settings = _settings with { Enabled = _saver.IsOn };
                OnPropertyChanged(nameof(IsOn));
            }
            finally { _applyingStored = false; }
        }
        finally
        {
            _busy = false;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsOn));
            OnPropertyChanged(nameof(CanAdjust));
            OnPropertyChanged(nameof(CanSwitch));
        }
    }

    /// <summary>A slider moved while the saver is on: the scheme is rewritten with the new
    /// values. Off, there is nothing to rewrite and the value is simply remembered.</summary>
    private Task ReapplyAsync()
    {
        if (_disposed || !_settings.Enabled) return Task.CompletedTask;
        if (_busy) { _apply.Schedule(); return Task.CompletedTask; }
        return SwitchAsync(true);
    }

    private void Change(BatterySaverSettings next)
    {
        if (next == _settings) return;
        _settings = next;
        OnPropertyChanged(nameof(ProcessorCap));
        OnPropertyChanged(nameof(Brightness));
        OnPropertyChanged(nameof(ProcessorCapText));
        OnPropertyChanged(nameof(BrightnessText));
        OnPropertyChanged(nameof(Savings));
        Remember(next);
        // Only a change that will actually reach the device is worth announcing as pending.
        if (_settings.Enabled) Apply = ApplyState.Applying;
        _apply.Schedule();
    }

    private void Remember(BatterySaverSettings settings)
    {
        _settings = settings;
        try { _store.Save(settings); }
        catch (Exception exception) { AppLog.Error("saver", "Einstellungen nicht gespeichert.", exception); }
    }

    private static uint Round(double value, uint lowest) =>
        BatterySaverPlan.Clamp((uint)Math.Round(value), lowest);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Deliberately left running. The saver is a setting, not a lease: closing the window
        // should not quietly put the machine back on a thirstier plan, and the scheme is
        // reinstated at the next start from the same stored choice.
    }
}
