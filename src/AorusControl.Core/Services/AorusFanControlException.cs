namespace AorusControl.Core.Services;

public sealed class AorusFanControlException : Exception
{
    public AorusFanControlException(string message)
        : base(message)
    {
    }

    public AorusFanControlException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
