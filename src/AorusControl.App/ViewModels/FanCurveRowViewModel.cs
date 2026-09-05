using AorusControl.App.Infrastructure;
using AorusControl.Core.Features.Cooling;

namespace AorusControl.App.ViewModels;

public sealed class FanCurveRowViewModel(int number) : ObservableObject
{
    private string _temperature = "", _value = "";
    private int _number = number;

    /// <summary>Its position in the curve, from one. Settable because handles can now be added
    /// and removed, so a point's place is not fixed for its lifetime any more.</summary>
    public int Number { get => _number; set => SetProperty(ref _number, value); }

    // Text preserves invalid/incomplete user input until explicit validation on save.
    public string Temperature
    {
        get => _temperature;
        set { if (SetProperty(ref _temperature, value)) OnPropertyChanged(nameof(TemperatureNumber)); }
    }

    public string Value
    {
        get => _value;
        set { if (SetProperty(ref _value, value)) OnPropertyChanged(nameof(Percent)); }
    }

    /// <summary>Numeric view of <see cref="Temperature"/> for the draggable curve chart,
    /// which always drags to a whole degree - unlike the text property, this never needs
    /// to represent an in-progress or invalid typed value.</summary>
    public double TemperatureNumber
    {
        get => byte.TryParse(_temperature, out byte t) ? t : 0;
        set => Temperature = ((byte)Math.Clamp(Math.Round(value), 0, 255)).ToString();
    }

    /// <summary>0-100% view of <see cref="Value"/> for the chart - see FanSpeedPercent for
    /// why the tested floor (raw 57) is 25%, not 0%.</summary>
    public int Percent
    {
        get => byte.TryParse(_value, out byte raw) ? FanSpeedPercent.ToPercent(raw) : 0;
        set => Value = FanSpeedPercent.ToRaw(value).ToString();
    }
}
