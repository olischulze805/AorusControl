using AorusControl.App.Features.Updates;

/// <summary>
/// The restart-for-update handshake. Worth a test because the mistake it guards against is
/// silent: the button used to call Velopack's "wait for exit, then update" and never exit, so
/// nothing happened at all and the app looked like it had ignored the click.
/// </summary>
internal static class UpdateRestartTests
{
    public static void Run()
    {
        // Not running from an installation, which is what a build tree always is: the module
        // must come up saying so rather than throwing.
        var updates = new UpdateViewModel();
        Check(!updates.IsSupported, "a build tree is not an installation and must report that");
        Check(!updates.CanCheck && !updates.CanInstall, "nothing is offered when updates cannot apply");

        // The module never restarts anything itself - it asks, because the fans and the
        // lighting have to be handed back first, and that is the window's close sequence.
        int asked = 0;
        updates.RestartRequested += (_, _) => asked++;
        updates.RestartCommand.Execute(null);
        Check(asked == 0, "asking to restart with nothing downloaded must do nothing");

        // And applying on exit is equally a no-op without a downloaded release, rather than
        // leaving an updater waiting for an exit that means nothing.
        updates.ApplyDownloadedUpdateOnExit();

        Console.WriteLine("PASS: update restart is requested, not performed, and does nothing without a download");
    }

    public static async Task RunStartupCheckAsync()
    {
        // The automatic check must stay quiet. On a build tree there is nothing to check at
        // all; on a real installation a failed check goes to the log and nowhere else,
        // because an app that greets every launch with "update check failed" from a café
        // network teaches the user to ignore the one time it matters.
        var immediately = new TaskCompletionSource();
        immediately.SetResult();
        var updates = new UpdateViewModel(wait: (_, _) => immediately.Task);
        string before = updates.Status;

        int announced = 0;
        updates.UpdateFound += (_, _) => announced++;
        await updates.CheckOnStartupAsync();

        Check(announced == 0, "nothing may be announced when no update was found");
        Check(updates.Status == before, "an automatic check must not overwrite the status with noise");

        // Cancelling is safe at any point, including before anything was ever started.
        updates.CancelStartupCheck();
        await updates.CheckOnStartupAsync();
        Check(announced == 0 && updates.Status == before, "a cancelled check leaves everything as it was");

        // What this cannot cover: the network path itself. UpdateManager refuses to construct
        // outside an installed copy, so on a build tree there is nothing to check against -
        // which is exactly the case asserted above, and the reason the rest is verified by
        // installing a real build rather than pretended at here.
        Console.WriteLine("PASS: the automatic update check stays silent on a build tree and cancels cleanly");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
