using System.Runtime.InteropServices;

namespace AorusControl.Core.Features.PowerMonitoring;

/// <summary>
/// Reads the NVIDIA card's power draw from NVML, the management library the driver ships in
/// System32. It is the only NVIDIA interface in this app, and the caller decides when it may
/// run: <see cref="DashboardPowerReader"/> calls it on AC power only, and only while Windows
/// already reports the card in D0.
///
/// The library is loaded lazily and only on that first permitted read, so a machine on
/// battery never opens it at all. <see cref="Close"/> gives it back the moment the card
/// leaves D0, so nothing of ours holds a driver handle across a sleep transition.
/// </summary>
public sealed class NvidiaWattReader : IDisposable
{
    private const int Success = 0;
    private bool _initialised;
    private bool _unavailable;
    private IntPtr _device;

    /// <summary>The card's current draw in watts, or null if it cannot be read.</summary>
    public double? ReadWatts()
    {
        if (_unavailable) return null;
        try
        {
            if (!_initialised)
            {
                if (nvmlInit_v2() != Success) { _unavailable = true; return null; }
                _initialised = true;
                // Index 0: this machine has exactly one NVIDIA card, and a second one would
                // already have stopped DashboardPowerReader before it got here.
                if (nvmlDeviceGetHandleByIndex_v2(0, out _device) != Success) { Close(); _unavailable = true; return null; }
            }
            if (nvmlDeviceGetPowerUsage(_device, out uint milliwatts) != Success) return null;
            double watts = milliwatts / 1000.0;
            // A laptop 3070 draws about 20 W idle and up to 130 W under its enforced limit.
            // Anything outside this says the reading is not a power figure.
            return watts is > 0 and <= 250 ? watts : null;
        }
        catch (DllNotFoundException) { _unavailable = true; return null; }
        catch (EntryPointNotFoundException) { _unavailable = true; return null; }
        catch { return null; }
    }

    /// <summary>Releases NVML. Safe to call repeatedly and when it was never initialised.</summary>
    public void Close()
    {
        if (!_initialised) return;
        _initialised = false;
        _device = IntPtr.Zero;
        try { nvmlShutdown(); } catch { }
    }

    public void Dispose() => Close();

    [DllImport("nvml.dll")] private static extern int nvmlInit_v2();
    [DllImport("nvml.dll")] private static extern int nvmlShutdown();
    [DllImport("nvml.dll")] private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);
    [DllImport("nvml.dll")] private static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint milliwatts);
}
