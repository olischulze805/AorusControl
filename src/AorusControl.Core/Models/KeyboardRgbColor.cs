namespace AorusControl.Core.Models;

public readonly record struct KeyboardRgbColor(byte Red, byte Green, byte Blue)
{
    public string Hex => $"#{Red:X2}{Green:X2}{Blue:X2}";
}
