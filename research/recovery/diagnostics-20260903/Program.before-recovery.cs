using System.Globalization;
using System.Management;
using System.Security.Principal;
using System.Text;
using AorusControl.Core.Models;
using AorusControl.Core.Services;
using HidSharp;
using HidSharp.Reports;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Diagnostics;

const string FirmwareNamespace = @"root\WMI";
string[] firmwareClasses = ["GB_WMIACPI_Get", "GB_WMIACPI_Set", "CLEVO_GET"];
bool readTelemetry = args.Any(argument =>
    argument.Equals("--read-telemetry", StringComparison.OrdinalIgnoreCase));
bool liveMonitor = args.Any(argument =>
    argument.Equals("--monitor", StringComparison.OrdinalIgnoreCase));
bool inspectKeyboard = args.Any(argument =>
    argument.Equals("--inspect-keyboard", StringComparison.OrdinalIgnoreCase));
bool readKeyboardState = args.Any(argument =>
    argument.Equals("--read-keyboard-state", StringComparison.OrdinalIgnoreCase));
bool queryKeyboardRgb = args.Any(argument =>
    argument.Equals("--query-keyboard-rgb", StringComparison.OrdinalIgnoreCase));
bool verifyKeyboardZoneWrite = args.Any(argument =>
    argument.Equals("--verify-keyboard-zone-write", StringComparison.OrdinalIgnoreCase));
bool setKeyboardGreen = args.Any(argument =>
    argument.Equals("--set-keyboard-green", StringComparison.OrdinalIgnoreCase));
bool monitorKeyboardBrightness = args.Any(argument =>
    argument.Equals("--monitor-keyboard-brightness", StringComparison.OrdinalIgnoreCase));
bool cycleKeyboardBrightness = args.Any(argument =>
    argument.Equals("--cycle-keyboard-brightness", StringComparison.OrdinalIgnoreCase));
bool testKeyboardBreathing = args.Any(argument =>
    argument.Equals("--test-keyboard-breathing", StringComparison.OrdinalIgnoreCase));
bool readKeyboardMatrix = args.Any(argument =>
    argument.Equals("--read-keyboard-matrix", StringComparison.OrdinalIgnoreCase));
bool probeKeyboardPictureMatrix = args.Any(argument =>
    argument.Equals("--probe-keyboard-picture-matrix", StringComparison.OrdinalIgnoreCase));
bool testKeyboardHostEffects = args.Any(argument =>
    argument.Equals("--test-keyboard-host-effects", StringComparison.OrdinalIgnoreCase));
bool interactiveHostEffectTest = args.Any(argument =>
    argument.Equals("--interactive-host-effect-test", StringComparison.OrdinalIgnoreCase));
bool testPictureMatrixWrite = args.Any(argument =>
    argument.Equals("--test-picture-matrix-write", StringComparison.OrdinalIgnoreCase));
bool isolateEffectSelection = args.Any(argument =>
    argument.Equals("--isolate-effect-selection", StringComparison.OrdinalIgnoreCase));
bool testEffectPalette = args.Any(argument =>
    argument.Equals("--test-effect-palette", StringComparison.OrdinalIgnoreCase));
bool sweepZoneBrightness = args.Any(argument =>
    argument.Equals("--sweep-zone-brightness", StringComparison.OrdinalIgnoreCase));
bool huntBrightnessSignal = args.Any(argument =>
    argument.Equals("--hunt-brightness-signal", StringComparison.OrdinalIgnoreCase));
bool monitorBrightnessEvents = args.Any(argument =>
    argument.Equals("--monitor-brightness-events", StringComparison.OrdinalIgnoreCase));
bool monitorPowerDraw = args.Any(argument =>
    argument.Equals("--monitor-power-draw", StringComparison.OrdinalIgnoreCase));
bool testBrightnessInteraction = args.Any(argument =>
    argument.Equals("--test-brightness-interaction", StringComparison.OrdinalIgnoreCase));
bool testBacklightLevel = args.Any(argument =>
    argument.Equals("--test-backlight-level", StringComparison.OrdinalIgnoreCase));
bool testKeyboardEffectsBatch1 = args.Any(argument =>
    argument.Equals("--test-keyboard-effects-batch1", StringComparison.OrdinalIgnoreCase));
bool interactiveKeyboardEffectTest = args.Any(argument =>
    argument.Equals("--interactive-keyboard-effect-test", StringComparison.OrdinalIgnoreCase));
bool setKeyboardSlowColorCycle = args.Any(argument =>
    argument.Equals("--set-keyboard-slow-color-cycle", StringComparison.OrdinalIgnoreCase));
bool setKeyboardOldDefaultPulse = args.Any(argument =>
    argument.Equals("--set-keyboard-old-default-pulse", StringComparison.OrdinalIgnoreCase));
bool inspectBattery = args.Any(argument =>
    argument.Equals("--inspect-battery", StringComparison.OrdinalIgnoreCase));
bool inspectThermalPower = args.Any(argument =>
    argument.Equals("--inspect-thermal-power", StringComparison.OrdinalIgnoreCase));
bool setFanNormal = args.Any(argument =>
    argument.Equals("--set-fan-normal", StringComparison.OrdinalIgnoreCase));
bool testFanQuiet = args.Any(argument =>
    argument.Equals("--test-fan-quiet", StringComparison.OrdinalIgnoreCase));
bool testFanGaming = args.Any(argument =>
    argument.Equals("--test-fan-gaming", StringComparison.OrdinalIgnoreCase));
bool testFanMaximum = args.Any(argument =>
    argument.Equals("--test-fan-maximum", StringComparison.OrdinalIgnoreCase));
bool testWindowsPowerModes = args.Any(argument =>
    argument.Equals("--test-windows-power-modes", StringComparison.OrdinalIgnoreCase));
bool testFanFixedScale = args.Any(argument =>
    argument.Equals("--test-fan-fixed-scale", StringComparison.OrdinalIgnoreCase));
bool testFanFixedLowScale = args.Any(argument =>
    argument.Equals("--test-fan-fixed-low-scale", StringComparison.OrdinalIgnoreCase));
bool testFanDynamic = args.Any(argument =>
    argument.Equals("--test-fan-dynamic", StringComparison.OrdinalIgnoreCase));
bool testFanCurveWrite = args.Any(argument =>
    argument.Equals("--test-fan-curve-write", StringComparison.OrdinalIgnoreCase));
int? requestedChargeLimit = ReadOptionalIntArgument("--set-charge-limit");
bool setStandardChargeMode = args.Any(argument =>
    argument.Equals("--set-standard-charge-mode", StringComparison.OrdinalIgnoreCase));

if (requestedChargeLimit.HasValue || setStandardChargeMode)
{
    RunBatteryChargeChange(requestedChargeLimit, setStandardChargeMode);
    return;
}

if (setFanNormal)
{
    RunFanNormalChange();
    return;
}

if (testFanQuiet)
{
    RunTemporaryFanProfileTest("quiet");
    return;
}

if (testFanGaming)
{
    RunTemporaryFanProfileTest("gaming");
    return;
}

if (testFanMaximum)
{
    RunTemporaryFanProfileTest("maximum");
    return;
}

if (testWindowsPowerModes)
{
    RunWindowsPowerModeTest();
    return;
}

if (testFanFixedScale)
{
    RunFanFixedScaleTest(lowRange: false);
    return;
}

if (testFanFixedLowScale)
{
    RunFanFixedScaleTest(lowRange: true);
    return;
}

if (testFanDynamic)
{
    RunTemporaryFanProfileTest("dynamic");
    return;
}

if (testFanCurveWrite)
{
    RunFanCurveWriteTest();
    return;
}

if (setKeyboardSlowColorCycle)
{
    RunKeyboardSlowColorCycle();
    return;
}

if (setKeyboardOldDefaultPulse)
{
    RunKeyboardOldDefaultPulse();
    return;
}

if (testKeyboardBreathing)
{
    RunKeyboardBreathingTest();
    return;
}

if (readKeyboardMatrix)
{
    RunKeyboardMatrixRead();
    return;
}

if (probeKeyboardPictureMatrix)
{
    RunKeyboardPictureMatrixProbe();
    return;
}

if (testKeyboardHostEffects)
{
    RunKeyboardHostEffectTest();
    return;
}

if (interactiveHostEffectTest)
{
    RunInteractiveHostEffectTest();
    return;
}

if (testPictureMatrixWrite)
{
    RunPictureMatrixWriteTest();
    return;
}

if (isolateEffectSelection)
{
    RunEffectSelectionIsolation();
    return;
}

if (testEffectPalette)
{
    RunEffectPaletteTest();
    return;
}

if (sweepZoneBrightness)
{
    RunZoneBrightnessSweep();
    return;
}

if (huntBrightnessSignal)
{
    RunBrightnessSignalHunt();
    return;
}

if (monitorPowerDraw)
{
    RunPowerDrawMonitor();
    return;
}

if (monitorBrightnessEvents)
{
    RunBrightnessEventMonitor();
    return;
}

if (testBrightnessInteraction)
{
    RunBrightnessInteractionTest();
    return;
}

if (testBacklightLevel)
{
    RunBacklightLevelTest();
    return;
}

if (testKeyboardEffectsBatch1)
{
    RunKeyboardEffectBatch1();
    return;
}

if (interactiveKeyboardEffectTest)
{
    RunInteractiveKeyboardEffectTest();
    return;
}

if (inspectBattery)
{
    RunBatteryInspection();
    return;
}

if (inspectThermalPower)
{
    RunThermalPowerInspection();
    return;
}

if (cycleKeyboardBrightness)
{
    RunKeyboardBrightnessCycle();
    return;
}

if (monitorKeyboardBrightness)
{
    RunKeyboardBrightnessMonitor();
    return;
}

if (setKeyboardGreen)
{
    RunSetKeyboardGreen();
    return;
}

if (verifyKeyboardZoneWrite)
{
    RunKeyboardZoneWriteVerification();
    return;
}

if (queryKeyboardRgb)
{
    RunKeyboardRgbQuery();
    return;
}

if (readKeyboardState)
{
    RunKeyboardFeatureRead();
    return;
}

if (inspectKeyboard)
{
    RunKeyboardHidInspection();
    return;
}

if (liveMonitor)
{
    RunLiveMonitor();
    return;
}

var report = new StringBuilder();
report.AppendLine("# AORUS read-only diagnostic report");
report.AppendLine();
report.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
report.AppendLine(readTelemetry
    ? "- Mode: metadata plus DSDT-verified read-only telemetry whitelist"
    : "- Mode: metadata and read-only operating-system queries");
report.AppendLine("- Firmware/EC write methods invoked: **no**");
report.AppendLine();

AddSection("Execution context");
AddValue("Windows", Environment.OSVersion.VersionString);
AddValue("64-bit process", Environment.Is64BitProcess ? "yes" : "no");
AddValue("Administrator", IsAdministrator() ? "yes" : "no");
AddValue(".NET runtime", Environment.Version.ToString());

AddSection("Device identity (privacy-safe)");
AppendFirstCimv2(
    "SELECT Manufacturer, Model FROM Win32_ComputerSystem",
    ["Manufacturer", "Model"]);
AppendFirstCimv2(
    "SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS",
    ["Manufacturer", "SMBIOSBIOSVersion", "ReleaseDate"]);
AppendFirstCimv2(
    "SELECT Manufacturer, Product, Version FROM Win32_BaseBoard",
    ["Manufacturer", "Product", "Version"]);

AddSection("Windows ACPI WMI bridge");
using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\WmiAcpi"))
{
    AddValue("WmiAcpi registry key", key is null ? "missing" : "present");
    AddValue("MofImagePath", key?.GetValue("MofImagePath")?.ToString() ?? "not configured");
}

report.AppendLine();
report.AppendLine("### ACPI PNP0C14 devices");
report.AppendLine();
var acpiDevices = Query(
        @"root\cimv2",
        "SELECT Name, PNPDeviceID, Status FROM Win32_PnPEntity")
    .Where(item => GetText(item, "PNPDeviceID").StartsWith(
        @"ACPI\PNP0C14", StringComparison.OrdinalIgnoreCase))
    .ToList();

if (acpiDevices.Count == 0)
{
    report.AppendLine("- None found");
}
else
{
    foreach (var device in acpiDevices)
    {
        report.AppendLine(
            $"- `{Escape(GetText(device, "PNPDeviceID"))}` — " +
            $"{Escape(GetText(device, "Name"))} ({Escape(GetText(device, "Status"))})");
    }
}

report.AppendLine();
report.AppendLine("### Processed binary MOF resources for ACPI WMI devices");
report.AppendLine();
var mofResources = Query(
        FirmwareNamespace,
        "SELECT Name, MofProcessed FROM WMIBinaryMofResource")
    .Where(item => GetText(item, "Name").Contains("PNP0C14", StringComparison.OrdinalIgnoreCase))
    .ToList();

if (mofResources.Count == 0)
{
    report.AppendLine("- None found");
}
else
{
    foreach (var resource in mofResources)
    {
        report.AppendLine(
            $"- `{Escape(GetText(resource, "Name"))}` — processed: " +
            $"{Escape(GetText(resource, "MofProcessed"))}");
    }
}

AddSection("Gigabyte/Clevo WMI class metadata");
foreach (string className in firmwareClasses)
{
    AppendClassMetadata(FirmwareNamespace, className);
}

if (readTelemetry)
{
    AppendWhitelistedTelemetry();
}

