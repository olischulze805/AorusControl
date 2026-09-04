using AorusControl.Core.Models;

namespace AorusControl.Core.Services;

public interface IAorusTelemetryReader : IDisposable
{
    DeviceCompatibility CheckCompatibility();

    Task<TelemetrySnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
