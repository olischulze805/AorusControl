using AorusControl.Core.Models;

namespace AorusControl.Core.Services;

public interface IAorusBatteryChargeController : IDisposable
{
    DeviceCompatibility CheckCompatibility();

    Task<BatteryChargeState> ReadAsync(CancellationToken cancellationToken = default);

    Task<BatteryChargeChangeResult> SetCustomLimitAsync(
        int limitPercent,
        CancellationToken cancellationToken = default);

    Task<BatteryChargeChangeResult> SetStandardModeAsync(
        CancellationToken cancellationToken = default);
}
