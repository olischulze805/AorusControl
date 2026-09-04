using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Keyboard;

/// <summary>
/// The pure frame math for every host-rendered keyboard effect: given an effect, an
/// elapsed time and the base colour, it returns the three zone colours for that instant.
/// No hardware, no clock, no state of its own.
///
/// Extracted from GigabyteHidKeyboardRgbController so the UI's live preview can render
/// from the exact same function the device is fed, rather than an imitation of it - a
/// preview that merely looks similar would be a lie about what is on the keys. The
/// controller now delegates here, so there is one implementation, not two.
/// </summary>
public static class KeyboardEffectFrames
{
    /// <summary>The renderer's frame interval (30 fps). Exposed so a preview can sample
    /// at a rate it can relate to the device's own.</summary>
    public static TimeSpan FrameInterval { get; } = TimeSpan.FromMilliseconds(1000.0 / 30);

    /// <summary>
    /// Which of the stored zone colours an effect actually uses. Declared here, right
    /// beside <see cref="Create"/>, because it is a statement about that function - and
    /// <c>KeyboardEffectFrameTests</c> verifies each entry against the real output rather
    /// than trusting it, so the two cannot drift apart.
    ///
    /// The UI needs this to avoid offering a choice that does nothing: picking a colour
    /// for the rainbow marquee changes nothing on the keyboard, and a control that
    /// pretends otherwise is worse than no control.
    /// </summary>
    public static KeyboardEffectColorUsage ColorUsage(KeyboardRgbEffect? effect) => effect switch
    {
        // Manual mode paints the three stored colours directly.
        null => KeyboardEffectColorUsage.AllZones,
        // Both modulate the brightness of zone 1's colour across all three zones.
        KeyboardRgbEffect.Breathing or KeyboardRgbEffect.Pulse => KeyboardEffectColorUsage.BaseColorOnly,
        // Everything else carries its own palette: hue sweeps, or fixed lit/dim pairs.
        _ => KeyboardEffectColorUsage.None
    };

    /// <param name="elapsed">Seconds since the effect started, already multiplied by the
    /// speed's time scale (<see cref="KeyboardEffectSpeeds.ToTimeScale"/>).</param>
    /// <param name="baseColor">Zone 1's stored colour, which is what the breathing and
    /// pulse effects modulate; the other effects carry their own palette.</param>
    public static KeyboardRgbColor[] Create(KeyboardRgbEffect effect, double elapsed, KeyboardRgbColor baseColor) =>
        effect switch
        {
            KeyboardRgbEffect.Breathing => Uniform(Scale(baseColor, Ramp(elapsed, 3.0))),
            KeyboardRgbEffect.Pulse => Uniform(Scale(baseColor, elapsed % 0.7 < 0.35 ? 1.0 : 0.04)),
            KeyboardRgbEffect.ColorCycle => Uniform(HueToRgb(elapsed / 6.0 % 1.0)),
            KeyboardRgbEffect.RainbowMarquee =>
            [
                HueToRgb((elapsed / 5.0 + 0.00) % 1.0),
                HueToRgb((elapsed / 5.0 + 0.33) % 1.0),
                HueToRgb((elapsed / 5.0 + 0.66) % 1.0)
            ],
            KeyboardRgbEffect.Wave => Travelling(elapsed, 0.5, new(0, 255, 120), new(0, 30, 15), false),
            KeyboardRgbEffect.Marquee => Travelling(elapsed, 0.18, new(255, 255, 255), new(0, 0, 0), false),
            KeyboardRgbEffect.Rotate => Travelling(elapsed, 0.4, new(120, 0, 255), new(12, 0, 25), true),
            KeyboardRgbEffect.Raindrop => CreateRaindropFrame(elapsed),
            KeyboardRgbEffect.FadeSweep => CreateFadeSweepFrame(elapsed),
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };

    private static KeyboardRgbColor[] Uniform(KeyboardRgbColor color) => [color, color, color];

    private static double Ramp(double elapsed, double periodSeconds) =>
        (1 - Math.Cos(elapsed * 2 * Math.PI / periodSeconds)) / 2;

    private static KeyboardRgbColor Scale(KeyboardRgbColor color, double factor) => new(
        Scale(color.Red, factor),
        Scale(color.Green, factor),
        Scale(color.Blue, factor));

    private static byte Scale(byte value, double factor) =>
        (byte)Math.Clamp(Math.Round(value * factor), 0, 255);

    private static KeyboardRgbColor[] Travelling(
        double elapsed,
        double secondsPerZone,
        KeyboardRgbColor lit,
        KeyboardRgbColor dim,
        bool pingPong)
    {
        int step = (int)(elapsed / secondsPerZone);
        int active = pingPong ? Math.Abs(2 - step % 4) : step % 3;
        var frame = new KeyboardRgbColor[3];
        for (int zone = 0; zone < frame.Length; zone++)
        {
            frame[zone] = zone == active ? lit : dim;
        }

        return frame;
    }

    private static KeyboardRgbColor[] CreateRaindropFrame(double elapsed)
    {
        var frame = new[]
        {
            new KeyboardRgbColor(0, 10, 30),
            new KeyboardRgbColor(0, 10, 30),
            new KeyboardRgbColor(0, 10, 30)
        };
        int slot = (int)(elapsed / 0.25);
        int zone = (int)(Math.Abs(Math.Sin(slot * 12.9898) * 43758.5453) % 3);
        frame[zone] = new KeyboardRgbColor(120, 200, 255);
        return frame;
    }

    private static KeyboardRgbColor[] CreateFadeSweepFrame(double elapsed)
    {
        var frame = new KeyboardRgbColor[3];
        for (int zone = 0; zone < frame.Length; zone++)
        {
            double phase = (elapsed / 1.2 - zone * 0.33) % 1.0;
            if (phase < 0)
            {
                phase += 1.0;
            }

            double level = Math.Max(0, 1 - phase * 1.6);
            frame[zone] = new KeyboardRgbColor(Scale(255, level), Scale(120, level), 0);
        }

        return frame;
    }

    private static KeyboardRgbColor HueToRgb(double hue)
    {
        double sector = hue * 6;
        int index = (int)Math.Floor(sector) % 6;
        byte rising = (byte)Math.Round((sector - Math.Floor(sector)) * 255);
        byte falling = (byte)(255 - rising);
        return index switch
        {
            0 => new(255, rising, 0),
            1 => new(falling, 255, 0),
            2 => new(0, 255, rising),
            3 => new(0, falling, 255),
            4 => new(rising, 0, 255),
            _ => new(255, 0, falling)
        };
    }
}
