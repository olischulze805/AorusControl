namespace AorusControl.Core.Models;

public sealed record TelemetrySnapshot(
    DateTimeOffset CapturedAt,
    ushort CpuTemperatureCelsius,
    ushort GpuTemperatureCelsius,
    ushort CpuFanRpm,
    ushort GpuFanRpm,
    ushort CpuFanDutyPercent,
    ushort GpuFanDutyPercent);
