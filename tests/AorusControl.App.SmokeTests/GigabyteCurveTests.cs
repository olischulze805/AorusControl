using AorusControl.Core.Features.Cooling;
using AorusControl.Core.Models;

/// <summary>
/// Gigabyte's own curve, lifted from its Control Center rather than guessed - and the two
/// places it has to be adapted, which are the two places a copied curve could quietly become
/// an unsafe one.
/// </summary>
internal static class GigabyteCurveTests
{
    public static void Run()
    {
        IReadOnlyList<(byte TemperatureCelsius, byte Percent)> gcc = GigabyteReferenceCurve.AsGigabyteDrawsIt;
        Check(gcc.Count == 10, "the reference is quoted as GCC has it, ten points");
        Check(gcc[0] == ((byte)55, (byte)0) && gcc[^1] == ((byte)92, (byte)99),
            "including the two values this firmware cannot use: 0 % at the bottom and 92 °C at the top");

        // Adapted for the device, it has to pass the same rules a hand-drawn curve does -
        // otherwise "load Gigabyte's curve" would be a button that writes a rejected curve.
        IReadOnlyList<FanCurvePoint> adapted = GigabyteReferenceCurve.ForThisFirmware();
        FanCurveValidation.Validate(adapted);
        Check(adapted.Count == 15, "the firmware takes exactly fifteen points");
        Check(adapted[0].Value >= 57, "nothing below the lowest duty this hardware was verified at");
        Check(adapted[^1].Temperature <= 90 && adapted[^1].Value == 229,
            "and full speed by 90 C at the latest, which the firmware insists on");

        for (int index = 1; index < adapted.Count; index++)
        {
            Check(adapted[index].Temperature > adapted[index - 1].Temperature, $"temperatures rise at point {index}");
            Check(adapted[index].Value >= adapted[index - 1].Value, $"speeds never fall at point {index}");
        }

        // The shape has to survive the adaptation, or it is no longer Gigabyte's curve: their
        // 45 % at 74 C should still be roughly that.
        FanCurvePoint at74 = adapted.First(point => point.Temperature == 74);
        int percentAt74 = FanSpeedPercent.ToPercent(at74.Value);
        Check(Math.Abs(percentAt74 - 45) <= 2, $"the shape is preserved, not redrawn: expected about 45 % at 74 C, got {percentAt74} %");

        Console.WriteLine("PASS: Gigabyte's own curve is quoted as-is and adapted only where the firmware demands it");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
