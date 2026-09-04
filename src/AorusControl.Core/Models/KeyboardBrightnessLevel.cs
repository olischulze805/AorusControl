namespace AorusControl.Core.Models;

/// <summary>
/// The four brightness steps the keyboard controller accepts in byte 6 of the zone
/// packet. Only these exact values work: anything else below <c>50</c> reads as off
/// and anything above <c>50</c> as full brightness. Writing one of them overrides the
/// step the user last selected with Fn+Space. Measured on firmware 19.0.4 and
/// documented in research/KEYBOARD-BRIGHTNESS.md.
/// </summary>
public enum KeyboardBrightnessLevel : byte
{
    Off = 0,
    Low = 24,
    Medium = 32,
    High = 50
}

public static class KeyboardBrightnessLevels
{
    /// <summary>The accepted raw values, ordered from off to full.</summary>
    public static IReadOnlyList<KeyboardBrightnessLevel> All { get; } =
    [
        KeyboardBrightnessLevel.Off,
        KeyboardBrightnessLevel.Low,
        KeyboardBrightnessLevel.Medium,
        KeyboardBrightnessLevel.High
    ];

    public static bool IsSupportedRawValue(byte raw) =>
        All.Any(level => (byte)level == raw);

    /// <summary>
    /// Maps a raw brightness byte to a level. Values that are not one of the four
    /// accepted ones are reported as <see cref="KeyboardBrightnessLevel.Off"/> below
    /// <c>50</c> and as <see cref="KeyboardBrightnessLevel.High"/> above it, matching
    /// the observed firmware behaviour rather than guessing an intermediate step.
    /// </summary>
    public static KeyboardBrightnessLevel FromRawValue(byte raw) => raw switch
    {
        (byte)KeyboardBrightnessLevel.Off => KeyboardBrightnessLevel.Off,
        (byte)KeyboardBrightnessLevel.Low => KeyboardBrightnessLevel.Low,
        (byte)KeyboardBrightnessLevel.Medium => KeyboardBrightnessLevel.Medium,
        _ => raw >= (byte)KeyboardBrightnessLevel.High
            ? KeyboardBrightnessLevel.High
            : KeyboardBrightnessLevel.Off
    };
}
