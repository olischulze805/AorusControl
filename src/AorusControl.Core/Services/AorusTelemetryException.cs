namespace AorusControl.Core.Services;

public sealed class AorusTelemetryException : Exception
{
    public AorusTelemetryException(string message)
        : base(message)
    {
    }

    public AorusTelemetryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
