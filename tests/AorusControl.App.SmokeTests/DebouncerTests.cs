using AorusControl.App.Infrastructure;

internal static class DebouncerTests
{
    public static async Task RunAsync()
    {
        // A controllable wait: the test decides when the delay elapses, so nothing here
        // sleeps and nothing depends on timing luck.
        var gate = new TaskCompletionSource();
        TaskCompletionSource current = gate;
        Func<TimeSpan, CancellationToken, Task> wait = (_, token) =>
        {
            var completion = current;
            return completion.Task.WaitAsync(token);
        };

        int runs = 0;
        var debouncer = new Debouncer(TimeSpan.FromMilliseconds(500), () => { runs++; return Task.CompletedTask; }, wait);

        Check(runs == 0 && !debouncer.HasPendingChange, "nothing runs before anything is scheduled");

        // Several changes in quick succession must collapse into a single write - this is
        // what makes dragging a slider safe to apply automatically.
        debouncer.Schedule();
        Check(debouncer.HasPendingChange, "a scheduled change is pending");
        current = new TaskCompletionSource();
        debouncer.Schedule();
        current = new TaskCompletionSource();
        debouncer.Schedule();
        Check(runs == 0, "rescheduling must not run the action early");

        current.SetResult();
        await debouncer.Pending;
        Check(runs == 1, $"three rapid changes must produce exactly one write, got {runs}");
        Check(!debouncer.HasPendingChange, "nothing is pending once it has run");

        // A later change runs again.
        current = new TaskCompletionSource();
        debouncer.Schedule();
        current.SetResult();
        await debouncer.Pending;
        Check(runs == 2, "a later change writes again");

        // Cancel drops the pending change entirely.
        var neverCompletes = new TaskCompletionSource();
        current = neverCompletes;
        debouncer.Schedule();
        debouncer.Cancel();
        Check(!debouncer.HasPendingChange, "cancel clears the pending change");
        neverCompletes.SetResult();
        await debouncer.Pending;
        Check(runs == 2, "a cancelled change must never reach the device");

        // Flush applies immediately - the shutdown path, so a value set a moment before
        // closing is not silently lost.
        current = new TaskCompletionSource();
        debouncer.Schedule();
        await debouncer.FlushAsync();
        Check(runs == 3, $"flush writes the pending change without waiting, got {runs}");
        Check(!debouncer.HasPendingChange, "flush leaves nothing pending");

        // Flushing with nothing pending must not write.
        await debouncer.FlushAsync();
        Check(runs == 3, "flush with nothing pending must not write");

        Console.WriteLine("PASS: debouncer collapses rapid changes into one write, cancels, and flushes on demand");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
