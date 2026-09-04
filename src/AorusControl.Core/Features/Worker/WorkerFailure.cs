using System.Management;

namespace AorusControl.Core.Features.Worker;

/// <summary>Stable client-facing errors; internal exception details stay out of IPC.</summary>
public sealed record WorkerFailure(string Code, string Message)
{
    public static WorkerFailure FromException(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            if (current is UnauthorizedAccessException ||
                current is ManagementException { ErrorCode: ManagementStatus.AccessDenied } ||
                current.HResult is unchecked((int)0x80070005) or unchecked((int)0x80041003))
                return new("access_denied", "Windows verweigert den Hardwarezugriff. Der Hardware-Worker benötigt einen entsprechend berechtigten Start; die Oberfläche muss dafür nicht dauerhaft als Administrator laufen.");
            if (current is OperationCanceledException or TimeoutException)
                return new("timeout", "Die Geräteabfrage wurde abgebrochen oder hat das Zeitlimit überschritten. Es liegt keine bestätigte Messung vor.");
        }
        return new("device_read_failed", "Die Geräteabfrage ist fehlgeschlagen. Details können mit der lokalen Worker-Diagnose geprüft werden.");
    }
}