AddSection("Standard ACPI thermal zones");
var thermalZones = Query(
    FirmwareNamespace,
    "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

if (thermalZones.Count == 0)
{
    report.AppendLine("- No readable thermal-zone data");
}
else
{
    foreach (var zone in thermalZones)
    {
        string raw = GetText(zone, "CurrentTemperature");
        string formatted = uint.TryParse(raw, out uint deciKelvin)
            ? $"{(deciKelvin / 10.0 - 273.15):F1} °C (raw {deciKelvin})"
            : raw;
        report.AppendLine($"- `{Escape(GetText(zone, "InstanceName"))}`: {Escape(formatted)}");
    }
}

report.AppendLine();
report.AppendLine("## Interpretation");
report.AppendLine();
report.AppendLine("- A class being present means its metadata is registered; it does not prove that every method is safe on this firmware.");
report.AppendLine(readTelemetry
    ? "- Only the explicit getter whitelist confirmed against this FB0F DSDT may be invoked; no setter is available in this mode."
    : "- This diagnostic intentionally does not invoke any class method, including methods whose names begin with `Get`.");
report.AppendLine("- Serial numbers, UUIDs, user names, and network identifiers are intentionally excluded.");

string root = FindRepositoryRoot();
string outputDirectory = Path.Combine(root, "research", "runs");
Directory.CreateDirectory(outputDirectory);
string outputPath = Path.Combine(
    outputDirectory,
    $"diagnostic-{DateTime.Now:yyyyMMdd-HHmmss}.md");
File.WriteAllText(outputPath, report.ToString(), new UTF8Encoding(false));

Console.WriteLine(report);
Console.WriteLine($"Report written to: {outputPath}");

void AddSection(string title)
{
    report.AppendLine();
    report.AppendLine($"## {title}");
    report.AppendLine();
}

void AddValue(string name, string value) =>
    report.AppendLine($"- {name}: {Escape(value)}");

void AppendFirstCimv2(string query, string[] properties)
{
    var item = Query(@"root\cimv2", query).FirstOrDefault();
    if (item is null)
    {
        report.AppendLine("- Query returned no instance");
        return;
    }

    foreach (string property in properties)
    {
        string value = GetText(item, property);
        if (property.Equals("ReleaseDate", StringComparison.OrdinalIgnoreCase))
        {
            value = FormatWmiDate(value);
        }

        AddValue(property, value);
    }
}

void AppendClassMetadata(string scopePath, string className)
{
    report.AppendLine($"### `{className}`");
    report.AppendLine();
    try
    {
        using var managementClass = new ManagementClass(scopePath, className, null);
        managementClass.Get();
        var methods = managementClass.Methods
            .Cast<MethodData>()
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        report.AppendLine("- Status: present");
        report.AppendLine($"- Method count: {methods.Length}");
        foreach (string method in methods)
        {
            report.AppendLine($"  - `{method}`");
        }
    }
    catch (ManagementException exception) when (
        exception.ErrorCode is ManagementStatus.InvalidClass or ManagementStatus.NotFound)
    {
        report.AppendLine("- Status: not registered");
    }
    catch (Exception exception)
    {
        report.AppendLine($"- Status: unavailable ({Escape(exception.Message)})");
    }

    report.AppendLine();
}

void AppendWhitelistedTelemetry()
{
    AddSection("DSDT-verified read-only telemetry");

    string model = GetFirstValue(
        @"root\cimv2",
        "SELECT Model FROM Win32_ComputerSystem",
        "Model");
    string bios = GetFirstValue(
        @"root\cimv2",
        "SELECT SMBIOSBIOSVersion FROM Win32_BIOS",
        "SMBIOSBIOSVersion");

    if (!model.Equals("AORUS 5 SE", StringComparison.OrdinalIgnoreCase) ||
        !bios.Equals("FB0F", StringComparison.OrdinalIgnoreCase))
    {
        report.AppendLine(
            $"- Refused: telemetry whitelist is approved only for `AORUS 5 SE / FB0F`; " +
            $"detected `{Escape(model)} / {Escape(bios)}`.");
        return;
    }

    string[] approvedMethods =
    [
        "getCpuTemp",
        "getGpuTemp1",
        "getGpuTemp2",
        "getRpm1",
        "getRpm2",
        "GetCPUFanDuty",
        "GetGPUFanDuty",
        "GetChargePolicy",
        "GetChargeStop",
        "GetFixedFanStatus",
        "GetFixedFanSpeed",
        "GetFanAdjustStatus",
        "GetAutoFanStatus",
        "GetFanSpeed"
    ];

    try
    {
        using var getClass = new ManagementClass(FirmwareNamespace, "GB_WMIACPI_Get", null);
        getClass.Get();
        using ManagementObjectCollection instances = getClass.GetInstances();
        using ManagementObject? instance = instances.Cast<ManagementObject>().FirstOrDefault();
        if (instance is null)
        {
            report.AppendLine("- `GB_WMIACPI_Get` is registered but has no instance.");
            return;
        }

        AddValue("Instance", instance.Path.Path);
        var availableMethods = getClass.Methods
            .Cast<MethodData>()
            .Select(method => method.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string methodName in approvedMethods)
        {
            if (!availableMethods.Contains(methodName))
            {
                report.AppendLine($"- `{methodName}`: not exposed by installed MOF");
                continue;
            }

            try
            {
                var options = new InvokeMethodOptions
                {
                    Timeout = TimeSpan.FromSeconds(2)
                };
                using ManagementBaseObject output =
                    instance.InvokeMethod(methodName, null, options);
                string values = string.Join(
                    ", ",
                    output.Properties.Cast<PropertyData>().Select(property =>
                        $"{property.Name}={Convert.ToString(property.Value, CultureInfo.InvariantCulture)}"));

                if ((methodName.Equals("getRpm1", StringComparison.OrdinalIgnoreCase) ||
                     methodName.Equals("getRpm2", StringComparison.OrdinalIgnoreCase)) &&
                    output["Data"] is not null)
                {
                    ushort rawRpm = Convert.ToUInt16(output["Data"], CultureInfo.InvariantCulture);
                    ushort decodedRpm = (ushort)((rawRpm >> 8) | (rawRpm << 8));
                    values += $" (byte-swapped: {decodedRpm} RPM)";
                }

                report.AppendLine($"- `{methodName}`: {Escape(values)}");
            }
            catch (Exception exception)
            {
                report.AppendLine($"- `{methodName}`: error ({Escape(exception.Message)})");
            }
        }
    }
    catch (ManagementException exception) when (
        exception.ErrorCode is ManagementStatus.InvalidClass or ManagementStatus.NotFound)
    {
        report.AppendLine("- `GB_WMIACPI_Get` is not registered. Install the signed MOF provider and reboot first.");
    }
    catch (Exception exception)
    {
        report.AppendLine($"- Telemetry unavailable: {Escape(exception.Message)}");
    }
}

void RunBatteryInspection()
{
    var batteryReport = new StringBuilder();
    batteryReport.AppendLine("# AORUS battery charge-limit inspection");
    batteryReport.AppendLine();
    batteryReport.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    batteryReport.AppendLine("- Mode: read-only");
    batteryReport.AppendLine("- Firmware/EC write methods invoked: **no**");
    batteryReport.AppendLine();

    string model = GetFirstValue(
        @"root\cimv2",
        "SELECT Model FROM Win32_ComputerSystem",
        "Model");
    string bios = GetFirstValue(
        @"root\cimv2",
        "SELECT SMBIOSBIOSVersion FROM Win32_BIOS",
        "SMBIOSBIOSVersion");

    batteryReport.AppendLine("## Compatibility gate");
    batteryReport.AppendLine();
    batteryReport.AppendLine($"- Model: `{Escape(model)}`");
    batteryReport.AppendLine($"- BIOS: `{Escape(bios)}`");
    batteryReport.AppendLine($"- Administrator: {(IsAdministrator() ? "yes" : "no")}");

    if (!model.Equals("AORUS 5 SE", StringComparison.OrdinalIgnoreCase) ||
        !bios.Equals("FB0F", StringComparison.OrdinalIgnoreCase))
    {
        batteryReport.AppendLine("- Result: refused; this inspection is allowlisted only for `AORUS 5 SE / FB0F`.");
        WriteBatteryInspectionReport(batteryReport);
        Environment.ExitCode = 2;
        return;
    }

    batteryReport.AppendLine("- Result: exact model/BIOS match");
    batteryReport.AppendLine();
    batteryReport.AppendLine("## Windows battery state");
    batteryReport.AppendLine();

    var batteries = Query(
        @"root\cimv2",
        "SELECT Name, BatteryStatus, EstimatedChargeRemaining, DesignVoltage FROM Win32_Battery");
    if (batteries.Count == 0)
    {
        batteryReport.AppendLine("- No `Win32_Battery` instance returned.");
    }
    else
    {
        foreach (IReadOnlyDictionary<string, object?> battery in batteries)
        {
            batteryReport.AppendLine($"- Name: `{Escape(GetText(battery, "Name"))}`");
            batteryReport.AppendLine($"- BatteryStatus: `{Escape(GetText(battery, "BatteryStatus"))}`");
            batteryReport.AppendLine($"- EstimatedChargeRemaining: `{Escape(GetText(battery, "EstimatedChargeRemaining"))}%`");
            batteryReport.AppendLine($"- DesignVoltage: `{Escape(GetText(battery, "DesignVoltage"))} mV`");
        }
    }

    batteryReport.AppendLine();
    batteryReport.AppendLine("## Firmware charge state");
    batteryReport.AppendLine();

    if (!IsAdministrator())
    {
        batteryReport.AppendLine("- Firmware read refused: Windows requires administrator rights for this ACPI-WMI invocation.");
        WriteBatteryInspectionReport(batteryReport);
        Environment.ExitCode = 1;
        return;
    }

    try
    {
        using var getClass = new ManagementClass(FirmwareNamespace, "GB_WMIACPI_Get", null);
        getClass.Get();
        var availableMethods = getClass.Methods
            .Cast<MethodData>()
            .Select(method => method.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] requiredMethods = ["GetChargePolicy", "GetChargeStop"];
        string[] missingMethods = requiredMethods
            .Where(method => !availableMethods.Contains(method))
            .ToArray();
        if (missingMethods.Length > 0)
        {
            batteryReport.AppendLine($"- Refused: missing getter(s): `{string.Join("`, `", missingMethods)}`.");
            WriteBatteryInspectionReport(batteryReport);
            Environment.ExitCode = 3;
            return;
        }

        using ManagementObjectCollection instances = getClass.GetInstances();
        using ManagementObject? instance = instances.Cast<ManagementObject>().FirstOrDefault();
        if (instance is null)
        {
            batteryReport.AppendLine("- Refused: `GB_WMIACPI_Get` has no live instance.");
            WriteBatteryInspectionReport(batteryReport);
            Environment.ExitCode = 3;
            return;
        }

        byte policy = InvokeByteGetter(instance, "GetChargePolicy");
        byte stop = InvokeByteGetter(instance, "GetChargeStop");
        string policyMeaning = policy switch
        {
            0 => "Standard/BIOS mode; stored stop byte is not an active custom limit",
            4 => "Custom charge limit enabled",
            _ => "unknown raw policy; do not write"
        };

        batteryReport.AppendLine($"- `GetChargePolicy`: `{policy}` — {policyMeaning}");
        batteryReport.AppendLine($"- `GetChargeStop`: `{stop}`");
        batteryReport.AppendLine($"- Effective interpretation: {(policy == 4 ? $"custom limit {stop}%" : policyMeaning)}");
    }
    catch (Exception exception)
    {
        batteryReport.AppendLine($"- Read failed: {Escape(exception.Message)}");
        Environment.ExitCode = 4;
    }

    WriteBatteryInspectionReport(batteryReport);
}

byte InvokeByteGetter(ManagementObject instance, string methodName)
{
    var options = new InvokeMethodOptions
    {
        Timeout = TimeSpan.FromSeconds(2)
    };
    using ManagementBaseObject output = instance.InvokeMethod(methodName, null, options);
    object? value = output["Data"];
    if (value is null)
    {
        throw new InvalidOperationException($"{methodName} returned no Data value.");
    }

    return Convert.ToByte(value, CultureInfo.InvariantCulture);
}

void WriteBatteryInspectionReport(StringBuilder batteryReport)
{
    string repositoryRoot = FindRepositoryRoot();
    string reportDirectory = Path.Combine(repositoryRoot, "research", "runs");
    Directory.CreateDirectory(reportDirectory);
    string reportPath = Path.Combine(
        reportDirectory,
        $"battery-inspection-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(reportPath, batteryReport.ToString(), new UTF8Encoding(false));
    Console.WriteLine(batteryReport);
    Console.WriteLine($"Report written to: {reportPath}");
}

void RunThermalPowerInspection()
{
    var inspection = new StringBuilder();
    inspection.AppendLine("# AORUS thermal, power and GPU capability inspection");
    inspection.AppendLine();
    inspection.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    inspection.AppendLine("- Mode: read-only");
    inspection.AppendLine("- Setter class opened: **no**");
    inspection.AppendLine("- Firmware/EC write methods invoked: **no**");
    inspection.AppendLine();

    string model = GetFirstValue(
        @"root\cimv2",
        "SELECT Model FROM Win32_ComputerSystem",
        "Model");
    string bios = GetFirstValue(
        @"root\cimv2",
        "SELECT SMBIOSBIOSVersion FROM Win32_BIOS",
        "SMBIOSBIOSVersion");

    inspection.AppendLine("## Compatibility gate");
    inspection.AppendLine();
    inspection.AppendLine($"- Model: `{Escape(model)}`");
    inspection.AppendLine($"- BIOS: `{Escape(bios)}`");
    inspection.AppendLine($"- Administrator: {(IsAdministrator() ? "yes" : "no")}");

    if (!model.Equals("AORUS 5 SE", StringComparison.OrdinalIgnoreCase) ||
        !bios.Equals("FB0F", StringComparison.OrdinalIgnoreCase))
    {
        inspection.AppendLine("- Result: refused; this inspection is allowlisted only for `AORUS 5 SE / FB0F`.");
        WriteThermalPowerInspectionReport(inspection);
        Environment.ExitCode = 2;
        return;
    }

    inspection.AppendLine("- Result: exact model/BIOS match");
    inspection.AppendLine();
    inspection.AppendLine("## Windows power state");
    inspection.AppendLine();
    AppendCommandOutput(inspection, "Active power scheme", "powercfg.exe", "/getactivescheme");
    AppendRegistryValue(
        inspection,
        @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes",
        "ActiveOverlayAcPowerScheme");
    AppendRegistryValue(
        inspection,
        @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes",
        "ActiveOverlayDcPowerScheme");

    inspection.AppendLine();
    inspection.AppendLine("## Windows display and GPU inventory");
    inspection.AppendLine();
    AppendQueryRows(
        inspection,
        @"root\cimv2",
        "SELECT Name, Status, DriverVersion, AdapterCompatibility, PNPDeviceID FROM Win32_VideoController",
        ["Name", "Status", "DriverVersion", "AdapterCompatibility", "PNPDeviceID"]);
    AppendQueryRows(
        inspection,
        @"root\cimv2",
        "SELECT Name, Status, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%NVIDIA%'",
        ["Name", "Status", "PNPDeviceID"]);
    AppendQueryRows(
        inspection,
        @"root\wmi",
        "SELECT Active, InstanceName FROM WmiMonitorID",
        ["Active", "InstanceName"]);

    inspection.AppendLine();
    inspection.AppendLine("## NVIDIA runtime (read-only)");
    inspection.AppendLine();
    string nvidiaSmi = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "System32",
        "nvidia-smi.exe");
    if (File.Exists(nvidiaSmi))
    {
        AppendCommandOutput(
            inspection,
            "GPU state",
            nvidiaSmi,
            "--query-gpu=name,pstate,power.draw,display_active,display_mode,temperature.gpu,fan.speed --format=csv,noheader");
        AppendCommandOutput(
            inspection,
            "GPU processes",
            nvidiaSmi,
            "--query-compute-apps=pid,process_name,used_memory --format=csv,noheader");
    }
    else
    {
        inspection.AppendLine("- `nvidia-smi.exe`: not found");
    }

    inspection.AppendLine();
    inspection.AppendLine("## Firmware getter state");
    inspection.AppendLine();

    if (!IsAdministrator())
    {
        inspection.AppendLine("- Firmware read refused: administrator rights are required for the ACPI-WMI invocation.");
        WriteThermalPowerInspectionReport(inspection);
        Environment.ExitCode = 1;
        return;
    }

    string[] approvedSimpleGetters =
    [
        "getCpuTemp",
        "getGpuTemp1",
        "getGpuTemp2",
        "getRpm1",
        "getRpm2",
        "GetCPUFanDuty",
        "GetGPUFanDuty",
        "GetFixedFanStatus",
        "GetFixedFanSpeed",
        "GetFanAdjustStatus",
        "GetAutoFanStatus",
        "GetStepFanStatus",
        "GetFanSpeed",
        "GetNvPowerConfig",
        "GetNvThermalTarget",
        "GetPEGorSG",
        "GetPEG2orSG2",
        "getAiPowerCtlCapability",
        "GetDynamicBoostStatus",
        "GetEcValueBoostStatus",
        "GetSmartCool",
        "GetSmartTurbo",
        "GetTurboMode",
        "GetWhisperMode",
        "GetTppStatus",
        "GetSuperQuiet",
        // Added 2026-09-03 after a review found these exposed by the installed MOF but
        // never probed. All of them declare out-only parameters, so each call is a pure
        // read. GetDeepFan is the notable one: a second, five-point curve interface
        // beside the fifteen-point FanIndexValue path.
        "GetDeepFan",
        "GetThermalData",
        "GetFanHealth",
        "GetFanPWMStatus",
        "QueryThermalSensor",
        "GetBatteryTemperature",
        "GetFan3Duty",
        "GetFan4Duty",
        "getRpm3",
        "getRpm4"
    ];

    try
    {
        using var getClass = new ManagementClass(FirmwareNamespace, "GB_WMIACPI_Get", null);
        getClass.Get();
        var methods = getClass.Methods.Cast<MethodData>()
            .ToDictionary(method => method.Name, StringComparer.OrdinalIgnoreCase);
        using ManagementObjectCollection instances = getClass.GetInstances();
        using ManagementObject? instance = instances.Cast<ManagementObject>().FirstOrDefault();
        if (instance is null)
        {
            inspection.AppendLine("- `GB_WMIACPI_Get` has no live instance.");
            Environment.ExitCode = 3;
            WriteThermalPowerInspectionReport(inspection);
            return;
        }

        inspection.AppendLine($"- Live instance: `{Escape(instance.Path.Path)}`");
        foreach (string methodName in approvedSimpleGetters)
        {
            if (!methods.TryGetValue(methodName, out MethodData? method))
            {
                inspection.AppendLine($"- `{methodName}`: not exposed by installed MOF");
                continue;
            }

            string signature = FormatMethodSignature(method);
            try
            {
                using ManagementBaseObject input = getClass.GetMethodParameters(methodName);
                using ManagementBaseObject output = instance.InvokeMethod(
                    methodName,
                    input,
                    new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) });
                inspection.AppendLine(
                    $"- `{methodName}` ({Escape(signature)}): {Escape(FormatManagementValues(output))}");
            }
            catch (Exception exception)
            {
                inspection.AppendLine(
                    $"- `{methodName}` ({Escape(signature)}): error ({Escape(exception.Message)})");
            }
        }

        inspection.AppendLine();
        inspection.AppendLine("## Repeated thermal samples");
        inspection.AppendLine();
        inspection.AppendLine("RPM values are decoded with the byte order already established by the existing telemetry reader.");
        inspection.AppendLine();
        string[] repeatedMethods =
        [
            "getCpuTemp",
            "getGpuTemp1",
            "getRpm1",
            "getRpm2",
            "GetCPUFanDuty",
            "GetGPUFanDuty",
            // Sampled repeatedly since 2026-09-03 to settle whether they are persistent
            // settings or live values. GetFixedFanSpeed read 57 in four consecutive
            // inspections and 194 in the next one with no fan write logged in between,
            // and in that run it matched GetFanAdjustStatus and GetFanPWMStatus exactly,
            // which suggests all three alias one EC register. This matters because
            // ModeStateEquals treats FixedSpeedRaw as persistent when verifying a
            // rollback, the same assumption that already produced a false failure for
            // GetGPUFanDuty.
            "GetFixedFanSpeed",
            "GetFanAdjustStatus",
            "GetFanPWMStatus"
        ];
        if (repeatedMethods.All(methods.ContainsKey))
        {
            for (int sample = 1; sample <= 3; sample++)
            {
                ushort cpuTemperature = InvokeUInt16GetterUnchecked(instance, getClass, "getCpuTemp");
                ushort gpuTemperature = InvokeUInt16GetterUnchecked(instance, getClass, "getGpuTemp1");
                ushort cpuRpmRaw = InvokeUInt16GetterUnchecked(instance, getClass, "getRpm1");
                ushort gpuRpmRaw = InvokeUInt16GetterUnchecked(instance, getClass, "getRpm2");
                ushort cpuDutyRaw = InvokeUInt16GetterUnchecked(instance, getClass, "GetCPUFanDuty");
                ushort gpuDutyRaw = InvokeUInt16GetterUnchecked(instance, getClass, "GetGPUFanDuty");
                ushort cpuRpm = (ushort)((cpuRpmRaw >> 8) | (cpuRpmRaw << 8));
                ushort gpuRpm = (ushort)((gpuRpmRaw >> 8) | (gpuRpmRaw << 8));
                inspection.AppendLine(
                    $"- Sample {sample} at {DateTimeOffset.Now:HH:mm:ss}: " +
                    $"CPU {cpuTemperature} °C, GPU {gpuTemperature} °C, " +
                    $"CPU fan raw {cpuRpmRaw} / {cpuRpm} RPM, " +
                    $"GPU fan raw {gpuRpmRaw} / {gpuRpm} RPM, " +
                    $"CPU duty raw {cpuDutyRaw}, GPU duty raw {gpuDutyRaw}, " +
                    $"fixed-speed raw {InvokeUInt16GetterUnchecked(instance, getClass, "GetFixedFanSpeed")}, " +
                    $"fan-adjust raw {InvokeUInt16GetterUnchecked(instance, getClass, "GetFanAdjustStatus")}, " +
                    $"fan-pwm raw {InvokeUInt16GetterUnchecked(instance, getClass, "GetFanPWMStatus")}");
                if (sample < 3)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
            }
        }
        else
        {
            string[] missing = repeatedMethods.Where(method => !methods.ContainsKey(method)).ToArray();
            inspection.AppendLine($"- Repeated sampling skipped; missing getter(s): `{string.Join("`, `", missing)}`.");
        }

        inspection.AppendLine();
        inspection.AppendLine("## Stored 15-point fan curve");
        inspection.AppendLine();
        if (!methods.TryGetValue("GetFanIndexValue", out MethodData? curveMethod))
        {
            inspection.AppendLine("- `GetFanIndexValue`: not exposed by installed MOF");
        }
        else
        {
            inspection.AppendLine($"- Signature: `{Escape(FormatMethodSignature(curveMethod))}`");
            for (byte index = 0; index < 15; index++)
            {
                try
                {
                    using ManagementBaseObject input = getClass.GetMethodParameters("GetFanIndexValue");
                    input["Index"] = index;
                    using ManagementBaseObject output = instance.InvokeMethod(
                        "GetFanIndexValue",
                        input,
                        new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) });
                    inspection.AppendLine($"- Point {index}: {Escape(FormatManagementValues(output))}");
                }
                catch (Exception exception)
                {
                    inspection.AppendLine($"- Point {index}: error ({Escape(exception.Message)})");
                }
            }
        }
    }
    catch (Exception exception)
    {
        inspection.AppendLine($"- Firmware inspection failed: {Escape(exception.Message)}");
        Environment.ExitCode = 4;
    }

    WriteThermalPowerInspectionReport(inspection);
}

