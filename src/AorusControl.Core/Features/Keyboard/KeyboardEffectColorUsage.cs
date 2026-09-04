namespace AorusControl.Core.Features.Keyboard;

/// <summary>
/// How much of the stored three-zone palette a lighting mode actually reads. See
/// <see cref="KeyboardEffectFrames.ColorUsage"/> - and note that the stored colours are
/// never lost either way: an effect that ignores them still leaves them saved, and they
/// apply again the moment manual mode is selected.
/// </summary>
public enum KeyboardEffectColorUsage
{
    /// <summary>Manual mode: each zone shows its own stored colour.</summary>
    AllZones,

    /// <summary>Zone 1's colour is the effect's base; zones 2 and 3 are not read.</summary>
    BaseColorOnly,

    /// <summary>The effect brings its own palette; no stored colour is read.</summary>
    None
}
