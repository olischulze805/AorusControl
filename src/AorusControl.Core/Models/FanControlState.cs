namespace AorusControl.Core.Models;

public sealed record FanControlState(
    ushort FixedStatusRaw,
    ushort StepStatusRaw,
    byte AutoStatusRaw,
    byte NvidiaThermalTargetRaw,
    ushort FixedSpeedRaw,
    byte GpuDutyRaw,
    IReadOnlyList<FanCurvePoint> Curve);
