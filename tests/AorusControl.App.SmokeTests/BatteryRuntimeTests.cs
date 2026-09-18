using System.Diagnostics;
using AorusControl.Core.Features.PowerMonitoring;

internal static class BatteryRuntimeTests
{
    public static void Run()
    {
        // 79,2 Wh left at the 24,4 W this machine was measured drawing at idle on 2026-09-15.
        BatteryFlow discharging = new(BatteryFlowDirection.Discharging, 24.4, 80, 79.2, 99.0);
        BatteryRuntime left = BatteryRuntimeMath.From(discharging, 24.4, 80)
            ?? throw new InvalidOperationException("a discharging pack with a rate must produce an estimate");
        Check(Math.Abs(left.Time.TotalHours - 3.246) < 0.01, $"79,2 Wh at 24,4 W is 3 h 15 min, got {left.Text}");
        Check(!left.UntilCharged, "a discharging pack is not counting towards a charge");
        Check(left.Text == "3 h 15 min", $"hours and minutes, no seconds - got {left.Text}");

        // The charge limit, not the full pack, is what charging runs to. Without it the
        // estimate would keep counting past the point where the machine stops taking power.
        BatteryFlow charging = new(BatteryFlowDirection.Charging, 40.0, 50, 49.5, 99.0);
        BatteryRuntime toLimit = BatteryRuntimeMath.From(charging, 40.0, 80)
            ?? throw new InvalidOperationException("a charging pack below its limit must produce an estimate");
        Check(Math.Abs(toLimit.Time.TotalHours - 0.7425) < 0.01, $"49,5 to 79,2 Wh at 40 W is 45 min, got {toLimit.Text}");
        Check(toLimit.UntilCharged, "a charging pack counts towards the limit");
        Check(BatteryRuntimeMath.From(charging, 40.0, null)!.Time > toLimit.Time,
            "without a limit the estimate runs to a full pack, which takes longer");

        // At or above the limit there is nothing left to charge, so there is no time to give.
        BatteryFlow atLimit = charging with { RemainingWattHours = 79.2 };
        Check(BatteryRuntimeMath.From(atLimit, 40.0, 80) is null, "a pack already at its limit has no charging time");

        Check(BatteryRuntimeMath.From(discharging, 0, 80) is null, "a rate of zero is a gap, not an eternity");
        Check(BatteryRuntimeMath.From(discharging, double.NaN, 80) is null, "NaN produces no estimate");
        Check(BatteryRuntimeMath.From(discharging with { Direction = BatteryFlowDirection.Resting }, 24.4, 80) is null,
            "a resting battery on mains has no remaining time");
        Check(BatteryRuntimeMath.From(discharging, 0.5, 80) is null,
            "158 hours is arithmetic, not information - beyond a day there is no estimate");
        Check(BatteryRuntimeMath.From(BatteryFlow.Unknown, 24.4, 80) is null, "an unknown reading produces nothing");

        Check(new BatteryRuntime(TimeSpan.FromMinutes(45), false).Text == "45 min", "under an hour, minutes alone");
        Check(new BatteryRuntime(TimeSpan.FromMinutes(119.7), false).Text == "2 h 0 min",
            "rounding happens once, so 119,7 minutes never reads as 1 h 60 min");
        Check(new BatteryRuntime(TimeSpan.FromSeconds(20), false).Text == "1 min", "never zero minutes");

        RunAverage();
        Console.WriteLine("PASS: battery runtime from watt hours and rate, charge limit, refusals, moving average");
    }

    private static void RunAverage()
    {
        long now = 0;
        // Two seconds per sample, five seconds of window: the fourth sample pushes the first
        // one past the edge, and nothing lands exactly on it where the boundary would decide.
        var average = new MovingAverage(TimeSpan.FromSeconds(5), () => now += 2 * Stopwatch.Frequency);

        Check(average.Add(10) == 10, "the first sample is its own mean");
        Check(average.Add(20) == 15, "two samples average");
        Check(Math.Abs(average.Add(30) - 20) < 0.001, "three samples inside the window average all three");
        Check(Math.Abs(average.Add(40) - 30) < 0.001, "a sample older than the window leaves the mean");

        average.Reset();
        Check(average.Add(7) == 7, "after a reset the next sample stands alone");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
