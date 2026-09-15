namespace AorusControl.App.Infrastructure;

/// <summary>
/// The last few minutes of one measurement, published as a fresh array whenever it changes.
///
/// A plain array rather than an observable collection: the drawing only ever wants the whole
/// series, and copying ninety doubles every couple of seconds is cheaper than the change
/// bookkeeping a collection would cost.
/// </summary>
public sealed class SampleHistory(int length = 90)
{
    private readonly List<double> _samples = [];

    /// <summary>Oldest first.</summary>
    public double[] Samples { get; private set; } = [];

    public double[] Add(double value)
    {
        _samples.Add(value);
        if (_samples.Count > length) _samples.RemoveRange(0, _samples.Count - length);
        return Samples = [.. _samples];
    }

    /// <summary>Throws the series away. Used when the thing being measured changes meaning -
    /// a line that runs from "40 W out of the battery" into "60 W into it" is two different
    /// measurements drawn as one.</summary>
    public double[] Reset()
    {
        _samples.Clear();
        return Samples = [];
    }
}
