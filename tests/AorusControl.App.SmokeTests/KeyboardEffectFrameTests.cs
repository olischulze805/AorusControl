using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Models;

internal static class KeyboardEffectFrameTests
{
    public static void Run()
    {
        var baseColor = new KeyboardRgbColor(200, 40, 10);

        foreach (KeyboardRgbEffect effect in Enum.GetValues<KeyboardRgbEffect>())
        {
            KeyboardRgbColor[] frame = KeyboardEffectFrames.Create(effect, 1.234, baseColor);
            Check(frame.Length == 3, $"{effect} must produce exactly three zones");
            // Same input must give the same output: the preview relies on this to show
            // what the device is being sent rather than an independent animation.
            Check(frame.SequenceEqual(KeyboardEffectFrames.Create(effect, 1.234, baseColor)),
                $"{effect} must be a pure function of (effect, elapsed, base colour)");
        }

        Check(KeyboardEffectFrames.Create(KeyboardRgbEffect.Breathing, 0, baseColor)[0] == new KeyboardRgbColor(0, 0, 0),
            "breathing starts fully dimmed at elapsed 0");
        Check(KeyboardEffectFrames.Create(KeyboardRgbEffect.Breathing, 1.5, baseColor)[0] == baseColor,
            "breathing reaches the stored colour at half its 3 s period");
        Check(KeyboardEffectFrames.Create(KeyboardRgbEffect.Breathing, 1.5, baseColor).Distinct().Count() == 1,
            "breathing lights all three zones identically");

        Check(KeyboardEffectFrames.Create(KeyboardRgbEffect.Pulse, 0, baseColor)[0] == baseColor,
            "pulse is fully lit in the first half of its period");
        Check(KeyboardEffectFrames.Create(KeyboardRgbEffect.Pulse, 0.5, baseColor)[0] != baseColor,
            "pulse is dimmed in the second half of its period");

        KeyboardRgbColor[] rainbow = KeyboardEffectFrames.Create(KeyboardRgbEffect.RainbowMarquee, 0, baseColor);
        Check(rainbow.Distinct().Count() == 3, "the rainbow marquee holds a different hue per zone");
        Check(!rainbow.Contains(baseColor) || baseColor == rainbow[0],
            "the rainbow marquee carries its own palette rather than the stored colour");

        // Travelling effects light exactly one zone at a time.
        foreach (KeyboardRgbEffect travelling in new[] { KeyboardRgbEffect.Wave, KeyboardRgbEffect.Marquee, KeyboardRgbEffect.Rotate })
        {
            for (double elapsed = 0; elapsed < 3; elapsed += 0.17)
            {
                KeyboardRgbColor[] frame = KeyboardEffectFrames.Create(travelling, elapsed, baseColor);
                Check(frame.Distinct().Count() == 2, $"{travelling} must light one zone and dim the other two");
            }
        }

        // The declared colour usage must match what Create actually does, not what a
        // comment claims. The UI hides or enables the colour swatches based on this, so a
        // wrong entry would mean offering a colour choice that changes nothing - or hiding
        // one that matters. Comparing two very different base colours proves it.
        var red = new KeyboardRgbColor(255, 0, 0);
        var blue = new KeyboardRgbColor(0, 0, 255);
        foreach (KeyboardRgbEffect effect in Enum.GetValues<KeyboardRgbEffect>())
        {
            bool reactsToBaseColor = false;
            for (double elapsed = 0; elapsed < 6 && !reactsToBaseColor; elapsed += 0.13)
            {
                reactsToBaseColor = !KeyboardEffectFrames.Create(effect, elapsed, red)
                    .SequenceEqual(KeyboardEffectFrames.Create(effect, elapsed, blue));
            }

            KeyboardEffectColorUsage declared = KeyboardEffectFrames.ColorUsage(effect);
            Check(declared != KeyboardEffectColorUsage.AllZones,
                $"{effect} is an effect, so it can never read all three stored zones");
            Check(reactsToBaseColor == (declared == KeyboardEffectColorUsage.BaseColorOnly),
                $"{effect} is declared {declared} but " +
                (reactsToBaseColor ? "does react to the base colour" : "ignores the base colour entirely"));
        }

        Check(KeyboardEffectFrames.ColorUsage(null) == KeyboardEffectColorUsage.AllZones,
            "manual mode paints all three stored colours");

        Check(KeyboardEffectFrames.FrameInterval.TotalMilliseconds is > 30 and < 40,
            "the shared frame interval stays the renderer's 30 fps");

        bool rejected = false;
        try { KeyboardEffectFrames.Create((KeyboardRgbEffect)999, 0, baseColor); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "an unknown effect must be rejected, not silently rendered as black");

        Console.WriteLine("PASS: shared effect frame math is pure, per-effect shapes hold, unknown effects rejected");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
