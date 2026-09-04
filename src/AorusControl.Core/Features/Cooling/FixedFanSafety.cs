using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Cooling;

public static class FixedFanSafety
{
    public static void Validate(TelemetrySnapshot sample, DateTimeOffset now)
    {
        TimeSpan age = now - sample.CapturedAt;
        if (age < TimeSpan.FromSeconds(-1) || age > TimeSpan.FromSeconds(5))
            throw new InvalidOperationException("Temperaturmessung veraltet oder Zeitstempel ungültig.");
        if (sample.CpuTemperatureCelsius is 0 or >= 65 || sample.GpuTemperatureCelsius is 0 or >= 65)
            throw new InvalidOperationException("Fixed ist nur bei gültigen Temperaturen von 1–64 °C freigegeben; ab 65 °C auf Normal zurückstellen.");
    }
}
