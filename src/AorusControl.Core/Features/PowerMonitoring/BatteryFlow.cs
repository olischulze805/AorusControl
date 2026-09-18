using System.Diagnostics;
using System.Management;

namespace AorusControl.Core.Features.PowerMonitoring;

/// <summary>Which way power is moving through the battery right now.</summary>
public enum BatteryFlowDirection
{
    /// <summary>Nothing readable - no battery, no valid rate.</summary>
    Unknown,
    /// <summary>Running off the battery. <see cref="BatteryFlow.Watts"/> is the whole
    /// machine's draw: processor, graphics, screen, radios, everything.</summary>
    Discharging,
    /// <summary>On mains and filling the battery. The watts are what goes into the battery,
    /// not what the machine takes from the adapter - that figure does not exist here.</summary>
    Charging,
    /// <summary>On mains with the battery neither filling nor emptying. The adapter carries
    /// the machine and the battery rests, which is the state a charge limit produces.</summary>
    Resting
}

/// <summary>
/// The battery as a power meter. Everything here comes from ACPI through the embedded
/// controller - no graphics driver is involved, so a sleeping card stays asleep.
/// </summary>
public sealed record BatteryFlow(
    BatteryFlowDirection Direction,
    double? Watts,
    double? Percent,
    double? RemainingWattHours,
    double? FullWattHours)
{
    public static readonly BatteryFlow Unknown = new(BatteryFlowDirection.Unknown, null, null, null, null);
}

public static class BatteryFlowMath
{
    /// <summary>Windows' "rate not known" sentinel.</summary>
    private const uint RateUnknown = uint.MaxValue;

    /// <summary>
    /// Turns one BatteryStatus row into a direction and a figure.
    ///
    /// The Charging and Discharging flags are deliberately ignored: this machine reports
    /// Discharging=true while sitting on mains at a full charge, with a rate of zero. The
    /// rates and the mains flag together are the honest reading - a rate of zero on mains is
    /// a battery at rest, not a battery being emptied.
    /// </summary>
    public static BatteryFlow From(bool online, uint chargeRate, uint dischargeRate,
        uint remainingCapacity, uint fullChargedCapacity)
    {
        double? full = fullChargedCapacity is 0 or RateUnknown ? null : fullChargedCapacity / 1000.0;
        double? remaining = remainingCapacity is RateUnknown ? null : remainingCapacity / 1000.0;
        double? percent = full > 0 && remaining is { } have ? Math.Clamp(have / full.Value * 100, 0, 100) : null;

        double? charge = Rate(chargeRate);
        double? discharge = Rate(dischargeRate);

        // On mains the charge rate decides; off mains only the discharge rate can be right.
        // Trusting the other one in each case is how a machine ends up claiming it is being
        // emptied while it is plugged in.
        (BatteryFlowDirection direction, double? watts) = online
            ? charge is > 0 ? (BatteryFlowDirection.Charging, charge) : (BatteryFlowDirection.Resting, (double?)null)
            : discharge is > 0 ? (BatteryFlowDirection.Discharging, discharge) : (BatteryFlowDirection.Unknown, (double?)null);

        return new(direction, watts, percent, remaining, full);
    }

    /// <summary>
    /// Carries the previous reading across a dropout.
    ///
    /// Measured on this machine on 2026-09-15: the battery refreshes its rate every 8 to 16
    /// seconds, and one reading in that minute came back as a plain 0 - not a value, a gap.
    /// Publishing "no value" for that single sample makes the card blink between a figure and
    /// a dash for no reason the reader can see.
    ///
    /// Holding the last figure for as long as the instrument's own refresh interval claims
    /// nothing the instrument does not: between two updates, the previous number *is* the
    /// current reading. Beyond that the gap is real and the dash is right. Only a discharge
    /// is bridged - on mains a zero rate is a resting battery, which is an answer, not a gap.
    /// </summary>
    public static readonly TimeSpan BridgeWindow = TimeSpan.FromSeconds(20);

    public static BatteryFlow Bridge(BatteryFlow fresh, BatteryFlow? previous, TimeSpan age) =>
        fresh.Direction == BatteryFlowDirection.Unknown
        && previous is { Direction: BatteryFlowDirection.Discharging }
        && age <= BridgeWindow
            ? previous with { Percent = fresh.Percent, RemainingWattHours = fresh.RemainingWattHours }
            : fresh;

