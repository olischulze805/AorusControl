using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Cooling;

/// <summary>One point the user placed: a temperature and how hard the fans should run there.</summary>
public sealed record FanCurveHandle(double TemperatureCelsius, int Percent);

/// <summary>
/// Between the curve a person draws and the curve the firmware takes.
///
/// The EC wants exactly fifteen points, always. Fifteen handles is far too many to shape a
/// curve with - most curves are three or four decisions - so the editor works with as few as
/// the user likes and this fills in the rest. The reverse direction matters just as much: a
/// curve read back from the device arrives as fifteen points, and dropping the ones that lie on
/// a straight line between their neighbours turns it back into something with handles you can
/// actually grab.
///
/// Both directions are pure functions, which is what makes the round trip testable without a
/// device: draw, expand, read back, collapse, and the shape has to survive.
/// </summary>
public static class FanCurveShape
{
    public const int FirmwarePoints = 15;
    public const int MinimumHandles = 2;
    public const double MaximumTemperature = 90;
    /// <summary>The floor above <see cref="FanCurveValidation.PassiveBelowCelsius"/>, where the
    /// lowest duty this hardware was ever measured at applies.</summary>
    public const int MinimumPercent = 25;

    /// <summary>What a handle at this temperature may be lowered to. Zero while the machine is
    /// cool enough for the fans to stand still, the verified floor above that.</summary>
    public static int MinimumPercentAt(double temperatureCelsius) =>
        temperatureCelsius >= FanCurveValidation.PassiveBelowCelsius ? MinimumPercent : 0;

    /// <summary>
    /// Expands handles into the fifteen points the firmware demands: the handles themselves,
    /// plus interpolated points splitting the widest gaps, so the table describes the drawn
    /// shape rather than fifteen copies of its end.
    /// </summary>
    public static IReadOnlyList<FanCurvePoint> ToFirmwareCurve(IReadOnlyList<FanCurveHandle> handles)
    {
        ArgumentNullException.ThrowIfNull(handles);
        if (handles.Count < MinimumHandles) throw new ArgumentException($"Mindestens {MinimumHandles} Punkte nötig.", nameof(handles));

        List<FanCurveHandle> shape = Normalize(handles);
        while (shape.Count < FirmwarePoints) SplitWidestGap(shape);

        var points = new FanCurvePoint[FirmwarePoints];
        byte previousRaw = 0;
        for (int index = 0; index < FirmwarePoints; index++)
        {
            byte temperature = (byte)Math.Round(shape[index].TemperatureCelsius);
            byte raw = FanSpeedPercent.ToRaw(shape[index].Percent);
            // Zero means the fans stop, which is only allowed while it is cool; anything else
            // is lifted to the lowest duty this hardware was measured at.
            if (raw > 0 || temperature >= FanCurveValidation.PassiveBelowCelsius)
                raw = Math.Max(raw, (byte)57);
            // Rounding two independent values can otherwise produce a step backwards, which the
            // firmware rejects outright.
            if (index > 0)
            {
                if (temperature < points[index - 1].Temperature) temperature = points[index - 1].Temperature;
                if (raw < previousRaw) raw = previousRaw;
            }
            points[index] = new FanCurvePoint((byte)index, temperature, raw);
            previousRaw = raw;
        }

        // The firmware insists the last point reaches full speed by 90 °C, so it is not the
        // user's to place.
        points[^1] = new FanCurvePoint(FirmwarePoints - 1, Math.Min(points[^1].Temperature, (byte)MaximumTemperature), 229);
        if (points[^2].Temperature > points[^1].Temperature)
            points[^2] = points[^2] with { Temperature = points[^1].Temperature };
        FanCurveValidation.Validate(points);
        return points;
    }

    /// <summary>
    /// Turns a device curve back into handles, dropping every point that sits on the straight
    /// line between its neighbours. Fifteen points read back as three or four grabbable ones,
    /// which is what they were drawn as.
    /// </summary>
    public static IReadOnlyList<FanCurveHandle> FromFirmwareCurve(IReadOnlyList<FanCurvePoint> curve)
    {
        ArgumentNullException.ThrowIfNull(curve);
        var handles = curve
            .Select(point => new FanCurveHandle(point.Temperature, FanSpeedPercent.ToPercent(point.Value)))
            .ToList();

        // Two passes' worth of tidying, in the order that matters: identical points first, then
        // the ones a straight line already covers.
        for (int index = handles.Count - 1; index > 0; index--)
            if (handles[index].TemperatureCelsius == handles[index - 1].TemperatureCelsius) handles.RemoveAt(index);

        for (int index = handles.Count - 2; index > 0; index--)
        {
            if (handles.Count <= MinimumHandles) break;
            if (IsOnTheLine(handles[index - 1], handles[index], handles[index + 1])) handles.RemoveAt(index);
        }

        return handles;
    }

    /// <summary>A point within one percent of where interpolation would put it anyway carries no
    /// information; keeping it would only give the user a handle that does nothing.</summary>
    private static bool IsOnTheLine(FanCurveHandle before, FanCurveHandle point, FanCurveHandle after)
    {
        double span = after.TemperatureCelsius - before.TemperatureCelsius;
        if (span <= 0) return true;
        double share = (point.TemperatureCelsius - before.TemperatureCelsius) / span;
        double expected = before.Percent + share * (after.Percent - before.Percent);
        return Math.Abs(expected - point.Percent) <= 1.0;
    }

    /// <summary>Sorted, clamped and non-decreasing - the rules the firmware enforces anyway,
    /// applied before anything is measured against them.</summary>
    private static List<FanCurveHandle> Normalize(IReadOnlyList<FanCurveHandle> handles)
    {
        var shape = handles
            .Select(handle => new FanCurveHandle(
                Math.Clamp(Math.Round(handle.TemperatureCelsius), 0, MaximumTemperature),
                Math.Clamp(handle.Percent, MinimumPercentAt(handle.TemperatureCelsius), 100)))
            .OrderBy(handle => handle.TemperatureCelsius)
            .ToList();

        for (int index = 1; index < shape.Count; index++)
            if (shape[index].Percent < shape[index - 1].Percent)
                shape[index] = shape[index] with { Percent = shape[index - 1].Percent };

        if (shape[^1].Percent < 100 || shape[^1].TemperatureCelsius < shape[^2].TemperatureCelsius)
            shape[^1] = new FanCurveHandle(Math.Max(shape[^1].TemperatureCelsius, shape[^2].TemperatureCelsius), 100);
        return shape;
    }

    private static void SplitWidestGap(List<FanCurveHandle> shape)
    {
        int widest = 0;
        double widestSpan = -1;
        for (int index = 1; index < shape.Count; index++)
        {
            double span = shape[index].TemperatureCelsius - shape[index - 1].TemperatureCelsius;
            if (span <= widestSpan) continue;
            widestSpan = span;
            widest = index;
        }

        FanCurveHandle before = shape[widest - 1], after = shape[widest];
        shape.Insert(widest, new FanCurveHandle(
            Math.Round((before.TemperatureCelsius + after.TemperatureCelsius) / 2),
            (int)Math.Round((before.Percent + after.Percent) / 2.0)));
    }
}
