using System.IO.Pipes;
using System.Security.Principal;

namespace AorusControl.Core.Features.Worker;

/// <summary>
/// Thin named-pipe client shared by every caller that talks to the hardware worker.
/// One request per connection, matching the worker's serve loop. Never retries a
/// write silently: a caller that needs a renewed lease decides that for itself.
/// </summary>
public static class WorkerClient
{
    public static string PipeName(string? testSuffix = null)
    {
        string sid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("Benutzerkennung fehlt.");
        string name = "AorusControl.Worker.v1." + sid;
        return testSuffix is null ? name : name + ".Test." + testSuffix;
    }

    public static async Task<WorkerResponse> SendAsync(
        WorkerRequest request,
        TimeSpan timeout,
        string? testSuffix = null,
        CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var client = new NamedPipeClientStream(
            ".", PipeName(testSuffix), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await client.ConnectAsync(deadline.Token).ConfigureAwait(false);
            await WorkerProtocol.WriteAsync(client, request, deadline.Token).ConfigureAwait(false);
            WorkerResponse response = await WorkerProtocol.ReadAsync<WorkerResponse>(client, deadline.Token).ConfigureAwait(false);
            if (response.Version != WorkerProtocol.Version || response.RequestId != request.RequestId)
                throw new InvalidDataException("Antwort gehört nicht zur Anfrage.");
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Der Hardware-Worker hat nicht rechtzeitig geantwortet. Läuft er?");
        }
    }

    /// <summary>
    /// True if a worker is already listening on this user's pipe.
    ///
    /// This deliberately only LOOKS at the pipe namespace and never connects. The worker
    /// serves one connection at a time, so a probe that connects and then sends nothing
    /// occupies its accept slot: the worker sits in its read waiting for a request that
    /// will never arrive, and the next real request blocks behind it until the caller's
    /// own timeout fires. That cost a five-second "worker did not answer in time" on the
    /// first Fixed attempt after every launch - the probe was starving the request it was
    /// meant to prepare for. Enumerating \\.\pipe\ has no such side effect.
    /// </summary>
    public static bool IsRunning(string? testSuffix = null)
    {
        string name = PipeName(testSuffix);
        try
        {
            foreach (string pipe in Directory.EnumerateFiles(@"\\.\pipe\"))
            {
                if (string.Equals(Path.GetFileName(pipe), name, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Some machines carry pipe names the enumerator chokes on; a direct existence
            // check is the fallback and still does not open a connection.
            return File.Exists(@"\\.\pipe\" + name);
        }
    }
}
