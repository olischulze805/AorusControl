using AorusControl.Core.Features.Startup;

internal static class StartupManagerTests
{
    private const string Executable = @"C:\Program Files\Aorus Control\AorusControl.exe";

    public static void Run()
    {
        Check(StartupManager.QueryArguments("AorusControl").SequenceEqual(["/Query", "/TN", "AorusControl"]),
            "query looks up the exact task name, nothing else");

        string[] create = StartupManager.CreateArguments("AorusControl", @"C:\Temp\task.xml");
        Check(create.SequenceEqual(["/Create", "/TN", "AorusControl", "/XML", @"C:\Temp\task.xml", "/F"]),
            "the task is registered from our own definition file, not from schtasks' switches");

        Check(StartupManager.DeleteArguments("AorusControl").SequenceEqual(["/Delete", "/TN", "AorusControl", "/F"]),
            "delete is unconditional (/F), so disabling never blocks on a confirmation prompt");

        // The four settings that schtasks' own defaults got wrong, measured on the machine on
        // 2026-09-15. Each of these is a way for the autostart to fail silently, and the last
        // two are why it used to start late or not at all.
        string xml = StartupTaskDefinition.Build(Executable, "AORUS5\\olive");
        Check(xml.Contains("<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>"),
            "must start on battery - that is exactly when fan control matters most");
        Check(xml.Contains("<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>"),
            "unplugging the mains must not make Windows kill the running app");
        Check(xml.Contains("<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>"),
            "no execution limit: the default three days would terminate a tray app that did nothing wrong");
        Check(xml.Contains("<Priority>5</Priority>"),
            "normal priority; the default of 7 is below normal and slows the very startup this exists to make prompt");

        Check(xml.Contains("<LogonTrigger>"), "must trigger at logon, not on a schedule that could run while logged out");
        Check(xml.Contains("<RunLevel>HighestAvailable</RunLevel>"),
            "must run elevated so the app never needs a UAC prompt at every login");
        Check(xml.Contains($"<Command>{Executable}</Command>"),
            "must point the task at the exact configured executable path");
        Check(xml.Contains($"<Arguments>{StartupTaskDefinition.BackgroundStartArgument}</Arguments>"),
            "must start into the tray at logon; a window in your face every login is why people disable autostart");
        Check(xml.Contains($"<Description>{StartupTaskDefinition.Version}</Description>"),
            "carries the version marker, or a later build cannot tell its own task from an older one");
        Check(xml.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-16\"?>", StringComparison.Ordinal),
            "schtasks reads the definition as UTF-16 and rejects anything else as malformed");

        // An account name is user input as far as XML is concerned.
        Check(StartupTaskDefinition.Build(Executable, "DOM\\a<b&c").Contains("a&lt;b&amp;c"),
            "the account name is escaped, so an odd character cannot break the document");

        Check(!StartupTaskDefinition.Matches(null, Executable), "a task that cannot be exported is not a match");
        Check(!StartupTaskDefinition.Matches("<Task><Description>Something else</Description></Task>", Executable),
            "a task written by another tool is not ours to trust");
        Check(!StartupTaskDefinition.Matches(
                $"<Task><Description>{StartupTaskDefinition.Version}</Description><Command>C:\\Old\\AorusControl.exe</Command></Task>",
                Executable),
            "our own marker with a stale path still needs replacing - an update moves the program");
        Check(StartupTaskDefinition.Matches(xml, Executable), "what we just wrote must count as current");
        Check(StartupTaskDefinition.Matches(xml.Replace("AORUS5", "AORUS\u00dc"), Executable),
            "an account the console code page mangles must not force a rewrite on every start");

        Console.WriteLine("PASS: startup task definition (battery conditions, no time limit, normal priority, version marker)");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
