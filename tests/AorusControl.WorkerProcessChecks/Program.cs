using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using AorusControl.Core.Features.Worker;

string workerPath = Path.GetFullPath("src/AorusControl.Worker/bin/Debug/net10.0-windows/AorusControl.Worker.exe");
string id = Guid.NewGuid().ToString("N");
string pipe = "AorusControl.Worker.v1." + WindowsIdentity.GetCurrent().User!.Value + ".Test." + id;
var start = new ProcessStartInfo(workerPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
start.ArgumentList.Add("--serve-test"); start.ArgumentList.Add(id);
using var child = Process.Start(start) ?? throw new Exception("Worker did not start");
Task<string> stdout = child.StandardOutput.ReadToEndAsync(), stderr = child.StandardError.ReadToEndAsync();
var report = new StringBuilder($"# Worker process IPC checks\n\nStarted: {DateTimeOffset.Now:O}\n\nIsolated test pipe. Status only; no telemetry or hardware writes.\n\n");
try
{
    await Status();
    await Bad("oversized payload", async client => await client.WriteAsync(BitConverter.GetBytes(WorkerProtocol.MaximumPayloadBytes + 1)));
    await Bad("unknown operation", client => WorkerProtocol.WriteAsync(client, new WorkerRequest(1, Guid.NewGuid(), (WorkerOperation)999), default));
    await Bad("missing request identity", client => WorkerProtocol.WriteAsync(client, new WorkerRequest(1, Guid.Empty, WorkerOperation.Status), default));
    await Bad("unknown JSON field", client => WorkerProtocol.WriteAsync(client, new { Version = 1, RequestId = Guid.NewGuid(), Operation = 1, Command = "not-allowed" }, default));
    await Bad("idle connected client timeout", _ => Task.CompletedTask);
    using (var client = await Connect()) { await client.WriteAsync(new byte[] { 1, 0 }); }
    await Status(); report.AppendLine("PASS: disconnected partial header; next status succeeds.");
    Console.WriteLine(report.ToString());
}
catch (Exception error)
{
    report.AppendLine("FAILED: " + error.GetType().Name + ": " + error.Message);
    throw;
}
finally
{
    // Only the exact child started above; it received no hardware-changing request.
    if (!child.HasExited) child.Kill();
    await child.WaitForExitAsync();
    report.AppendLine("Owned test worker stopped. No production worker targeted.");
    report.AppendLine("\nServer output:\n```\n" + await stdout + await stderr + "\n```");
    Directory.CreateDirectory("research/runs");
    string path = Path.GetFullPath($"research/runs/worker-ipc-{DateTime.Now:yyyyMMdd-HHmmss}-{id}.md");
    using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
    using var writer = new StreamWriter(file);
    writer.Write(report.ToString());
    Console.WriteLine(path);
}

async Task<NamedPipeClientStream> Connect()
{
    var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    try { await client.ConnectAsync(8000); return client; }
    catch { client.Dispose(); throw; }
}

async Task Status()
{
    using var client = await Connect();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
    var request = new WorkerRequest(1, Guid.NewGuid(), WorkerOperation.Status);
    await WorkerProtocol.WriteAsync(client, request, timeout.Token);
    WorkerResponse response = await WorkerProtocol.ReadAsync<WorkerResponse>(client, timeout.Token);
    if (!response.Success || response.RequestId != request.RequestId || response.Version != 1)
        throw new Exception("Status response invalid");
}

async Task Bad(string name, Func<NamedPipeClientStream, Task> write)
{
    using (var client = await Connect())
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await write(client);
        try
        {
            int count = await client.ReadAsync(new byte[1], timeout.Token);
            if (count != 0) throw new Exception("Malformed request produced data instead of disconnection");
        }
        catch (IOException) { } // Windows may report pipe closure as EOF or broken pipe.
    }
    await Status();
    report.AppendLine("PASS: " + name + "; connection rejected, next status succeeds.");
}
