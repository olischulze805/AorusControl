using AorusControl.App.Infrastructure;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.Cooling;
using AorusControl.Core.Models;

namespace AorusControl.App.Features.Cooling;

/// <summary>
/// What the two fans are doing right now, in the numbers the cooling page draws its rotors
/// and its live marker from.
///
/// It exists as its own small model rather than as six more strings on the shell because the
/// cooling page needs the values, not the sentences: a rotor turns at an RPM, a tint follows
/// a temperature, and the marker on the curve sits at a coordinate. The shell's dashboard
/// keeps its formatted text; this keeps the measurements.
///
/// Everything is fed from the same telemetry read the rest of the app already does, so the
/// live feedback costs no extra hardware access at all.
/// </summary>
public sealed class FanLiveViewModel : ObservableObject
{
    private bool _live;
    private double _cpuRpm, _gpuRpm, _cpuTemperature = double.NaN, _gpuTemperature = double.NaN;

    /// <summary>Whether these numbers come from a reading that actually happened. Everything
    /// on the page reads this rather than guessing from a zero: a stopped fan and an unread
    /// fan look nothing alike, and must not.</summary>
    public bool IsLive
    {
        get => _live;
        private set { if (SetProperty(ref _live, value)) Announce(); }
    }

    public double CpuRpm { get => _cpuRpm; private set => SetProperty(ref _cpuRpm, value); }
    public double GpuRpm { get => _gpuRpm; private set => SetProperty(ref _gpuRpm, value); }
    public double CpuTemperature { get => _cpuTemperature; private set => SetProperty(ref _cpuTemperature, value); }
    public double GpuTemperature { get => _gpuTemperature; private set => SetProperty(ref _gpuTemperature, value); }

    /// <summary>How hard each fan is working, as a share of the firmware's own maximum. Drawn
    /// as the arc around the rotor: the speed says how fast it turns, this says how much of
    /// what it could do is being used.</summary>
    public double CpuDuty { get => _cpuDuty; private set => SetProperty(ref _cpuDuty, value); }
    public double GpuDuty { get => _gpuDuty; private set => SetProperty(ref _gpuDuty, value); }

    private double _cpuDuty, _gpuDuty;

    public string CpuRpmText => Speed(_cpuRpm);
    public string GpuRpmText => Speed(_gpuRpm);
    public string CpuTemperatureText => Degrees(_cpuTemperature);
    public string GpuTemperatureText => Degrees(_gpuTemperature);
    public string CpuDutyText => Duty(_cpuDuty);
    public string GpuDutyText => Duty(_gpuDuty);

    /// <summary>Where the live marker sits on the curve: the temperature the fans are really
    /// answering to is the higher of the two, and the duty is the harder-working fan.</summary>
    public double MarkerTemperature => IsLive ? Math.Max(_cpuTemperature, _gpuTemperature) : double.NaN;
    public double MarkerPercent => IsLive ? Math.Max(_cpuDuty, _gpuDuty) : double.NaN;

    public void Update(TelemetrySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        CpuRpm = snapshot.CpuFanRpm;
        GpuRpm = snapshot.GpuFanRpm;
        CpuTemperature = snapshot.CpuTemperatureCelsius;
        GpuTemperature = snapshot.GpuTemperatureCelsius;
        CpuDuty = FanSpeedPercent.ToPercent((byte)Math.Min(snapshot.CpuFanDutyPercent, (ushort)255));
        GpuDuty = FanSpeedPercent.ToPercent((byte)Math.Min(snapshot.GpuFanDutyPercent, (ushort)255));
        IsLive = true;
        Announce();
    }

    /// <summary>Monitoring stopped or a read failed. The last numbers stay on screen but stop
    /// claiming to be current, and the rotors come to a halt.</summary>
    public void MarkStale() => IsLive = false;

    private void Announce()
    {
        foreach (string derived in Derived) OnPropertyChanged(derived);
    }

    private static readonly string[] Derived =
    [
        nameof(CpuRpmText), nameof(GpuRpmText), nameof(CpuTemperatureText), nameof(GpuTemperatureText),
        nameof(CpuDutyText), nameof(GpuDutyText), nameof(MarkerTemperature), nameof(MarkerPercent)
    ];

    private string Speed(double rpm) =>
        !IsLive ? "– U/min" : rpm < 1 ? "steht" : $"{rpm:N0} U/min";

    private string Degrees(double celsius) =>
        !IsLive || double.IsNaN(celsius) ? "– °C" : $"{celsius:N0} °C";

    private string Duty(double percent) => IsLive ? $"{percent:N0} % Leistung" : "– % Leistung";
}
