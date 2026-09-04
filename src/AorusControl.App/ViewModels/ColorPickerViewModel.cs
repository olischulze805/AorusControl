using System.Windows.Input;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using AorusControl.App.Infrastructure;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Models;

namespace AorusControl.App.ViewModels;

/// <summary>One remembered color, exposed pre-converted to a frozen brush so the
/// swatch list never re-creates brushes on every render pass.</summary>
public sealed class RecentColorViewModel(KeyboardRgbColor color)
{
    public KeyboardRgbColor Color { get; } = color;
    public MediaBrush Brush { get; } = CreateBrush(color);
    private static MediaBrush CreateBrush(KeyboardRgbColor color)
    {
        var brush = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(color.Red, color.Green, color.Blue));
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// Backs the custom HSV color picker (gradient square + hue bar + hex box + recent
/// colors) that replaced the Windows Forms ColorDialog. All three representations
/// (Hue/Saturation/Value, RGB, hex text) are kept in sync from whichever one the user
/// just touched, without feedback loops: each setter recomputes the other two directly
/// rather than through property-changed round-trips.
/// </summary>
public sealed class ColorPickerViewModel : ObservableObject
{
    private readonly IRecentColorsStore _recentColorsStore;
    private double _hue;
    private double _saturation = 1;
    private double _value = 1;
    private string _hexText = "#00FF00";
    private bool _updatingHex;

    public ColorPickerViewModel(KeyboardRgbColor initial, IRecentColorsStore recentColorsStore)
    {
        _recentColorsStore = recentColorsStore;
        SetFromRgb(initial);
        foreach (KeyboardRgbColor color in _recentColorsStore.Load())
            RecentColors.Add(new RecentColorViewModel(color));
        SelectRecentCommand = new RelayCommand<RecentColorViewModel>(recent =>
        {
            if (recent is not null) SetFromRgb(recent.Color);
        });
    }

    public double Hue
    {
        get => _hue;
        set { if (SetProperty(ref _hue, value)) OnHsvChanged(); }
    }

    public double Saturation
    {
        get => _saturation;
        set { if (SetProperty(ref _saturation, Math.Clamp(value, 0, 1))) OnHsvChanged(); }
    }

    public double Value
    {
        get => _value;
        set { if (SetProperty(ref _value, Math.Clamp(value, 0, 1))) OnHsvChanged(); }
    }

    /// <summary>Free-typed hex text; invalid/incomplete text is left as-is (not
    /// reformatted or rejected mid-typing) and simply does not update the preview
    /// until it parses.</summary>
    public string HexText
    {
        get => _hexText;
        set
        {
            if (!SetProperty(ref _hexText, value)) return;
            if (_updatingHex) return;
            if (TryParseHex(value, out KeyboardRgbColor color)) ApplyRgb(color, updateHex: false);
        }
    }

    public KeyboardRgbColor CurrentColor => new HsvColor(Hue, Saturation, Value).ToRgb();
    public MediaBrush PreviewBrush => ToBrush(CurrentColor);
    public MediaBrush HueTrackBrush => ToBrush(new HsvColor(Hue, 1, 1).ToRgb());

    public System.Collections.ObjectModel.ObservableCollection<RecentColorViewModel> RecentColors { get; } = new();
    public ICommand SelectRecentCommand { get; }

    /// <summary>Called once the user confirms the dialog; pushes the final color to the
    /// front of the recent list (capped, de-duplicated) and persists it. Not called on
    /// every drag, since that would mean a disk write per mouse-move event.</summary>
    public void CommitToRecentColors()
    {
        KeyboardRgbColor chosen = CurrentColor;
        var updated = new List<KeyboardRgbColor> { chosen };
        updated.AddRange(RecentColors.Select(r => r.Color).Where(c => c != chosen));
        try { _recentColorsStore.Save(updated); }
        catch { /* Convenience list only; never block closing the picker over this. */ }
    }

    private void SetFromRgb(KeyboardRgbColor color) => ApplyRgb(color, updateHex: true);

    private void ApplyRgb(KeyboardRgbColor color, bool updateHex)
    {
        HsvColor hsv = HsvColor.FromRgb(color);
        _hue = hsv.Hue;
        _saturation = hsv.Saturation;
        _value = hsv.Value;
        OnPropertyChanged(nameof(Hue));
        OnPropertyChanged(nameof(Saturation));
        OnPropertyChanged(nameof(Value));
        RaiseColorChanged();
        if (updateHex) SetHexTextInternal(color.Hex);
    }

    private void OnHsvChanged()
    {
        RaiseColorChanged();
        SetHexTextInternal(CurrentColor.Hex);
    }

    private void RaiseColorChanged()
    {
        OnPropertyChanged(nameof(CurrentColor));
        OnPropertyChanged(nameof(PreviewBrush));
        OnPropertyChanged(nameof(HueTrackBrush));
    }

    private void SetHexTextInternal(string hex)
    {
        _updatingHex = true;
        HexText = hex;
        _updatingHex = false;
    }

    private static bool TryParseHex(string? hex, out KeyboardRgbColor color)
    {
        color = default;
        if (hex is not { Length: 7 } || hex[0] != '#') return false;
        if (!byte.TryParse(hex.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r)) return false;
        if (!byte.TryParse(hex.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g)) return false;
        if (!byte.TryParse(hex.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b)) return false;
        color = new KeyboardRgbColor(r, g, b);
        return true;
    }

    private static MediaBrush ToBrush(KeyboardRgbColor color)
    {
        var brush = new System.Windows.Media.SolidColorBrush(MediaColor.FromRgb(color.Red, color.Green, color.Blue));
        brush.Freeze();
        return brush;
    }
}
