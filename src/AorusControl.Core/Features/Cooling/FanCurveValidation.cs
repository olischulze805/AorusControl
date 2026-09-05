using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Cooling;

public static class FanCurveValidation
{
    /// <summary>
    /// Up to this temperature a curve point may be anything down to zero, fans included.
    ///
    /// Measured rather than chosen: the EC stores raw 0 and drives it - both fans reported
    /// 0 RPM for twelve seconds at 45-48 °C (research/runs/fan-floor-rpm-test-20260905-135015.md)
    /// - and the vendor's own Quiet profile stops the fans at around 51 °C. Gigabyte's shipped
    /// curve for this model likewise sits at 0 % below 55 °C and rises from 59 °C.
    /// </summary>
    public const byte PassiveBelowCelsius = 60;

    public static void Validate(IReadOnlyList<FanCurvePoint> curve)
    {
        ArgumentNullException.ThrowIfNull(curve);
        if (curve.Count != 15) throw new ArgumentException("Eine Lüfterkurve muss genau 15 Punkte enthalten.", nameof(curve));
        for (int index = 0; index < curve.Count; index++)
        {
            FanCurvePoint point = curve[index] ?? throw new ArgumentException("Leerer Kurvenpunkt.", nameof(curve));
            if (point.Index != index) throw new ArgumentException("Kurvenindizes müssen lückenlos 0 bis 14 sein.", nameof(curve));
            // Below the passive limit the fans may stand still: raw 0 was measured on this
            // device as both stored and driven - 0 RPM on both fans for twelve seconds at
            // 45-48 °C - and Gigabyte's own Quiet profile does exactly the same at ~51 °C.
            // From there upwards the lowest verified duty applies, so a curve can never be
            // silent into the temperatures where silence stops being harmless.
            byte minimum = point.Temperature >= PassiveBelowCelsius ? (byte)57 : (byte)0;
            if (point.Value > 229 || point.Value < minimum)
                throw new ArgumentException(
                    point.Temperature >= PassiveBelowCelsius
                        ? $"Ab {PassiveBelowCelsius} °C müssen Kurvenwerte im bestätigten Bereich 57 bis 229 liegen."
                        : "Kurvenwerte dürfen höchstens 229 sein.",
                    nameof(curve));
            if (index > 0 && (point.Temperature < curve[index - 1].Temperature || point.Value < curve[index - 1].Value))
                throw new ArgumentException("Temperaturen und Rohwerte müssen monoton sein.", nameof(curve));
        }
        if (curve[^1].Temperature > 90 || curve[^1].Value != 229)
            throw new ArgumentException("Der letzte Punkt muss spätestens bei 90 °C den Rohwert 229 erzwingen.", nameof(curve));
    }
}
