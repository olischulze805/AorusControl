using AorusControl.App.Infrastructure;

internal static class TrayStatusTests
{
    public static void Run()
    {
        Check(TrayStatus.Build("Dynamic", 80) == "AORUS Control · Lüfter Dynamic · Limit 80 %",
            $"the two things worth knowing without opening the window, got: {TrayStatus.Build("Dynamic", 80)}");
        Check(TrayStatus.Build("Normal", null) == "AORUS Control · Lüfter Normal · Standardladen",
            "no limit is a state of its own, not a missing half");
        Check(TrayStatus.Build(null, 80) == "AORUS Control · Limit 80 %", "an unread profile is left out, not guessed");
        Check(TrayStatus.Build("  ", 80) == "AORUS Control · Limit 80 %", "and so is a blank one");

        // A device that never answered must not be quoted as charging normally.
        Check(TrayStatus.Build("Normal", null, batteryKnown: false) == "AORUS Control · Lüfter Normal",
            "an unknown battery says nothing rather than something wrong");
        Check(TrayStatus.Build(null, null, batteryKnown: false) == "AORUS Control", "the name alone is the floor");

        // Windows refuses a longer tooltip outright, which is a poor way to find out.
        string long_ = TrayStatus.Build(new string('x', 200), 80);
        Check(long_.Length <= TrayStatus.MaxLength, $"never longer than Windows accepts, got {long_.Length}");
        Check(long_ == "AORUS Control", "and it drops whole labels rather than cutting one in half");

        Console.WriteLine("PASS: tray tooltip states what is set, omits what is unknown, and fits Windows' buffer");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
