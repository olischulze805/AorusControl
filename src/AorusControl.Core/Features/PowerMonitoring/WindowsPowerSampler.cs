using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AorusControl.Core.Features.PowerMonitoring;

/// <summary>
/// Read-only, on-demand sampler. No NVML/nvidia-smi calls or process enumeration.
/// Call at least one second apart from a single worker and dispose when no longer needed.
/// One shared GPU category snapshot per sample avoids repeated provider queries.
/// </summary>
public sealed class WindowsPowerSampler : IDisposable
{
    private readonly PerformanceCounterCategory _gpuCategory = new("GPU Engine");
    private Dictionary<string, CounterSample> _previousGpu = new(StringComparer.Ordinal);
    private long? _lastGpuRead;
    private static readonly Regex Identity = new(
        @"luid_(?<id>0x[0-9a-f]+_0x[0-9a-f]+)_phys_(?<physical>\d+)_eng_(?<engine>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private CpuTimes? _previousCpu;
    private bool _disposed;

    public PowerSnapshot Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var clock = Stopwatch.StartNew();
        var notes = new List<string>();
        double? discharge = ReadBattery(notes);
        double? cpu = ReadCpu(notes);
        IReadOnlyList<GpuActivity> gpus = ReadGpu(notes);
        return new(DateTimeOffset.Now, discharge, cpu, gpus, notes, clock.Elapsed);
    }

    private static double? ReadBattery(List<string> notes)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi",
                "SELECT PowerOnline, Discharging, DischargeRate, Active FROM BatteryStatus");
            using ManagementObjectCollection rows = searcher.Get();
            foreach (ManagementBaseObject row in rows)
            {
                using (row)
                {
                    if (row["Active"] is not true) continue;
                    bool online = Convert.ToBoolean(row["PowerOnline"]);
                    bool discharging = Convert.ToBoolean(row["Discharging"]);
                    uint rate = row["DischargeRate"] is uint value ? value : uint.MaxValue;
                    double? watts = PowerSampleMath.DischargeWatts(online, discharging, rate);
                    if (watts is null)
                        notes.Add(online ? "Netzbetrieb: Akkuentladung nicht verfügbar." : "Keine gültige Akkuentladerate.");
                    return watts;
                }
            }
            notes.Add("Kein aktiver Akku-Messdatensatz.");
        }
        catch (Exception exception) { notes.Add($"Akku-Messfehler: {exception.Message}"); }
        return null;
    }

    private double? ReadCpu(List<string> notes)
    {
        if (!GetSystemTimes(out ulong idle, out ulong kernel, out ulong user))
        {
            _previousCpu = null;
            notes.Add($"CPU-Messfehler: Windows {Marshal.GetLastWin32Error()}.");
            return null;
        }
        var current = new CpuTimes(idle, kernel, user);
        double? result = _previousCpu is { } previous ? PowerSampleMath.CpuUsage(previous, current) : null;
        _previousCpu = current;
        if (result is null) notes.Add("CPU: Basisprobe, noch kein gültiges Messintervall.");
        return result;
    }

    private IReadOnlyList<GpuActivity> ReadGpu(List<string> notes)
    {
        try
        {
            // ReadCategory fetches the provider once, not once per process/engine counter.
            InstanceDataCollection instances = _gpuCategory.ReadCategory()["Utilization Percentage"]
                ?? throw new InvalidOperationException("GPU-Auslastungszähler fehlt.");
            long now = Stopwatch.GetTimestamp();
            bool intervalReady = _lastGpuRead is { } last && Stopwatch.GetElapsedTime(last, now).TotalSeconds >= 1;
            var current = new Dictionary<string, CounterSample>(StringComparer.Ordinal);
            var engines = new Dictionary<(string Adapter, string Engine), double>();
            var adapters = new HashSet<string>();
            var incomplete = new HashSet<string>();
            foreach (string instance in instances.Keys)
            {
                Match identity = Identity.Match(instance);
                if (!identity.Success) continue;
                string adapter = identity.Groups["id"].Value + "/" + identity.Groups["physical"].Value;
                adapters.Add(adapter);
                CounterSample sample = instances[instance].Sample;
                current.Add(instance, sample);
                if (!intervalReady || !_previousGpu.TryGetValue(instance, out CounterSample previous) ||
                    sample.RawValue < previous.RawValue || sample.TimeStamp100nSec <= previous.TimeStamp100nSec)
                {
                    incomplete.Add(adapter);
                    continue;
                }
                float value = CounterSample.Calculate(previous, sample);
                if (!float.IsFinite(value) || value < 0) { incomplete.Add(adapter); continue; }
                var key = (adapter, identity.Groups["engine"].Value);
                engines[key] = engines.GetValueOrDefault(key) + value;
            }
            _previousGpu = current;
            _lastGpuRead = now;
            if (adapters.Count == 0) notes.Add("Keine GPU-Aktivitätszähler verfügbar; Zustand unbekannt.");
            var result = new List<GpuActivity>();
            foreach (string adapter in adapters.Order())
            {
                double? busy = incomplete.Contains(adapter) ? null
                    : engines.Where(x => x.Key.Adapter == adapter).Select(x => Math.Clamp(x.Value, 0, 100)).DefaultIfEmpty().Max();
                result.Add(new(adapter, busy));
                if (busy is null) notes.Add($"GPU {adapter}: Basisprobe oder unvollständiges Messintervall.");
            }
            return result;
        }
        catch (Exception exception)
        {
            _previousGpu.Clear();
            _lastGpuRead = null;
            notes.Add($"GPU-Messfehler: {exception.Message}");
            return [];
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _previousGpu.Clear();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
}
