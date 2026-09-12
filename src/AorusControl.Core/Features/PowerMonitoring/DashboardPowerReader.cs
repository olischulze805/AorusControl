using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AorusControl.Core.Features.PowerProfiles;

namespace AorusControl.Core.Features.PowerMonitoring;

public sealed record DashboardPowerReading(double? CpuPackageWatts, string GpuStatus, double? GpuWatts = null);

/// <summary>
/// CPU watts and the card's device state come from Windows alone. The card's own watt figure
/// comes from NVML, under two conditions checked on every single read: the laptop is on AC,
/// and Windows already reports the card in D0.
///
/// On battery nothing here touches NVIDIA at all, so a sleeping card stays asleep and the
/// dashboard keeps showing its device state instead of a number.
/// </summary>
public sealed class DashboardPowerReader : IDisposable
{
    private readonly PerformanceCounterCategory _energy = new("Energy Meter");
    private readonly Func<LaptopPowerSource> _readPowerSource;
    private readonly NvidiaWattReader _gpu = new();
    private CounterSample? _previous;
    private long _previousTime;

    public DashboardPowerReader(Func<LaptopPowerSource>? readPowerSource = null) =>
        _readPowerSource = readPowerSource ?? ReadWindowsPowerSource;

    /// <summary>
    /// Whether a watt reading is allowed right now. D0 on its own would not be enough: the
    /// card can fall asleep between this check and the NVML call, and repeated reads can keep
    /// it awake. On battery that would spend exactly the power the GPU switching exists to
    /// save, so AC is the second half of the rule - there a card held awake costs runtime
    /// nobody is counting.
    /// </summary>
    public static bool MayReadGpuWatts(LaptopPowerSource source, uint deviceState) =>
        source == LaptopPowerSource.Ac && deviceState == 1;

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
        uint state = 0;
        string status;
        try { (state, status) = ReadNvidiaPowerState(); }
        catch { status = "Status unbekannt"; }

        double? gpuWatts = null;
        try
        {
            if (MayReadGpuWatts(_readPowerSource(), state)) gpuWatts = _gpu.ReadWatts();
            else _gpu.Close();
        }
        catch { gpuWatts = null; }
        return new(watts, status, gpuWatts);
    }

    /// <summary>
    /// Hands the NVIDIA library back without shutting this reader down. The dashboard calls
    /// it the moment it stops showing power, so nothing of ours holds a driver handle while
    /// the app waits in the tray.
    /// </summary>
    public void ReleaseGpu() => _gpu.Close();

    public void Dispose() => _gpu.Dispose();

    private static LaptopPowerSource ReadWindowsPowerSource() =>
        GetSystemPowerStatus(out SystemPowerStatus status)
            ? LaptopPowerSources.FromWindowsStatus(status.AcLineStatus)
            : LaptopPowerSource.Unknown;

    public static double? CalculateWatts(CounterSample previous, CounterSample current)
    {
        if (current.CounterType != PerformanceCounterType.AverageCount64 || previous.CounterType != current.CounterType ||
            current.RawValue <= previous.RawValue || current.BaseValue <= previous.BaseValue)
            return null;
        double watts = CounterSample.Calculate(previous, current) / 1000.0;
        return double.IsFinite(watts) && watts > 0 && watts <= 250 ? watts : null;
    }

    public static string DescribePowerData(byte[] data) => DecodePowerData(data).Status;

    /// <summary>The raw D-state next to its wording; 0 means Windows gave no usable answer.</summary>
    public static (uint State, string Status) DecodePowerData(byte[] data)
    {
        if (data.Length < 56 || BitConverter.ToUInt32(data, 0) != 56) return (0, "Status unbekannt");
        uint state = BitConverter.ToUInt32(data, 4);
        return (state is >= 1 and <= 4 ? state : 0, state switch
        {
            1 => "Aktiv · Windows D0",
            2 => "Energiesparen · Windows D1",
            3 => "Energiesparen · Windows D2",
            4 => "Ruhezustand · Windows D3",
            _ => "Status unbekannt"
        });
    }

    private static (uint State, string Status) ReadNvidiaPowerState()
    {
        Guid displayClass = new("4d36e968-e325-11ce-bfc1-08002be10318");
        IntPtr devices = SetupDiGetClassDevsW(ref displayClass, null, IntPtr.Zero, 2); // DIGCF_PRESENT
        if (devices == new IntPtr(-1)) return (0, "Status unbekannt");
        try
        {
            (uint State, string Status)? result = null;
            for (uint index = 0; ; index++)
            {
                var device = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
                if (!SetupDiEnumDeviceInfo(devices, index, ref device))
                {
                    if (Marshal.GetLastWin32Error() != 259) return (0, "Status unbekannt");
                    break;
                }
                byte[] ids = new byte[8192];
                if (!SetupDiGetDeviceRegistryPropertyW(devices, ref device, 1, out _, ids, (uint)ids.Length, out uint size))
                    return (0, "Status unbekannt");
                if (size > ids.Length || !Encoding.Unicode.GetString(ids, 0, (int)size)
                    .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                    .Any(id => id.StartsWith("PCI\\VEN_10DE&", StringComparison.OrdinalIgnoreCase))) continue;
                // A second card would make NVML's "index 0" ambiguous, so this stops before
                // any watt reading rather than guessing which card the dashboard means.
                if (result is not null) return (0, "Mehrere NVIDIA-GPUs · Status unbekannt");
                byte[] power = new byte[56];
                result = SetupDiGetDeviceRegistryPropertyW(devices, ref device, 0x1E, out uint type, power, 56, out size)
                    && type == 3 && size == 56 ? DecodePowerData(power) : (0, "Status unbekannt");
            }
            return result ?? (0, "NVIDIA nicht erkannt");
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}
