using AorusControl.Core.Features.Cooling;

internal static class FanSpeedPercentTests
{
    public static void Run()
    {
        Check(FanSpeedPercent.ToPercent(57) == 25, "the tested floor (57) reads as 25%, not 0% - it is not \"off\"");
        Check(FanSpeedPercent.ToPercent(229) == 100, "the max raw value is exactly 100%");
        Check(FanSpeedPercent.ToPercent(0) == 0, "zero raw is zero percent");

        Check(FanSpeedPercent.ToRaw(25) == 57, "25% maps back to the tested floor");
        Check(FanSpeedPercent.ToRaw(100) == 229, "100% maps back to the max raw value");
        Check(FanSpeedPercent.ToRaw(0) == 0, "0% maps to raw 0");
        Check(FanSpeedPercent.ToRaw(-10) == 0, "negative percent is clamped, not thrown or wrapped");
        Check(FanSpeedPercent.ToRaw(150) == 229, "over 100 percent is clamped to the max raw value");

        foreach (byte raw in new byte[] { 57, 68, 91, 114, 137, 160, 194, 229 })
        {
            int percent = FanSpeedPercent.ToPercent(raw);
            byte roundTripped = FanSpeedPercent.ToRaw(percent);
            Check(Math.Abs(roundTripped - raw) <= 1, $"raw {raw} -> {percent}% -> raw must stay within rounding tolerance, got {roundTripped}");
        }

        Console.WriteLine("PASS: fan raw/percent conversion, clamping, and round trip for every tested Fixed value");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
