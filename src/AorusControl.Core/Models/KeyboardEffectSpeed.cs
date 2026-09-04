namespace AorusControl.Core.Models;

/// <summary>
/// Playback speed for the host-rendered effects. This is deliberately not Gigabyte's
/// firmware speed byte: that byte belongs to the global effect command, which renders
/// nothing on firmware 19.0.4 (see research/RGB-EFFECT-INVESTIGATION.md). Because the
/// animation is produced on the host, the speed is a plain time scale and is not
/// limited to the nine discrete values the firmware exposed.
/// </summary>
public enum KeyboardEffectSpeed
{
    VerySlow,
    Slow,
    Normal,
    Fast,
    VeryFast
}

public static class KeyboardEffectSpeeds
{
    public static IReadOnlyList<KeyboardEffectSpeed> All { get; } =
    [
        KeyboardEffectSpeed.VerySlow,
        KeyboardEffectSpeed.Slow,
        KeyboardEffectSpeed.Normal,
        KeyboardEffectSpeed.Fast,
        KeyboardEffectSpeed.VeryFast
    ];

    /// <summary>
    /// The factor applied to elapsed time. <c>Normal</c> is 1.0, so the existing effect
    /// timings stay exactly as they were verified by the owner.
    /// </summary>
    public static double ToTimeScale(this KeyboardEffectSpeed speed) => speed switch
    {
        KeyboardEffectSpeed.VerySlow => 0.25,
        KeyboardEffectSpeed.Slow => 0.5,
        KeyboardEffectSpeed.Fast => 2.0,
        KeyboardEffectSpeed.VeryFast => 4.0,
        _ => 1.0
    };
}