void RunFanNormalChange()
{
    bool confirmed = args.Any(argument =>
        argument.Equals("--confirm-fan-write", StringComparison.OrdinalIgnoreCase));
    var change = new StringBuilder();
    change.AppendLine("# AORUS fan Normal-profile change");
    change.AppendLine();
    change.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    change.AppendLine("- Requested profile: Normal / firmware curve");
    change.AppendLine($"- Explicit write confirmation present: {(confirmed ? "yes" : "no")}");
    change.AppendLine();

    if (!confirmed)
    {
        change.AppendLine("- Refused before opening the setter: `--confirm-fan-write` is required.");
        change.AppendLine("- Firmware/EC write methods invoked: **no**");
        WriteFanChangeReport(change);
        Environment.ExitCode = 2;
        return;
    }

    try
    {
        using IAorusFanController controller = new GigabyteWmiFanController();
        DeviceCompatibility compatibility = controller.CheckCompatibility();
        change.AppendLine("## Compatibility gate");
        change.AppendLine();
        change.AppendLine($"- Manufacturer: `{Escape(compatibility.Manufacturer)}`");
        change.AppendLine($"- Model: `{Escape(compatibility.Model)}`");
        change.AppendLine($"- BIOS: `{Escape(compatibility.BiosVersion)}`");
        change.AppendLine($"- Result: {(compatibility.IsSupported ? "exact allowlist match" : Escape(compatibility.Message))}");
        change.AppendLine();

        FanProfileChangeResult result = controller.SetNormalAsync().GetAwaiter().GetResult();
        change.AppendLine("## Verified result");
        change.AppendLine();
        AppendFanState(change, "Original", result.OriginalState);
        AppendFanState(change, "Verified", result.VerifiedState);
        change.AppendLine($"- Curve preserved exactly: {(result.OriginalState.Curve.SequenceEqual(result.VerifiedState.Curve) ? "yes" : "no")}");
        change.AppendLine("- Setter order: Fixed off, Step off, Auto off, NVIDIA thermal target 0");
        change.AppendLine("- Result: success");
    }
    catch (Exception exception)
    {
        change.AppendLine("## Result");
        change.AppendLine();
        change.AppendLine($"- Failed: {Escape(exception.Message)}");
        change.AppendLine("- If a write had started, the controller attempted and verified rollback before returning this error.");
        Environment.ExitCode = 5;
    }

    WriteFanChangeReport(change);
}

void RunWindowsPowerModeTest()
{
    bool confirmed = args.Any(argument =>
        argument.Equals("--confirm-power-mode-write", StringComparison.OrdinalIgnoreCase));
    var test = new StringBuilder();
    test.AppendLine("# Windows power overlay round-trip test");
    test.AppendLine();
    test.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    test.AppendLine("- Scope: Windows power overlay only");
    test.AppendLine("- Gigabyte firmware/EC methods invoked: **no**");
    test.AppendLine($"- Explicit write confirmation present: {(confirmed ? "yes" : "no")}");
    test.AppendLine();

    if (!confirmed)
    {
        test.AppendLine("- Refused before calling the Windows setter: `--confirm-power-mode-write` is required.");
        WriteWindowsPowerModeReport(test);
        Environment.ExitCode = 2;
        return;
    }

    var controller = new WindowsPowerOverlayController();
    Guid? original = null;
    try
    {
        if (!controller.IsOnAcPower())
        {
            throw new InvalidOperationException("Der Rundlauf wird nur bei angeschlossenem Netzteil ausgeführt.");
        }

        original = controller.ReadActiveForCurrentPowerSource();
        test.AppendLine($"- Original AC overlay: `{original}` ({DescribeOverlay(original.Value)})");
        test.AppendLine();
        test.AppendLine("## Round trip");
        test.AppendLine();

        WindowsPowerOverlayMode[] modes =
        [
            WindowsPowerOverlayMode.BestEfficiency,
            WindowsPowerOverlayMode.Balanced,
            WindowsPowerOverlayMode.BestPerformance
        ];
        foreach (WindowsPowerOverlayMode mode in modes)
        {
            controller.Set(mode);
            Thread.Sleep(500);
            Guid readback = controller.ReadActiveForCurrentPowerSource();
            Guid expected = mode switch
            {
                WindowsPowerOverlayMode.Balanced => WindowsPowerOverlayController.BalancedGuid,
                WindowsPowerOverlayMode.BestEfficiency => WindowsPowerOverlayController.BestEfficiencyGuid,
                WindowsPowerOverlayMode.BestPerformance => WindowsPowerOverlayController.BestPerformanceGuid,
                _ => throw new ArgumentOutOfRangeException()
            };
            test.AppendLine($"- {mode}: expected `{expected}`, read `{readback}` — {(readback == expected ? "match" : "MISMATCH")}");
            if (readback != expected)
            {
                throw new InvalidOperationException($"Readback mismatch for {mode}.");
            }
        }
    }
    catch (Exception exception)
    {
        test.AppendLine();
        test.AppendLine($"- Test failed: {Escape(exception.Message)}");
        Environment.ExitCode = 5;
    }
    finally
    {
        if (original.HasValue)
        {
            test.AppendLine();
            test.AppendLine("## Restore");
            test.AppendLine();
            try
            {
                controller.Set(original.Value);
                Thread.Sleep(500);
                Guid restored = controller.ReadActiveForCurrentPowerSource();
                test.AppendLine($"- Restored `{restored}` ({DescribeOverlay(restored)})");
                test.AppendLine($"- Exact original restored: {(restored == original.Value ? "yes" : "no")}");
                if (restored != original.Value)
                {
                    Environment.ExitCode = 6;
                }
            }
            catch (Exception restoreException)
            {
                test.AppendLine($"- CRITICAL: restore failed: {Escape(restoreException.Message)}");
                Environment.ExitCode = 6;
            }
        }
    }

    WriteWindowsPowerModeReport(test);
}

void RunFanFixedScaleTest(bool lowRange)
{
    byte[] targets = lowRange ? [57, 68, 91, 114, 137] : [160, 194, 229];
    string rangeName = lowRange ? "low" : "high";
    bool confirmed = args.Any(argument =>
        argument.Equals("--confirm-fan-write", StringComparison.OrdinalIgnoreCase));
    var test = new StringBuilder();
    test.AppendLine("# AORUS fixed fan raw-scale test");
    test.AppendLine();
    test.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    test.AppendLine($"- Targets: {string.Join(", ", targets)} ({rangeName} range)");
    test.AppendLine($"- Explicit write confirmation present: {(confirmed ? "yes" : "no")}");
    test.AppendLine("- Mandatory exact restore of the original persistent state");
    test.AppendLine();

    if (!confirmed)
    {
        test.AppendLine("- Refused before opening the setter: `--confirm-fan-write` is required.");
        test.AppendLine("- Firmware/EC write methods invoked: **no**");
        WriteFixedScaleReport(test);
        Environment.ExitCode = 2;
        return;
    }

    using IAorusFanController controller = new GigabyteWmiFanController();
    FanControlState? original = null;
    bool aWriteSucceeded = false;
    try
    {
        original = controller.ReadAsync().GetAwaiter().GetResult();
        if (original.FixedStatusRaw != 0 || original.StepStatusRaw != 0 ||
            original.AutoStatusRaw != 0 || original.NvidiaThermalTargetRaw != 0)
        {
            throw new AorusFanControlException("Der Test startet nur aus dem verifizierten Normalzustand.");
        }

        using IAorusTelemetryReader telemetry = new GigabyteWmiTelemetryReader();
        foreach (byte rawValue in targets)
        {
            FanProfileChangeResult selected = controller.SetFixedAsync(rawValue).GetAwaiter().GetResult();
            aWriteSucceeded = true;
            test.AppendLine($"## Raw {rawValue}");
            test.AppendLine();
            AppendFanState(test, "Verified fixed", selected.VerifiedState);
            int sampleCount = lowRange ? 2 : 3;
            for (int sample = 1; sample <= sampleCount; sample++)
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
                TelemetrySnapshot value = telemetry.ReadAsync().GetAwaiter().GetResult();
                test.AppendLine(
                    $"- Sample {sample}: CPU {value.CpuTemperatureCelsius} °C, GPU {value.GpuTemperatureCelsius} °C, " +
                    $"CPU {value.CpuFanRpm} RPM / raw {value.CpuFanDutyPercent}, " +
                    $"GPU {value.GpuFanRpm} RPM / raw {value.GpuFanDutyPercent}");
                if (value.CpuTemperatureCelsius > 65 || value.GpuTemperatureCelsius > 65)
                {
                    throw new AorusFanControlException(
                        $"Temperature guard triggered at CPU {value.CpuTemperatureCelsius} °C / GPU {value.GpuTemperatureCelsius} °C.");
                }
            }
            test.AppendLine();
        }
    }
    catch (Exception exception)
    {
        test.AppendLine($"- Test failed: {Escape(exception.Message)}");
        Environment.ExitCode = 5;
    }
    finally
    {
        if (aWriteSucceeded && original is not null)
        {
            test.AppendLine("## Restore");
            test.AppendLine();
            try
            {
                FanProfileChangeResult restored = controller.RestoreAsync(original).GetAwaiter().GetResult();
                AppendFanState(test, "Verified original", restored.VerifiedState);
                test.AppendLine("- Result: exact persistent original restored");
            }
            catch (Exception restoreException)
            {
                test.AppendLine($"- CRITICAL: restore failed: {Escape(restoreException.Message)}");
                Environment.ExitCode = 6;
            }
        }
    }

    WriteFixedScaleReport(test);
}

