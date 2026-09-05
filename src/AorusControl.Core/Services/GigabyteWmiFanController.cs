using System.Globalization;
using System.Management;
using System.Security.Principal;
using AorusControl.Core.Device;
using AorusControl.Core.Models;

namespace AorusControl.Core.Services;

public sealed class GigabyteWmiFanController : IAorusFanController
{
    /// <summary>
    /// Fans off is a legitimate fixed value on this device: raw 0 was measured as both stored
    /// and driven, with both fans reporting 0 RPM
    /// (research/runs/fan-floor-rpm-test-20260905-135015.md), and the vendor's own Quiet profile
    /// does the same. What keeps it safe is not a floor here but the worker's lease: it refuses
    /// to hold any fixed value at 65 °C and restores Normal on its own.
    /// </summary>
    public const byte MinimumFixedRaw = 0;
    public const byte MaximumFixedRaw = 229;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private ManagementObject? _getterInstance;
    private ManagementObject? _setterInstance;
    private bool _disposed;

    public DeviceCompatibility CheckCompatibility()
    {
        ThrowIfDisposed();
        string manufacturer = QueryFirstValue("SELECT Manufacturer FROM Win32_ComputerSystem", "Manufacturer");
        string model = QueryFirstValue("SELECT Model FROM Win32_ComputerSystem", "Model");
        string bios = QueryFirstValue("SELECT SMBIOSBIOSVersion FROM Win32_BIOS", "SMBIOSBIOSVersion");
        bool supported =
            manufacturer.Equals(AorusDeviceProfile.ExpectedManufacturer, StringComparison.OrdinalIgnoreCase) &&
            model.Equals(AorusDeviceProfile.ExpectedModel, StringComparison.OrdinalIgnoreCase) &&
            bios.Equals(AorusDeviceProfile.ExpectedBios, StringComparison.OrdinalIgnoreCase);
        return new DeviceCompatibility(
            supported,
            manufacturer,
            model,
            bios,
            supported
                ? "Gerät und BIOS entsprechen der geprüften Lüfter-Freigabeliste."
                : $"Nicht freigegeben: erkannt wurde {manufacturer} / {model} / {bios}.");
    }

    public async Task<FanControlState> ReadAsync(CancellationToken cancellationToken = default)
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

