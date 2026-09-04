namespace AorusControl.Core.Features.PowerProfiles;

public enum LaptopPowerSource { Unknown, Battery, Ac }

public static class LaptopPowerSources
{
    public static LaptopPowerSource FromWindowsStatus(byte acLineStatus) => acLineStatus switch
    {
        0 => LaptopPowerSource.Battery,
        1 => LaptopPowerSource.Ac,
        _ => LaptopPowerSource.Unknown
    };
}

public sealed record PowerProfileAssignments(Guid? AcProfile, Guid? BatteryProfile)
{
    public Guid? For(LaptopPowerSource source) => source switch
    {
        LaptopPowerSource.Ac => AcProfile,
        LaptopPowerSource.Battery => BatteryProfile,
        _ => null
    };
}

/// <summary>
/// Debounces observed source transitions. Only selects a profile; it never applies
/// hardware values or treats a selected profile as successfully applied.
/// Call Reset after suspend or loss of the observation source.
/// </summary>
public sealed class PowerSourceSelection(TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private static readonly TimeSpan StablePeriod = TimeSpan.FromSeconds(2);
    private LaptopPowerSource _candidate;
    private long _candidateSince;
    private long _lastObserved;

    public Guid? Observe(LaptopPowerSource source, PowerProfileAssignments assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        if (source is not (LaptopPowerSource.Ac or LaptopPowerSource.Battery))
        {
            Reset();
            return null;
        }
        long now = _clock.GetTimestamp();
        bool observationGap = _candidate != LaptopPowerSource.Unknown &&
            _clock.GetElapsedTime(_lastObserved, now) > TimeSpan.FromSeconds(5);
        _lastObserved = now;
        if (source != _candidate || observationGap)
        {
            _candidate = source;
            _candidateSince = now;
            return null;
        }
        if (_clock.GetElapsedTime(_candidateSince) < StablePeriod) return null;
        Guid? selected = assignments.For(source);
        return selected == Guid.Empty ? null : selected;
    }

    public void Reset() => _candidate = LaptopPowerSource.Unknown;
}
