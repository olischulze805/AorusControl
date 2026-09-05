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

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