    public async Task<FanProfileChangeResult> SetNormalAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => ChangeProfile("Normal", fixedStatus: 0, stepStatus: 0, autoStatus: 0, thermalTarget: 0),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<FanProfileChangeResult> SetQuietAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => ChangeProfile("Quiet", fixedStatus: 0, stepStatus: 0, autoStatus: 0, thermalTarget: 1),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<FanProfileChangeResult> SetGamingAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => ChangeProfile("Gaming", fixedStatus: 0, stepStatus: 0, autoStatus: 1, thermalTarget: 0),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<FanProfileChangeResult> SetMaximumAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => ChangeProfile(
                    "Maximum",
                    fixedStatus: 1,
                    stepStatus: 1,
                    autoStatus: 0,
                    thermalTarget: 0,
                    fixedSpeed: 229,
                    gpuDuty: 229),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<FanProfileChangeResult> SetFixedAsync(
        byte rawValue,
        CancellationToken cancellationToken = default)
    {
        if (rawValue > MaximumFixedRaw)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawValue),
                rawValue,
                $"Der Fixed-Modus erlaubt Rohwerte {MinimumFixedRaw}–{MaximumFixedRaw}.");
        }

        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => ChangeProfile(
                    $"Fixed {rawValue}",
                    fixedStatus: 1,
                    stepStatus: 1,
                    autoStatus: 0,
                    thermalTarget: 0,
                    fixedSpeed: rawValue,
                    gpuDuty: rawValue),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<FanProfileChangeResult> SetDynamicAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => ChangeProfile(
                    "Dynamic",
                    fixedStatus: 0,
                    stepStatus: 1,
                    autoStatus: 0,
                    thermalTarget: 0),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<FanProfileChangeResult> SetCurveAsync(
        IReadOnlyList<FanCurvePoint> curve,
        CancellationToken cancellationToken = default)
    {
        ValidateCurve(curve);
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => ChangeCurve(curve), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<FanProfileChangeResult> RestoreAsync(
        FanControlState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => RestoreExactState(state), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

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

    private FanProfileChangeResult ChangeProfile(
        string profileName,
        byte fixedStatus,
        byte stepStatus,
        byte autoStatus,
        byte thermalTarget,
        byte? fixedSpeed = null,
        byte? gpuDuty = null)
    {
        EnsureWritePreconditions();
        FanControlState original = ReadState();
        ValidateRollbackState(original);

        try
        {
            WriteProfileSequence(
                fixedStatus,
                stepStatus,
                autoStatus,
                thermalTarget,
                fixedSpeed,
                gpuDuty);
            FanControlState verified = ReadState();
            if (verified.FixedStatusRaw != fixedStatus ||
                verified.StepStatusRaw != stepStatus ||
                verified.AutoStatusRaw != autoStatus ||
                verified.NvidiaThermalTargetRaw != thermalTarget ||
                (fixedSpeed.HasValue && verified.FixedSpeedRaw != fixedSpeed.Value) ||
                (gpuDuty.HasValue && verified.GpuDutyRaw != gpuDuty.Value))
            {
                throw new AorusFanControlException(
                    $"{profileName}-Profil wurde nicht exakt zurückgelesen: " +
                    FormatModeState(verified));
            }

            if (!verified.Curve.SequenceEqual(original.Curve))
            {
                throw new AorusFanControlException(
                    "Die gespeicherte Lüfterkurve hat sich unerwartet verändert.");
            }

            return new FanProfileChangeResult(original, verified);
        }
        catch (Exception changeException)
        {
            try
            {
                RestoreModeState(original);
                FanControlState restored = ReadState();
                if (!ModeStateEquals(restored, original) ||
                    !restored.Curve.SequenceEqual(original.Curve))
                {
                    throw new AorusFanControlException(
                        $"Rollback-Rücklesen stimmt nicht überein: erwartet {FormatModeState(original)}, " +
                        $"gelesen {FormatModeState(restored)}.");
                }
            }
            catch (Exception rollbackException)
            {
                throw new AorusFanControlException(
                    "Lüfteränderung und automatisches Zurückrollen sind fehlgeschlagen.",
                    new AggregateException(changeException, rollbackException));
            }

            throw new AorusFanControlException(
                "Lüfteränderung ist fehlgeschlagen; der vorherige Modus wurde verifiziert wiederhergestellt.",
                changeException);
        }
    }

    private FanProfileChangeResult RestoreExactState(FanControlState target)
    {
        EnsureWritePreconditions();
        ValidateRollbackState(target);
        FanControlState before = ReadState();
        if (!before.Curve.SequenceEqual(target.Curve))
        {
            WriteCurve(target.Curve);
        }
        RestoreModeState(target);
        FanControlState verified = ReadState();
        if (!ModeStateEquals(verified, target) ||
            !verified.Curve.SequenceEqual(target.Curve))
        {
            throw new AorusFanControlException(
                $"Explizite Wiederherstellung stimmt nicht überein: erwartet {FormatModeState(target)}, " +
                $"gelesen {FormatModeState(verified)}.");
        }

        return new FanProfileChangeResult(before, verified);
    }

    private FanProfileChangeResult ChangeCurve(IReadOnlyList<FanCurvePoint> targetCurve)
    {
        EnsureWritePreconditions();
        FanControlState original = ReadState();
        ValidateRollbackState(original);
        try
        {
            WriteCurve(targetCurve);
            FanControlState verified = ReadState();
            if (!verified.Curve.SequenceEqual(targetCurve))
            {
                throw new AorusFanControlException("Die Lüfterkurve wurde nicht exakt zurückgelesen.");
            }
            return new FanProfileChangeResult(original, verified);
        }
        catch (Exception changeException)
        {
            try
            {
                WriteCurve(original.Curve);
                FanControlState restored = ReadState();
                if (!restored.Curve.SequenceEqual(original.Curve))
                {
                    throw new AorusFanControlException("Kurven-Rollback wurde nicht exakt zurückgelesen.");
                }
            }
            catch (Exception rollbackException)
            {
                throw new AorusFanControlException(
                    "Kurvenänderung und automatisches Zurückrollen sind fehlgeschlagen.",
                    new AggregateException(changeException, rollbackException));
            }
            throw new AorusFanControlException(
                "Kurvenänderung ist fehlgeschlagen; die Originalkurve wurde verifiziert wiederhergestellt.",
                changeException);
        }
    }

    private void WriteCurve(IReadOnlyList<FanCurvePoint> curve)
    {
        ValidateCurve(curve);
        ManagementObject setter = _setterInstance ?? throw new AorusFanControlException(
            "Die Gigabyte-Lüfterschreibschnittstelle ist nicht geöffnet.");
        foreach (FanCurvePoint point in curve)
        {
            using ManagementBaseObject input = setter.GetMethodParameters("SetFanIndexValue");
            input["Index"] = point.Index;
            input["Temperture"] = point.Temperature;
            input["Value"] = point.Value;
            using ManagementBaseObject output = setter.InvokeMethod(
                "SetFanIndexValue",
                input,
                new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) });
        }
    }

    private static void ValidateCurve(IReadOnlyList<FanCurvePoint> curve)
        => Features.Cooling.FanCurveValidation.Validate(curve);

    private FanControlState ReadState()
    {
        EnsureCompatibleDevice();
        EnsureAdministrator();
        ManagementObject getter = GetOrCreateInstance(
            AorusDeviceProfile.GetterClass,
            AorusDeviceProfile.FanGetterMethods,
            ref _getterInstance,
            validateSetters: false);
        var curve = new List<FanCurvePoint>(15);
        for (byte index = 0; index < 15; index++)
        {
            using ManagementBaseObject input = getter.GetMethodParameters("GetFanIndexValue");
            input["Index"] = index;
            using ManagementBaseObject output = getter.InvokeMethod(
                "GetFanIndexValue",
                input,
                new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) });
            curve.Add(new FanCurvePoint(
                index,
                Convert.ToByte(output["Temperture"], CultureInfo.InvariantCulture),
                Convert.ToByte(output["Value"], CultureInfo.InvariantCulture)));
        }

        return new FanControlState(
            InvokeUInt16Getter(getter, "GetFixedFanStatus"),
            InvokeUInt16Getter(getter, "GetStepFanStatus"),
            InvokeByteGetter(getter, "GetAutoFanStatus"),
            InvokeByteGetter(getter, "GetNvThermalTarget"),
            InvokeUInt16Getter(getter, "GetFixedFanSpeed"),
            InvokeByteGetter(getter, "GetGPUFanDuty"),
            curve);
    }

    private void WriteProfileSequence(
        byte fixedStatus,
        byte stepStatus,
        byte autoStatus,
        byte thermalTarget,
        byte? fixedSpeed,
        byte? gpuDuty)
    {
        ManagementObject setter = _setterInstance ?? throw new AorusFanControlException(
            "Die Gigabyte-Lüfterschreibschnittstelle ist nicht geöffnet.");
        InvokeByteSetter(setter, "SetFixedFanStatus", 0);
        InvokeByteSetter(setter, "SetStepFanStatus", 0);
        InvokeByteSetter(setter, "SetAutoFanStatus", 0);
        InvokeByteSetter(setter, "SetNvThermalTarget", thermalTarget);
        if (fixedSpeed.HasValue)
        {
            InvokeByteSetter(setter, "SetFixedFanSpeed", fixedSpeed.Value);
        }
        if (gpuDuty.HasValue)
        {
            InvokeByteSetter(setter, "SetGPUFanDuty", gpuDuty.Value);
        }
        InvokeByteSetter(setter, "SetStepFanStatus", stepStatus);
        InvokeByteSetter(setter, "SetFixedFanStatus", fixedStatus);
        InvokeByteSetter(setter, "SetAutoFanStatus", autoStatus);
    }

    private void RestoreModeState(FanControlState original)
    {
        ManagementObject setter = _setterInstance ?? throw new AorusFanControlException(
            "Die Gigabyte-Lüfterschreibschnittstelle ist nicht geöffnet.");
        InvokeByteSetter(setter, "SetFixedFanStatus", 0);
        InvokeByteSetter(setter, "SetStepFanStatus", 0);
        InvokeByteSetter(setter, "SetAutoFanStatus", 0);
        InvokeByteSetter(setter, "SetFixedFanSpeed", checked((byte)original.FixedSpeedRaw));
        InvokeByteSetter(setter, "SetGPUFanDuty", original.GpuDutyRaw);
        InvokeByteSetter(setter, "SetNvThermalTarget", original.NvidiaThermalTargetRaw);
        InvokeByteSetter(setter, "SetAutoFanStatus", checked((byte)original.AutoStatusRaw));
        InvokeByteSetter(setter, "SetStepFanStatus", checked((byte)original.StepStatusRaw));
        InvokeByteSetter(setter, "SetFixedFanStatus", checked((byte)original.FixedStatusRaw));
    }

    private static void ValidateRollbackState(FanControlState state)
    {
        if (state.FixedStatusRaw > 1 || state.StepStatusRaw > 1 || state.AutoStatusRaw > 1 ||
            state.NvidiaThermalTargetRaw > 1 || state.FixedSpeedRaw > 229 ||
            state.GpuDutyRaw > 229 || state.Curve.Count != 15)
        {
            throw new AorusFanControlException(
                $"Unbekannter Ausgangsmodus; kein sicherer Rollback möglich: {FormatModeState(state)}.");
        }
    }

    private static bool ModeStateEquals(FanControlState left, FanControlState right) =>
        left.FixedStatusRaw == right.FixedStatusRaw &&
        left.StepStatusRaw == right.StepStatusRaw &&
        left.AutoStatusRaw == right.AutoStatusRaw &&
        left.NvidiaThermalTargetRaw == right.NvidiaThermalTargetRaw &&
        left.FixedSpeedRaw == right.FixedSpeedRaw;

    private static string FormatModeState(FanControlState state) =>
        $"fixed={state.FixedStatusRaw}, step={state.StepStatusRaw}, " +
        $"auto={state.AutoStatusRaw}, thermal={state.NvidiaThermalTargetRaw}, " +
        $"stored-fixed={state.FixedSpeedRaw}, live-gpu-duty={state.GpuDutyRaw}";

    private void EnsureWritePreconditions()
    {
        EnsureCompatibleDevice();
        EnsureAdministrator();
        _ = GetOrCreateInstance(
            AorusDeviceProfile.SetterClass,
            AorusDeviceProfile.FanNormalSetterMethods,
            ref _setterInstance,
            validateSetters: true);
    }

    private static ManagementObject GetOrCreateInstance(
        string className,
        IReadOnlySet<string> requiredMethods,
        ref ManagementObject? cachedInstance,
        bool validateSetters)
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
            throw new AorusFanControlException(
                $"Erwartete Methoden fehlen in {className}: {string.Join(", ", missing)}.");
        }

        if (validateSetters)
        {
            foreach (string methodName in requiredMethods)
            {
                PropertyData[] inputs = methods[methodName].InParameters?.Properties
                    .Cast<PropertyData>().ToArray() ?? [];
                bool valid = methodName.Equals("SetFanIndexValue", StringComparison.OrdinalIgnoreCase)
                    ? inputs.Length == 3 &&
                      inputs.All(input => input.Type == CimType.UInt8) &&
                      inputs.Select(input => input.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
                          .SetEquals(["Index", "Temperture", "Value"])
                    : inputs.Length == 1 &&
                      inputs[0].Name.Equals("Data", StringComparison.OrdinalIgnoreCase) &&
                      inputs[0].Type == CimType.UInt8;
                if (!valid)
                {
                    throw new AorusFanControlException(
                        $"Unerwartete Signatur für {methodName}.");
                }
            }
        }

        using ManagementObjectCollection instances = managementClass.GetInstances();
        cachedInstance = instances.Cast<ManagementObject>().FirstOrDefault();
        return cachedInstance ?? throw new AorusFanControlException(
            $"Die WMI-Klasse {className} besitzt keine aktive Geräteinstanz.");
    }

    private static byte InvokeByteGetter(ManagementObject instance, string methodName)
    {
        using ManagementBaseObject output = instance.InvokeMethod(
            methodName,
            null,
            new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) });
        return Convert.ToByte(output["Data"], CultureInfo.InvariantCulture);
    }

    private static ushort InvokeUInt16Getter(ManagementObject instance, string methodName)
    {
        using ManagementBaseObject output = instance.InvokeMethod(
            methodName,
            null,
            new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) });
        return Convert.ToUInt16(output["Data"], CultureInfo.InvariantCulture);
    }

    private static void InvokeByteSetter(ManagementObject instance, string methodName, byte value)
    {
        if (!AorusDeviceProfile.FanNormalSetterMethods.Contains(methodName))
        {
            throw new AorusFanControlException($"Nicht freigegebene Schreibmethode: {methodName}.");
        }

        using ManagementBaseObject input = instance.GetMethodParameters(methodName);
        input["Data"] = value;
        using ManagementBaseObject output = instance.InvokeMethod(
            methodName,
            input,
            new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) });
    }

    private void EnsureCompatibleDevice()
    {
        DeviceCompatibility compatibility = CheckCompatibility();
        if (!compatibility.IsSupported)
        {
            throw new AorusFanControlException(compatibility.Message);
        }
    }

    private static void EnsureAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            throw new AorusFanControlException(
                "Windows-Administratorrechte sind für die Lüftersteuerung erforderlich.");
        }
    }

    private static string QueryFirstValue(string query, string property)
    {
        using var searcher = new ManagementObjectSearcher(@"root\cimv2", query);
        using ManagementObjectCollection results = searcher.Get();
        using ManagementObject? item = results.Cast<ManagementObject>().FirstOrDefault();
        return Convert.ToString(item?[property], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
