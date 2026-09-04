namespace AorusControl.Core.Models;

/// <summary>
/// Hue/saturation/value color, used only by the UI's picker for the gradient-square +
/// hue-slider interaction; the device and every persisted setting still speak plain RGB
/// (<see cref="KeyboardRgbColor"/>). Hue in degrees [0, 360); saturation and value in
/// [0, 1].
/// </summary>
public readonly record struct HsvColor(double Hue, double Saturation, double Value)
{
    public static HsvColor FromRgb(KeyboardRgbColor rgb)
    {
        double r = rgb.Red / 255.0, g = rgb.Green / 255.0, b = rgb.Blue / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double hue;
        if (delta < 1e-9) hue = 0;
        else if (max == r) hue = 60 * (((g - b) / delta) % 6);
        else if (max == g) hue = 60 * (((b - r) / delta) + 2);
        else hue = 60 * (((r - g) / delta) + 4);
        if (hue < 0) hue += 360;

        double saturation = max < 1e-9 ? 0 : delta / max;
        return new HsvColor(hue, saturation, max);
    }

    public KeyboardRgbColor ToRgb()
    {
        double hue = ((Hue % 360) + 360) % 360;
        double saturation = Math.Clamp(Saturation, 0, 1);
        double value = Math.Clamp(Value, 0, 1);

        double c = value * saturation;
        double x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        double m = value - c;

        (double r, double g, double b) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return new KeyboardRgbColor(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
