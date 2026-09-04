using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Cooling;

public static class FanCurveValidation
{
    public static void Validate(IReadOnlyList<FanCurvePoint> curve)
    {
        ArgumentNullException.ThrowIfNull(curve);
        if (curve.Count != 15) throw new ArgumentException("Eine Lüfterkurve muss genau 15 Punkte enthalten.", nameof(curve));
        for (int index = 0; index < curve.Count; index++)
        {
            FanCurvePoint point = curve[index] ?? throw new ArgumentException("Leerer Kurvenpunkt.", nameof(curve));
            if (point.Index != index) throw new ArgumentException("Kurvenindizes müssen lückenlos 0 bis 14 sein.", nameof(curve));
            if (point.Value is < 57 or > 229) throw new ArgumentException("Kurvenwerte müssen im bestätigten Bereich 57 bis 229 liegen.", nameof(curve));
            if (index > 0 && (point.Temperature < curve[index - 1].Temperature || point.Value < curve[index - 1].Value))
                throw new ArgumentException("Temperaturen und Rohwerte müssen monoton sein.", nameof(curve));
        }
        if (curve[^1].Temperature > 90 || curve[^1].Value != 229)
            throw new ArgumentException("Der letzte Punkt muss spätestens bei 90 °C den Rohwert 229 erzwingen.", nameof(curve));
    }
}
