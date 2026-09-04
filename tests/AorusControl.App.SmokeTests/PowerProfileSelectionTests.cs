using AorusControl.Core.Features.PowerProfiles;

internal static class PowerProfileSelectionTests
{
    public static void Run()
    {
        var clock = new TestClock();
        var selector = new PowerSourceSelection(clock);
        var assignments = new PowerProfileAssignments(Guid.NewGuid(), Guid.NewGuid());
        Check(LaptopPowerSources.FromWindowsStatus(0) == LaptopPowerSource.Battery, "battery");
        Check(LaptopPowerSources.FromWindowsStatus(1) == LaptopPowerSource.Ac, "AC");
        Check(LaptopPowerSources.FromWindowsStatus(255) == LaptopPowerSource.Unknown, "unknown is not battery");
        Check(selector.Observe(LaptopPowerSource.Ac, assignments) is null, "no immediate startup switch");
        clock.Advance(1);
        Check(selector.Observe(LaptopPowerSource.Ac, assignments) is null, "wait for stability");
        clock.Advance(1);
        Check(selector.Observe(LaptopPowerSource.Ac, assignments) == assignments.AcProfile, "stable AC");
        Check(selector.Observe(LaptopPowerSource.Battery, assignments) is null, "unplug starts a new interval");
        clock.Advance(1);
        Check(selector.Observe(LaptopPowerSource.Ac, assignments) is null, "bounce resets interval");
        clock.Advance(2);
        Check(selector.Observe(LaptopPowerSource.Ac, assignments) == assignments.AcProfile, "stable after bounce");
        Check(selector.Observe(LaptopPowerSource.Unknown, assignments) is null, "unknown suppresses selection");
        clock.Advance(10);
        Check(selector.Observe(LaptopPowerSource.Battery, assignments) is null, "unknown time cannot establish stability");
        clock.Advance(2);
        Check(selector.Observe(LaptopPowerSource.Battery, assignments) == assignments.BatteryProfile, "battery profile");
        Check(selector.Observe(LaptopPowerSource.Battery, assignments with { BatteryProfile = null }) is null, "unassigned means no change");
        selector.Reset();
        Check(selector.Observe(LaptopPowerSource.Battery, assignments) is null, "resume needs fresh observations");
        clock.Advance(6);
        Check(selector.Observe(LaptopPowerSource.Battery, assignments) is null, "long observation gap resets stability");
        clock.Advance(2);
        Check(selector.Observe(LaptopPowerSource.Battery, assignments) == assignments.BatteryProfile, "new stable observations after gap");
        Console.WriteLine("PASS: AC/battery profile selection, unknown source, debounce, unassigned and resume reset");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class TestClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(int seconds) => _ticks += TimeSpan.FromSeconds(seconds).Ticks;
    }
}
