using AorusControl.Core.Models;

namespace AorusControl.App.ViewModels;

/// <summary>
/// One selectable playback speed with its German label. The speed is a host-side time
/// scale, not Gigabyte's firmware speed byte; see research/RGB-EFFECT-INVESTIGATION.md.
/// </summary>
public sealed record KeyboardEffectSpeedChoice(KeyboardEffectSpeed Speed, string Label);
