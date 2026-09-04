using AorusControl.Core.Models;

namespace AorusControl.App.ViewModels;

/// <summary>One selectable lighting effect with its German label, for the same
/// SelectedValuePath/ItemTemplate binding pattern used by brightness and speed.</summary>
public sealed record KeyboardEffectChoice(KeyboardRgbEffect Effect, string Label);
