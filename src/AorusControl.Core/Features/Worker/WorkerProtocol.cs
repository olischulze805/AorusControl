using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Worker;

// Fan writes are gated behind FanSafetySupervisor inside the worker; the pipe carries
// only lease-scoped intents, never raw hardware commands, and Fixed is the only mode
// requiring a live watchdog. Normal/Quiet/Gaming/Maximum stay adaptive firmware modes
// applied directly and need no lease.
public enum WorkerOperation
{
    Status = 1,
    ReadTelemetry = 2,
    AcquireFixedFan = 3,
    RenewFixedFan = 4,
    ReleaseFixedFan = 5,
    ReadFanStatus = 6
}

public sealed record WorkerRequest(
    int Version,
    Guid RequestId,
    WorkerOperation Operation,
    byte? FixedRawValue = null,
    Guid? Lease = null);

public sealed record WorkerResponse(
    int Version,
    Guid RequestId,
    bool Success,
    string Message,
    TelemetrySnapshot? Telemetry = null,
    string? ErrorCode = null,
    Guid? Lease = null,
    bool? FanRequiresRestoration = null);

public static class WorkerProtocol
{
    public const int Version = 1;
    public const int MaximumPayloadBytes = 16 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        byte[] prefix = new byte[4];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length is <= 0 or > MaximumPayloadBytes) throw new InvalidDataException("Ungültige Nachrichtengröße.");
        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(payload, Options) ?? throw new InvalidDataException("Leere Nachricht.");
    }

    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, Options);
        if (payload.Length > MaximumPayloadBytes) throw new InvalidDataException("Nachricht zu groß.");
        byte[] prefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static void Validate(WorkerRequest request)
    {
        if (request.Version != Version || request.RequestId == Guid.Empty || !Enum.IsDefined(request.Operation))
            throw new InvalidDataException("Unbekannte Protokollversion, Anfrage oder Operation.");
        if (request.Operation == WorkerOperation.AcquireFixedFan && request.FixedRawValue is null)
            throw new InvalidDataException("AcquireFixedFan erfordert einen Rohwert.");
        bool needsLease = request.Operation is WorkerOperation.RenewFixedFan or WorkerOperation.ReleaseFixedFan;
        if (needsLease && (request.Lease is null || request.Lease.Value == Guid.Empty))
            throw new InvalidDataException("Diese Operation erfordert eine gültige Freigabe.");
    }
}
