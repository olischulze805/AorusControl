namespace AorusControl.Core.Features.PowerMonitoring;

public sealed record PowerSnapshot(
    DateTimeOffset CapturedAt,
    double? BatteryDischargeWatts,
    double? CpuPercent,
    IReadOnlyList<GpuActivity> Gpus,
    IReadOnlyList<string> Notes,
    TimeSpan SamplingDuration);

// An adapter ID is deliberately not labelled Intel/NVIDIA without an actual mapping.
public sealed record GpuActivity(string AdapterId, double? BusiestEnginePercent);

public readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);

public static class PowerSampleMath
{
    public static double? CpuUsage(CpuTimes previous, CpuTimes current)
    {
        if (current.Idle < previous.Idle || current.Kernel < previous.Kernel || current.User < previous.User)
            return null;
        double total = (double)(current.Kernel - previous.Kernel) + (current.User - previous.User);
        double idle = current.Idle - previous.Idle;
        return total <= 0 || idle > total ? null : Math.Clamp((total - idle) / total * 100, 0, 100);
    }

    public static double? DischargeWatts(bool online, bool discharging, uint rate) =>
        online || !discharging || rate == uint.MaxValue || rate == 0 ? null : rate / 1000.0;
}