void RunFanCurveWriteTest()
{
    bool confirmed = args.Any(argument =>
        argument.Equals("--confirm-fan-curve-write", StringComparison.OrdinalIgnoreCase));
    var test = new StringBuilder();
    test.AppendLine("# AORUS conservative fan-curve write test");
    test.AppendLine();
    test.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    test.AppendLine("- Change: point 1 value 68 to 80; no temperature lowered");
    test.AppendLine($"- Explicit curve-write confirmation present: {(confirmed ? "yes" : "no")}");
    test.AppendLine("- Mandatory restore: all 15 original points plus original mode");
    test.AppendLine();

    if (!confirmed)
    {
        test.AppendLine("- Refused before opening the setter: `--confirm-fan-curve-write` is required.");
        test.AppendLine("- Firmware/EC write methods invoked: **no**");
        WriteCurveTestReport(test);
        Environment.ExitCode = 2;
        return;
    }

    using IAorusFanController controller = new GigabyteWmiFanController();
    FanControlState? original = null;
    bool curveWriteStarted = false;
    try
    {
        original = controller.ReadAsync().GetAwaiter().GetResult();
        if (original.FixedStatusRaw != 0 || original.StepStatusRaw != 0 ||
            original.AutoStatusRaw != 0 || original.NvidiaThermalTargetRaw != 0)
        {
            throw new AorusFanControlException("Der Test startet nur aus dem verifizierten Normalzustand.");
        }
        if (original.Curve[1] != new FanCurvePoint(1, 50, 68) ||
            original.Curve[2].Value < 80)
        {
            throw new AorusFanControlException("Die erwartete Originalkurve liegt nicht mehr vor.");
        }

        FanCurvePoint[] modified = original.Curve.ToArray();
        modified[1] = new FanCurvePoint(1, 50, 80);
        curveWriteStarted = true;
        FanProfileChangeResult curveResult = controller.SetCurveAsync(modified).GetAwaiter().GetResult();
        test.AppendLine("## Curve readback");
        test.AppendLine();
        test.AppendLine($"- Original point 1: ({curveResult.OriginalState.Curve[1].Temperature}, {curveResult.OriginalState.Curve[1].Value})");
        test.AppendLine($"- Modified point 1: ({curveResult.VerifiedState.Curve[1].Temperature}, {curveResult.VerifiedState.Curve[1].Value})");
        test.AppendLine($"- Other 14 points unchanged: {(curveResult.OriginalState.Curve.Where((_, i) => i != 1).SequenceEqual(curveResult.VerifiedState.Curve.Where((_, i) => i != 1)) ? "yes" : "no")}");

        FanProfileChangeResult dynamic = controller.SetDynamicAsync().GetAwaiter().GetResult();
        test.AppendLine();
        test.AppendLine("## Dynamic result with modified point");
        test.AppendLine();
        AppendFanState(test, "Dynamic", dynamic.VerifiedState);
        using IAorusTelemetryReader telemetry = new GigabyteWmiTelemetryReader();
        for (int sample = 1; sample <= 3; sample++)
        {
            Thread.Sleep(TimeSpan.FromSeconds(2));
            TelemetrySnapshot value = telemetry.ReadAsync().GetAwaiter().GetResult();
            test.AppendLine(
                $"- Sample {sample}: CPU {value.CpuTemperatureCelsius} °C, GPU {value.GpuTemperatureCelsius} °C, " +
                $"CPU {value.CpuFanRpm} RPM / raw {value.CpuFanDutyPercent}, " +
                $"GPU {value.GpuFanRpm} RPM / raw {value.GpuFanDutyPercent}");
            if (value.CpuTemperatureCelsius > 65 || value.GpuTemperatureCelsius > 65)
            {
                throw new AorusFanControlException("Temperature guard triggered.");
            }
        }
    }
    catch (Exception exception)
    {
        test.AppendLine($"- Test failed: {Escape(exception.Message)}");
        Environment.ExitCode = 5;
    }
    finally
    {
        if (curveWriteStarted && original is not null)
        {
            test.AppendLine();
            test.AppendLine("## Restore");
            test.AppendLine();
            try
            {
                FanProfileChangeResult restored = controller.RestoreAsync(original).GetAwaiter().GetResult();
                AppendFanState(test, "Verified original", restored.VerifiedState);
                test.AppendLine($"- All 15 original points restored exactly: {restored.VerifiedState.Curve.SequenceEqual(original.Curve)}");
            }
            catch (Exception restoreException)
            {
                test.AppendLine($"- CRITICAL: restore failed: {Escape(restoreException.Message)}");
                Environment.ExitCode = 6;
            }
        }
    }

    WriteCurveTestReport(test);
}

