namespace AorusControl.App.Infrastructure;

/// <summary>
/// Runs an action once the caller stops asking for it. Every <see cref="Schedule"/> call
/// restarts the wait, so dragging a slider across a dozen values writes to the hardware
/// once, at the value the user settled on - which is the whole point: it removes the
/// "apply" button without turning one gesture into a dozen device transactions.
///
/// Deliberately not built on a DispatcherTimer: the wait is injected, so tests drive it
/// directly instead of sleeping, and the class has no opinion about which thread it runs
/// on. <see cref="Pending"/> is what makes an otherwise fire-and-forget mechanism
/// observable - both for tests and for the shutdown path, which has to flush rather than
/// silently drop a change made a moment before closing.
/// </summary>
public sealed class Debouncer(
    TimeSpan delay,
    Func<Task> action,
    Func<TimeSpan, CancellationToken, Task>? wait = null)
{
    private readonly Func<TimeSpan, CancellationToken, Task> _wait = wait ?? Task.Delay;
    private readonly object _gate = new();
    private CancellationTokenSource? _pendingCancellation;

    /// <summary>The wait-and-run currently in flight, or a completed task. Awaiting it
    /// waits for the action itself, not just for the delay.</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>True while a change is waiting to be written.</summary>
    public bool HasPendingChange
    {
        get { lock (_gate) return _pendingCancellation is not null; }
    }

    public void Schedule()
    {
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            _pendingCancellation?.Cancel();
            _pendingCancellation?.Dispose();
            _pendingCancellation = cancellation = new CancellationTokenSource();
            Pending = RunAfterDelayAsync(cancellation, cancellation.Token);
        }
    }

    /// <summary>Runs a pending change now instead of waiting out the rest of the delay.
    /// Used when closing: a value the user just set must not be lost because the window
    /// happened to close half a second later.</summary>
    public async Task FlushAsync()
    {
        bool wasPending;
        lock (_gate)
        {
            wasPending = _pendingCancellation is not null;
            _pendingCancellation?.Cancel();
            _pendingCancellation?.Dispose();
            _pendingCancellation = null;
        }

        // Let the cancelled wait unwind before running the action, so it cannot run twice.
        try { await Pending.ConfigureAwait(false); } catch (OperationCanceledException) { }
        if (wasPending) await action().ConfigureAwait(false);
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _pendingCancellation?.Cancel();
            _pendingCancellation?.Dispose();
            _pendingCancellation = null;
        }
    }

    private async Task RunAfterDelayAsync(CancellationTokenSource owner, CancellationToken cancellationToken)
    {
        try
        {
            await _wait(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_gate)
        {
            // A newer Schedule() has taken over; this run is stale.
            if (!ReferenceEquals(_pendingCancellation, owner)) return;
            _pendingCancellation = null;
        }

        owner.Dispose();
        await action().ConfigureAwait(false);
    }
}
