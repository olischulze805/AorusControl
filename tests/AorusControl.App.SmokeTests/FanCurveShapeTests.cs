using AorusControl.Core.Features.Cooling;
using AorusControl.Core.Models;

/// <summary>
/// Between the curve a person draws and the fifteen points the firmware takes. The failure
/// this guards against is the expensive kind: a shape that looks right in the editor and is
/// rejected - or worse, quietly bent - on the way to the EC.
/// </summary>
internal static class FanCurveShapeTests
{
    public static void Run()
    {
        // Three handles are a normal curve. Whatever the user draws, the firmware gets fifteen
        // valid points - that is not negotiable, so it is asserted first.
        FanCurveHandle[] simple = [new(40, 25), new(65, 50), new(85, 100)];
        IReadOnlyList<FanCurvePoint> curve = FanCurveShape.ToFirmwareCurve(simple);
        FanCurveValidation.Validate(curve);
        Check(curve.Count == 15, "the firmware takes exactly fifteen points, whatever was drawn");
        Check(curve[^1].Value == 229 && curve[^1].Temperature <= 90, "and full speed by 90 C, which is not the user's to place");

        // The drawn shape has to survive the expansion: the middle handle said 50 % at 65 C.
        FanCurvePoint at65 = curve.First(point => point.Temperature >= 65);
        Check(Math.Abs(FanSpeedPercent.ToPercent(at65.Value) - 50) <= 3,
            $"the drawn shape must survive, expected about 50 % at 65 C, got {FanSpeedPercent.ToPercent(at65.Value)} %");

        // And it has to survive the round trip, which is what makes the editor usable at all:
        // fifteen points read back must collapse to the handles they were drawn as.
        IReadOnlyList<FanCurveHandle> back = FanCurveShape.FromFirmwareCurve(curve);
        Check(back.Count <= 5, $"a three-handle curve must not read back as fifteen handles, got {back.Count}");
        Check(back.Count >= 3, "and must keep the corners that were drawn");
        Check(Math.Abs(back[0].TemperatureCelsius - 40) <= 1, "the first handle comes back where it was placed");

        // The rules the firmware enforces are applied before it ever sees them, rather than
        // letting the write fail: order, the 25 % floor, and never falling.
        IReadOnlyList<FanCurvePoint> messy = FanCurveShape.ToFirmwareCurve([new(80, 90), new(40, 5), new(60, 70), new(70, 30)]);
        FanCurveValidation.Validate(messy);
        Check(messy[0].Temperature <= messy[1].Temperature, "handles are sorted, not rejected");
        Check(FanSpeedPercent.ToPercent(messy[0].Value) >= 0, "a cool point may be anything, fans included");

        // The floor is not a single number any more: below 60 °C the fans may stand still,
        // which was measured on this device; above it the lowest verified duty applies, so a
        // curve can never be silent into the temperatures where silence stops being harmless.
        IReadOnlyList<FanCurvePoint> silent = FanCurveShape.ToFirmwareCurve([new(30, 0), new(50, 0), new(85, 100)]);
        FanCurveValidation.Validate(silent);
        Check(silent[0].Value == 0, "a curve may switch the fans off while the machine is cool");
        foreach (FanCurvePoint point in silent.Where(point => point.Temperature >= FanCurveValidation.PassiveBelowCelsius))
            Check(point.Value >= 57, $"but never above {FanCurveValidation.PassiveBelowCelsius} C");

        IReadOnlyList<FanCurvePoint> silentHot = FanCurveShape.ToFirmwareCurve([new(30, 0), new(75, 0), new(85, 100)]);
        FanCurveValidation.Validate(silentHot);
        Check(FanSpeedPercent.ToPercent(silentHot.First(point => point.Temperature >= 75).Value) >= 25,
            "a handle drawn silent at 75 C is lifted to the floor rather than accepted");
        for (int index = 1; index < messy.Count; index++)
            Check(messy[index].Value >= messy[index - 1].Value, $"a curve never falls, checked at {index}");

        // Two handles is the fewest that still describes a line, and has to work.
        FanCurveValidation.Validate(FanCurveShape.ToFirmwareCurve([new(30, 25), new(85, 100)]));
        bool refused = false;
        try { FanCurveShape.ToFirmwareCurve([new(50, 40)]); } catch (ArgumentException) { refused = true; }
        Check(refused, "a single point is not a curve and is refused rather than guessed at");

        // Collapsing only removes what a straight line already covers. A curve that is
        // genuinely a straight line therefore comes back as two handles - correctly, since
        // that is all it ever said - while every bend in a curved one is kept.
        FanCurvePoint[] straight = Enumerable.Range(0, 15)
            .Select(i => new FanCurvePoint((byte)i, (byte)(30 + i * 4), (byte)(i == 14 ? 229 : 57 + i * 12))).ToArray();
        Check(FanCurveShape.FromFirmwareCurve(straight).Count == 2, "a straight line is two handles, not fifteen");

        FanCurvePoint[] bent = Enumerable.Range(0, 15)
            .Select(i => new FanCurvePoint((byte)i, (byte)(30 + i * 4), (byte)(i == 14 ? 229 : 57 + i * i))).ToArray();
        Check(FanCurveShape.FromFirmwareCurve(bent).Count >= 6, $"a curved one keeps its bends, got {FanCurveShape.FromFirmwareCurve(bent).Count} handles");

        Console.WriteLine("PASS: drawn curves expand to fifteen valid points and read back as handles again");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
