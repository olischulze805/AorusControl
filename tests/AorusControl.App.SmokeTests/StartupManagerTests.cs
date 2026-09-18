using System.Reflection;
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

        ValidateAgainstScheduler(xml);
        Console.WriteLine("PASS: startup task definition parses in the real Task Scheduler (battery conditions, no time limit, restart on failure, normal priority, version marker)");
    }

    /// <summary>
    /// Hands the definition to the Task Scheduler itself and lets it parse.
    ///
    /// The element order in this file is not a matter of taste - the scheduler validates
    /// against a schema, and a document in the wrong order is rejected outright. Checking
    /// that with string searches only proves the text is there, not that Windows will take
    /// it, and the way that failure shows up otherwise is an autostart that silently never
    /// gets written.
    ///
    /// Nothing is registered: <c>NewTask</c> plus the <c>XmlText</c> setter parses and
    /// populates a definition object in memory. No task, no elevation, no side effect.
    /// </summary>
    private static void ValidateAgainstScheduler(string xml)
    {
        Type? service = Type.GetTypeFromProgID("Schedule.Service");
        if (service is null) return;
        object? scheduler = Activator.CreateInstance(service);
        if (scheduler is null) return;
        try
        {
            service.InvokeMember("Connect", BindingFlags.InvokeMethod, null, scheduler, [Type.Missing, Type.Missing, Type.Missing, Type.Missing]);
            object definition = service.InvokeMember("NewTask", BindingFlags.InvokeMethod, null, scheduler, [0u])!;
            // Throws when the schema refuses the document, which is the whole point.
            definition.GetType().InvokeMember("XmlText", BindingFlags.SetProperty, null, definition, [xml]);

            object settings = definition.GetType().InvokeMember("Settings", BindingFlags.GetProperty, null, definition, null)!;
            object Read(string name) => settings.GetType().InvokeMember(name, BindingFlags.GetProperty, null, settings, null)!;

            Check((int)Read("RestartCount") == 3, "a crashed tray app is brought back, three times");
            Check((string)Read("RestartInterval") == "PT1M", "a minute apart, not instantly in a loop");
            Check((int)Read("Priority") == 5, "normal priority, so the start it is meant to make prompt is not slowed");
            Check((string)Read("ExecutionTimeLimit") == "PT0S", "no execution limit; Windows kills a long-running tray app otherwise");
            Check(!(bool)Read("DisallowStartIfOnBatteries"), "it starts on battery - that is when the fans matter most");
            Check(!(bool)Read("StopIfGoingOnBatteries"), "and unplugging the mains does not kill it");
        }
        finally
        {
            if (OperatingSystem.IsWindows()) System.Runtime.InteropServices.Marshal.ReleaseComObject(scheduler);
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
