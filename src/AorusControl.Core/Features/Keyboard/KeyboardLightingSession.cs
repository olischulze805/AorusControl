namespace AorusControl.Core.Features.Keyboard;

/// <summary>
/// Owns all transitions for one keyboard. The caller owns the transport lifetime.
/// Never use another writer concurrently. Initialization is read-only.
/// </summary>
public sealed class KeyboardLightingSession(IKeyboardLightingTransport transport) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private KeyboardLightingSettings? _settings;
    private Task? _effectTask;
    private CancellationTokenSource? _effectCancellation;
    private bool _disposed;

    public async Task<KeyboardLightingSettings> ReadSettingsAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _settings ??= KeyboardLightingSettings.FromHardware(await Task.Run(transport.ReadState).ConfigureAwait(false));
        }
        finally { _gate.Release(); }
    }

    public Task<KeyboardLightingSettings> ReapplyAsync() => ChangeAsync(s => s, forceWrite: true);

    public async Task<KeyboardLightingSettings> ChangeAsync(Func<KeyboardLightingSettings, KeyboardLightingSettings> change, bool forceWrite = false)
    {
        ArgumentNullException.ThrowIfNull(change);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            KeyboardLightingSettings current = _settings ??= KeyboardLightingSettings.FromHardware(
                await Task.Run(transport.ReadState).ConfigureAwait(false));
            KeyboardLightingSettings desired = change(current);
            desired.Validate();
            bool needsEffect = desired.Enabled && desired.Effect is not null;
            if (!forceWrite && desired == current && (!needsEffect || _effectTask is { IsCompleted: false })) return current;

            bool onlyBrightness = (desired with { Enabled = current.Enabled, OnBrightness = current.OnBrightness }) == current;
            if (!forceWrite && onlyBrightness && _effectTask is { IsCompleted: false } && transport is ILiveEffectBrightness live)
            {
                live.UpdateEffectBrightness(desired.Enabled ? desired.OnBrightness : Models.KeyboardBrightnessLevel.Off);
                if (desired.Enabled)
                {
                    // Keep animation phase; renderer errors are surfaced by CheckEffectAsync.
                    _settings = desired;
                    return desired;
                }
                // For Off, stop below. Its restoration now uses Off, never the old brightness.
            }

            // The legacy renderer restores its start snapshot. Wait for that restoration
            // before applying new intent, so an old worker cannot overwrite a new choice.
            await StopEffectAsync().ConfigureAwait(false);
            await Task.Run(() => transport.ApplyState(desired.ToHardwareState())).ConfigureAwait(false);
            _settings = desired;
            if (desired.Enabled && desired.Effect is { } effect)
            {
                var cancellation = new CancellationTokenSource();
                try
                {
                    _effectTask = transport.PlayEffectAsync(effect, desired.Speed, cancellation.Token);
                    _effectCancellation = cancellation;
                }
                catch
                {
                    cancellation.Dispose();
                    throw;
                }
            }
            return desired;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Allows the UI to surface asynchronous renderer failure without hiding it.</summary>
    public async Task CheckEffectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_effectTask?.IsCompleted == true) await StopEffectAsync().ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task StopEffectAsync()
    {
        Task? task = _effectTask;
        CancellationTokenSource? cancellation = _effectCancellation;
        _effectTask = null;
        _effectCancellation = null;
        try
        {
            cancellation?.Cancel();
            if (task is not null) await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true)
        {
            // A transport may complete cancellation by returning a canceled task.
        }
        finally { cancellation?.Dispose(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            // Same reasoning as SuspendAsync: nothing writes after this, so putting the old
            // state back would be the app's parting act rather than the user's last choice.
            if (transport is IEffectHandover handover) handover.KeepLightingOnStop();
            await StopEffectAsync().ConfigureAwait(false);
            _disposed = true;
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// Lets go of the keyboard without changing what is on it.
    ///
    /// This runs when the app closes, and it is the one stop that is not followed by a write.
    /// The renderer is therefore told to leave its last frame standing: the colours and the
    /// mode stay as the user left them, which is what somebody who set a colour scheme
    /// expects of a program that is merely quitting.
    /// </summary>
    public async Task SuspendAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (transport is IEffectHandover handover) handover.KeepLightingOnStop();
            await StopEffectAsync().ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}
