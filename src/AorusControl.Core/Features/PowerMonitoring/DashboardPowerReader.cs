using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AorusControl.Core.Features.PowerMonitoring;

public sealed record DashboardPowerReading(double? CpuPackageWatts, string GpuStatus);

/// <summary>Windows-owned telemetry only. Never opens a NVIDIA API or device handle.</summary>
public sealed class DashboardPowerReader
{
    private readonly PerformanceCounterCategory _energy = new("Energy Meter");
    private CounterSample? _previous;
    private long _previousTime;

    public DashboardPowerReading Read()
    {
        double? watts = null;
        try
        {
            CounterSample sample = _energy.ReadCategory()["Power"]["RAPL_Package0_PKG"].Sample;
            long now = Stopwatch.GetTimestamp();
            if (_previous is { } previous && Stopwatch.GetElapsedTime(_previousTime, now).TotalSeconds is >= 0.5 and <= 5)
                watts = CalculateWatts(previous, sample);
            _previous = sample;
            _previousTime = now;
        }
        catch { _previous = null; }
        // A missing CPU counter must not prevent the independent PnP status query.
        string status;
        try { status = ReadNvidiaPowerState(); }
        catch { status = "Status unbekannt"; }
        return new(watts, status);
    }

    public static double? CalculateWatts(CounterSample previous, CounterSample current)
    {
        if (current.CounterType != PerformanceCounterType.AverageCount64 || previous.CounterType != current.CounterType ||
            current.RawValue <= previous.RawValue || current.BaseValue <= previous.BaseValue)
            return null;
        double watts = CounterSample.Calculate(previous, current) / 1000.0;
        return double.IsFinite(watts) && watts > 0 && watts <= 250 ? watts : null;
    }

    public static string DescribePowerData(byte[] data)
    {
        if (data.Length < 56 || BitConverter.ToUInt32(data, 0) != 56) return "Status unbekannt";
        return BitConverter.ToUInt32(data, 4) switch
        {
            1 => "Aktiv · Windows D0",
            2 => "Energiesparen · Windows D1",
            3 => "Energiesparen · Windows D2",
            4 => "Ruhezustand · Windows D3",
            _ => "Status unbekannt"
        };
    }

    private static string ReadNvidiaPowerState()
    {
        Guid displayClass = new("4d36e968-e325-11ce-bfc1-08002be10318");
        IntPtr devices = SetupDiGetClassDevsW(ref displayClass, null, IntPtr.Zero, 2); // DIGCF_PRESENT
        if (devices == new IntPtr(-1)) return "Status unbekannt";
        try
        {
            string? result = null;
            for (uint index = 0; ; index++)
            {
                var device = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
                if (!SetupDiEnumDeviceInfo(devices, index, ref device))
                {
                    if (Marshal.GetLastWin32Error() != 259) return "Status unbekannt";
                    break;
                }
                byte[] ids = new byte[8192];
                if (!SetupDiGetDeviceRegistryPropertyW(devices, ref device, 1, out _, ids, (uint)ids.Length, out uint size))
                    return "Status unbekannt";
                if (size > ids.Length || !Encoding.Unicode.GetString(ids, 0, (int)size)
                    .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                    .Any(id => id.StartsWith("PCI\\VEN_10DE&", StringComparison.OrdinalIgnoreCase))) continue;
                if (result is not null) return "Mehrere NVIDIA-GPUs · Status unbekannt";
                byte[] power = new byte[56];
                result = SetupDiGetDeviceRegistryPropertyW(devices, ref device, 0x1E, out uint type, power, 56, out size)
                    && type == 3 && size == 56 ? DescribePowerData(power) : "Status unbekannt";
            }
            return result ?? "NVIDIA nicht erkannt";
        }
        finally { SetupDiDestroyDeviceInfoList(devices); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfo { public uint Size; public Guid ClassGuid; public uint DevInst; public UIntPtr Reserved; }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr devices, uint index, ref DeviceInfo device);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr devices, ref DeviceInfo device, uint property,
        out uint type, byte[] buffer, uint bufferSize, out uint requiredSize);
    [DllImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr devices);
}
