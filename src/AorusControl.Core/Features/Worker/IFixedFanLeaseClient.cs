namespace AorusControl.Core.Features.Worker;

/// <summary>
/// Client-facing surface for Fixed-mode fan control. The implementation owns where the
/// actual <c>FanSafetySupervisor</c> lease lives; callers only see acquire/renew/release.
/// Production code must use an implementation whose supervisor survives the caller's own
/// crash, or this guarantee is worthless. See <see cref="WorkerFixedFanLeaseClient"/>.
/// </summary>
public interface IFixedFanLeaseClient
{
    Task<Guid> AcquireAsync(byte rawValue, CancellationToken cancellationToken = default);

    Task RenewAsync(Guid lease, CancellationToken cancellationToken = default);

    Task ReleaseAsync(Guid lease, CancellationToken cancellationToken = default);
}
