using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Cooling;

/// <summary>
/// The fan curve Gigabyte's own Control Center draws for this laptop, taken from its code
/// rather than guessed.
///
/// Found in the decompiled notebook module (GBT_Notebook 26.06.23.01,
/// ucNotebook.Views/FanControlNb.cs, the AORUS branch of the FanControlNb constructor). GCC
/// hardcodes one curve per model family: the 16-inch H models and the Aorus 15/17 B/9/SF get
/// their own curve per fan mode, and everything else in the AORUS family - this laptop
/// included - gets the single default below.
///
/// That is also the honest answer to "which curve do Leise/Normal/Gaming/Maximal set": on this
/// model, none. Those modes are four status flags, the firmware regulates internally, and GCC
/// had exactly one curve to show for this machine - this one.
///
/// Two points cannot be used as GCC states them, and both are noted where they are adjusted
/// rather than quietly bent:
/// * GCC starts at 0% below 55 °C, and that turned out to be literally true rather than a
///   drawing convention: the fans really do stop. It is kept as it is.
/// * GCC's last point is 99% at 92 °C. The firmware requires the last point to reach full
///   speed by 90 °C at the latest, so the end is pulled in to (90, 100%).
/// </summary>
public static class GigabyteReferenceCurve
{
    /// <summary>The points exactly as GCC has them: temperature in °C, fan speed in percent.</summary>
    public static IReadOnlyList<(byte TemperatureCelsius, byte Percent)> AsGigabyteDrawsIt { get; } =
    [
        (55, 0), (59, 25), (62, 29), (65, 33), (68, 37),
        (71, 41), (74, 45), (77, 50), (87, 60), (92, 99)
    ];

    /// <summary>
    /// The same shape as the fifteen points this firmware accepts: sampled at the temperatures
    /// GCC uses where it has them, held flat below its first point, and ending where the
    /// firmware demands. Every value passes <see cref="FanCurveValidation"/>.
    /// </summary>
    public static IReadOnlyList<FanCurvePoint> ForThisFirmware()
    {
        // Fifteen temperatures spread over the range the curve editor shows, ending at the 90 °C
        // the firmware insists on.
        byte[] temperatures = [30, 40, 50, 55, 59, 62, 65, 68, 71, 74, 77, 81, 85, 87, 90];
        var points = new FanCurvePoint[15];
        byte previous = 0;
        for (int index = 0; index < temperatures.Length; index++)
        {
            byte percent = index == temperatures.Length - 1 ? (byte)100 : PercentAt(temperatures[index]);
            byte raw = FanSpeedPercent.ToRaw(percent);
            if (raw > 0 || temperatures[index] >= FanCurveValidation.PassiveBelowCelsius)
                raw = Math.Max(raw, (byte)57);
            // Non-decreasing is a firmware rule, and interpolation plus rounding can otherwise
            // produce a step backwards of one raw unit.
            if (raw < previous) raw = previous;
            points[index] = new FanCurvePoint((byte)index, temperatures[index], raw);
            previous = raw;
        }
        FanCurveValidation.Validate(points);
        return points;
    }

    /// <summary>Linear between GCC's points, flat outside them, and never below what this
    /// firmware was verified at for that temperature - which is zero while it is cool.</summary>
    private static byte PercentAt(byte temperature)
    {
        IReadOnlyList<(byte Temperature, byte Percent)> curve = AsGigabyteDrawsIt;
        if (temperature <= curve[0].Temperature) return curve[0].Percent;
        if (temperature >= curve[^1].Temperature) return curve[^1].Percent;

        for (int index = 1; index < curve.Count; index++)
        {
            if (temperature > curve[index].Temperature) continue;
            (byte fromTemperature, byte fromPercent) = curve[index - 1];
            (byte toTemperature, byte toPercent) = curve[index];
            double share = (double)(temperature - fromTemperature) / (toTemperature - fromTemperature);
            double interpolated = Math.Round(fromPercent + share * (toPercent - fromPercent));
            return (byte)Math.Max(FanCurveShape.MinimumPercentAt(temperature), interpolated);
        }

        return curve[^1].Percent;
    }
}
