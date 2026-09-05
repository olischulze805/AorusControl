using AorusControl.Core.Models;
using AorusControl.Core.Services;

namespace AorusControl.Core.Features.Cooling;

public sealed record FanSafetyStatus(bool RequiresRestoration, Guid? Lease, string Message);

/// <summary>
/// UI-independent manual-fan lifetime. The owner must run RunAsync on a background worker.
/// This is not a process-crash watchdog: a separate process is still required for that.
/// No native WMI call can be assumed interruptible by a cancellation token.
/// </summary>
public sealed class FanSafetySupervisor(
    IAorusFanController fans,
    IAorusTelemetryReader telemetry,
    TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(10);
    private long _renewedAt;
    private Guid? _lease;
    private bool _requiresRestoration;
    private bool _stopping;
    private int _running;
    private string _message = "Keine manuelle Lüfterfreigabe";

    public async Task<Guid> AcquireFixedAsync(byte rawValue)
    {
        // Zero is allowed: the fans really do stop, and the supervision below - a temperature
        // check on acquisition and on every renewal - is what makes holding any low value safe,
        // not a floor on the value itself.
        if (rawValue > 229) throw new ArgumentOutOfRangeException(nameof(rawValue));
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_stopping) throw new InvalidOperationException("Lüfterüberwachung wird beendet.");
            if (Volatile.Read(ref _running) != 1) throw new InvalidOperationException("Lüfterüberwachung muss vor Fixed laufen.");
            if (_requiresRestoration) throw new InvalidOperationException("Bestehende Freigabe zuerst auf Normal zurückstellen.");
            DeviceCompatibility compatibility = await Task.Run(fans.CheckCompatibility).ConfigureAwait(false);
            if (!compatibility.IsSupported) throw new InvalidOperationException(compatibility.Message);
            await VerifyTelemetryAsync().ConfigureAwait(false);

            // Even a partial write followed by failed rollback requires recovery.
            _requiresRestoration = true;
            try
            {
                await fans.SetFixedAsync(rawValue).ConfigureAwait(false);
                _lease = Guid.NewGuid();
                _renewedAt = _clock.GetTimestamp();
                _message = $"Fixed {rawValue}; zeitlich begrenzte Freigabe aktiv";
                return _lease.Value;
            }
            catch (Exception writeError)
            {
                bool restored = await RestoreCoreAsync("Fixed-Übernahme fehlgeschlagen").ConfigureAwait(false);
                throw new InvalidOperationException(restored
                    ? "Fixed fehlgeschlagen; Normal wiederhergestellt."
                    : $"Fixed fehlgeschlagen; {_message}", writeError);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task RenewAsync(Guid lease)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_stopping || !_requiresRestoration || _lease != lease)
                throw new InvalidOperationException("Keine passende aktive Lüfterfreigabe.");
            if (LeaseExpired())
            {
                await RestoreCoreAsync("Freigabe abgelaufen").ConfigureAwait(false);
                throw new InvalidOperationException("Abgelaufene Freigabe kann nicht verlängert werden.");
            }
            try { await VerifyTelemetryAsync().ConfigureAwait(false); }
            catch
            {
                await RestoreCoreAsync("Keine sichere frische Temperaturmessung").ConfigureAwait(false);
                throw;
            }
            // Reading must not revive a lease that expired during a slow native call.
            if (LeaseExpired())
            {
                await RestoreCoreAsync("Freigabe während Messung abgelaufen").ConfigureAwait(false);
                throw new InvalidOperationException("Messung dauerte länger als die Freigabe.");
            }
            _renewedAt = _clock.GetTimestamp();
        }
        finally { _gate.Release(); }
    }

    public async Task TickAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_requiresRestoration) return; // Firmware mode: no polling overhead.
            if (_lease is null || LeaseExpired())
            {
                await RestoreCoreAsync("Freigabe fehlt oder ist abgelaufen").ConfigureAwait(false);
                return;
            }
            try { await VerifyTelemetryAsync().ConfigureAwait(false); }
            catch (Exception exception)
            {
                await RestoreCoreAsync(exception.Message).ConfigureAwait(false);
                return;
            }
            if (LeaseExpired()) await RestoreCoreAsync("Freigabe während Messung abgelaufen").ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task ReleaseAsync(Guid lease)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_lease != lease) throw new InvalidOperationException("Falsche Lüfterfreigabe.");
            if (!await RestoreCoreAsync("Manuelle Steuerung beendet").ConfigureAwait(false))
                throw new InvalidOperationException(_message);
        }
        finally { _gate.Release(); }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _stopping = true;
            if (_requiresRestoration && !await RestoreCoreAsync("Überwachung wird beendet").ConfigureAwait(false))
                throw new InvalidOperationException(_message);
        }
        finally { _gate.Release(); }
    }

    public async Task<FanSafetyStatus> ReadStatusAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { return new(_requiresRestoration, _lease, _message); }
        finally { _gate.Release(); }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            throw new InvalidOperationException("Lüfterüberwachung läuft bereits.");
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), _clock);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                await TickAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally
        {
            try { await StopAsync().ConfigureAwait(false); }
            finally { Volatile.Write(ref _running, 0); }
        }
    }

    private bool LeaseExpired() => _clock.GetElapsedTime(_renewedAt) >= LeaseDuration;

    private async Task VerifyTelemetryAsync()
    {
        TelemetrySnapshot sample = await telemetry.ReadAsync().ConfigureAwait(false);
        FixedFanSafety.Validate(sample, _clock.GetUtcNow());
    }

    private async Task<bool> RestoreCoreAsync(string reason)
    {
        _lease = null; // No caller may renew a failed/expired authorization.
        try
        {
            await fans.SetNormalAsync().ConfigureAwait(false);
            _requiresRestoration = false;
            _message = $"Normal bestätigt: {reason}";
            return true;
        }
        catch (Exception exception)
        {
            _requiresRestoration = true;
            _message = $"ACHTUNG: Rückstellung ausstehend ({reason}): {exception.Message}";
            return false;
        }
    }
}
