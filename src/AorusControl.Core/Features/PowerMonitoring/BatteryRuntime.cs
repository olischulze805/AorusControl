using System.Diagnostics;

namespace AorusControl.Core.Features.PowerMonitoring;

/// <summary>
/// A mean over the last stretch of time rather than over the last n samples.
///
/// The pack reports its rate every 8 to 16 seconds while the dashboard ticks every two, so
/// counting samples would weight one slow instrument by how often a fast clock happened to
/// ask it. A window in seconds says what it means and survives a changed tick interval.
/// </summary>
public sealed class MovingAverage(TimeSpan window, Func<long>? clock = null)
{
    private readonly Func<long> _now = clock ?? Stopwatch.GetTimestamp;
    private readonly Queue<(long At, double Value)> _samples = new();
    private double _sum;

    /// <summary>Takes one sample and returns the mean of the window it now belongs to.</summary>
    public double Add(double value)
    {
        long now = _now();
        _samples.Enqueue((now, value));
        _sum += value;
        // One sample always stays: a window that has just been reset would otherwise throw
        // away the only reading it has and divide by zero.
        while (_samples.Count > 1 && Stopwatch.GetElapsedTime(_samples.Peek().At, now) > window)
            _sum -= _samples.Dequeue().Value;
        return _sum / _samples.Count;
    }

    public void Reset()
    {
        _samples.Clear();
        _sum = 0;
    }
}

/// <summary>How much time the current battery figure amounts to.</summary>
/// <param name="Time">How long from now.</param>
/// <param name="UntilCharged">True while charging, where the time runs to the point at which
/// the battery stops taking power - the charge limit, which is usually not full.</param>
public sealed record BatteryRuntime(TimeSpan Time, bool UntilCharged)
{
    /// <summary>Hours and minutes, or minutes alone under an hour. Seconds would be a
    /// precision this estimate does not have.</summary>
    public string Text
    {
        get
        {
            // Rounded once, then split - rounding the two parts separately is how a display
            // ends up reading "3 h 60 min".
            int minutes = Math.Max((int)Math.Round(Time.TotalMinutes), 1);
            return minutes >= 60 ? $"{minutes / 60} h {minutes % 60} min" : $"{minutes} min";
        }
    }
}

/// <summary>
/// Turns watts into time. Windows' own estimate is not usable here: this machine answers
/// <c>BatteryRuntime.EstimatedRuntime</c> with 0xFFFFFFFF and <c>Win32_Battery</c> with the
/// same placeholder, measured 2026-09-18. The two figures the estimate needs - watt hours
/// left and the rate - we already read ourselves for the dashboard.
/// </summary>
public static class BatteryRuntimeMath
{
    /// <summary>Beyond this the figure is arithmetic rather than information: a nearly idle
    /// machine divided by a rate that small says "two days" and means "the rate is noise".</summary>
    public static readonly TimeSpan Longest = TimeSpan.FromHours(24);

    /// <param name="flow">The reading the direction and the capacities come from.</param>
    /// <param name="watts">The smoothed rate. The instantaneous one changes by a factor of two
    /// between a still desktop and a scrolling page, and an estimate that jumps with it is
    /// worse than none.</param>
    /// <param name="stopPercent">Where charging stops. Without it a charge estimate would run
    /// to 100 % and promise an hour that never happens, because the limit ends it first.</param>
    public static BatteryRuntime? From(BatteryFlow flow, double watts, int? stopPercent)
    {
        ArgumentNullException.ThrowIfNull(flow);
        if (watts <= 0 || double.IsNaN(watts)) return null;

        double? wattHours = flow.Direction switch
        {
            BatteryFlowDirection.Discharging => flow.RemainingWattHours,
            BatteryFlowDirection.Charging => StillToGo(flow, stopPercent),
            _ => null
        };
        if (wattHours is not { } left || left <= 0) return null;

        TimeSpan time = TimeSpan.FromHours(left / watts);
        return time > Longest || time <= TimeSpan.Zero
            ? null
            : new BatteryRuntime(time, flow.Direction == BatteryFlowDirection.Charging);
    }

    /// <summary>The watt hours between here and the point where charging stops.</summary>
    private static double? StillToGo(BatteryFlow flow, int? stopPercent)
    {
        if (flow is not { FullWattHours: { } full, RemainingWattHours: { } remaining }) return null;
        double missing = full * Math.Clamp(stopPercent ?? 100, 1, 100) / 100.0 - remaining;
        return missing > 0 ? missing : null;
    }
}
