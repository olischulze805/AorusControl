namespace AorusControl.Core.Services;

public sealed class AorusBatteryChargeException : Exception
{
    public AorusBatteryChargeException(string message)
        : base(message)
    {
    }

    public AorusBatteryChargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
