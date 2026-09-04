using AorusControl.Core.Models;

namespace AorusControl.App.ViewModels;

/// <summary>
/// One selectable brightness step with its German label. Only the four values the
/// firmware accepts are offered; see research/KEYBOARD-BRIGHTNESS.md.
/// </summary>
public sealed record KeyboardBrightnessChoice(KeyboardBrightnessLevel Level, string Label);
