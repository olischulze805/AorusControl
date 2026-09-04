using System.IO.Pipes;
using System.Security.Principal;
using AorusControl.Core.Features.Cooling;
using AorusControl.Core.Features.Diagnostics;
using AorusControl.Core.Features.Worker;
using AorusControl.Core.Services;

// Development host, not yet an installed Windows service or autostart entry.
string sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Benutzerkennung fehlt.");
bool isolatedTest = args.Length == 2 && args[0] == "--serve-test" && Guid.TryParseExact(args[1], "N", out _);
string? testSuffix = isolatedTest ? args[1] : null;
string pipeName = WorkerClient.PipeName(testSuffix);
bool serve = args.Length > 0 && (args[0] == "--serve" || isolatedTest);
bool isAcquireFixed = args.Length == 2 && args[0] == "--acquire-fixed" && byte.TryParse(args[1], out _);
if (!isolatedTest && !isAcquireFixed && (args.Length != 1 || args[0] is not ("--serve" or "--status" or "--telemetry" or "--fan-status" or "--diagnose" or "--diagnose-report")))
{
    Console.Error.WriteLine("Verwendung: --serve | --status | --telemetry | --fan-status | --acquire-fixed <raw> | --diagnose. Noch kein installierter Dienst.");
    return 2;
}

AppLog.Initialize("worker");

// Everything the worker says goes to the log as well as the console: it runs elevated
// with no visible window in normal use, so the console alone left failures untraceable.
void Report(string message, Exception? error = null)
{
    if (error is null) { Console.WriteLine(message); AppLog.Info("worker", message); }
    else { Console.Error.WriteLine(message + " " + error.Message); AppLog.Error("worker", message, error); }
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
if (args[0] is "--diagnose" or "--diagnose-report")
{
    var report = new System.Text.StringBuilder("# Worker-Zugriffsdiagnose\n\nNur lesende Geräteabfragen.\n\n");
    void Log(string message) { Console.WriteLine(message); report.AppendLine(message); }
    using var identity = WindowsIdentity.GetCurrent();
    Log("Zeit: " + DateTimeOffset.Now.ToString("O"));
    Log("Administrator-Token: " + new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator));
    using var diagnosticReader = new GigabyteWmiTelemetryReader();
    try
    {
        Log(System.Text.Json.JsonSerializer.Serialize(diagnosticReader.CheckCompatibility()));
        Log(System.Text.Json.JsonSerializer.Serialize(await diagnosticReader.ReadAsync(cancellation.Token)));
        return 0;
    }
    catch (Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            Log($"{current.GetType().Name} (0x{current.HResult:X8}): {current.Message}");
        return 1;
    }
    finally
    {
        if (args[0] == "--diagnose-report")
        {
            string directory = Path.Combine(Environment.CurrentDirectory, "research", "runs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"worker-access-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.md");
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var output = new StreamWriter(file);
            output.Write(report.ToString());
        }
    }
}
if (isAcquireFixed)
{
    // Diagnostic only: acquires a Fixed lease and exits immediately without ever
    // renewing or releasing it, deliberately simulating a client that crashes the
    // instant after acquiring. Proves the worker's own supervisor - not this process -
    // is what eventually restores Normal.
    try
    {
        WorkerResponse response = await WorkerClient.SendAsync(
            new WorkerRequest(WorkerProtocol.Version, Guid.NewGuid(), WorkerOperation.AcquireFixedFan, FixedRawValue: byte.Parse(args[1])),
            TimeSpan.FromSeconds(10),
            cancellationToken: cancellation.Token);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(response));
        return response.Success ? 0 : 1;
    }
    catch (Exception error)
    {
        Console.Error.WriteLine("Worker-Anfrage fehlgeschlagen: " + error.Message);
        return 1;
    }
}
if (!serve)
{
    WorkerOperation operation = args[0] switch
    {
        "--status" => WorkerOperation.Status,
        "--fan-status" => WorkerOperation.ReadFanStatus,
        _ => WorkerOperation.ReadTelemetry
    };
    try
    {
        WorkerResponse response = await WorkerClient.SendAsync(
            new WorkerRequest(WorkerProtocol.Version, Guid.NewGuid(), operation),
            TimeSpan.FromSeconds(10),
            cancellationToken: cancellation.Token);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(response));
        return response.Success ? 0 : 1;
    }
    catch (Exception error)
    {
        Console.Error.WriteLine("Worker-Anfrage fehlgeschlagen: " + error.Message);
        return 1;
    }
}

