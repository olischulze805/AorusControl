using System.Globalization;
using System.Management;
using System.Security.Principal;
using AorusControl.Core.Device;
using AorusControl.Core.Models;

namespace AorusControl.Core.Services;

public sealed class GigabyteWmiBatteryChargeController : IAorusBatteryChargeController
{
    public const int MinimumCustomLimitPercent = 60;
    public const int MaximumCustomLimitPercent = 100;

    private const byte StandardPolicy = 0;
    private const byte CustomPolicy = 4;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private ManagementObject? _getterInstance;
    private ManagementObject? _setterInstance;
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
            ? "Gerät und BIOS entsprechen der geprüften Akku-Freigabeliste."
            : $"Nicht freigegeben: erkannt wurde {manufacturer} / {model} / {bios}.";

        return new DeviceCompatibility(supported, manufacturer, model, bios, message);
    }

    public async Task<BatteryChargeState> ReadAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(ReadState, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public Task<BatteryChargeChangeResult> SetCustomLimitAsync(
        int limitPercent,
        CancellationToken cancellationToken = default)
    {
        if (limitPercent is < MinimumCustomLimitPercent or > MaximumCustomLimitPercent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limitPercent),
                limitPercent,
                $"Das Akkulimit muss zwischen {MinimumCustomLimitPercent} und {MaximumCustomLimitPercent} Prozent liegen.");
        }

        return ChangeStateAsync(CustomPolicy, checked((byte)limitPercent), cancellationToken);
    }

    public Task<BatteryChargeChangeResult> SetStandardModeAsync(
        CancellationToken cancellationToken = default) =>
        ChangeStateAsync(StandardPolicy, MaximumCustomLimitPercent, cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _getterInstance?.Dispose();
        _setterInstance?.Dispose();
        _operationLock.Dispose();
        _disposed = true;
    }

    private async Task<BatteryChargeChangeResult> ChangeStateAsync(
        byte targetPolicy,
        byte targetStop,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => ChangeState(targetPolicy, targetStop),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private BatteryChargeChangeResult ChangeState(byte targetPolicy, byte targetStop)
    {
        EnsureWritePreconditions();
        BatteryChargeState original = ReadState();
        if (original.PolicyRaw is not StandardPolicy and not CustomPolicy ||
            original.StoredStopPercent is < MinimumCustomLimitPercent or > MaximumCustomLimitPercent)
        {
            throw new AorusBatteryChargeException(
                $"Unbekannter Ausgangszustand {original.PolicyRaw}+{original.StoredStopPercent}; " +
                "es wird nichts geschrieben, weil kein sicherer Rollback möglich wäre.");
        }

        try
        {
            WritePair(targetPolicy, targetStop);
            BatteryChargeState verified = ReadState();
            if (verified.PolicyRaw != targetPolicy || verified.StoredStopPercent != targetStop)
            {
                throw new AorusBatteryChargeException(
                    $"Firmware-Rücklesen stimmt nicht überein: erwartet {targetPolicy}+{targetStop}, " +
                    $"gelesen {verified.PolicyRaw}+{verified.StoredStopPercent}.");
            }

            return new BatteryChargeChangeResult(original, verified);
        }
        catch (Exception changeException)
        {
            try
            {
                WritePair(original.PolicyRaw, original.StoredStopPercent);
                BatteryChargeState restored = ReadState();
                if (restored != original)
                {
                    throw new AorusBatteryChargeException(
                        $"Rollback-Rücklesen stimmt nicht überein: erwartet {original.PolicyRaw}+{original.StoredStopPercent}, " +
                        $"gelesen {restored.PolicyRaw}+{restored.StoredStopPercent}.");
                }
            }
            catch (Exception rollbackException)
            {
                throw new AorusBatteryChargeException(
                    "Änderung und automatisches Zurückrollen des Akkulimits sind fehlgeschlagen.",
                    new AggregateException(changeException, rollbackException));
            }

            throw new AorusBatteryChargeException(
                "Änderung des Akkulimits ist fehlgeschlagen; der vorherige Zustand wurde verifiziert wiederhergestellt.",
                changeException);
        }
    }

    private BatteryChargeState ReadState()
    {
        EnsureCompatibleDevice();
        EnsureAdministrator();
        ManagementObject getter = GetOrCreateInstance(
            AorusDeviceProfile.GetterClass,
            AorusDeviceProfile.BatteryGetterMethods,
            ref _getterInstance,
            validateSetterSignature: false);
        byte policy = InvokeByteGetter(getter, "GetChargePolicy");
        byte stop = InvokeByteGetter(getter, "GetChargeStop");
        return new BatteryChargeState(policy, stop);
    }

    private void EnsureWritePreconditions()
    {
        EnsureCompatibleDevice();
        EnsureAdministrator();
        _ = GetOrCreateInstance(
            AorusDeviceProfile.SetterClass,
            AorusDeviceProfile.BatterySetterMethods,
            ref _setterInstance,
            validateSetterSignature: true);
    }

    private void WritePair(byte policy, byte stop)
    {
        if (policy is not StandardPolicy and not CustomPolicy)
        {
            throw new AorusBatteryChargeException($"Nicht freigegebener Policy-Rohwert: {policy}.");
        }

        if (stop is < MinimumCustomLimitPercent or > MaximumCustomLimitPercent)
        {
            throw new AorusBatteryChargeException($"Nicht freigegebener Stopwert: {stop}.");
        }

        ManagementObject setter = _setterInstance ?? throw new AorusBatteryChargeException(
            "Die Gigabyte-Schreibschnittstelle ist nicht geöffnet.");
        InvokeByteSetter(setter, "SetChargePolicy", policy);
        InvokeByteSetter(setter, "SetChargeStop", stop);
    }

    private static byte InvokeByteGetter(ManagementObject instance, string methodName)
    {
        if (!AorusDeviceProfile.BatteryGetterMethods.Contains(methodName))
        {
            throw new AorusBatteryChargeException($"Nicht freigegebene Lesemethode: {methodName}.");
        }

        var options = new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) };
        using ManagementBaseObject output = instance.InvokeMethod(methodName, null, options);
        return Convert.ToByte(output["Data"], CultureInfo.InvariantCulture);
    }

    private static void InvokeByteSetter(ManagementObject instance, string methodName, byte value)
    {
        if (!AorusDeviceProfile.BatterySetterMethods.Contains(methodName))
        {
            throw new AorusBatteryChargeException($"Nicht freigegebene Schreibmethode: {methodName}.");
        }

        using ManagementBaseObject input = instance.GetMethodParameters(methodName);
        input["Data"] = value;
        var options = new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) };
        using ManagementBaseObject output = instance.InvokeMethod(methodName, input, options);
    }

    private static ManagementObject GetOrCreateInstance(
        string className,
        IReadOnlySet<string> requiredMethods,
        ref ManagementObject? cachedInstance,
        bool validateSetterSignature)
    {
        if (cachedInstance is not null)
        {
            return cachedInstance;
        }

        using var managementClass = new ManagementClass(
            AorusDeviceProfile.FirmwareNamespace,
            className,
            null);
        managementClass.Get();
        var methods = managementClass.Methods.Cast<MethodData>().ToDictionary(
            method => method.Name,
            StringComparer.OrdinalIgnoreCase);
        string[] missing = requiredMethods.Where(method => !methods.ContainsKey(method)).ToArray();
        if (missing.Length > 0)
        {
            throw new AorusBatteryChargeException(
                $"Erwartete Methoden fehlen in {className}: {string.Join(", ", missing)}.");
        }

        if (validateSetterSignature)
        {
            foreach (string methodName in requiredMethods)
            {
                MethodData method = methods[methodName];
                PropertyData? data = method.InParameters?.Properties
                    .Cast<PropertyData>()
                    .FirstOrDefault(property => property.Name.Equals("Data", StringComparison.OrdinalIgnoreCase));
                if (data?.Type != CimType.UInt8)
                {
                    throw new AorusBatteryChargeException(
                        $"Unerwartete Signatur für {methodName}; erwartet wird ein UInt8-Eingang namens Data.");
                }
            }
        }

        using ManagementObjectCollection instances = managementClass.GetInstances();
        cachedInstance = instances.Cast<ManagementObject>().FirstOrDefault();
        return cachedInstance ?? throw new AorusBatteryChargeException(
            $"Die WMI-Klasse {className} besitzt keine aktive Geräteinstanz.");
    }

    private void EnsureCompatibleDevice()
    {
        DeviceCompatibility compatibility = CheckCompatibility();
        if (!compatibility.IsSupported)
        {
            throw new AorusBatteryChargeException(compatibility.Message);
        }
    }

    private static void EnsureAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            throw new AorusBatteryChargeException(
                "Windows-Administratorrechte sind für die Akku-Firmwaresteuerung erforderlich.");
        }
    }

    private static string QueryFirstValue(string query, string property)
    {
        using var searcher = new ManagementObjectSearcher(@"root\cimv2", query);
        using ManagementObjectCollection results = searcher.Get();
        using ManagementObject? item = results.Cast<ManagementObject>().FirstOrDefault();
        return Convert.ToString(item?[property], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
