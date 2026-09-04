namespace AorusControl.Core.Features.Cooling;

/// <summary>
/// Converts the firmware's raw fan duty byte to and from a 0-100% figure for display,
/// using the same "out of 229" convention the Dashboard already shows ("Rohwert X / 229")
/// rather than treating the curve's tested floor (57) as if it meant "fan off" - it does
/// not, 57 is just the lowest verified-safe duty.
/// </summary>
public static class FanSpeedPercent
{
    public const byte MaxRaw = 229;

    public static int ToPercent(byte raw) =>
        (int)Math.Round(raw / (double)MaxRaw * 100.0, MidpointRounding.AwayFromZero);

    public static byte ToRaw(int percent)
    {
        double raw = Math.Clamp(percent, 0, 100) / 100.0 * MaxRaw;
        return (byte)Math.Clamp(Math.Round(raw, MidpointRounding.AwayFromZero), 0, 255);
    }
}
