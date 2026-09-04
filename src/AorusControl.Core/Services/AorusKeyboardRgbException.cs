namespace AorusControl.Core.Services;

public sealed class AorusKeyboardRgbException(string message, Exception? innerException = null)
    : Exception(message, innerException);
