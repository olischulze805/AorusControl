using AorusControl.Core.Features.Diagnostics;

namespace AorusControl.Core.Features.Worker;

/// <summary>
/// Talks to the out-of-process hardware worker. This is the only implementation that
/// actually delivers the crash guarantee: the worker's own <c>FanSafetySupervisor</c>
/// keeps running and expires the lease even if this process disappears mid-renewal.
/// </summary>
public sealed class WorkerFixedFanLeaseClient(TimeSpan? timeout = null) : IFixedFanLeaseClient
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(5);

    public async Task<Guid> AcquireAsync(byte rawValue, CancellationToken cancellationToken = default)
    {
        // Ensuring a backing worker process is this implementation's own concern: it is
        // the only one that needs one. A caller programming against IFixedFanLeaseClient
        // must not need to know or care that a worker process is involved at all.
        if (!await WorkerLauncher.EnsureRunningAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Hardware-Worker konnte nicht gestartet werden; Fixed ist ohne ihn nicht sicher.");
        WorkerResponse response = await SendAsync(
            new WorkerRequest(WorkerProtocol.Version, Guid.NewGuid(), WorkerOperation.AcquireFixedFan, FixedRawValue: rawValue),
            cancellationToken).ConfigureAwait(false);
        return response.Lease ?? throw new InvalidOperationException("Worker hat keine Freigabe geliefert.");
    }

    public Task RenewAsync(Guid lease, CancellationToken cancellationToken = default) =>
        SendAsync(new WorkerRequest(WorkerProtocol.Version, Guid.NewGuid(), WorkerOperation.RenewFixedFan, Lease: lease), cancellationToken);

    public Task ReleaseAsync(Guid lease, CancellationToken cancellationToken = default) =>
        SendAsync(new WorkerRequest(WorkerProtocol.Version, Guid.NewGuid(), WorkerOperation.ReleaseFixedFan, Lease: lease), cancellationToken);

    private async Task<WorkerResponse> SendAsync(WorkerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            WorkerResponse response = await WorkerClient.SendAsync(request, _timeout, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!response.Success)
            {
                AppLog.Error("worker", $"{request.Operation} abgelehnt: {response.Message} ({response.ErrorCode})");
                throw new InvalidOperationException(response.Message);
            }

            AppLog.Info("worker", $"{request.Operation} erfolgreich.");
            return response;
        }
        catch (Exception error) when (error is not InvalidOperationException)
        {
            // A transport failure (no worker, timeout, broken pipe) says nothing about
            // the request itself, so record which operation was in flight.
            AppLog.Error("worker", $"{request.Operation} nicht zustellbar.", error);
            throw;
        }
    }
}