using var reader = new GigabyteWmiTelemetryReader();
using var fans = new GigabyteWmiFanController();
var supervisor = new FanSafetySupervisor(fans, reader);
// Runs independently of any connected client: if the caller who acquired Fixed mode
// disappears (crash, kill, disconnect), the lease still expires and this loop restores
// Normal on its own within one lease period, with no cooperation from that caller.
Task supervisorTask = supervisor.RunAsync(cancellation.Token);
try
{
    // FirstPipeInstance prevents a second host from becoming another server.
    using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.FirstPipeInstance);
    Report("Hardware-Worker bereit (Fixed-Lüfter über Lease, sonst nur lesend). Strg+C beendet ihn.");
    while (!cancellation.IsCancellationRequested)
    {
        await server.WaitForConnectionAsync(cancellation.Token);
        WorkerRequest? received = null;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            // A caller that has connected must send its request promptly; only the
            // handling itself may take a while. Keeping the read on a short leash means a
            // connection that never sends anything cannot hold the single accept slot for
            // as long as the caller's own timeout, which would look like a hung worker.
            deadline.CancelAfter(TimeSpan.FromSeconds(2));
            received = await WorkerProtocol.ReadAsync<WorkerRequest>(server, deadline.Token);
            WorkerProtocol.Validate(received);
            using var handling = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            handling.CancelAfter(TimeSpan.FromSeconds(5));
            WorkerResponse response = await HandleAsync(received, reader, supervisor, handling.Token);
            await WorkerProtocol.WriteAsync(server, response, handling.Token);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or System.Text.Json.JsonException or OperationCanceledException)
        {
            Report("Anfrage verworfen: " + error.Message, error);
            // Answer rejected requests instead of just disconnecting. A silent
            // disconnect is indistinguishable from a hang at the other end, so a
            // version or field mismatch used to surface as a timeout with nothing to
            // go on; a real response names the problem.
            if (received is { RequestId: var id } && id != Guid.Empty && server.IsConnected)
            {
                try
                {
                    using var replyDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
                    replyDeadline.CancelAfter(TimeSpan.FromSeconds(2));
                    await WorkerProtocol.WriteAsync(
                        server,
                        new WorkerResponse(WorkerProtocol.Version, id, false, "Anfrage abgelehnt: " + error.Message, null, "rejected_request"),
                        replyDeadline.Token);
                }
                catch (Exception replyError) { Report("Ablehnung konnte nicht gesendet werden.", replyError); }
            }
        }
        finally { if (server.IsConnected) server.Disconnect(); }
    }
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
catch (IOException error)
{
    Report("Worker konnte nicht gestartet/fortgesetzt werden.", error);
    return 1;
}
finally
{
    cancellation.Cancel();
    try { await supervisorTask; } catch (OperationCanceledException) { }
}
return 0;

static async Task<WorkerResponse> HandleAsync(
    WorkerRequest request,
    GigabyteWmiTelemetryReader reader,
    FanSafetySupervisor supervisor,
    CancellationToken cancellationToken)
{
    try
    {
        switch (request.Operation)
        {
            case WorkerOperation.Status:
                return new(1, request.RequestId, true, "Worker bereit; Fixed-Lüfter über zeitlich begrenzte Freigabe, sonst nur lesend.", null);

            case WorkerOperation.ReadTelemetry:
                // Compatibility gate remains local to the worker, not trusted to a client.
                var compatibility = reader.CheckCompatibility();
                return compatibility.IsSupported
                    ? new(1, request.RequestId, true, "Telemetrie gelesen", await reader.ReadAsync(cancellationToken))
                    : new(1, request.RequestId, false, compatibility.Message, null, "unsupported_device");

            case WorkerOperation.AcquireFixedFan:
                Guid lease = await supervisor.AcquireFixedAsync(request.FixedRawValue!.Value);
                return new(1, request.RequestId, true, "Fixed-Freigabe erteilt.", Lease: lease);

            case WorkerOperation.RenewFixedFan:
                await supervisor.RenewAsync(request.Lease!.Value);
                return new(1, request.RequestId, true, "Freigabe verlängert.", Lease: request.Lease);

            case WorkerOperation.ReleaseFixedFan:
                await supervisor.ReleaseAsync(request.Lease!.Value);
                return new(1, request.RequestId, true, "Freigabe beendet; Normal wiederhergestellt.");

            case WorkerOperation.ReadFanStatus:
                FanSafetyStatus status = await supervisor.ReadStatusAsync();
                return new(1, request.RequestId, true, status.Message, Lease: status.Lease, FanRequiresRestoration: status.RequiresRestoration);

            default:
                return new(1, request.RequestId, false, "Unbekannte Operation.", null, "unknown_operation");
        }
    }
    catch (Exception error)
    {
        // FanSafetySupervisor's own exceptions already carry safe, curated German text
        // describing the lease/business-rule problem; pass it through directly instead
        // of the generic hardware-failure mapping used for raw WMI/HID exceptions.
        if (error is InvalidOperationException or ArgumentOutOfRangeException)
            return new(1, request.RequestId, false, error.Message, null, "fan_operation_failed");
        WorkerFailure failure = WorkerFailure.FromException(error);
        return new(1, request.RequestId, false, failure.Message, null, failure.Code);
    }
}
