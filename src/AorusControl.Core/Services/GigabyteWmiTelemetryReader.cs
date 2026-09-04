using System.Globalization;
using System.Management;
using AorusControl.Core.Device;
using AorusControl.Core.Models;

namespace AorusControl.Core.Services;

public sealed class GigabyteWmiTelemetryReader : IAorusTelemetryReader
{
    private readonly SemaphoreSlim _readLock = new(1, 1);
    private ManagementObject? _instance;
    private bool _disposed;

    public DeviceCompatibility CheckCompatibility()
    {
        ThrowIfDisposed();

        string manufacturer = QueryFirstValue(
            "SELECT Manufacturer FROM Win32_ComputerSystem",
            "Manufacturer");
        string model = QueryFirstValue(
            "SELECT Model FROM Win32_ComputerSystem",
            "Model");
        string bios = QueryFirstValue(
            "SELECT SMBIOSBIOSVersion FROM Win32_BIOS",
            "SMBIOSBIOSVersion");

        bool supported =
            manufacturer.Equals(AorusDeviceProfile.ExpectedManufacturer, StringComparison.OrdinalIgnoreCase) &&
            model.Equals(AorusDeviceProfile.ExpectedModel, StringComparison.OrdinalIgnoreCase) &&
            bios.Equals(AorusDeviceProfile.ExpectedBios, StringComparison.OrdinalIgnoreCase);

        string message = supported
            ? "Gerät und BIOS entsprechen der geprüften Freigabeliste."
            : $"Nicht freigegeben: erkannt wurde {manufacturer} / {model} / {bios}.";

        return new DeviceCompatibility(supported, manufacturer, model, bios, message);
    }

    public async Task<TelemetrySnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(ReadSnapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _readLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _instance?.Dispose();
        _readLock.Dispose();
        _disposed = true;
    }

    private TelemetrySnapshot ReadSnapshot()
    {
        DeviceCompatibility compatibility = CheckCompatibility();
        if (!compatibility.IsSupported)
        {
            throw new AorusTelemetryException(compatibility.Message);
        }

        ManagementObject instance = GetOrCreateInstance();
        // Date the oldest value, not completion of the last native getter.
        // A delayed RPM/duty read must not make earlier temperatures look fresh.
        DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
        ushort cpuTemperature = InvokeUInt16Getter(instance, "getCpuTemp");
        ushort gpuTemperature = InvokeUInt16Getter(instance, "getGpuTemp1");
        ushort cpuRpm = SwapBytes(InvokeUInt16Getter(instance, "getRpm1"));
        ushort gpuRpm = SwapBytes(InvokeUInt16Getter(instance, "getRpm2"));
        ushort cpuDuty = InvokeUInt16Getter(instance, "GetCPUFanDuty");
        ushort gpuDuty = InvokeUInt16Getter(instance, "GetGPUFanDuty");

        ValidateRange("CPU-Temperatur", cpuTemperature, 0, 120);
        ValidateRange("GPU-Temperatur", gpuTemperature, 0, 120);
        ValidateRange("CPU-Lüfter", cpuRpm, 0, 10000);
        ValidateRange("GPU-Lüfter", gpuRpm, 0, 10000);
        ValidateRange("CPU-Lüfterrohwert", cpuDuty, 0, 229);
        ValidateRange("GPU-Lüfterrohwert", gpuDuty, 0, 229);

        return new TelemetrySnapshot(
            capturedAt,
            cpuTemperature,
            gpuTemperature,
            cpuRpm,
            gpuRpm,
            cpuDuty,
            gpuDuty);
    }

    private ManagementObject GetOrCreateInstance()
    {
        if (_instance is not null)
        {
            return _instance;
        }

        try
        {
            using var getClass = new ManagementClass(
                AorusDeviceProfile.FirmwareNamespace,
                AorusDeviceProfile.GetterClass,
                null);
            getClass.Get();

            var availableMethods = getClass.Methods
                .Cast<MethodData>()
                .Select(method => method.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string[] missingMethods = AorusDeviceProfile.LiveTelemetryMethods
                .Where(method => !availableMethods.Contains(method))
                .OrderBy(method => method, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (missingMethods.Length > 0)
            {
                throw new AorusTelemetryException(
                    $"Erwartete Lesemethoden fehlen: {string.Join(", ", missingMethods)}");
            }

            using ManagementObjectCollection instances = getClass.GetInstances();
            _instance = instances.Cast<ManagementObject>().FirstOrDefault();
            return _instance ?? throw new AorusTelemetryException(
                "Die Gigabyte-WMI-Klasse besitzt keine aktive Geräteinstanz.");
        }
        catch (AorusTelemetryException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new AorusTelemetryException(
                "Die Gigabyte-WMI-Telemetrie konnte nicht geöffnet werden.",
                exception);
        }
    }

    private static ushort InvokeUInt16Getter(ManagementObject instance, string methodName)
    {
        if (!AorusDeviceProfile.LiveTelemetryMethods.Contains(methodName))
        {
            throw new AorusTelemetryException($"Nicht freigegebene Methode: {methodName}");
        }

        try
        {
            var options = new InvokeMethodOptions
            {
                Timeout = TimeSpan.FromSeconds(2)
            };
            using ManagementBaseObject output = instance.InvokeMethod(methodName, null, options);
            return Convert.ToUInt16(output["Data"], CultureInfo.InvariantCulture);
        }
        catch (Exception exception)
        {
            throw new AorusTelemetryException(
                $"Lesefehler bei {methodName}.",
                exception);
        }
    }

    private static string QueryFirstValue(string query, string property)
    {
        using var searcher = new ManagementObjectSearcher(@"root\cimv2", query);
        using ManagementObjectCollection results = searcher.Get();
        using ManagementObject? item = results.Cast<ManagementObject>().FirstOrDefault();
        return Convert.ToString(item?[property], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static ushort SwapBytes(ushort value) =>
        (ushort)((value >> 8) | (value << 8));

    private static void ValidateRange(string name, ushort value, ushort minimum, ushort maximum)
    {
        if (value < minimum || value > maximum)
        {
            throw new AorusTelemetryException(
                $"Ungültiger Messwert für {name}: {value} (erwartet {minimum}–{maximum}).");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