    /// <summary>
    /// One WMI value as an unsigned rate.
    ///
    /// The two rates in BatteryStatus are declared SInt32 and arrive as Int32, while the
    /// capacities right next to them are UInt32. A reader that accepts only uint turns every
    /// rate into "unknown" and nothing else - which is exactly what the dashboard showed on
    /// battery: a charge level, watt hours, and no watts.
    /// </summary>
    public static uint Unsigned(object? raw) => raw switch
    {
        uint value => value,
        int value and >= 0 => (uint)value,
        _ => uint.MaxValue
    };

    /// <summary>Milliwatts to watts, with the sentinel and an implausible reading refused.
    /// A 100 Wh laptop battery does not move 400 W in either direction.</summary>
    private static double? Rate(uint milliwatts) =>
        milliwatts is RateUnknown or 0 || milliwatts > 400_000 ? null : milliwatts / 1000.0;
}

/// <summary>Reads the ACPI battery through WMI. Cheap, and it never touches the GPU.</summary>
public sealed class BatteryFlowReader
{
    // The full-charge capacity is a property of the pack, not of this second, so it is read
    // once and kept; re-reading it on every dashboard tick would be a WMI query for a number
    // that changes a few times a year. The flag rather than a null check, so a machine that
    // cannot answer is not asked again every two seconds.
    private uint _fullCapacity;
    private bool _capacityRead;
    private BatteryFlow? _lastGood;
    private long _lastGoodAt;
    private BatteryFlow? _cached;
    private long _cachedAt;

    /// <summary>
    /// How long one answer stands before the pack is asked again.
    ///
    /// The dashboard ticks every two seconds, and this query costs 6 ms of the 7 ms such a
    /// tick takes in total - measured 2026-09-18. But the pack only refreshes its rate every
    /// 8 to 16 seconds, so three of every four of those queries went to fetch a number that
    /// had not changed. Five seconds is short enough to stay well inside the instrument's own
    /// interval and long enough that most ticks cost nothing at all.
    /// </summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(5);

    public BatteryFlow Read()
    {
        if (_cached is { } fresh && Stopwatch.GetElapsedTime(_cachedAt, Stopwatch.GetTimestamp()) < MinimumInterval)
            return fresh;
        try
        {
            if (!_capacityRead)
            {
                _fullCapacity = ReadFullChargedCapacity();
                _capacityRead = true;
            }
            using var searcher = new ManagementObjectSearcher(@"root\wmi",
                "SELECT Active, PowerOnline, ChargeRate, DischargeRate, RemainingCapacity FROM BatteryStatus");
            using ManagementObjectCollection rows = searcher.Get();
            foreach (ManagementBaseObject row in rows)
            {
                using (row)
                {
                    if (row["Active"] is not true) continue;
                    BatteryFlow flow = BatteryFlowMath.From(
                        Convert.ToBoolean(row["PowerOnline"]),
                        Value(row["ChargeRate"]),
                        Value(row["DischargeRate"]),
                        Value(row["RemainingCapacity"]),
                        _fullCapacity);
                    flow = BatteryFlowMath.Bridge(flow, _lastGood,
                        Stopwatch.GetElapsedTime(_lastGoodAt, Stopwatch.GetTimestamp()));
                    if (flow.Direction == BatteryFlowDirection.Discharging)
                    {
                        _lastGood = flow;
                        _lastGoodAt = Stopwatch.GetTimestamp();
                    }
                    return Keep(flow);
                }
            }
        }
        catch { /* A missing battery is a state, not a failure. */ }
        // Also held: a machine without a readable pack should not be asked twice a second
        // for an answer it has already refused.
        return Keep(BatteryFlow.Unknown);
    }

    private BatteryFlow Keep(BatteryFlow flow)
    {
        _cached = flow;
        _cachedAt = Stopwatch.GetTimestamp();
        return flow;
    }

    /// <summary>Milliwatt-hours, or 0 when the pack does not report it.</summary>
    private static uint ReadFullChargedCapacity()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi",
                "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity");
            using ManagementObjectCollection rows = searcher.Get();
            foreach (ManagementBaseObject row in rows)
                using (row)
                {
                    uint capacity = Value(row["FullChargedCapacity"]);
                    return capacity is uint.MaxValue ? 0 : capacity;
                }
        }
        catch { }
        return 0;
    }

    private static uint Value(object? raw) => BatteryFlowMath.Unsigned(raw);
}
