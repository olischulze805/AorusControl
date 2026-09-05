namespace AorusControl.App.Infrastructure;

/// <summary>
/// Where a value sits along a slider. Pure arithmetic, in one place, because two things have
/// to agree on it: the slider's own thumb (placed by WPF's Track) and the scale of ticks and
/// labels drawn underneath it. When they disagreed, the marks drifted away from the thumb
/// towards both ends - which is exactly the kind of wrong that is easy to see and hard to
/// pin down.
///
/// The thumb does not travel the full width: it starts half a thumb in and ends half a thumb
/// short, so its centre moves over <c>width - thumbWidth</c>.
/// </summary>
public static class SliderGeometry
{
    public static double PositionOf(double value, double minimum, double maximum, double width, double thumbWidth)
    {
        double span = maximum - minimum;
        double fraction = span <= 0 ? 0 : Math.Clamp((value - minimum) / span, 0, 1);
        return thumbWidth / 2 + fraction * Math.Max(0, width - thumbWidth);
    }
}
