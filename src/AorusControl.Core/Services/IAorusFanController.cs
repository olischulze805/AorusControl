using AorusControl.Core.Models;

namespace AorusControl.Core.Services;

public interface IAorusFanController : IDisposable
{
    DeviceCompatibility CheckCompatibility();

    Task<FanControlState> ReadAsync(CancellationToken cancellationToken = default);

    Task<FanProfileChangeResult> SetNormalAsync(CancellationToken cancellationToken = default);

    Task<FanProfileChangeResult> SetQuietAsync(CancellationToken cancellationToken = default);

    Task<FanProfileChangeResult> SetGamingAsync(CancellationToken cancellationToken = default);

    Task<FanProfileChangeResult> SetMaximumAsync(CancellationToken cancellationToken = default);

    Task<FanProfileChangeResult> SetFixedAsync(
        byte rawValue,
        CancellationToken cancellationToken = default);

    Task<FanProfileChangeResult> SetDynamicAsync(CancellationToken cancellationToken = default);

    Task<FanProfileChangeResult> SetCurveAsync(
        IReadOnlyList<FanCurvePoint> curve,
        CancellationToken cancellationToken = default);

    Task<FanProfileChangeResult> RestoreAsync(
        FanControlState state,
        CancellationToken cancellationToken = default);
}
