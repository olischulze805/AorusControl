using AorusControl.Core.Features.Startup;

internal static class StartupManagerTests
{
    public static void Run()
    {
        Check(StartupManager.QueryArguments("AorusControl").SequenceEqual(["/Query", "/TN", "AorusControl"]),
            "query looks up the exact task name, nothing else");

        string[] create = StartupManager.CreateArguments("AorusControl", @"C:\Program Files\Aorus Control\AorusControl.exe");
        Check(create.Contains("/SC") && create[Array.IndexOf(create, "/SC") + 1] == "ONLOGON",
            "must trigger at logon, not on a schedule that could run while logged out");
        Check(create.Contains("/RL") && create[Array.IndexOf(create, "/RL") + 1] == "HIGHEST",
            "must run elevated so the app never needs a UAC prompt at every login");
        Check(create.Contains("/F"), "must overwrite a stale existing task instead of failing");
        Check(create.Any(a => a.Contains(@"C:\Program Files\Aorus Control\AorusControl.exe")),
            "must point the task at the exact configured executable path");
        Check(create.Any(a => a.Contains(StartupManager.BackgroundStartArgument)),
            "must start into the tray at logon; a window in your face every login is why people disable autostart");
        Check(create.Any(a => a.Contains(@"""C:\Program Files\Aorus Control\AorusControl.exe"" --background")),
            "the quoted path must stay quoted with the argument outside it, or a path with spaces breaks the task");

        Check(StartupManager.DeleteArguments("AorusControl").SequenceEqual(["/Delete", "/TN", "AorusControl", "/F"]),
            "delete is unconditional (/F), so disabling never blocks on a confirmation prompt");

        Console.WriteLine("PASS: startup task command construction (logon trigger, elevated, exact path, force flags)");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
