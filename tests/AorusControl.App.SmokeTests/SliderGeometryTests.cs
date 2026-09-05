using AorusControl.App.Infrastructure;

/// <summary>
/// The slider scale's arithmetic. Worth pinning down because the failure it prevents is a
/// visual one: ticks and labels that drift away from the thumb towards the ends of the track,
/// which looks like a rounding wobble rather than a wrong formula.
/// </summary>
internal static class SliderGeometryTests
{
    public static void Run()
    {
        const double width = 220, thumb = 20;

        // The thumb centre starts half a thumb in and ends half a thumb short - anything else
        // and the first and last marks sit outside the range the thumb can actually reach.
        Check(At(0, 0, 4, width, thumb) == thumb / 2, "the minimum sits half a thumb from the left edge");
        Check(At(4, 0, 4, width, thumb) == width - thumb / 2, "the maximum sits half a thumb from the right edge");
        Check(At(2, 0, 4, width, thumb) == width / 2, "the middle of the range is the middle of the track");

        // Evenly spaced values must be evenly spaced pixels, whatever the range.
        double[] steps = [At(0, 0, 3, width, thumb), At(1, 0, 3, width, thumb), At(2, 0, 3, width, thumb), At(3, 0, 3, width, thumb)];
        double gap = steps[1] - steps[0];
        for (int index = 2; index < steps.Length; index++)
            Check(Math.Abs(steps[index] - steps[index - 1] - gap) < 0.001, $"step {index} must keep the same spacing");

        // A range that does not start at zero is the normal case here: the fan slider runs
        // 25-100 % and the charge limit 60-100 %.
        Check(At(60, 60, 100, width, thumb) == thumb / 2, "a range starting above zero still starts at the left end");
        Check(At(80, 60, 100, width, thumb) == width / 2, "80 % of a 60-100 range is the middle");

        // Out-of-range values are clamped rather than drawn off the track.
        Check(At(-5, 0, 4, width, thumb) == At(0, 0, 4, width, thumb), "below the minimum clamps to the left end");
        Check(At(99, 0, 4, width, thumb) == At(4, 0, 4, width, thumb), "above the maximum clamps to the right end");

        // Degenerate inputs must not produce NaN: an empty range happens while a control is
        // still being initialised, and a width below the thumb happens mid-resize.
        Check(At(1, 5, 5, width, thumb) == thumb / 2, "an empty range collapses to the start instead of dividing by zero");
        Check(At(4, 0, 4, 10, thumb) == thumb / 2, "a track narrower than the thumb has no travel at all");

        Console.WriteLine("PASS: slider scale places values where the thumb can actually reach them");
    }

    private static double At(double value, double min, double max, double width, double thumb) =>
        SliderGeometry.PositionOf(value, min, max, width, thumb);

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
