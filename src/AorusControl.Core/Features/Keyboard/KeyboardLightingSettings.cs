using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Keyboard;

/// <summary>User intent, not the transient colors currently rendered by an animation.</summary>
public sealed record KeyboardLightingSettings(
    bool Enabled,
    KeyboardBrightnessLevel OnBrightness,
    KeyboardRgbEffect? Effect,
    KeyboardEffectSpeed Speed,
    KeyboardRgbColor Left,
    KeyboardRgbColor Center,
    KeyboardRgbColor Right)
{
    public KeyboardLightingSettings WithBrightness(KeyboardBrightnessLevel level)
    {
        if (!KeyboardBrightnessLevels.All.Contains(level)) throw new ArgumentOutOfRangeException(nameof(level));
        return level == KeyboardBrightnessLevel.Off
            ? this with { Enabled = false }
            : this with { Enabled = true, OnBrightness = level };
    }

    public KeyboardLightingSettings WithColor(int zone, KeyboardRgbColor color, bool allZones) => allZones
        ? this with { Left = color, Center = color, Right = color }
        : zone switch
        {
            1 => this with { Left = color },
            2 => this with { Center = color },
            3 => this with { Right = color },
            _ => throw new ArgumentOutOfRangeException(nameof(zone))
        };

    public KeyboardRgbState ToHardwareState()
    {
        Validate();
        byte brightness = Enabled ? (byte)OnBrightness : (byte)0;
        return new(new KeyboardRgbZoneState[] { new(1, Left, brightness), new(2, Center, brightness), new(3, Right, brightness) });
    }

    public void Validate()
    {
        if (OnBrightness == KeyboardBrightnessLevel.Off || !KeyboardBrightnessLevels.All.Contains(OnBrightness))
            throw new ArgumentOutOfRangeException(nameof(OnBrightness));
        if (!Enum.IsDefined(Speed)) throw new ArgumentOutOfRangeException(nameof(Speed));
        if (Effect is { } effect && !Enum.IsDefined(effect)) throw new ArgumentOutOfRangeException(nameof(Effect));
    }

    public static KeyboardLightingSettings FromHardware(KeyboardRgbState state) => new(
        state.IsEnabled,
        state.Brightness == KeyboardBrightnessLevel.Off ? KeyboardBrightnessLevel.High : state.Brightness,
        null, KeyboardEffectSpeed.Normal,
        state.GetZone(1).Color, state.GetZone(2).Color, state.GetZone(3).Color);
}
