using AorusControl.Core.Models;

internal static class HsvColorTests
{
    public static void Run()
    {
        Check(HsvColor.FromRgb(new KeyboardRgbColor(255, 0, 0)) is { Saturation: 1, Value: 1 } red && IsClose(red.Hue, 0),
            "pure red is hue 0, full saturation and value");
        Check(HsvColor.FromRgb(new KeyboardRgbColor(0, 255, 0)) is { Value: 1 } green && IsClose(green.Hue, 120),
            "pure green is hue 120");
        Check(HsvColor.FromRgb(new KeyboardRgbColor(0, 0, 255)) is { Value: 1 } blue && IsClose(blue.Hue, 240),
            "pure blue is hue 240");
        Check(HsvColor.FromRgb(new KeyboardRgbColor(0, 0, 0)) is { Saturation: 0, Value: 0 },
            "black has zero saturation and value regardless of hue");
        Check(HsvColor.FromRgb(new KeyboardRgbColor(255, 255, 255)) is { Saturation: 0, Value: 1 },
            "white has zero saturation and full value");

        foreach (KeyboardRgbColor original in new[]
        {
            new KeyboardRgbColor(200, 80, 40),
            new KeyboardRgbColor(10, 220, 130),
            new KeyboardRgbColor(1, 1, 1),
            new KeyboardRgbColor(255, 255, 0),
        })
        {
            KeyboardRgbColor roundTripped = HsvColor.FromRgb(original).ToRgb();
            Check(Math.Abs(roundTripped.Red - original.Red) <= 1
                  && Math.Abs(roundTripped.Green - original.Green) <= 1
                  && Math.Abs(roundTripped.Blue - original.Blue) <= 1,
                $"RGB->HSV->RGB round trip for {original.Hex} must stay within rounding tolerance, got {roundTripped.Hex}");
        }

        Check(new HsvColor(720, 1, 1).ToRgb() == new HsvColor(0, 1, 1).ToRgb(), "hue wraps modulo 360, including negative multiples handled by the mod-and-add-360 path");
        Check(new HsvColor(-90, 1, 1).ToRgb() == new HsvColor(270, 1, 1).ToRgb(), "negative hue wraps correctly");
        Check(new HsvColor(0, 2, 2).ToRgb() == new HsvColor(0, 1, 1).ToRgb(), "out-of-range saturation/value are clamped, not thrown");

        Console.WriteLine("PASS: HSV/RGB conversion for primaries, black/white, round trip tolerance, and out-of-range clamping");
    }

    private static bool IsClose(double actual, double expected) => Math.Abs(actual - expected) < 0.01;

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
