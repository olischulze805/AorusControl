using System.IO;
using AorusControl.Core.Features.Worker;

internal static class WorkerProtocolTests
{
    public static async Task RunAsync()
    {
        var request = new WorkerRequest(1, Guid.NewGuid(), WorkerOperation.Status);
        using var stream = new MemoryStream();
        await WorkerProtocol.WriteAsync(stream, request, default);
        stream.Position = 0;
        var copy = await WorkerProtocol.ReadAsync<WorkerRequest>(stream, default);
        if (copy != request) throw new Exception("IPC roundtrip failed");
        WorkerProtocol.Validate(copy);
        foreach (var invalid in new[] { request with { Version = 2 }, request with { RequestId = Guid.Empty }, request with { Operation = (WorkerOperation)999 } })
        {
            try { WorkerProtocol.Validate(invalid); throw new Exception("Invalid request accepted"); }
            catch (InvalidDataException) { }
        }
        foreach (int size in new[] { -1, 0, WorkerProtocol.MaximumPayloadBytes + 1 })
        {
            using var bad = new MemoryStream(BitConverter.GetBytes(size));
            try { await WorkerProtocol.ReadAsync<WorkerRequest>(bad, default); throw new Exception("Invalid length accepted"); }
            catch (InvalidDataException) { }
        }
        using var truncated = new MemoryStream(new byte[] { 10, 0, 0, 0, 123 });
        try { await WorkerProtocol.ReadAsync<WorkerRequest>(truncated, default); throw new Exception("Truncated message accepted"); }
        catch (EndOfStreamException) { }
        using var unknown = new MemoryStream();
        await WorkerProtocol.WriteAsync(unknown, new { Version = 1, RequestId = Guid.NewGuid(), Operation = 1, Shell = "forbidden" }, default);
        unknown.Position = 0;
        try { await WorkerProtocol.ReadAsync<WorkerRequest>(unknown, default); throw new Exception("Unknown field accepted"); }
        catch (System.Text.Json.JsonException) { }
        Console.WriteLine("PASS: worker protocol roundtrip, version/operation/id, length bounds, truncation and unknown fields");
        if (WorkerFailure.FromException(new Exception("wrapper", new UnauthorizedAccessException())).Code != "access_denied")
            throw new Exception("Nested access denial not classified");
        if (WorkerFailure.FromException(new TimeoutException()).Code != "timeout")
            throw new Exception("Timeout not classified");
        var generic = WorkerFailure.FromException(new Exception("private path or provider details"));
        if (generic.Code != "device_read_failed" || generic.Message.Contains("private path"))
            throw new Exception("Internal exception exposed through IPC");
        Console.WriteLine("PASS: worker access denial, timeout and bounded public error messages");
    }
}
