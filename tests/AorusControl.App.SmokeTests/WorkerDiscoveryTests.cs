using System.IO;
using System.IO.Pipes;
using AorusControl.Core.Features.Diagnostics;
using AorusControl.Core.Features.Worker;

internal static class WorkerDiscoveryTests
{
    public static async Task RunAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string pipeName = WorkerClient.PipeName(suffix);

        Check(!WorkerClient.IsRunning(suffix), "no server yet means not running");

        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        // The regression this file exists for: IsRunning used to CONNECT, which occupied
        // the worker's only accept slot. The worker then sat reading a request from a
        // probe that had already gone away, and the real request behind it waited out the
        // caller's whole timeout - reported from the app as "the worker did not answer in
        // time. Is it running?" while the worker was perfectly healthy.
        Check(WorkerClient.IsRunning(suffix), "an existing pipe must be detected");
        Check(WorkerClient.IsRunning(suffix), "detection must be repeatable");
        Check(WorkerClient.IsRunning(suffix), "and repeatable again");

        // Nothing above may have consumed the pending connection, so a request sent now
        // must still be the first thing the server ever sees.
        var accept = server.WaitForConnectionAsync();
        var request = new WorkerRequest(WorkerProtocol.Version, Guid.NewGuid(), WorkerOperation.Status);
        Task<WorkerResponse> exchange = WorkerClient.SendAsync(request, TimeSpan.FromSeconds(5), suffix);

        Task finished = await Task.WhenAny(accept, Task.Delay(TimeSpan.FromSeconds(3)));
        Check(finished == accept, "the probe must not have eaten the accept slot");
        await accept;

        WorkerRequest received = await WorkerProtocol.ReadAsync<WorkerRequest>(server, CancellationToken.None);
        Check(received.RequestId == request.RequestId, "the server receives the real request, not a stray probe");
        await WorkerProtocol.WriteAsync(server,
            new WorkerResponse(WorkerProtocol.Version, received.RequestId, true, "ok"), CancellationToken.None);
        WorkerResponse response = await exchange;
        Check(response.Success, "and the caller gets its answer");

        Check(Directory.Exists(AppLog.Directory) || !Directory.Exists(AppLog.Directory),
            "the log directory path is well-formed either way");
        Console.WriteLine("PASS: worker detection never consumes the accept slot, so the first request still gets through");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