void WriteCurveTestReport(StringBuilder test)
{
    string repositoryRoot = FindRepositoryRoot();
    string reportDirectory = Path.Combine(repositoryRoot, "research", "runs");
    Directory.CreateDirectory(reportDirectory);
    string reportPath = Path.Combine(
        reportDirectory,
        $"fan-curve-write-test-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(reportPath, test.ToString(), new UTF8Encoding(false));
    Console.WriteLine(test);
    Console.WriteLine($"Report written to: {reportPath}");
}

void WriteFixedScaleReport(StringBuilder test)
{
    string repositoryRoot = FindRepositoryRoot();
    string reportDirectory = Path.Combine(repositoryRoot, "research", "runs");
    Directory.CreateDirectory(reportDirectory);
    string reportPath = Path.Combine(
        reportDirectory,
        $"fan-fixed-scale-test-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(reportPath, test.ToString(), new UTF8Encoding(false));
    Console.WriteLine(test);
    Console.WriteLine($"Report written to: {reportPath}");
}

static string DescribeOverlay(Guid value) => value == WindowsPowerOverlayController.BalancedGuid
    ? "Balanced"
    : value == WindowsPowerOverlayController.BestEfficiencyGuid
        ? "Best efficiency"
        : value == WindowsPowerOverlayController.BestPerformanceGuid
            ? "Best performance"
            : "unknown";

void WriteWindowsPowerModeReport(StringBuilder test)
{
    string repositoryRoot = FindRepositoryRoot();
    string reportDirectory = Path.Combine(repositoryRoot, "research", "runs");
    Directory.CreateDirectory(reportDirectory);
    string reportPath = Path.Combine(
        reportDirectory,
        $"windows-power-overlay-test-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(reportPath, test.ToString(), new UTF8Encoding(false));
    Console.WriteLine(test);
    Console.WriteLine($"Report written to: {reportPath}");
}

void RunTemporaryFanProfileTest(string profileSlug)
{
    string profileName = profileSlug switch
    {
        "quiet" => "Quiet",
        "gaming" => "Gaming",
        "maximum" => "Maximum",
        "dynamic" => "Dynamic",
        _ => throw new ArgumentOutOfRangeException(nameof(profileSlug))
    };
    bool confirmed = args.Any(argument =>
        argument.Equals("--confirm-fan-write", StringComparison.OrdinalIgnoreCase));
    var test = new StringBuilder();
    test.AppendLine($"# AORUS temporary {profileName} fan-profile test");
    test.AppendLine();
    test.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    test.AppendLine($"- Requested test: {profileName}, five samples, mandatory return to Normal");
    test.AppendLine($"- Explicit write confirmation present: {(confirmed ? "yes" : "no")}");
    test.AppendLine("- Windows power and GPU power configuration: not changed");
    test.AppendLine();

    if (!confirmed)
    {
        test.AppendLine("- Refused before opening the setter: `--confirm-fan-write` is required.");
        test.AppendLine("- Firmware/EC write methods invoked: **no**");
        WriteTemporaryFanTestReport(test, profileSlug);
        Environment.ExitCode = 2;
        return;
    }

    using IAorusFanController controller = new GigabyteWmiFanController();
    bool quietWasVerified = false;
    FanControlState? originalState = null;
    try
    {
        FanControlState before = controller.ReadAsync().GetAwaiter().GetResult();
        originalState = before;
        if (before.FixedStatusRaw != 0 || before.StepStatusRaw != 0 ||
            before.AutoStatusRaw != 0 || before.NvidiaThermalTargetRaw != 0)
        {
            throw new AorusFanControlException(
                "Der Test startet nur aus dem verifizierten Normalzustand.");
        }

        FanProfileChangeResult selected = profileSlug switch
        {
            "quiet" => controller.SetQuietAsync().GetAwaiter().GetResult(),
            "gaming" => controller.SetGamingAsync().GetAwaiter().GetResult(),
            "maximum" => controller.SetMaximumAsync().GetAwaiter().GetResult(),
            "dynamic" => controller.SetDynamicAsync().GetAwaiter().GetResult(),
            _ => throw new ArgumentOutOfRangeException(nameof(profileSlug))
        };
        quietWasVerified = true;
        test.AppendLine($"## {profileName} readback");
        test.AppendLine();
        AppendFanState(test, "Before", selected.OriginalState);
        AppendFanState(test, profileName, selected.VerifiedState);
        test.AppendLine($"- Curve preserved exactly: {(selected.OriginalState.Curve.SequenceEqual(selected.VerifiedState.Curve) ? "yes" : "no")}");
        test.AppendLine();
        test.AppendLine($"## Telemetry while {profileName} is active");
        test.AppendLine();
        using IAorusTelemetryReader telemetry = new GigabyteWmiTelemetryReader();
        for (int sample = 1; sample <= 5; sample++)
        {
            TelemetrySnapshot value = telemetry.ReadAsync().GetAwaiter().GetResult();
            test.AppendLine(
                $"- Sample {sample} at {value.CapturedAt:HH:mm:ss}: " +
                $"CPU {value.CpuTemperatureCelsius} °C, GPU {value.GpuTemperatureCelsius} °C, " +
                $"CPU {value.CpuFanRpm} RPM / raw duty {value.CpuFanDutyPercent}, " +
                $"GPU {value.GpuFanRpm} RPM / raw duty {value.GpuFanDutyPercent}");
            if (sample < 5)
            {
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
        }
    }
    catch (Exception exception)
    {
        test.AppendLine();
        test.AppendLine("## Test error");
        test.AppendLine();
        test.AppendLine($"- {Escape(exception.Message)}");
        Environment.ExitCode = 5;
    }
    finally
    {
        if (quietWasVerified)
        {
            test.AppendLine();
            test.AppendLine("## Mandatory return to Normal");
            test.AppendLine();
            try
            {
                FanProfileChangeResult restored = controller.RestoreAsync(originalState!).GetAwaiter().GetResult();
                AppendFanState(test, "Before restore", restored.OriginalState);
                AppendFanState(test, "Verified original", restored.VerifiedState);
                test.AppendLine("- Result: original state restored and verified, including stored fixed speed and GPU duty");
            }
            catch (Exception restoreException)
            {
                test.AppendLine($"- CRITICAL: Normal restore failed: {Escape(restoreException.Message)}");
                Environment.ExitCode = 6;
            }
        }
    }

    WriteTemporaryFanTestReport(test, profileSlug);
}

void WriteTemporaryFanTestReport(StringBuilder test, string profileSlug)
{
    string repositoryRoot = FindRepositoryRoot();
    string reportDirectory = Path.Combine(repositoryRoot, "research", "runs");
    Directory.CreateDirectory(reportDirectory);
    string reportPath = Path.Combine(
        reportDirectory,
        $"fan-{profileSlug}-test-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(reportPath, test.ToString(), new UTF8Encoding(false));
    Console.WriteLine(test);
    Console.WriteLine($"Report written to: {reportPath}");
}

static void AppendFanState(StringBuilder target, string label, FanControlState state)
{
    target.AppendLine(
        $"- {label}: fixed `{state.FixedStatusRaw}`, step `{state.StepStatusRaw}`, " +
        $"auto `{state.AutoStatusRaw}`, thermal `{state.NvidiaThermalTargetRaw}`, " +
        $"stored fixed speed `{state.FixedSpeedRaw}`, current GPU duty `{state.GpuDutyRaw}`");
}

void WriteFanChangeReport(StringBuilder change)
{
    string repositoryRoot = FindRepositoryRoot();
    string reportDirectory = Path.Combine(repositoryRoot, "research", "runs");
    Directory.CreateDirectory(reportDirectory);
    string reportPath = Path.Combine(
        reportDirectory,
        $"fan-normal-change-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(reportPath, change.ToString(), new UTF8Encoding(false));
    Console.WriteLine(change);
    Console.WriteLine($"Report written to: {reportPath}");
}

static ushort InvokeUInt16GetterUnchecked(
    ManagementObject instance,
    ManagementClass getClass,
    string methodName)
{
    using ManagementBaseObject input = getClass.GetMethodParameters(methodName);
    using ManagementBaseObject output = instance.InvokeMethod(
        methodName,
        input,
        new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) });
    object? value = output["Data"];
    if (value is null)
    {
        throw new InvalidOperationException($"{methodName} returned no Data value.");
    }

    return Convert.ToUInt16(value, CultureInfo.InvariantCulture);
}

static string FormatMethodSignature(MethodData method)
{
    string inputs = string.Join(
        ", ",
        method.InParameters?.Properties.Cast<PropertyData>()
            .Select(property => $"{property.Name}:{property.Type}") ?? []);
    string outputs = string.Join(
        ", ",
        method.OutParameters?.Properties.Cast<PropertyData>()
            .Select(property => $"{property.Name}:{property.Type}") ?? []);
    return $"in [{inputs}], out [{outputs}]";
}

static string FormatManagementValues(ManagementBaseObject values) =>
    string.Join(
        ", ",
        values.Properties.Cast<PropertyData>().Select(property =>
            $"{property.Name}={Convert.ToString(property.Value, CultureInfo.InvariantCulture)}"));

void AppendRegistryValue(StringBuilder target, string subKey, string valueName)
{
    try
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(subKey, writable: false);
        object? value = key?.GetValue(valueName);
        target.AppendLine($"- `{valueName}`: `{Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "not set")}`");
    }
    catch (Exception exception)
    {
        target.AppendLine($"- `{valueName}`: unavailable ({Escape(exception.Message)})");
    }
}

void AppendQueryRows(
    StringBuilder target,
    string scope,
    string query,
    string[] properties)
{
    try
    {
        List<Dictionary<string, object?>> rows = Query(scope, query);
        if (rows.Count == 0)
        {
            target.AppendLine($"- `{Escape(query)}`: no rows");
            return;
        }

        foreach (IReadOnlyDictionary<string, object?> row in rows)
        {
            target.AppendLine("- " + string.Join(
                "; ",
                properties.Select(property => $"{property}=`{Escape(GetText(row, property))}`")));
        }
    }
    catch (Exception exception)
    {
        target.AppendLine($"- Query failed: {Escape(exception.Message)}");
    }
}

void AppendCommandOutput(StringBuilder target, string label, string executable, string arguments)
{
    try
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.Start();
        string stdout = process.StandardOutput.ReadToEnd().Trim();
        string stderr = process.StandardError.ReadToEnd().Trim();
        if (!process.WaitForExit(5000))
        {
            process.Kill(entireProcessTree: true);
            target.AppendLine($"- {label}: timed out");
            return;
        }

        target.AppendLine($"- {label}: exit `{process.ExitCode}`");
        target.AppendLine("```text");
        target.AppendLine(string.IsNullOrWhiteSpace(stdout) ? "(no output)" : stdout);
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            target.AppendLine("stderr: " + stderr);
        }
        target.AppendLine("```");
    }
    catch (Exception exception)
    {
        target.AppendLine($"- {label}: unavailable ({Escape(exception.Message)})");
    }
}

void WriteThermalPowerInspectionReport(StringBuilder inspection)
{
    string repositoryRoot = FindRepositoryRoot();
    string reportDirectory = Path.Combine(repositoryRoot, "research", "runs");
    Directory.CreateDirectory(reportDirectory);
    string reportPath = Path.Combine(
        reportDirectory,
        $"thermal-power-inspection-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(reportPath, inspection.ToString(), new UTF8Encoding(false));
    Console.WriteLine(inspection);
    Console.WriteLine($"Report written to: {reportPath}");
}

void RunBatteryChargeChange(int? limitPercent, bool standardMode)
{
    bool confirmed = args.Any(argument =>
        argument.Equals("--confirm-battery-write", StringComparison.OrdinalIgnoreCase));
    var changeReport = new StringBuilder();
    changeReport.AppendLine("# AORUS battery charge-limit change");
    changeReport.AppendLine();
    changeReport.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    changeReport.AppendLine($"- Requested state: {(standardMode ? "Standard mode (raw 0 + 100)" : $"Custom {limitPercent}% (raw 4 + {limitPercent})")}");
    changeReport.AppendLine($"- Explicit write confirmation present: {(confirmed ? "yes" : "no")}");
    changeReport.AppendLine();

    if (limitPercent.HasValue && standardMode)
    {
        changeReport.AppendLine("- Refused: custom limit and Standard mode cannot be requested together.");
        WriteBatteryChangeReport(changeReport);
        Environment.ExitCode = 2;
        return;
    }

    if (!confirmed)
    {
        changeReport.AppendLine("- Refused before opening the setter: `--confirm-battery-write` is required.");
        changeReport.AppendLine("- Firmware/EC write methods invoked: **no**");
        WriteBatteryChangeReport(changeReport);
        Environment.ExitCode = 2;
        return;
    }

    if (limitPercent is < GigabyteWmiBatteryChargeController.MinimumCustomLimitPercent or
        > GigabyteWmiBatteryChargeController.MaximumCustomLimitPercent)
    {
        changeReport.AppendLine("- Refused before opening the setter: custom limit must be 60–100%.");
        changeReport.AppendLine("- Firmware/EC write methods invoked: **no**");
        WriteBatteryChangeReport(changeReport);
        Environment.ExitCode = 2;
        return;
    }

    try
    {
        using IAorusBatteryChargeController controller = new GigabyteWmiBatteryChargeController();
        DeviceCompatibility compatibility = controller.CheckCompatibility();
        changeReport.AppendLine("## Compatibility gate");
        changeReport.AppendLine();
        changeReport.AppendLine($"- Manufacturer: `{Escape(compatibility.Manufacturer)}`");
        changeReport.AppendLine($"- Model: `{Escape(compatibility.Model)}`");
        changeReport.AppendLine($"- BIOS: `{Escape(compatibility.BiosVersion)}`");
        changeReport.AppendLine($"- Result: {(compatibility.IsSupported ? "exact allowlist match" : Escape(compatibility.Message))}");
        changeReport.AppendLine();

        BatteryChargeChangeResult result = standardMode
            ? controller.SetStandardModeAsync().GetAwaiter().GetResult()
            : controller.SetCustomLimitAsync(limitPercent!.Value).GetAwaiter().GetResult();

        changeReport.AppendLine("## Verified result");
        changeReport.AppendLine();
        changeReport.AppendLine($"- Original firmware pair: `{result.OriginalState.PolicyRaw} + {result.OriginalState.StoredStopPercent}`");
        changeReport.AppendLine($"- Verified firmware pair: `{result.VerifiedState.PolicyRaw} + {result.VerifiedState.StoredStopPercent}`");
        changeReport.AppendLine("- Write order: policy first, threshold second");
        changeReport.AppendLine("- Readback: exact match");
        changeReport.AppendLine("- Result: success");
    }
    catch (Exception exception)
    {
        changeReport.AppendLine("## Result");
        changeReport.AppendLine();
        changeReport.AppendLine($"- Failed: {Escape(exception.Message)}");
        changeReport.AppendLine("- See the error above for the automatic rollback result.");
        Environment.ExitCode = 5;
    }

    WriteBatteryChangeReport(changeReport);
}

void WriteBatteryChangeReport(StringBuilder changeReport)
{
    string repositoryRoot = FindRepositoryRoot();
    string reportDirectory = Path.Combine(repositoryRoot, "research", "runs");
    Directory.CreateDirectory(reportDirectory);
    string reportPath = Path.Combine(
        reportDirectory,
        $"battery-change-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(reportPath, changeReport.ToString(), new UTF8Encoding(false));
    Console.WriteLine(changeReport);
    Console.WriteLine($"Report written to: {reportPath}");
}

void RunLiveMonitor()
{
    const int defaultIntervalMilliseconds = 2000;
    int intervalMilliseconds = ReadPositiveIntArgument("--interval-ms", defaultIntervalMilliseconds);
    int sampleLimit = ReadPositiveIntArgument("--samples", int.MaxValue);
    bool plainOutput = args.Any(argument =>
        argument.Equals("--plain", StringComparison.OrdinalIgnoreCase));

    Console.OutputEncoding = Encoding.UTF8;
    Console.WriteLine(plainOutput
        ? "AORUS 5 SE - Live-Monitor (read-only)"
        : "AORUS 5 SE – Live-Monitor (nur lesend)");
    Console.WriteLine();

    if (!IsAdministrator())
    {
        Console.Error.WriteLine("Windows erlaubt die ACPI-Sensorabfrage nur als Administrator.");
        Console.Error.WriteLine("Bitte tools\\Start-AorusMonitor.ps1 starten oder das Programm als Administrator öffnen.");
        Environment.ExitCode = 1;
        return;
    }

    try
    {
        using IAorusTelemetryReader reader = new GigabyteWmiTelemetryReader();
        DeviceCompatibility compatibility = reader.CheckCompatibility();
        if (!compatibility.IsSupported)
        {
            Console.Error.WriteLine($"Sicherheitsstopp: {compatibility.Message}");
            Environment.ExitCode = 2;
            return;
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            for (int sample = 1; sample <= sampleLimit && !cancellation.IsCancellationRequested; sample++)
            {
                TelemetrySnapshot snapshot = reader.ReadAsync(cancellation.Token)
                    .GetAwaiter()
                    .GetResult();
                string timestamp = snapshot.CapturedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

                if (!plainOutput)
                {
                    TryClearConsole();
                    Console.WriteLine("AORUS 5 SE – Live-Monitor (nur lesend)");
                    Console.WriteLine("BIOS FB0F | Beenden mit Strg+C");
                    Console.WriteLine();
                    Console.WriteLine($"CPU-Temperatur       {snapshot.CpuTemperatureCelsius,5} °C");
                    Console.WriteLine($"GPU-Temperatur       {snapshot.GpuTemperatureCelsius,5} °C");
                    Console.WriteLine($"CPU-Lüfter           {snapshot.CpuFanRpm,5} U/min   ({snapshot.CpuFanDutyPercent} %)");
                    Console.WriteLine($"GPU-Lüfter           {snapshot.GpuFanRpm,5} U/min   ({snapshot.GpuFanDutyPercent} %)");
                    Console.WriteLine();
                    Console.WriteLine($"Letzte Messung: {timestamp} | Intervall: {intervalMilliseconds / 1000.0:F1} s");
                }
                else
                {
                    Console.WriteLine(
                        $"{timestamp} CPU={snapshot.CpuTemperatureCelsius}C GPU={snapshot.GpuTemperatureCelsius}C " +
                        $"CPU-Fan={snapshot.CpuFanRpm}RPM/{snapshot.CpuFanDutyPercent}% " +
                        $"GPU-Fan={snapshot.GpuFanRpm}RPM/{snapshot.GpuFanDutyPercent}%");
                }

                if (sample < sampleLimit &&
                    cancellation.Token.WaitHandle.WaitOne(intervalMilliseconds))
                {
                    break;
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Live-Monitor konnte nicht lesen: {exception.Message}");
        Environment.ExitCode = 5;
    }
}

int ReadPositiveIntArgument(string name, int fallback)
{
    for (int index = 0; index < args.Length - 1; index++)
    {
        if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int value) &&
            value > 0)
        {
            return value;
        }
    }

    return fallback;
}

byte[] ReadByteListArgument(string name, byte[] fallback)
{
    for (int index = 0; index < args.Length - 1; index++)
    {
        if (!args[index].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        byte[] parsed = args[index + 1]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => byte.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte value)
                ? (byte?)value
                : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        if (parsed.Length > 0)
        {
            return parsed;
        }
    }

    return fallback;
}

int? ReadOptionalIntArgument(string name)
{
    for (int index = 0; index < args.Length - 1; index++)
    {
        if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return value;
        }
    }

    return null;
}

static void TryClearConsole()
{
    try
    {
        Console.Clear();
    }
    catch (IOException)
    {
        // Redirected/non-interactive output cannot be cleared.
    }
}

void RunKeyboardHidInspection()
{
    const int keyboardVendorId = 0x1044;
    const int keyboardProductId = 0x7A41;

    var keyboardReport = new StringBuilder();
    keyboardReport.AppendLine("# AORUS keyboard read-only HID inventory");
    keyboardReport.AppendLine();
    keyboardReport.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    keyboardReport.AppendLine("- Target: `VID 1044 / PID 7A41`");
    keyboardReport.AppendLine("- HID communication stream opened: **no**");
    keyboardReport.AppendLine("- Input/feature report requested: **no**");
    keyboardReport.AppendLine("- Output report sent: **no**");
    keyboardReport.AppendLine();

    HidDevice[] devices = DeviceList.Local
        .GetHidDevices(keyboardVendorId, keyboardProductId)
        .OrderBy(device => GetInterfaceLabel(device.DevicePath), StringComparer.OrdinalIgnoreCase)
        .ToArray();

    keyboardReport.AppendLine($"## HID collections ({devices.Length})");
    keyboardReport.AppendLine();
    if (devices.Length == 0)
    {
        keyboardReport.AppendLine("- No matching HID collection found.");
    }

    foreach (HidDevice device in devices)
    {
        string interfaceLabel = GetInterfaceLabel(device.DevicePath);
        keyboardReport.AppendLine($"### `{interfaceLabel}`");
        keyboardReport.AppendLine();
        keyboardReport.AppendLine($"- Manufacturer: {Escape(TryRead(() => device.GetManufacturer()))}");
        keyboardReport.AppendLine($"- Product: {Escape(TryRead(() => device.GetProductName()))}");
        keyboardReport.AppendLine($"- Maximum input report: {device.GetMaxInputReportLength()} bytes");
        keyboardReport.AppendLine($"- Maximum output report: {device.GetMaxOutputReportLength()} bytes");
        keyboardReport.AppendLine($"- Maximum feature report: {device.GetMaxFeatureReportLength()} bytes");
        keyboardReport.AppendLine("- Device path intentionally omitted (may contain a device-unique identifier).");
        try
        {
            ReportDescriptor descriptor = device.GetReportDescriptor();
            keyboardReport.AppendLine($"- Uses report IDs: {(descriptor.ReportsUseID ? "yes" : "no")}");
            foreach (Report hidReport in descriptor.Reports
                         .OrderBy(item => item.ReportType)
                         .ThenBy(item => item.ReportID))
            {
                string usages = string.Join(
                    ", ",
                    hidReport.GetAllUsages()
                        .Distinct()
                        .OrderBy(usage => usage)
                        .Select(usage => $"0x{usage:X8}"));
                keyboardReport.AppendLine(
                    $"  - {hidReport.ReportType}: ID `0x{hidReport.ReportID:X2}`, " +
                    $"length {hidReport.Length} bytes, usages {Escape(string.IsNullOrEmpty(usages) ? "none" : usages)}");
            }
        }
        catch (Exception exception)
        {
            keyboardReport.AppendLine($"- Parsed descriptor: unavailable ({Escape(exception.Message)})");
        }

        keyboardReport.AppendLine();
    }

    keyboardReport.AppendLine("## Interpretation");
    keyboardReport.AppendLine();
    keyboardReport.AppendLine("- Enumeration reads Windows HID metadata only; no communication stream was opened and no report reached the keyboard.");
    keyboardReport.AppendLine("- A vendor-defined collection with a large output or feature report is the likely RGB-control channel.");
    keyboardReport.AppendLine("- Report lengths alone do not reveal packet contents or authorize sending a report.");

    string root = FindRepositoryRoot();
    string outputDirectory = Path.Combine(root, "research", "runs");
    Directory.CreateDirectory(outputDirectory);
    string outputPath = Path.Combine(
        outputDirectory,
        $"keyboard-hid-inventory-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(outputPath, keyboardReport.ToString(), new UTF8Encoding(false));

    Console.WriteLine(keyboardReport);
    Console.WriteLine($"Report written to: {outputPath}");
}

void RunKeyboardFeatureRead()
{
    const int keyboardVendorId = 0x1044;
    const int keyboardProductId = 0x7A41;
    string[] approvedCollections = ["MI_02 / COL_07", "MI_03"];

    var stateReport = new StringBuilder();
    stateReport.AppendLine("# AORUS keyboard read-only feature report");
    stateReport.AppendLine();
    stateReport.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    stateReport.AppendLine("- Target: `VID 1044 / PID 7A41`");
    stateReport.AppendLine("- Operation: USB HID `GET_REPORT (Feature)` only");
    stateReport.AppendLine("- Output report sent: **no**");
    stateReport.AppendLine("- Feature report set: **no**");
    stateReport.AppendLine();

    HidDevice[] devices = DeviceList.Local
        .GetHidDevices(keyboardVendorId, keyboardProductId)
        .Where(device => approvedCollections.Contains(
            GetInterfaceLabel(device.DevicePath),
            StringComparer.OrdinalIgnoreCase))
        .OrderBy(device => GetInterfaceLabel(device.DevicePath), StringComparer.OrdinalIgnoreCase)
        .ToArray();

    foreach (HidDevice device in devices)
    {
        string interfaceLabel = GetInterfaceLabel(device.DevicePath);
        int reportLength = device.GetMaxFeatureReportLength();
        byte reportId = interfaceLabel.Equals("MI_02 / COL_07", StringComparison.OrdinalIgnoreCase)
            ? (byte)0x5A
            : (byte)0x00;
        stateReport.AppendLine($"## `{interfaceLabel}`");
        stateReport.AppendLine();

        if (reportLength <= 0)
        {
            stateReport.AppendLine("- No feature report exposed.");
            stateReport.AppendLine();
            continue;
        }

        try
        {
            byte[] buffer = new byte[reportLength];
            buffer[0] = reportId;
            using HidStream stream = device.Open();
            stream.GetFeature(buffer);
            stateReport.AppendLine($"- Report ID: `0x{reportId:X2}`");
            stateReport.AppendLine($"- Length: {buffer.Length} bytes including report ID byte");
            stateReport.AppendLine($"- Raw bytes: `{Convert.ToHexString(buffer)}`");
            stateReport.AppendLine($"- Payload bytes: `{Convert.ToHexString(buffer.AsSpan(1))}`");
        }
        catch (Exception exception)
        {
            stateReport.AppendLine($"- Read failed: {Escape(exception.Message)}");
        }

        stateReport.AppendLine();
    }

    if (devices.Length == 0)
    {
        stateReport.AppendLine("- No approved feature-report collection found.");
        stateReport.AppendLine();
    }

    stateReport.AppendLine("## Interpretation");
    stateReport.AppendLine();
    stateReport.AppendLine("- Returned bytes are retained as uninterpreted state until their meaning is confirmed by independent observations.");
    stateReport.AppendLine("- Reading a feature report does not reveal whether each byte is a color, mode, brightness, version, or capability flag.");

    string root = FindRepositoryRoot();
    string outputDirectory = Path.Combine(root, "research", "runs");
    Directory.CreateDirectory(outputDirectory);
    string outputPath = Path.Combine(
        outputDirectory,
        $"keyboard-feature-read-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(outputPath, stateReport.ToString(), new UTF8Encoding(false));

    Console.WriteLine(stateReport);
    Console.WriteLine($"Report written to: {outputPath}");
}

void RunKeyboardRgbQuery()
{
    const int keyboardVendorId = 0x1044;
    const int keyboardProductId = 0x7A41;
    const int featureLength = 9;
    const byte lightingQueryCommand = 0x88;
    const byte firmwareQueryCommand = 0x80;

    var rgbReport = new StringBuilder();
    rgbReport.AppendLine("# AORUS keyboard RGB query");
    rgbReport.AppendLine();
    rgbReport.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    rgbReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
    rgbReport.AppendLine("- Official Gigabyte query commands sent with `SET_FEATURE`: **yes (`0x80` firmware, `0x88` lighting)**");
    rgbReport.AppendLine("- State-changing Gigabyte command sent in this mode: **no**");
    rgbReport.AppendLine("- Output report sent: **no**");
    rgbReport.AppendLine();

    HidDevice? device = DeviceList.Local
        .GetHidDevices(keyboardVendorId, keyboardProductId)
        .SingleOrDefault(candidate =>
            GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) &&
            candidate.GetMaxFeatureReportLength() == featureLength);

    if (device is null)
    {
        rgbReport.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
        WriteKeyboardRgbReport(rgbReport);
        return;
    }

    try
    {
        using HidStream stream = device.Open();
        byte[] firmware = Query(firmwareQueryCommand, 0, 10);
        string firmwarePart = firmware[3].ToString(CultureInfo.InvariantCulture);
        string firmwareVersion = firmwarePart.Length == 1
            ? $"{firmware[2]}.0.{firmwarePart}"
            : $"{firmware[2]}.{firmwarePart[0]}.{firmwarePart[1]}";

        rgbReport.AppendLine("## Keyboard firmware");
        rgbReport.AppendLine();
        rgbReport.AppendLine($"- Raw response: `{Convert.ToHexString(firmware)}`");
        rgbReport.AppendLine($"- Gigabyte-formatted version: `{firmwareVersion}`");
        rgbReport.AppendLine();

        byte[] effect = Query(lightingQueryCommand, 0, 500);

        rgbReport.AppendLine("## Global lighting state");
        rgbReport.AppendLine();
        rgbReport.AppendLine($"- Raw response: `{Convert.ToHexString(effect)}`");
        rgbReport.AppendLine($"- Effect code: `{effect[3]}` (`0x{effect[3]:X2}`)");
        rgbReport.AppendLine($"- Speed: `{effect[4]}`");
        rgbReport.AppendLine($"- Nominal brightness byte: `{effect[5]}` (Gigabyte UI scale label: {Math.Min(effect[5] * 2, 100)}%; not proven as visible PWM on this firmware)");
        rgbReport.AppendLine($"- Color code: `{effect[6]}` (`0x{effect[6]:X2}`)");
        rgbReport.AppendLine($"- Direction code: `{effect[7]}` (`0x{effect[7]:X2}`)");
        rgbReport.AppendLine();

        rgbReport.AppendLine("## Three RGB zones");
        rgbReport.AppendLine();
        for (byte zone = 1; zone <= 3; zone++)
        {
            byte[] zoneState = Query(lightingQueryCommand, zone, 65);
            rgbReport.AppendLine($"### Zone {zone}");
            rgbReport.AppendLine();
            rgbReport.AppendLine($"- Raw response: `{Convert.ToHexString(zoneState)}`");
            rgbReport.AppendLine($"- RGB: `({zoneState[3]}, {zoneState[4]}, {zoneState[5]})`");
            rgbReport.AppendLine($"- Hex color: `#{zoneState[3]:X2}{zoneState[4]:X2}{zoneState[5]:X2}`");
            rgbReport.AppendLine($"- Nominal brightness byte: `{zoneState[6]}` (`50`=on and tested values below `50`=off on firmware 19.0.4)");
            rgbReport.AppendLine();
        }

        byte[] Query(byte command, byte selector, int delayMilliseconds)
        {
            byte[] request = new byte[featureLength];
            request[1] = command;
            request[2] = selector;
            request[8] = CalculateGigabyteChecksum(request);
            stream.SetFeature(request);
            Thread.Sleep(delayMilliseconds);

            byte[] response = new byte[featureLength];
            stream.GetFeature(response);
            return response;
        }
    }
    catch (Exception exception)
    {
        rgbReport.AppendLine("## Query failure");
        rgbReport.AppendLine();
        rgbReport.AppendLine($"- {Escape(exception.Message)}");
    }

    rgbReport.AppendLine("## Interpretation boundary");
    rgbReport.AppendLine();
    rgbReport.AppendLine("- Byte meanings come from Gigabyte's signed `GBT_Keyboard 25.07.25.01` implementation for this exact USB identity.");
    rgbReport.AppendLine("- Official enum mappings are documented in `research/KEYBOARD-CAPABILITIES.md`; the all-zero global response is outside the defined effect enum and is therefore reported without guessing.");
    WriteKeyboardRgbReport(rgbReport);
}

static byte CalculateGigabyteChecksum(ReadOnlySpan<byte> packet)
{
    int sum = 0;
    for (int index = 1; index <= 7; index++)
    {
        sum += packet[index];
    }

    return unchecked((byte)(255 - sum));
}

static void WriteKeyboardRgbReport(StringBuilder rgbReport)
{
    string root = FindRepositoryRoot();
    string outputDirectory = Path.Combine(root, "research", "runs");
    Directory.CreateDirectory(outputDirectory);
    string outputPath = Path.Combine(
        outputDirectory,
        $"keyboard-rgb-query-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(outputPath, rgbReport.ToString(), new UTF8Encoding(false));

    Console.WriteLine(rgbReport);
    Console.WriteLine($"Report written to: {outputPath}");
}

void RunKeyboardZoneWriteVerification()
{
    const int keyboardVendorId = 0x1044;
    const int keyboardProductId = 0x7A41;
    const int featureLength = 9;

    var testReport = new StringBuilder();
    testReport.AppendLine("# AORUS guarded RGB zone write verification");
    testReport.AppendLine();
    testReport.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    testReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
    testReport.AppendLine("- Scope: zone 1 only; temporary color; original state restored in `finally`");
    testReport.AppendLine("- Key matrix, macros, effects, firmware, BIOS, and EC modified: **no**");
    testReport.AppendLine();

    HidDevice? device = DeviceList.Local
        .GetHidDevices(keyboardVendorId, keyboardProductId)
        .SingleOrDefault(candidate =>
            GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) &&
            candidate.GetMaxFeatureReportLength() == featureLength);

    if (device is null)
    {
        testReport.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
        WriteZoneTestReport(testReport);
        return;
    }

    byte[]? original = null;
    using HidStream stream = device.Open();
    try
    {
        original = QueryZone(1);
        byte testRed = original[5];
        byte testGreen = original[4];
        byte testBlue = original[3];
        if (testRed == original[3] && testGreen == original[4] && testBlue == original[5])
        {
            testRed = unchecked((byte)(original[3] + 64));
        }

        testReport.AppendLine($"- Original: `#{original[3]:X2}{original[4]:X2}{original[5]:X2}`, brightness `{original[6]}`");
        testReport.AppendLine($"- Temporary test: `#{testRed:X2}{testGreen:X2}{testBlue:X2}`, brightness `{original[6]}`");
        SetZone(1, testRed, testGreen, testBlue, original[6]);
        Thread.Sleep(350);
        byte[] observed = QueryZone(1);
        bool applied = observed[3] == testRed && observed[4] == testGreen &&
            observed[5] == testBlue && observed[6] == original[6];
        testReport.AppendLine($"- Readback during test: `#{observed[3]:X2}{observed[4]:X2}{observed[5]:X2}`, brightness `{observed[6]}`");
        testReport.AppendLine($"- Temporary write verified: **{(applied ? "yes" : "no")}**");
    }
    catch (Exception exception)
    {
        testReport.AppendLine($"- Test error: {Escape(exception.Message)}");
    }
    finally
    {
        if (original is not null)
        {
            try
            {
                SetZone(1, original[3], original[4], original[5], original[6]);
                Thread.Sleep(65);
                byte[] restored = QueryZone(1);
                bool restoreVerified = restored[3] == original[3] && restored[4] == original[4] &&
                    restored[5] == original[5] && restored[6] == original[6];
                testReport.AppendLine($"- Final readback: `#{restored[3]:X2}{restored[4]:X2}{restored[5]:X2}`, brightness `{restored[6]}`");
                testReport.AppendLine($"- Original state restored and verified: **{(restoreVerified ? "yes" : "no")}**");
            }
            catch (Exception exception)
            {
                testReport.AppendLine($"- RESTORE ERROR: {Escape(exception.Message)}");
            }
        }
    }

    WriteZoneTestReport(testReport);

    byte[] QueryZone(byte zone)
    {
        byte[] request = new byte[featureLength];
        request[1] = 0x88;
        request[2] = zone;
        request[8] = CalculateGigabyteChecksum(request);
        stream.SetFeature(request);
        Thread.Sleep(65);
        byte[] response = new byte[featureLength];
        stream.GetFeature(response);
        return response;
    }

    void SetZone(byte zone, byte red, byte green, byte blue, byte brightness)
    {
        byte[] request = new byte[featureLength];
        request[1] = 0x08;
        request[2] = zone;
        request[3] = red;
        request[4] = green;
        request[5] = blue;
        request[6] = brightness;
        request[8] = CalculateGigabyteChecksum(request);
        stream.SetFeature(request);
        Thread.Sleep(65);
    }
}

static void WriteZoneTestReport(StringBuilder testReport)
{
    string root = FindRepositoryRoot();
    string outputDirectory = Path.Combine(root, "research", "runs");
    Directory.CreateDirectory(outputDirectory);
    string outputPath = Path.Combine(outputDirectory, $"keyboard-zone-write-test-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(outputPath, testReport.ToString(), new UTF8Encoding(false));
    Console.WriteLine(testReport);
    Console.WriteLine($"Report written to: {outputPath}");
}

void RunSetKeyboardGreen()
{
    const int keyboardVendorId = 0x1044;
    const int keyboardProductId = 0x7A41;
    const int featureLength = 9;

    var setReport = new StringBuilder();
    setReport.AppendLine("# AORUS keyboard persistent green setting");
    setReport.AppendLine();
    setReport.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    setReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
    setReport.AppendLine("- Requested color: `#00FF00` on zones 1–3");
    setReport.AppendLine("- Restore after write: **no, explicitly requested by user**");
    setReport.AppendLine();

    HidDevice? device = DeviceList.Local
        .GetHidDevices(keyboardVendorId, keyboardProductId)
        .SingleOrDefault(candidate =>
            GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) &&
            candidate.GetMaxFeatureReportLength() == featureLength);

    if (device is null)
    {
        setReport.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
        WritePersistentColorReport(setReport);
        return;
    }

    try
    {
        using HidStream stream = device.Open();
        for (byte zone = 1; zone <= 3; zone++)
        {
            byte[] before = QueryZone(zone);
            SetZone(zone, 0, 255, 0, before[6]);
            byte[] after = QueryZone(zone);
            bool verified = after[3] == 0 && after[4] == 255 && after[5] == 0 && after[6] == before[6];
            setReport.AppendLine($"- Zone {zone}: before `#{before[3]:X2}{before[4]:X2}{before[5]:X2}`, " +
                $"after `#{after[3]:X2}{after[4]:X2}{after[5]:X2}`, brightness `{after[6]}`, " +
                $"verified **{(verified ? "yes" : "no")}**");
        }

        byte[] QueryZone(byte zone)
        {
            byte[] request = new byte[featureLength];
            request[1] = 0x88;
            request[2] = zone;
            request[8] = CalculateGigabyteChecksum(request);
            stream.SetFeature(request);
            Thread.Sleep(65);
            byte[] response = new byte[featureLength];
            stream.GetFeature(response);
            return response;
        }

        void SetZone(byte zone, byte red, byte green, byte blue, byte brightness)
        {
            byte[] request = new byte[featureLength];
            request[1] = 0x08;
            request[2] = zone;
            request[3] = red;
            request[4] = green;
            request[5] = blue;
            request[6] = brightness;
            request[8] = CalculateGigabyteChecksum(request);
            stream.SetFeature(request);
            Thread.Sleep(65);
        }
    }
    catch (Exception exception)
    {
        setReport.AppendLine($"- Setting failed: {Escape(exception.Message)}");
    }

    WritePersistentColorReport(setReport);
}

static void WritePersistentColorReport(StringBuilder setReport)
{
    string root = FindRepositoryRoot();
    string outputDirectory = Path.Combine(root, "research", "runs");
    Directory.CreateDirectory(outputDirectory);
    string outputPath = Path.Combine(outputDirectory, $"keyboard-set-green-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(outputPath, setReport.ToString(), new UTF8Encoding(false));
    Console.WriteLine(setReport);
    Console.WriteLine($"Report written to: {outputPath}");
}

void RunKeyboardMatrixRead()
{
    const int keyboardVendorId = 0x1044;
    const int keyboardProductId = 0x7A41;
    const int featureLength = 9;
    const int inputLength = 65;
    const int matrixLength = 512;
    const byte matrixQueryCommand = 0x8D;
    const string signedDefaultMatrixSha256 = "92431FE3FAE62A5777FC124D73F090F00877BA7DAFA3080F496CB313F72EC78A";

    var matrixReport = new StringBuilder();
    matrixReport.AppendLine("# AORUS keyboard key-matrix read");
    matrixReport.AppendLine();
    matrixReport.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    matrixReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature / 65-byte Input report`");
    matrixReport.AppendLine("- Official Gigabyte device class: `ITE / ZoneRgb / 3a4041`");
    matrixReport.AppendLine("- Known query command: `0x8D`");
    matrixReport.AppendLine("- Expected transfer: eight 65-byte input reports carrying 512 matrix bytes");
    matrixReport.AppendLine("- Matrix, macros, RGB, firmware, BIOS, and EC written: **no**");
    matrixReport.AppendLine("- Serial number recorded: **no**");
    matrixReport.AppendLine();

    HidDevice? device = DeviceList.Local
        .GetHidDevices(keyboardVendorId, keyboardProductId)
        .SingleOrDefault(candidate =>
            GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) &&
            candidate.GetMaxFeatureReportLength() == featureLength &&
            candidate.GetMaxInputReportLength() == inputLength);

    if (device is null)
    {
        matrixReport.AppendLine("- Exact approved matrix interface was not found; no command was sent.");
        WriteKeyboardMatrixReport(matrixReport);
        return;
    }

    try
    {
        using HidStream stream = device.Open();
        stream.ReadTimeout = 2500;

        byte[] request = new byte[featureLength];
        request[1] = matrixQueryCommand;
        request[8] = CalculateGigabyteChecksum(request);
        stream.SetFeature(request);

        byte[] featureHandshake = new byte[featureLength];
        featureHandshake[1] = matrixQueryCommand;
        featureHandshake[4] = 8;
        stream.GetFeature(featureHandshake);

        byte[] matrix = new byte[matrixLength];
        var transferHashes = new List<string>();
        for (int block = 0; block < 8; block++)
        {
            byte[] input = new byte[inputLength];
            int received = stream.Read(input, 0, input.Length);
            if (received != inputLength)
            {
                throw new InvalidOperationException(
                    $"Matrix block {block + 1} returned {received} bytes instead of {inputLength}.");
            }

            input.AsSpan(1, 64).CopyTo(matrix.AsSpan(block * 64, 64));
            transferHashes.Add(Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(input.AsSpan(1, 64))));
        }

        string matrixHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(matrix));
        int activeRecords = Enumerable.Range(0, 128)
            .Count(index => matrix.AsSpan(index * 4, 4).IndexOfAnyExcept((byte)0) >= 0);
        int distinctRecords = Enumerable.Range(0, 128)
            .Select(index => Convert.ToHexString(matrix.AsSpan(index * 4, 4)))
            .Distinct(StringComparer.Ordinal)
            .Count();
        bool matchesDefault = matrixHash.Equals(
            signedDefaultMatrixSha256,
            StringComparison.OrdinalIgnoreCase);

        matrixReport.AppendLine("## Transfer result");
        matrixReport.AppendLine();
        matrixReport.AppendLine("- Blocks received: `8 / 8`");
        matrixReport.AppendLine($"- Matrix bytes received: `{matrix.Length}`");
        matrixReport.AppendLine($"- Matrix SHA-256: `{matrixHash}`");
        matrixReport.AppendLine($"- Signed-module default SHA-256: `{signedDefaultMatrixSha256}`");
        matrixReport.AppendLine($"- Exact default-matrix match: **{(matchesDefault ? "yes" : "no")}**");
        matrixReport.AppendLine($"- Non-empty four-byte records: `{activeRecords} / 128`");
        matrixReport.AppendLine($"- Distinct four-byte records including zero: `{distinctRecords}`");
        matrixReport.AppendLine();
        matrixReport.AppendLine("### Per-block payload hashes");
        matrixReport.AppendLine();
        for (int block = 0; block < transferHashes.Count; block++)
        {
            matrixReport.AppendLine($"- Block {block + 1}: `{transferHashes[block]}`");
        }

        matrixReport.AppendLine();
        matrixReport.AppendLine("## Raw 512-byte matrix");
        matrixReport.AppendLine();
        matrixReport.AppendLine("```text");
        for (int offset = 0; offset < matrix.Length; offset += 16)
        {
            matrixReport.AppendLine($"{offset:X3}: {Convert.ToHexString(matrix.AsSpan(offset, 16))}");
        }
        matrixReport.AppendLine("```");
        matrixReport.AppendLine();
        matrixReport.AppendLine("## Interpretation boundary");
        matrixReport.AppendLine();
        matrixReport.AppendLine("- The controller stores 128 four-byte slots; the signed software maps these slots to the model-specific keyboard layout.");
        matrixReport.AppendLine("- A default-matrix match proves factory assignments are present, not that every shared macro feature is enabled in the UI.");
        matrixReport.AppendLine("- Macro records were not requested by this diagnostic.");
    }
    catch (Exception exception)
    {
        matrixReport.AppendLine("## Read failure");
        matrixReport.AppendLine();
        matrixReport.AppendLine($"- {Escape(exception.Message)}");
        Environment.ExitCode = 5;
    }

    WriteKeyboardMatrixReport(matrixReport);
}

static void WriteKeyboardMatrixReport(StringBuilder matrixReport)
{
    string root = FindRepositoryRoot();
    string outputDirectory = Path.Combine(root, "research", "runs");
    Directory.CreateDirectory(outputDirectory);
    string outputPath = Path.Combine(
        outputDirectory,
        $"keyboard-matrix-read-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(outputPath, matrixReport.ToString(), new UTF8Encoding(false));
    Console.WriteLine(matrixReport);
    Console.WriteLine($"Report written to: {outputPath}");
}

// Read-only probe of the second lighting path found in Gigabyte's signed module:
// the 960-byte PictureMatrix addressed by official getter 0x92. Only the getter is
// implemented here; setter 0x12 is deliberately absent. Transfer shape matches the
// already verified 0x8D key-matrix read: one feature handshake, then eight
// 65-byte input reports carrying 512 bytes.
void RunKeyboardPictureMatrixProbe()
{
    const int keyboardVendorId = 0x1044;
    const int keyboardProductId = 0x7A41;
    const int featureLength = 9;
    const int inputLength = 65;
    const int blockCount = 8;
    const int payloadLength = blockCount * 64;
    const byte pictureMatrixQueryCommand = 0x92;

    int requestedSlot = Math.Clamp(ReadPositiveIntArgument("--slot", 1) - 1, 0, 4);

    var report = new StringBuilder();
    report.AppendLine("# AORUS keyboard picture-matrix probe");
    report.AppendLine();
    report.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    report.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature / 65-byte Input report`");
    report.AppendLine("- Official Gigabyte device class: `ITE / ZoneRgb / 3a4041`");
    report.AppendLine($"- Official query command: `0x{pictureMatrixQueryCommand:X2}` (`LoadPictureMatrixValue`)");
    report.AppendLine($"- Requested custom slot: `{requestedSlot}` (effect enum `{51 + requestedSlot}`, Custom {requestedSlot + 1})");
    report.AppendLine("- Setter `0x12` implemented: **no**");
    report.AppendLine("- Picture matrix, key matrix, macros, RGB zones, firmware, BIOS, and EC written: **no**");
    report.AppendLine("- Report ID `0x5A` (ITE flash channel) touched: **no**");
    report.AppendLine("- Serial number recorded: **no**");
    report.AppendLine();

    HidDevice? device = DeviceList.Local
        .GetHidDevices(keyboardVendorId, keyboardProductId)
        .SingleOrDefault(candidate =>
            GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) &&
            candidate.GetMaxFeatureReportLength() == featureLength &&
            candidate.GetMaxInputReportLength() == inputLength);

    if (device is null)
    {
        report.AppendLine("- Exact approved interface was not found; no command was sent.");
        WriteReport();
        Environment.ExitCode = 4;
        return;
    }

    try
    {
        using HidStream stream = device.Open();
        stream.ReadTimeout = 2500;

        byte[] request = new byte[featureLength];
        request[1] = pictureMatrixQueryCommand;
        request[2] = 0;
        request[3] = (byte)requestedSlot;
        request[8] = CalculateGigabyteChecksum(request);
        stream.SetFeature(request);

        // Gigabyte pre-fills this buffer before GET_REPORT, which makes its response
        // indistinguishable from the request. A zeroed buffer keeps the answer purely
        // device-sourced, as the existing 0x88 query helper already does.
        byte[] handshake = new byte[featureLength];
        stream.GetFeature(handshake);

        report.AppendLine("## Handshake");
        report.AppendLine();
        report.AppendLine($"- Request: `{Convert.ToHexString(request)}`");
        report.AppendLine($"- Feature response (read into a zeroed buffer): `{Convert.ToHexString(handshake)}`");
        report.AppendLine();

        byte[] payload = new byte[payloadLength];
        int blocksReceived = 0;
        string? transferError = null;
        var blockHashes = new List<string>();

        for (int block = 0; block < blockCount; block++)
        {
            byte[] input = new byte[inputLength];
            int received;
            try
            {
                received = stream.Read(input, 0, input.Length);
            }
            catch (TimeoutException)
            {
                transferError = $"Block {block + 1} timed out after 2500 ms.";
                break;
            }

            if (received != inputLength)
            {
                transferError = $"Block {block + 1} returned {received} bytes instead of {inputLength}.";
                break;
            }

            input.AsSpan(1, 64).CopyTo(payload.AsSpan(block * 64, 64));
            blockHashes.Add(Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(input.AsSpan(1, 64))));
            blocksReceived++;
            Thread.Sleep(25);
        }

        report.AppendLine("## Transfer result");
        report.AppendLine();
        report.AppendLine($"- Blocks received: `{blocksReceived} / {blockCount}`");
        if (transferError is not null)
        {
            report.AppendLine($"- Transfer stopped: {Escape(transferError)}");
        }

        if (blocksReceived > 0)
        {
            int validBytes = blocksReceived * 64;
            Span<byte> valid = payload.AsSpan(0, validBytes);
            int nonZero = validBytes - valid.Count((byte)0);
            int distinct = valid.ToArray().Distinct().Count();

            report.AppendLine($"- Payload bytes received: `{validBytes}`");
            report.AppendLine($"- Payload SHA-256: `{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(valid))}`");
            report.AppendLine($"- Non-zero bytes: `{nonZero} / {validBytes}`");
            report.AppendLine($"- Distinct byte values: `{distinct}`");
            report.AppendLine();
            report.AppendLine("### Per-block payload hashes");
            report.AppendLine();
            for (int block = 0; block < blockHashes.Count; block++)
            {
                report.AppendLine($"- Block {block + 1}: `{blockHashes[block]}`");
            }

            report.AppendLine();
            report.AppendLine($"## Raw {validBytes}-byte payload");
            report.AppendLine();
            report.AppendLine("```text");
            for (int offset = 0; offset < validBytes; offset += 16)
            {
                report.AppendLine($"{offset:X3}: {Convert.ToHexString(valid.Slice(offset, 16))}");
            }

            report.AppendLine("```");
        }

        report.AppendLine();
        report.AppendLine("## Interpretation boundary");
        report.AppendLine();
        report.AppendLine("- A timeout on block 1 means firmware 19.0.4 does not answer `0x92` on this device; the picture-matrix path would then be closed for `7A41`.");
        report.AppendLine("- An all-zero payload proves the command is answered but the slot is empty; it does not prove the slot is writable.");
        report.AppendLine("- Structured non-zero data would indicate a usable second lighting layer and justify a separate guarded write design.");
        report.AppendLine("- Gigabyte's signed module uses 512 of the declared 960 `PictureMatrix` bytes; only those 512 are requested here.");
    }
    catch (Exception exception)
    {
        report.AppendLine("## Probe failure");
        report.AppendLine();
        report.AppendLine($"- {Escape(exception.Message)}");
        Environment.ExitCode = 5;
    }

    WriteReport();

    void WriteReport()
    {
        string root = FindRepositoryRoot();
        string outputDirectory = Path.Combine(root, "research", "runs");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(
            outputDirectory,
            $"keyboard-picture-matrix-probe-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(outputPath, report.ToString(), new UTF8Encoding(false));
        Console.WriteLine(report);
        Console.WriteLine($"Report written to: {outputPath}");
    }
}

// Every zone-brightness test so far ran at whatever hardware step happened to be
// active, and the interaction between the two was never examined. If the visible
// result is a combination of the Fn+Space step and the zone brightness byte, the zone
// byte behaves differently depending on the step, which would explain the owner's
// impression that setting brightness sometimes works and sometimes does nothing.
//
// This builds the full matrix. The hardware step is no longer guessed: it is read
// live from MI_02/COL_04, so each row is labelled with a measured value.
void RunBrightnessInteractionTest()
{
    const int keyboardVendorId = 0x1044;
    const int keyboardProductId = 0x7A41;
    const int featureLength = 9;
    const int keyboardUsagePage = 0x0007;
    const string stepCollection = "MI_02 / COL_04";
    const int stepReportLength = 4;

    // Aligned with the measured hardware steps rather than the older sweep list. The
    // first run used 0, 25, 50, which carried 25 over from the pre-discovery sweep and
    // omitted 32 entirely, so the exact step values were never paired against a
    // matching hardware step.
    byte[] zoneValues = ReadByteListArgument("--zone-values", [0, 24, 32, 50]);
    byte[] expectedSteps = [0, 24, 32, 50];

    Console.OutputEncoding = Encoding.UTF8;
    Console.WriteLine("AORUS 5 SE - Zusammenspiel von Hardware-Stufe und Zonen-Helligkeitsbyte");
    Console.WriteLine();
    Console.WriteLine("Fuer jede der vier Fn+Space-Stufen wird das Zonen-Helligkeitsbyte");
    Console.WriteLine($"durchgeschaltet: {string.Join(", ", zoneValues)}. Die Farbe bleibt weiss.");
    Console.WriteLine("Die aktive Hardware-Stufe wird live mitgelesen, nicht geraten.");
    Console.WriteLine();

    var report = new StringBuilder();
    report.AppendLine("# AORUS brightness interaction matrix");
    report.AppendLine();
    report.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    report.AppendLine($"- Exact target device: `VID {keyboardVendorId:X4} / PID {keyboardProductId:X4}`");
    report.AppendLine("- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88`, plus read-only input listening");
    report.AppendLine($"- Hardware step read live from `{stepCollection}`, report ID `0x04`, byte 2");
    report.AppendLine("- Global effect command, picture matrix, WMI, and EC: **not used**");
    report.AppendLine($"- Privacy gate: collections declaring keyboard usage page `0x{keyboardUsagePage:X4}` are never opened");
    report.AppendLine($"- Zone brightness values per step: `{string.Join("`, `", zoneValues)}`");
    report.AppendLine("- Purpose: determine whether the zone brightness byte behaves differently depending on the active hardware step");
    report.AppendLine();

    HidDevice? rgbDevice = DeviceList.Local
        .GetHidDevices(keyboardVendorId, keyboardProductId)
        .SingleOrDefault(candidate =>
            GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) &&
            candidate.GetMaxFeatureReportLength() == featureLength);

    HidDevice? stepDevice = null;
    foreach (HidDevice candidate in DeviceList.Local.GetHidDevices(keyboardVendorId, keyboardProductId))
    {
        if (!GetInterfaceLabel(candidate.DevicePath).Equals(stepCollection, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        try
        {
            ReportDescriptor descriptor = candidate.GetReportDescriptor();
            bool declaresKeyboard = descriptor.DeviceItems
                .SelectMany(item => item.Reports)
                .SelectMany(deviceReport => deviceReport.DataItems)
                .SelectMany(dataItem => dataItem.Usages.GetAllValues())
                .Any(usage => (usage >> 16) == keyboardUsagePage)
                || candidate.DevicePath.EndsWith("\\kbd", StringComparison.OrdinalIgnoreCase);
            if (!declaresKeyboard && candidate.GetMaxInputReportLength() == stepReportLength)
            {
                stepDevice = candidate;
            }
        }
        catch (Exception)
        {
        }

        break;
    }

    if (rgbDevice is null)
    {
        Console.Error.WriteLine("Die exakt zugelassene RGB-Schnittstelle wurde nicht gefunden.");
        report.AppendLine("- Exact approved RGB feature collection was not found; nothing was sent.");
        WriteReport();
        Environment.ExitCode = 4;
        return;
    }

    int observedStep = -1;
    var originalZones = new Dictionary<byte, byte[]>();
    var rows = new List<(string Step, byte ZoneValue, byte Stored, string Observation)>();

    using var stepCancellation = new CancellationTokenSource();
    Thread? stepListener = null;
    HidStream? stepStream = null;

    if (stepDevice is not null)
    {
        try
        {
            stepStream = stepDevice.Open();
            stepStream.ReadTimeout = 250;
            HidStream capturedStream = stepStream;
            stepListener = new Thread(() =>
            {
                byte[] buffer = new byte[stepReportLength];
                while (!stepCancellation.IsCancellationRequested)
                {
                    try
                    {
                        int received = capturedStream.Read(buffer, 0, buffer.Length);
                        if (received > 2)
                        {
                            Volatile.Write(ref observedStep, buffer[2]);
                        }
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            })
            {
                IsBackground = true,
                Name = "aorus-step-listener"
            };
            stepListener.Start();
            report.AppendLine($"- `{stepCollection}` opened for live step reading: **yes**");
        }
        catch (Exception exception)
        {
            report.AppendLine($"- `{stepCollection}` could not be opened ({Escape(exception.Message)}); steps fall back to the owner's own statement.");
        }
    }
    else
    {
        report.AppendLine($"- `{stepCollection}` was not found or is not approved; steps fall back to the owner's own statement.");
    }

    try
    {
        using HidStream rgbStream = rgbDevice.Open();

        report.AppendLine();
        report.AppendLine("## Captured original zone state");
        report.AppendLine();
        for (byte zone = 1; zone <= 3; zone++)
        {
            byte[] state = QueryZone(rgbStream, zone);
            originalZones.Add(zone, state);
            report.AppendLine($"- Zone {zone}: `#{state[3]:X2}{state[4]:X2}{state[5]:X2}`, brightness `{state[6]}`");
        }

        for (int stepIndex = 0; stepIndex < expectedSteps.Length; stepIndex++)
        {
            byte wanted = expectedSteps[stepIndex];
            string wantedName = wanted switch
            {
                0 => "aus",
                24 => "niedrig",
                32 => "mittel",
                _ => "hell"
            };

            Console.WriteLine();
            Console.WriteLine($"--- Hardware-Stufe {stepIndex + 1} von {expectedSteps.Length}: {wantedName} (erwartet {wanted}) ---");
            Console.WriteLine($"  Schalte mit Fn+Space auf '{wantedName}' und druecke dann Enter.");
            Console.Write("  Enter: ");
            Console.ReadLine();

            int measured = Volatile.Read(ref observedStep);
            string stepLabel = measured >= 0
                ? $"{measured} (gemessen)"
                : $"{wanted} (angenommen, nicht gemessen)";
            Console.WriteLine($"  Aktive Stufe: {stepLabel}");

            foreach (byte zoneValue in zoneValues)
            {
                for (byte zone = 1; zone <= 3; zone++)
                {
                    WriteZone(rgbStream, zone, 255, 255, 255, zoneValue);
                    Thread.Sleep(65);
                }

                byte[] readback = QueryZone(rgbStream, zone: 1);
                Console.WriteLine($"  Zonen-Byte {zoneValue} gesetzt, gespeichert {readback[6]}.");
                Console.Write("    Beobachtung: ");
                string observation = Console.ReadLine()?.Trim() ?? string.Empty;
                rows.Add((stepLabel, zoneValue, readback[6],
                    observation.Length == 0 ? "(keine Beschreibung)" : observation));

                if (observation.Equals("/stop", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Test wird beendet.");
                    return;
                }
            }
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Testfehler: {exception.Message}");
        report.AppendLine();
        report.AppendLine($"- Test error: {Escape(exception.Message)}");
        Environment.ExitCode = 5;
    }
    finally
    {
        stepCancellation.Cancel();
        stepListener?.Join(1500);
        stepStream?.Dispose();

        report.AppendLine();
        report.AppendLine("## Matrix");
        report.AppendLine();
        if (rows.Count == 0)
        {
            report.AppendLine("- No row was recorded.");
        }
        else
        {
            report.AppendLine("| Hardware step | Zone byte | Stored | Owner observation |");
            report.AppendLine("|---|---|---|---|");
            foreach ((string step, byte zoneValue, byte stored, string observation) in rows)
            {
                report.AppendLine($"| `{step}` | `{zoneValue}` | `{stored}` | {Escape(observation)} |");
            }
        }

        try
        {
            using HidStream restoreStream = rgbDevice.Open();
            report.AppendLine();
            report.AppendLine("## Restoration");
            report.AppendLine();
            foreach ((byte zone, byte[] original) in originalZones.OrderBy(item => item.Key))
            {
                WriteZone(restoreStream, zone, original[3], original[4], original[5], original[6]);
                Thread.Sleep(65);
                byte[] restored = QueryZone(restoreStream, zone);
                bool exact = restored[3] == original[3] && restored[4] == original[4] &&
                             restored[5] == original[5] && restored[6] == original[6];
                report.AppendLine(
                    $"- Zone {zone}: `#{restored[3]:X2}{restored[4]:X2}{restored[5]:X2}`, brightness `{restored[6]}`, exact match: **{(exact ? "yes" : "no")}**");
                if (!exact)
                {
                    Environment.ExitCode = 6;
                }
            }

            Console.WriteLine("Die vorherigen drei RGB-Zonen wurden wiederhergestellt.");
        }
        catch (Exception exception)
        {
            report.AppendLine($"- RESTORE ERROR: {Escape(exception.Message)}");
            Console.Error.WriteLine($"Wiederherstellungsfehler: {exception.Message}");
            Environment.ExitCode = 7;
        }

        WriteReport();
    }

    static void WriteZone(HidStream stream, byte zone, byte red, byte green, byte blue, byte brightness)
    {
        byte[] request = new byte[featureLength];
        request[1] = 0x08;
        request[2] = zone;
        request[3] = red;
        request[4] = green;
        request[5] = blue;
        request[6] = brightness;
        request[8] = CalculateGigabyteChecksum(request);
        stream.SetFeature(request);
    }

    static byte[] QueryZone(HidStream stream, byte zone)
    {
        byte[] query = new byte[featureLength];
        query[1] = 0x88;
        query[2] = zone;
        query[8] = CalculateGigabyteChecksum(query);
        stream.SetFeature(query);
        Thread.Sleep(10);
        byte[] response = new byte[featureLength];
        stream.GetFeature(response);
        return response;
    }

    void WriteReport()
    {
        string root = FindRepositoryRoot();
        string outputDirectory = Path.Combine(root, "research", "runs");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(
            outputDirectory,
            $"keyboard-brightness-interaction-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(outputPath, report.ToString(), new UTF8Encoding(false));
        Console.WriteLine();
        Console.WriteLine($"Report written to: {outputPath}");
    }
}

Dictionary<int, (string Name, TimeSpan Cpu)>? _previousCpuSnapshot = null;
DateTimeOffset? _previousCpuSampledAt = null;

// Correlates system power draw with what is actually consuming it. Deliberately does
// not call nvidia-smi: a single call wakes the discrete GPU and costs roughly 22 W on
// this laptop, so the tool would create the consumption it claims to report. Every
// source used here is a passive WMI performance counter.
void RunPowerDrawMonitor()
{
    int durationSeconds = Math.Clamp(ReadPositiveIntArgument("--seconds", 120), 15, 1800);
    int intervalMilliseconds = Math.Clamp(ReadPositiveIntArgument("--interval-ms", 3000), 1000, 30000);
    const string integratedAdapterLuid = "0x0001149C";

    Console.OutputEncoding = Encoding.UTF8;
    Console.WriteLine("AORUS 5 SE - Verbrauchsmonitor");
    Console.WriteLine();
    Console.WriteLine($"Laufzeit {durationSeconds} s, Abstand {intervalMilliseconds} ms.");
    Console.WriteLine("Die Entladerate ist nur im AKKUBETRIEB verfuegbar; am Netz meldet Windows 0.");
    Console.WriteLine("Beenden mit Strg+C.");
    Console.WriteLine();

    var report = new StringBuilder();
    report.AppendLine("# AORUS system power draw correlation");
    report.AppendLine();
    report.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    report.AppendLine("- Mode: read-only. Passive WMI performance counters only");
    report.AppendLine("- `nvidia-smi` invoked: **no**, because a single call wakes the discrete GPU and costs about 22 W on this laptop");
    report.AppendLine($"- Duration: `{durationSeconds}` s, interval `{intervalMilliseconds}` ms");
    report.AppendLine($"- Adapter treated as the integrated GPU: LUID `{integratedAdapterLuid}`");
    report.AppendLine("- Discharge rate is the **total** system draw in milliwatts, not the draw of any single component");
    report.AppendLine($"- CPU percentages are normalised across `{Environment.ProcessorCount}` logical processors, so the total ranges from 0 to 100");
    report.AppendLine("- **The monitor influences its own measurement.** Each sample enumerates every process through WMI, which shows up as `WmiPrvSE` load. Interactive sessions and the tool itself are part of the reported draw, so compare samples within one run rather than against an untouched idle machine.");
    report.AppendLine();

    var samples = new List<PowerSample>();
    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    Console.CancelKeyPress += cancelHandler;

    try
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < durationSeconds && !cancellation.IsCancellationRequested)
        {
            PowerSample sample = CapturePowerSample(integratedAdapterLuid);
            samples.Add(sample);
            Console.WriteLine(
                $"  {sample.At:HH:mm:ss}  {sample.DischargeMilliwatts / 1000.0,5:F1} W  " +
                $"CPU {sample.CpuPercent,5:F1} %  " +
                $"iGPU {sample.IntegratedGpuPercent,5:F1} %  " +
                $"dGPU {sample.DiscreteGpuPercent,5:F1} %  " +
                $"top: {sample.TopProcesses}");
            if (clock.Elapsed.TotalSeconds < durationSeconds && !cancellation.IsCancellationRequested)
            {
                Thread.Sleep(intervalMilliseconds);
            }
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Monitorfehler: {exception.Message}");
        report.AppendLine($"- Monitor error: {Escape(exception.Message)}");
        Environment.ExitCode = 5;
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
    }

    report.AppendLine("## Samples");
    report.AppendLine();
    if (samples.Count == 0)
    {
        report.AppendLine("- No sample was taken.");
    }
    else
    {
        report.AppendLine("| Time | Draw | CPU | iGPU | dGPU | Top processes by CPU |");
        report.AppendLine("|---|---|---|---|---|---|");
        foreach (PowerSample sample in samples)
        {
            report.AppendLine(
                $"| `{sample.At:HH:mm:ss}` | `{sample.DischargeMilliwatts / 1000.0:F1}` W | " +
                $"`{sample.CpuPercent:F1}` % | `{sample.IntegratedGpuPercent:F1}` % | " +
                $"`{sample.DiscreteGpuPercent:F1}` % | {Escape(sample.TopProcesses)} |");
        }

        PowerSample[] onBattery = samples.Where(sample => sample.DischargeMilliwatts > 0).ToArray();
        report.AppendLine();
        report.AppendLine("## Summary");
        report.AppendLine();
        report.AppendLine($"- Samples: `{samples.Count}`, of which `{onBattery.Length}` carried a usable discharge rate");

        if (onBattery.Length == 0)
        {
            report.AppendLine("- Every sample reported `0` mW, so the machine was on AC for the whole run. Repeat on battery to obtain power figures; the CPU and GPU columns remain valid.");
        }
        else
        {
            double minimum = onBattery.Min(sample => sample.DischargeMilliwatts) / 1000.0;
            double maximum = onBattery.Max(sample => sample.DischargeMilliwatts) / 1000.0;
            double average = onBattery.Average(sample => sample.DischargeMilliwatts) / 1000.0;
            report.AppendLine($"- Total draw: minimum `{minimum:F1}` W, average `{average:F1}` W, maximum `{maximum:F1}` W");
            report.AppendLine($"- Spread between quietest and busiest sample: `{maximum - minimum:F1}` W");

            PowerSample peak = onBattery.OrderByDescending(sample => sample.DischargeMilliwatts).First();
            report.AppendLine(
                $"- Busiest sample `{peak.At:HH:mm:ss}` at `{peak.DischargeMilliwatts / 1000.0:F1}` W with " +
                $"CPU `{peak.CpuPercent:F1}` %, iGPU `{peak.IntegratedGpuPercent:F1}` %, dGPU `{peak.DiscreteGpuPercent:F1}` %: {Escape(peak.TopProcesses)}");

            bool anyDiscrete = samples.Any(sample => sample.DiscreteGpuPercent > 0.1);
            report.AppendLine(anyDiscrete
                ? "- The discrete GPU showed activity in at least one sample, so it was awake during the run."
                : "- **The discrete GPU showed no activity in any sample.** The observed spread therefore comes from CPU and application load, not from the RTX.");
        }

        report.AppendLine();
        report.AppendLine("## Interpretation boundary");
        report.AppendLine();
        report.AppendLine("- The discharge rate covers the whole machine: panel, CPU, RAM, storage, radios and every running application.");
        report.AppendLine("- A GPU engine percentage is a utilisation figure, not a power figure. Zero utilisation does not prove the adapter is powered down, only that nothing is rendering on it.");
        report.AppendLine("- Per-process CPU values are not normalised; a single process can exceed 100 when it spans several cores.");
        report.AppendLine($"- The integrated adapter is identified by LUID `{integratedAdapterLuid}`, inferred from the desktop compositor running on the internal panel.");
    }

    WriteReport();

    void WriteReport()
    {
        string root = FindRepositoryRoot();
        string outputDirectory = Path.Combine(root, "research", "runs");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(
            outputDirectory,
            $"power-draw-monitor-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(outputPath, report.ToString(), new UTF8Encoding(false));
        Console.WriteLine();
        Console.WriteLine($"Report written to: {outputPath}");
    }
}

PowerSample CapturePowerSample(string integratedAdapterLuid)
{
    uint discharge = 0;
    string? batteryError = null;
    try
    {
        using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT DischargeRate FROM BatteryStatus");
        foreach (ManagementBaseObject item in searcher.Get())
        {
            using (item)
            {
                discharge = Convert.ToUInt32(item["DischargeRate"] ?? 0u, CultureInfo.InvariantCulture);
                break;
            }
        }
    }
    catch (Exception exception)
    {
        batteryError = exception.Message;
    }

    // GPU utilisation comes from PDH, not from the WMI performance classes. On battery
    // Win32_PerfFormattedData_* throws ("call cancelled"), and an earlier version of
    // this monitor swallowed that and reported zeros, which looked like an idle system
    // instead of a failed measurement. PDH keeps working in that state.
    double integrated = 0;
    double discrete = 0;
    string? gpuError = null;
    try
    {
        var category = new System.Diagnostics.PerformanceCounterCategory("GPU Engine");
        foreach (string instance in category.GetInstanceNames())
        {
            using var counter = new System.Diagnostics.PerformanceCounter(
                "GPU Engine",
                "Utilization Percentage",
                instance,
                readOnly: true);
            double value = counter.NextValue();
            if (value <= 0)
            {
                continue;
            }

            if (instance.Contains(integratedAdapterLuid, StringComparison.OrdinalIgnoreCase))
            {
                integrated += value;
            }
            else
            {
                discrete += value;
            }
        }
    }
    catch (Exception exception)
    {
        gpuError = exception.Message;
    }

    // Per-process CPU from TotalProcessorTime deltas. This needs no WMI and no
    // performance counters, so it survives the state in which the WMI performance
    // provider refuses to answer, and it does not add WmiPrvSE load of its own.
    var current = new Dictionary<int, (string Name, TimeSpan Cpu)>();
    foreach (System.Diagnostics.Process process in System.Diagnostics.Process.GetProcesses())
    {
        using (process)
        {
            try
            {
                current[process.Id] = (process.ProcessName, process.TotalProcessorTime);
            }
            catch (Exception)
            {
                // Protected processes deny access to their CPU time; skip them.
            }
        }
    }

    DateTimeOffset now = DateTimeOffset.Now;
    var processes = new List<(string Name, double Cpu)>();
    double cpuTotal = 0;
    if (_previousCpuSnapshot is not null && _previousCpuSampledAt is not null)
    {
        double elapsedSeconds = (now - _previousCpuSampledAt.Value).TotalSeconds;
        if (elapsedSeconds > 0.05)
        {
            double logical = Math.Max(1, Environment.ProcessorCount);
            foreach ((int id, (string name, TimeSpan cpu)) in current)
            {
                if (!_previousCpuSnapshot.TryGetValue(id, out (string Name, TimeSpan Cpu) before))
                {
                    continue;
                }

                double percent = (cpu - before.Cpu).TotalSeconds / elapsedSeconds * 100.0;
                if (percent > 0.5)
                {
                    processes.Add((name, percent));
                }

                cpuTotal += percent;
            }

            cpuTotal /= logical;
        }
    }

    _previousCpuSnapshot = current;
    _previousCpuSampledAt = now;

    string top = processes.Count == 0
        ? "-"
        : string.Join(", ", processes
            .OrderByDescending(entry => entry.Cpu)
            .Take(3)
            .Select(entry => $"{entry.Name} {entry.Cpu:F0} %"));

    string? error = batteryError is null && gpuError is null
        ? null
        : string.Join("; ", new[] { batteryError, gpuError }.Where(message => message is not null));

    return new PowerSample(now, discharge, cpuTotal, integrated, discrete, top, error);
}

internal readonly record struct PowerSample(
    DateTimeOffset At,
    uint DischargeMilliwatts,
    double CpuPercent,
    double IntegratedGpuPercent,
    double DiscreteGpuPercent,
    string TopProcesses,
    string? Error);
