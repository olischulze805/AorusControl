using System.Diagnostics;
using AorusControl.Core.Features.GpuPreferences;

internal static class ProgramCloserTests
{
    /// <summary>
    /// The one place in this suite that ends a real process - its own. A console command with
    /// no window is exactly the case the graceful path cannot serve: there is nothing to ask,
    /// so the grace period runs out and it is terminated. That escalation is the part worth
    /// proving, because the alternative is a button that silently does nothing to precisely
    /// the background programs that hold the card awake.
    /// </summary>
    public static async Task RunAsync()
    {
        var closer = new ProgramCloser(grace: TimeSpan.FromMilliseconds(300));

        Check((await closer.CloseAsync([])) == new CloseOutcome(0, 0, 0), "nothing asked, nothing done");

        // A process id that is long gone counts as closed: the caller wanted it not running.
        Check((await closer.CloseAsync([int.MaxValue])).Closed == 1, "an already-dead process is not a failure");

        using Process sleeper = Start();
        try
        {
            CloseOutcome outcome = await closer.CloseAsync([sleeper.Id]);
            Check(outcome.Closed == 1, $"the process is gone, got {outcome}");
            Check(outcome.Forced == 1, "and it took forcing, because a console command has no window to ask");
            Check(outcome.Refused == 0, "nothing was refused");
            Check(sleeper.HasExited, "confirmed by the process object itself, not only by the result");
        }
        finally
        {
            try { if (!sleeper.HasExited) sleeper.Kill(entireProcessTree: true); } catch { }
        }

        Console.WriteLine("PASS: closing asks first, escalates for a windowless process, and treats a vanished one as done");
    }

    private static Process Start() => Process.Start(new ProcessStartInfo
    {
        // ping, not timeout: timeout refuses to run without a real console and would exit
        // at once, which would quietly turn this into a test of nothing.
        FileName = "ping.exe",
        Arguments = "-n 30 127.0.0.1",
        CreateNoWindow = true,
        UseShellExecute = false
    })!;

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
