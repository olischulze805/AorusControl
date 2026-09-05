using System.Globalization;
using System.Management;
using System.Security.Principal;
using System.Text;
using AorusControl.Core.Device;
using AorusControl.Core.Models;
using AorusControl.Core.Services;
using HidSharp;
using HidSharp.Reports;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("AORUS Control Diagnostics");
    Console.WriteLine("Read-only: --read-telemetry, --monitor, --query-keyboard-rgb, --inspect-thermal-power");
    Console.WriteLine("Power monitoring: --monitor-power-draw --seconds 30 --interval-ms 3000");
    Console.WriteLine("Hardware write tests require their own explicit flags and confirmations. See README.md.");
    Console.WriteLine("--help performs no device queries or writes.");
    return;
}

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
bool probeFanCurveFloor = args.Any(argument =>
    argument.Equals("--probe-fan-curve-floor", StringComparison.OrdinalIgnoreCase));
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

if (probeFanCurveFloor)
{
    RunFanCurveFloorProbe();
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
    RunKeyboardEffectBatch();
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
var acpiDevices = Query2(
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
var mofResources = Query2(
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
var thermalZones = Query2(
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
    var item = Query2(@"root\cimv2", query).FirstOrDefault();
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

    var batteries = Query2(
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

void RunFanCurveFloorProbe()
{
    bool confirmed = args.Any(argument =>
        argument.Equals("--confirm-fan-curve-write", StringComparison.OrdinalIgnoreCase));
    var test = new StringBuilder();
    test.AppendLine("# AORUS fan-curve floor probe");
    test.AppendLine();
    test.AppendLine($"- Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    test.AppendLine("- Question: does the EC store curve values below the verified floor of raw 57, or clamp them?");
    test.AppendLine("- Method: write one candidate into points 0 and 1, read all 15 points back, restore.");
    test.AppendLine("- The fan mode is never switched to Dynamic, so the probe curve never regulates the fans.");
    test.AppendLine($"- Explicit curve-write confirmation present: {(confirmed ? "yes" : "no")}");
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
    bool written = false;
    try
    {
        original = controller.ReadAsync().GetAwaiter().GetResult();
        if (original.FixedStatusRaw != 0 || original.StepStatusRaw != 0 ||
            original.AutoStatusRaw != 0 || original.NvidiaThermalTargetRaw != 0)
        {
            throw new AorusFanControlException("Der Test startet nur aus dem verifizierten Normalzustand.");
        }

        test.AppendLine("## Original curve");
        test.AppendLine();
        test.AppendLine("- " + string.Join(", ", original.Curve.Select(point => $"({point.Temperature},{point.Value})")));
        test.AppendLine();
        test.AppendLine("## Candidates");
        test.AppendLine();
        test.AppendLine("| Written | Read back point 0 | Read back point 1 | Other 13 points unchanged |");
        test.AppendLine("|---|---|---|---|");

        // Descending, so the first refusal answers the question and everything below it is
        // recorded anyway - a clamp at one value does not prove the same clamp lower down.
        using var setterProbe = new CurvePointWriter();
        foreach (byte candidate in new byte[] { 50, 40, 30, 20, 10, 0 })
        {
            written = true;
            setterProbe.Write(0, original.Curve[0].Temperature, candidate);
            setterProbe.Write(1, original.Curve[1].Temperature, candidate);
            FanControlState after = controller.ReadAsync().GetAwaiter().GetResult();
            bool restUnchanged = after.Curve.Skip(2).SequenceEqual(original.Curve.Skip(2));
            test.AppendLine($"| {candidate} | {after.Curve[0].Value} | {after.Curve[1].Value} | {(restUnchanged ? "yes" : "**no**")} |");
        }
    }
    catch (Exception exception)
    {
        test.AppendLine();
        test.AppendLine($"- Probe failed: {Escape(exception.Message)}");
        Environment.ExitCode = 5;
    }
    finally
    {
        if (written && original is not null)
        {
            test.AppendLine();
            test.AppendLine("## Restore");
            test.AppendLine();
            try
            {
                FanProfileChangeResult restored = controller.SetCurveAsync(original.Curve).GetAwaiter().GetResult();
                bool exact = restored.VerifiedState.Curve.SequenceEqual(original.Curve);
                test.AppendLine($"- All 15 original points restored and verified: {(exact ? "yes" : "**no**")}");
                test.AppendLine("- " + string.Join(", ", restored.VerifiedState.Curve.Select(point => $"({point.Temperature},{point.Value})")));
                if (!exact) Environment.ExitCode = 6;
            }
            catch (Exception restoreError)
            {
                test.AppendLine($"- Restore failed: {Escape(restoreError.Message)}");
                test.AppendLine("- Use tools/Start-FanNormalRestore.cmd and re-check the curve.");
                Environment.ExitCode = 7;
            }
        }
    }

    WriteCurveTestReport(test);
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
        List<Dictionary<string, object?>> rows = Query2(scope, query);
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

void RunPowerDrawMonitor()
{
    AorusControl.Diagnostics.Features.PowerMonitoring.PowerMonitorCommand.Run(
        Math.Clamp(ReadPositiveIntArgument("--seconds", 120), 15, 1800),
        Math.Clamp(ReadPositiveIntArgument("--interval-ms", 3000), 1000, 30000),
        FindRepositoryRoot());
}

// Recovered from the preserved 2026-09-03 15:44 diagnostics assembly.
#nullable disable warnings
#pragma warning disable CS8321 // Preserved recovered helpers, pending extraction into feature modules.
		void AppendFirstCimv(string query, string[] properties)
		{
			Dictionary<string, object> item = Query2("root\\cimv2", query).FirstOrDefault();
			if (item == null)
			{
				report.AppendLine("- Query returned no instance");
			}
			else
			{
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
		}


		static string Escape(string value)
		{
			return value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
		}


		static string FindRepositoryRoot()
		{
			string[] array2 = new string[2]
			{
				AppContext.BaseDirectory,
				Environment.CurrentDirectory
			};
			for (int i = 0; i < array2.Length; i++)
			{
				for (DirectoryInfo directory = new DirectoryInfo(array2[i]); directory != null; directory = directory.Parent)
				{
					if (File.Exists(Path.Combine(directory.FullName, "AorusControl.slnx")))
					{
						return directory.FullName;
					}
				}
			}
			return Environment.CurrentDirectory;
		}


		static string FormatWmiDate(string value)
		{
			try
			{
				return ManagementDateTimeConverter.ToDateTime(value).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
			}
			catch
			{
				return value;
			}
		}


		static string GetFirstValue(string scopePath, string query, string property)
		{
			Dictionary<string, object> item = Query2(scopePath, query).FirstOrDefault();
			if (item != null)
			{
				return GetText(item, property);
			}
			return string.Empty;
		}


		static string GetInterfaceLabel(string devicePath)
		{
			Match match = Regex.Match(devicePath, "&mi_(?<interface>[0-9a-f]{2})(?:&col(?<collection>[0-9a-f]{2}))?", RegexOptions.IgnoreCase);
			if (!match.Success)
			{
				return "HID collection";
			}
			string label = "MI_" + match.Groups["interface"].Value.ToUpperInvariant();
			if (match.Groups["collection"].Success)
			{
				label = label + " / COL_" + match.Groups["collection"].Value.ToUpperInvariant();
			}
			return label;
		}


		static string GetText(IReadOnlyDictionary<string, object?> item, string property)
		{
			if (item.TryGetValue(property, out object value))
			{
				return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
			}
			return string.Empty;
		}


		static (byte r, byte g, byte b) HostHueToRgb(double hue)
		{
			double num2 = hue * 6.0;
			int index = (int)Math.Floor(num2) % 6;
			byte rising = (byte)Math.Round((num2 - Math.Floor(num2)) * 255.0);
			byte falling = (byte)(255 - rising);
			return index switch
			{
				0 => (r: byte.MaxValue, g: rising, b: 0), 
				1 => (r: falling, g: byte.MaxValue, b: 0), 
				2 => (r: 0, g: byte.MaxValue, b: rising), 
				3 => (r: 0, g: falling, b: byte.MaxValue), 
				4 => (r: rising, g: 0, b: byte.MaxValue), 
				_ => (r: byte.MaxValue, g: 0, b: falling), 
			};
		}


		static (byte r, byte g, byte b) HueToRgb(double hue)
		{
			double num2 = hue * 6.0;
			int index = (int)Math.Floor(num2) % 6;
			byte rising = (byte)Math.Round((num2 - Math.Floor(num2)) * 255.0);
			byte falling = (byte)(255 - rising);
			return index switch
			{
				0 => (r: byte.MaxValue, g: rising, b: 0), 
				1 => (r: falling, g: byte.MaxValue, b: 0), 
				2 => (r: 0, g: byte.MaxValue, b: rising), 
				3 => (r: 0, g: falling, b: byte.MaxValue), 
				4 => (r: rising, g: 0, b: byte.MaxValue), 
				_ => (r: byte.MaxValue, g: 0, b: falling), 
			};
		}


		static byte InvokeGetter(ManagementObject instance)
		{
			InvokeMethodOptions options = new InvokeMethodOptions
			{
				Timeout = TimeSpan.FromSeconds(2L)
			};
			ManagementBaseObject output = instance.InvokeMethod("GetKeyBoardBackLight", null, options);
			try
			{
				return Convert.ToByte(output["Data"], CultureInfo.InvariantCulture);
			}
			finally
			{
				((IDisposable)output)?.Dispose();
			}
		}


		static void InvokeSetter(ManagementObject instance, byte value)
		{
			ManagementBaseObject input = instance.GetMethodParameters("SetKeyBoardBackLight");
			try
			{
				input["Data"] = value;
				InvokeMethodOptions options = new InvokeMethodOptions
				{
					Timeout = TimeSpan.FromSeconds(2L)
				};
				ManagementBaseObject output = instance.InvokeMethod("SetKeyBoardBackLight", input, options);
				try
				{
				}
				finally
				{
					((IDisposable)output)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)input)?.Dispose();
			}
		}


		static bool IsAdministrator()
		{
			using WindowsIdentity identity = WindowsIdentity.GetCurrent();
			return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
		}


		static ManagementObject OpenInstance(string text2, string requiredMethod)
		{
			ManagementClass managementClass = new ManagementClass("root\\WMI", text2, null);
			try
			{
				managementClass.Get();
				if (managementClass.Methods.Cast<MethodData>().FirstOrDefault((MethodData candidate) => candidate.Name.Equals(requiredMethod, StringComparison.OrdinalIgnoreCase)) == null)
				{
					throw new InvalidOperationException(text2 + " exposes no method " + requiredMethod + ".");
				}
				using ManagementObjectCollection instances = managementClass.GetInstances();
				return instances.Cast<ManagementObject>().FirstOrDefault() ?? throw new InvalidOperationException(text2 + " has no live device instance.");
			}
			finally
			{
				((IDisposable)managementClass)?.Dispose();
			}
		}


		static byte[] Query(HidStream stream, byte command, byte selector)
		{
			byte[] query = new byte[9];
			query[1] = command;
			query[2] = selector;
			query[8] = CalculateGigabyteChecksum(query);
			stream.SetFeature(query);
			Thread.Sleep(65);
			byte[] response = new byte[9];
			stream.GetFeature(response);
			return response;
		}


		static List<Dictionary<string, object?>> Query2(string scopePath, string query)
		{
			List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
			try
			{
				ManagementObjectSearcher searcher = new ManagementObjectSearcher(scopePath, query);
				try
				{
					using ManagementObjectCollection objects = searcher.Get();
					foreach (ManagementObject item in objects)
					{
						Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
						foreach (PropertyData property in item.Properties)
						{
							values[property.Name] = property.Value;
						}
						results.Add(values);
						item.Dispose();
					}
				}
				finally
				{
					((IDisposable)searcher)?.Dispose();
				}
			}
			catch (ManagementException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
			return results;
		}


		static byte[] QueryZone(HidStream stream, byte b)
		{
			byte[] query = new byte[9];
			query[1] = 136;
			query[2] = b;
			query[8] = CalculateGigabyteChecksum(query);
			stream.SetFeature(query);
			Thread.Sleep(10);
			byte[] response = new byte[9];
			stream.GetFeature(response);
			return response;
		}


		static double Ramp(double elapsed, double periodSeconds)
		{
			return (1.0 - Math.Cos(elapsed * 2.0 * Math.PI / periodSeconds)) / 2.0;
		}


		static string ReadSingleWmiValue(string query, string property)
		{
			ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
			try
			{
				using (ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = searcher.Get().GetEnumerator())
				{
					if (managementObjectEnumerator.MoveNext())
					{
						ManagementBaseObject item = managementObjectEnumerator.Current;
						ManagementBaseObject managementBaseObject = item;
						try
						{
							return item[property]?.ToString()?.Trim() ?? string.Empty;
						}
						finally
						{
							((IDisposable)managementBaseObject)?.Dispose();
						}
					}
				}
				return string.Empty;
			}
			finally
			{
				((IDisposable)searcher)?.Dispose();
			}
		}


		void RunBacklightLevelTest()
		{
			bool confirmed = args.Any((string argument) => argument.Equals("--confirm-backlight-write", StringComparison.OrdinalIgnoreCase));
			byte[] requestedLevels = ReadByteListArgument("--levels", new byte[4] { 0, 24, 32, 50 });
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS keyboard backlight level test");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Interface: `GB_WMIACPI_Get.GetKeyBoardBackLight` and `GB_WMIACPI_Set.SetKeyBoardBackLight`, WMI method ID `0xF6`");
			report2.AppendLine("- FB0F DSDT target: EC field `KBLL` at offset `0xD7`");
			report2.AppendLine("- Gates: exact model and BIOS, administrator rights, and `--confirm-backlight-write`");
			report2.AppendLine("- Battery, fan, charge, key matrix, macros, HID, BIOS, and firmware: **not touched**");
			report2.AppendLine("- Rollback: the original value is read first and rewritten plus verified in `finally`");
			report2.AppendLine();
			if (!confirmed)
			{
				Console.Error.WriteLine("Dieser Test ruft erstmals den ACPI-Setter SetKeyBoardBackLight auf.");
				Console.Error.WriteLine("Er verlangt zusaetzlich --confirm-backlight-write.");
				report2.AppendLine("- Refused before any firmware access: `--confirm-backlight-write` was not supplied.");
				WriteReport();
				Environment.ExitCode = 2;
			}
			else
			{
				Console.OutputEncoding = Encoding.UTF8;
				Console.WriteLine("AORUS 5 SE - Test der Tastatur-Hintergrundbeleuchtungsstufe");
				Console.WriteLine();
				try
				{
					string manufacturer = ReadSingleWmiValue("SELECT Manufacturer FROM Win32_ComputerSystem", "Manufacturer");
					string model = ReadSingleWmiValue("SELECT Model FROM Win32_ComputerSystem", "Model");
					string bios = ReadSingleWmiValue("SELECT SMBIOSBIOSVersion FROM Win32_BIOS", "SMBIOSBIOSVersion");
					bool deviceApproved = manufacturer.Equals("GIGABYTE", StringComparison.OrdinalIgnoreCase) && model.Equals("AORUS 5 SE", StringComparison.OrdinalIgnoreCase) && bios.Equals("FB0F", StringComparison.OrdinalIgnoreCase);
					report2.AppendLine("## Gates");
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder8 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 3, stringBuilder6);
					handler2.AppendLiteral("- Detected device: `");
					handler2.AppendFormatted(Escape(manufacturer));
					handler2.AppendLiteral("` / `");
					handler2.AppendFormatted(Escape(model));
					handler2.AppendLiteral("` / `");
					handler2.AppendFormatted(Escape(bios));
					handler2.AppendLiteral("`");
					stringBuilder8.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(29, 1, stringBuilder6);
					handler2.AppendLiteral("- Exact approved device: **");
					handler2.AppendFormatted(deviceApproved ? "yes" : "no");
					handler2.AppendLiteral("**");
					stringBuilder9.AppendLine(ref handler2);
					if (!deviceApproved)
					{
						Console.Error.WriteLine("Nicht freigegebenes Geraet; es wurde nichts geschrieben.");
						report2.AppendLine("- Refused: device is not on the approved list.");
						WriteReport();
						Environment.ExitCode = 3;
						return;
					}
					using WindowsIdentity identity = WindowsIdentity.GetCurrent();
					bool elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
					stringBuilder6 = report2;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
					handler2.AppendLiteral("- Administrator: **");
					handler2.AppendFormatted(elevated ? "yes" : "no");
					handler2.AppendLiteral("**");
					stringBuilder10.AppendLine(ref handler2);
					if (!elevated)
					{
						Console.Error.WriteLine("Administratorrechte sind erforderlich; es wurde nichts geschrieben.");
						report2.AppendLine("- Refused: administrator rights are required for ACPI writes.");
						WriteReport();
						Environment.ExitCode = 3;
						return;
					}
					ManagementObject getter = OpenInstance("GB_WMIACPI_Get", "GetKeyBoardBackLight");
					try
					{
						ManagementObject setter = OpenInstance("GB_WMIACPI_Set", "SetKeyBoardBackLight");
						try
						{
							byte original = InvokeGetter(getter);
							report2.AppendLine();
							report2.AppendLine("## Original value");
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder11 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(45, 1, stringBuilder6);
							handler2.AppendLiteral("- `GetKeyBoardBackLight` before any write: `");
							handler2.AppendFormatted(original);
							handler2.AppendLiteral("`");
							stringBuilder11.AppendLine(ref handler2);
							Console.WriteLine($"Aktueller gespeicherter Wert: {original}");
							Console.WriteLine();
							Console.WriteLine("Es werden nun die Werte 0 bis 4 geschrieben. Beschreibe nach jedem Schritt,");
							Console.WriteLine("was du an der Tastaturbeleuchtung siehst. /stop beendet vorzeitig.");
							Console.WriteLine();
							report2.AppendLine();
							report2.AppendLine("## Levels");
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder12 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder6);
							handler2.AppendLiteral("- Levels written: `");
							handler2.AppendFormatted(string.Join("`, `", requestedLevels));
							handler2.AppendLiteral("`");
							stringBuilder12.AppendLine(ref handler2);
							report2.AppendLine();
							report2.AppendLine("| Written | Readback | Owner observation |");
							report2.AppendLine("|---|---|---|");
							try
							{
								byte[] array2 = requestedLevels;
								foreach (byte level in array2)
								{
									InvokeSetter(setter, level);
									Thread.Sleep(400);
									byte readback = InvokeGetter(getter);
									Console.WriteLine($"Wert {level} geschrieben, zurueckgelesen als {readback}.");
									Console.Write("  Beobachtung: ");
									string observation = Console.ReadLine()?.Trim() ?? string.Empty;
									stringBuilder6 = report2;
									StringBuilder stringBuilder13 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 3, stringBuilder6);
									handler2.AppendLiteral("| `");
									handler2.AppendFormatted(level);
									handler2.AppendLiteral("` | `");
									handler2.AppendFormatted(readback);
									handler2.AppendLiteral("` | ");
									handler2.AppendFormatted(Escape((observation.Length == 0) ? "(keine Beschreibung)" : observation));
									handler2.AppendLiteral(" |");
									stringBuilder13.AppendLine(ref handler2);
									if (observation.Equals("/stop", StringComparison.OrdinalIgnoreCase))
									{
										Console.WriteLine("Test wird beendet.");
										break;
									}
								}
							}
							finally
							{
								report2.AppendLine();
								report2.AppendLine("## Rollback");
								report2.AppendLine();
								try
								{
									InvokeSetter(setter, original);
									Thread.Sleep(400);
									byte restored = InvokeGetter(getter);
									stringBuilder6 = report2;
									StringBuilder stringBuilder14 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(61, 3, stringBuilder6);
									handler2.AppendLiteral("- Original value `");
									handler2.AppendFormatted(original);
									handler2.AppendLiteral("` rewritten, readback `");
									handler2.AppendFormatted(restored);
									handler2.AppendLiteral("`, exact match: **");
									handler2.AppendFormatted((restored == original) ? "yes" : "no");
									handler2.AppendLiteral("**");
									stringBuilder14.AppendLine(ref handler2);
									if (restored != original)
									{
										Environment.ExitCode = 6;
									}
									Console.WriteLine($"Der Ausgangswert {original} wurde wiederhergestellt.");
								}
								catch (Exception ex)
								{
									stringBuilder6 = report2;
									StringBuilder stringBuilder15 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
									handler2.AppendLiteral("- RESTORE ERROR: ");
									handler2.AppendFormatted(Escape(ex.Message));
									stringBuilder15.AppendLine(ref handler2);
									Console.Error.WriteLine("Wiederherstellungsfehler: " + ex.Message);
									Environment.ExitCode = 7;
								}
							}
						}
						finally
						{
							((IDisposable)setter)?.Dispose();
						}
					}
					finally
					{
						((IDisposable)getter)?.Dispose();
					}
				}
				catch (Exception ex2)
				{
					Console.Error.WriteLine("Testfehler: " + ex2.Message);
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder16 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex2.Message));
					stringBuilder16.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				WriteReport();
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-backlight-level-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}


		void RunBrightnessEventMonitor()
		{
			int durationSeconds = Math.Clamp(ReadPositiveIntArgument("--seconds", 45), 10, 300);
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - Monitor der Fn+Space-Helligkeitsereignisse");
			Console.WriteLine();
			Console.WriteLine($"Laufzeit {durationSeconds} Sekunden. Schalte in dieser Zeit mit Fn+Space");
			Console.WriteLine("mehrfach durch ALLE Stufen, inklusive der hellsten.");
			Console.WriteLine("Jedes Ereignis wird sofort angezeigt.");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS Fn+Space brightness event monitor");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(36, 2, stringBuilder6);
			handler2.AppendLiteral("- Exact target device: `VID ");
			handler2.AppendFormatted(4164, "X4");
			handler2.AppendLiteral(" / PID ");
			handler2.AppendFormatted(31297, "X4");
			handler2.AppendLiteral("`");
			stringBuilder8.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(49, 2, stringBuilder6);
			handler2.AppendLiteral("- Listened collection: `");
			handler2.AppendFormatted("MI_02 / COL_04");
			handler2.AppendLiteral("`, input report length `");
			handler2.AppendFormatted(4);
			handler2.AppendLiteral("`");
			stringBuilder9.AppendLine(ref handler2);
			report2.AppendLine("- Mode: **read-only**. No setter, no output report, no feature report, no WMI, no EC access");
			stringBuilder6 = report2;
			StringBuilder stringBuilder10 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(72, 1, stringBuilder6);
			handler2.AppendLiteral("- Privacy gate: the collection must not declare keyboard usage page `0x");
			handler2.AppendFormatted(7, "X4");
			handler2.AppendLiteral("`");
			stringBuilder10.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder11 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder6);
			handler2.AppendLiteral("- Duration: `");
			handler2.AppendFormatted(durationSeconds);
			handler2.AppendLiteral("` s");
			stringBuilder11.AppendLine(ref handler2);
			report2.AppendLine("- Known so far: byte 0 is report ID `0x04`, byte 1 is constant `0x01`, byte 2 carries the step; observed `0`, `24`, `32`");
			report2.AppendLine();
			HidDevice target = null;
			foreach (HidDevice candidate in DeviceList.Local.GetHidDevices(4164, 31297))
			{
				if (GetInterfaceLabel(candidate.DevicePath).Equals("MI_02 / COL_04", StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						ReportDescriptor descriptor = candidate.GetReportDescriptor();
						if (!descriptor.DeviceItems.SelectMany((DeviceItem item) => item.Reports).SelectMany((Report deviceReport) => deviceReport.DataItems).SelectMany((DataItem dataItem) => dataItem.Usages.GetAllValues())
							.Any((uint usage) => usage >> 16 == 7) && !descriptor.DeviceItems.SelectMany((DeviceItem item) => item.Usages.GetAllValues()).Any((uint usage) => usage >> 16 == 7 || usage == 65542) && !candidate.DevicePath.EndsWith("\\kbd", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxInputReportLength() == 4)
						{
							goto IL_03c7;
						}
					}
					catch (Exception)
					{
					}
				}
				continue;
				IL_03c7:
				target = candidate;
				break;
			}
			if (target == null)
			{
				Console.Error.WriteLine("Die Collection MI_02 / COL_04 wurde nicht gefunden oder ist nicht zugelassen.");
				report2.AppendLine("- `MI_02 / COL_04` was not found, declares keyboard usages, or has an unexpected report length; nothing was opened.");
				WriteReport();
				Environment.ExitCode = 4;
			}
			else
			{
				List<(TimeSpan At, byte[] Payload)> events = new List<(TimeSpan At, byte[] Payload)>();
				try
				{
					using HidStream stream = target.Open();
					stream.ReadTimeout = 250;
					Stopwatch clock = Stopwatch.StartNew();
					byte[] buffer = new byte[4];
					while (clock.Elapsed.TotalSeconds < (double)durationSeconds)
					{
						int received;
						try
						{
							received = stream.Read(buffer, 0, buffer.Length);
						}
						catch (TimeoutException)
						{
							continue;
						}
						if (received > 0)
						{
							byte[] payload = buffer.AsSpan(0, received).ToArray();
							events.Add((clock.Elapsed, payload));
							string level = ((received > 2) ? payload[2].ToString(CultureInfo.InvariantCulture) : "?");
							Console.WriteLine($"  {clock.Elapsed.TotalSeconds,6:F1} s  {Convert.ToHexString(payload)}  Stufe {level}");
						}
					}
					clock.Stop();
				}
				catch (Exception ex3)
				{
					Console.Error.WriteLine("Monitorfehler: " + ex3.Message);
					stringBuilder6 = report2;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
					handler2.AppendLiteral("- Monitor error: ");
					handler2.AppendFormatted(Escape(ex3.Message));
					stringBuilder12.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				report2.AppendLine("## Events");
				report2.AppendLine();
				if (events.Count == 0)
				{
					report2.AppendLine("- No input report was received during the monitoring window.");
				}
				else
				{
					report2.AppendLine("| At | Raw report | Byte 1 | Byte 2 (step) | Byte 3 |");
					report2.AppendLine("|---|---|---|---|---|");
					foreach (var item4 in events)
					{
						TimeSpan at = item4.Item1;
						byte[] payload2 = item4.Item2;
						string b1 = ((payload2.Length > 1) ? $"`0x{payload2[1]:X2}`" : "-");
						string b2 = ((payload2.Length > 2) ? $"`{payload2[2]}` / `0x{payload2[2]:X2}`" : "-");
						string b3 = ((payload2.Length > 3) ? $"`0x{payload2[3]:X2}`" : "-");
						stringBuilder6 = report2;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(22, 5, stringBuilder6);
						handler2.AppendLiteral("| `");
						handler2.AppendFormatted(at.TotalSeconds, "F1");
						handler2.AppendLiteral("` s | `");
						handler2.AppendFormatted(Convert.ToHexString(payload2));
						handler2.AppendLiteral("` | ");
						handler2.AppendFormatted(b1);
						handler2.AppendLiteral(" | ");
						handler2.AppendFormatted(b2);
						handler2.AppendLiteral(" | ");
						handler2.AppendFormatted(b3);
						handler2.AppendLiteral(" |");
						stringBuilder13.AppendLine(ref handler2);
					}
					byte[] distinctSteps = (from value in (from item in events
							where item.Payload.Length > 2
							select item.Payload[2]).Distinct()
						orderby value
						select value).ToArray();
					byte[] distinctTypes = (from value in (from item in events
							where item.Payload.Length > 1
							select item.Payload[1]).Distinct()
						orderby value
						select value).ToArray();
					report2.AppendLine();
					report2.AppendLine("## Summary");
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder14 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
					handler2.AppendLiteral("- Events captured: `");
					handler2.AppendFormatted(events.Count);
					handler2.AppendLiteral("`");
					stringBuilder14.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder15 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(26, 1, stringBuilder6);
					handler2.AppendLiteral("- Distinct byte-1 values: ");
					handler2.AppendFormatted(string.Join(", ", distinctTypes.Select((byte value) => $"`0x{value:X2}`")));
					stringBuilder15.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder16 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder6);
					handler2.AppendLiteral("- Distinct steps in byte 2: ");
					handler2.AppendFormatted(string.Join(", ", distinctSteps.Select((byte value) => $"`{value}`")));
					stringBuilder16.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder17 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(30, 1, stringBuilder6);
					handler2.AppendLiteral("- Number of distinct steps: `");
					handler2.AppendFormatted(distinctSteps.Length);
					handler2.AppendLiteral("`");
					stringBuilder17.AppendLine(ref handler2);
					if (distinctSteps.Length >= 4)
					{
						report2.AppendLine("- All four physical steps are now covered, so the value table is complete.");
					}
					else
					{
						report2.AppendLine("- Fewer than four distinct steps were seen, so the table is still incomplete. Missing steps need another run in which every level is cycled.");
					}
				}
				WriteReport();
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-brightness-events-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine();
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}


		static void RunBrightnessSignalHunt()
		{
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - Suche nach einem Host-Signal der Fn+Space-Stufen");
			Console.WriteLine();
			Console.WriteLine("Ablauf pro Runde: Du druckst Fn+Space, wartest kurz, und druckst dann Enter.");
			Console.WriteLine("Waehrend der Wartezeit wird auf allen Nicht-Tastatur-Collections mitgehoert.");
			Console.WriteLine("Nach Enter wird der komplette lesbare Zustand abgefragt.");
			Console.WriteLine("Am Ende werden alle Runden gegeneinander verglichen.");
			Console.WriteLine();
			Console.WriteLine("Es wird ausschliesslich gelesen. Collections mit Tastatur-Usages werden");
			Console.WriteLine("uebersprungen, es koennen also keine Tastenanschlaege erfasst werden.");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS Fn+Space brightness signal hunt");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(36, 2, stringBuilder6);
			handler2.AppendLiteral("- Exact target device: `VID ");
			handler2.AppendFormatted(4164, "X4");
			handler2.AppendLiteral(" / PID ");
			handler2.AppendFormatted(31297, "X4");
			handler2.AppendLiteral("`");
			stringBuilder8.AppendLine(ref handler2);
			report2.AppendLine("- Mode: **read-only**. No setter, no output report, no WMI, no EC write");
			stringBuilder6 = report2;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(112, 1, stringBuilder6);
			handler2.AppendLiteral("- Privacy gate: every collection declaring keyboard usage page `0x");
			handler2.AppendFormatted(7, "X4");
			handler2.AppendLiteral("` is skipped, so keystrokes cannot be captured");
			stringBuilder9.AppendLine(ref handler2);
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) written: **no**");
			stringBuilder6 = report2;
			StringBuilder stringBuilder10 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(109, 1, stringBuilder6);
			handler2.AppendLiteral("- New ground 1: official getter `0x88` queried for selectors `0` to `");
			handler2.AppendFormatted((byte)15);
			handler2.AppendLiteral("`; only `0` to `3` were ever used before");
			stringBuilder10.AppendLine(ref handler2);
			report2.AppendLine("- New ground 2: input listening on the small `MI_02` collections, including `COL_03` and `COL_04`, which declare no usages");
			report2.AppendLine();
			List<(string, HidDevice)> listenDevices = new List<(string, HidDevice)>();
			HidDevice rgbDevice = null;
			foreach (HidDevice candidate in DeviceList.Local.GetHidDevices(4164, 31297))
			{
				string label = GetInterfaceLabel(candidate.DevicePath);
				bool declaresKeyboard;
				int inputLength;
				try
				{
					ReportDescriptor descriptor = candidate.GetReportDescriptor();
					bool num2 = descriptor.DeviceItems.SelectMany((DeviceItem item) => item.Reports).SelectMany((Report deviceReport) => deviceReport.DataItems).SelectMany((DataItem dataItem) => dataItem.Usages.GetAllValues())
						.Any((uint usage) => usage >> 16 == 7);
					bool keyUsageOnCollection = descriptor.DeviceItems.SelectMany((DeviceItem item) => item.Usages.GetAllValues()).Any((uint usage) => usage >> 16 == 7 || usage == 65542);
					bool keyboardDevicePath = candidate.DevicePath.EndsWith("\\kbd", StringComparison.OrdinalIgnoreCase);
					declaresKeyboard = num2 | keyUsageOnCollection | keyboardDevicePath;
					inputLength = candidate.GetMaxInputReportLength();
				}
				catch (Exception ex)
				{
					stringBuilder6 = report2;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(39, 2, stringBuilder6);
					handler2.AppendLiteral("- `");
					handler2.AppendFormatted(label);
					handler2.AppendLiteral("`: descriptor unreadable, skipped (");
					handler2.AppendFormatted(Escape(ex.Message));
					handler2.AppendLiteral(")");
					stringBuilder11.AppendLine(ref handler2);
					continue;
				}
				if (GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9)
				{
					rgbDevice = candidate;
				}
				if (declaresKeyboard)
				{
					stringBuilder6 = report2;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(39, 1, stringBuilder6);
					handler2.AppendLiteral("- `");
					handler2.AppendFormatted(label);
					handler2.AppendLiteral("`: skipped, declares keyboard usages");
					stringBuilder12.AppendLine(ref handler2);
				}
				else if (inputLength <= 0)
				{
					stringBuilder6 = report2;
					StringBuilder stringBuilder13 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 1, stringBuilder6);
					handler2.AppendLiteral("- `");
					handler2.AppendFormatted(label);
					handler2.AppendLiteral("`: skipped, no input reports");
					stringBuilder13.AppendLine(ref handler2);
				}
				else
				{
					listenDevices.Add((label, candidate));
				}
			}
			report2.AppendLine();
			stringBuilder6 = report2;
			StringBuilder stringBuilder14 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(29, 1, stringBuilder6);
			handler2.AppendLiteral("- Collections listened to: `");
			handler2.AppendFormatted(listenDevices.Count);
			handler2.AppendLiteral("`");
			stringBuilder14.AppendLine(ref handler2);
			foreach (var item7 in listenDevices)
			{
				string label2 = item7.Item1;
				HidDevice device2 = item7.Item2;
				stringBuilder6 = report2;
				StringBuilder stringBuilder15 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(30, 2, stringBuilder6);
				handler2.AppendLiteral("  - `");
				handler2.AppendFormatted(label2);
				handler2.AppendLiteral("`, input report length `");
				handler2.AppendFormatted(device2.GetMaxInputReportLength());
				handler2.AppendLiteral("`");
				stringBuilder15.AppendLine(ref handler2);
			}
			if (rgbDevice == null)
			{
				Console.Error.WriteLine("Die exakt zugelassene RGB-Schnittstelle wurde nicht gefunden.");
				report2.AppendLine();
				report2.AppendLine("- Exact approved RGB feature collection was not found; no state dump possible.");
				WriteReport();
				Environment.ExitCode = 4;
			}
			else
			{
				List<(string, Dictionary<string, string>)> rounds = new List<(string, Dictionary<string, string>)>();
				ConcurrentBag<string> captured = new ConcurrentBag<string>();
				try
				{
					using HidStream rgbStream = rgbDevice.Open();
					for (int round = 1; round <= 5; round++)
					{
						Console.WriteLine($"Runde {round} von 5.");
						Console.WriteLine("  Druecke jetzt Fn+Space, warte einen Moment, dann Enter.");
						Console.Write("  Beschreibe die aktuelle Helligkeit: ");
						CancellationTokenSource cancellation = new CancellationTokenSource();
						try
						{
							List<Thread> listeners = new List<Thread>();
							List<HidStream> streams = new List<HidStream>();
							foreach (var item8 in listenDevices)
							{
								var (label3, device3) = item8;
								HidStream stream;
								try
								{
									stream = device3.Open();
								}
								catch (Exception ex2)
								{
									captured.Add($"Round {round}: `{label3}` could not be opened ({Escape(ex2.Message)})");
									continue;
								}
								stream.ReadTimeout = 250;
								streams.Add(stream);
								Thread listener = new Thread((ThreadStart)delegate
								{
									byte[] array3 = new byte[device3.GetMaxInputReportLength()];
									while (!cancellation.IsCancellationRequested)
									{
										try
										{
											int num4 = stream.Read(array3, 0, array3.Length);
											if (num4 > 0)
											{
												captured.Add($"Round {round}, `{label3}`: `{Convert.ToHexString(array3.AsSpan(0, num4))}`");
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
									Name = "aorus-listen-" + label3
								};
								listener.Start();
								listeners.Add(listener);
							}
							string description = Console.ReadLine()?.Trim() ?? string.Empty;
							cancellation.Cancel();
							foreach (Thread item9 in listeners)
							{
								item9.Join(1500);
							}
							foreach (HidStream item10 in streams)
							{
								item10.Dispose();
							}
							Dictionary<string, string> state = new Dictionary<string, string>(StringComparer.Ordinal);
							for (byte selector = 0; selector <= 15; selector++)
							{
								state[$"0x88 sel {selector,2}"] = Convert.ToHexString(Query(rgbStream, 136, selector));
							}
							state["0x80 firmware"] = Convert.ToHexString(Query(rgbStream, 128, 0));
							rounds.Add(((description.Length == 0) ? $"Runde {round}" : description, state));
							Console.WriteLine();
							if (description.Equals("/stop", StringComparison.OrdinalIgnoreCase))
							{
								break;
							}
						}
						finally
						{
							if (cancellation != null)
							{
								((IDisposable)cancellation).Dispose();
							}
						}
					}
				}
				catch (Exception ex3)
				{
					Console.Error.WriteLine("Testfehler: " + ex3.Message);
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder16 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex3.Message));
					stringBuilder16.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				report2.AppendLine();
				report2.AppendLine("## Captured input reports");
				report2.AppendLine();
				string[] capturedLines = captured.OrderBy<string, string>((string result) => result, StringComparer.Ordinal).ToArray();
				if (capturedLines.Length == 0)
				{
					report2.AppendLine("- **No input report at all** was emitted by any listened collection.");
				}
				else
				{
					string[] array2 = capturedLines;
					foreach (string line in array2)
					{
						stringBuilder6 = report2;
						StringBuilder stringBuilder17 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder6);
						handler2.AppendLiteral("- ");
						handler2.AppendFormatted(line);
						stringBuilder17.AppendLine(ref handler2);
					}
				}
				report2.AppendLine();
				report2.AppendLine("## State per round");
				report2.AppendLine();
				if (rounds.Count > 0)
				{
					report2.AppendLine("| Query | " + string.Join(" | ", rounds.Select<(string, Dictionary<string, string>), string>(((string Label, Dictionary<string, string> State) item) => Escape(item.Label))) + " |");
					report2.AppendLine(string.Concat("|---", string.Concat(Enumerable.Repeat("|---", rounds.Count)), "|"));
					List<string> changing = new List<string>();
					foreach (string key2 in rounds[0].Item2.Keys)
					{
						string[] values = rounds.Select<(string, Dictionary<string, string>), string>(((string Label, Dictionary<string, string> State) item) => (!item.State.TryGetValue(key2, out var value)) ? "-" : value).ToArray();
						bool differs = values.Distinct<string>(StringComparer.Ordinal).Count() > 1;
						if (differs)
						{
							changing.Add(key2);
						}
						report2.AppendLine($"| `{key2}`{(differs ? " **changed**" : string.Empty)} | " + string.Join(" | ", values.Select((string value) => "`" + value + "`")) + " |");
					}
					report2.AppendLine();
					report2.AppendLine("## Verdict");
					report2.AppendLine();
					if (changing.Count == 0)
					{
						report2.AppendLine("- **No queried value changed across the rounds.** Neither the extended selector range of the official getter nor the firmware query carries the Fn+Space step.");
					}
					else
					{
						stringBuilder6 = report2;
						StringBuilder stringBuilder18 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(145, 1, stringBuilder6);
						handler2.AppendLiteral("- Values that changed across rounds: ");
						handler2.AppendFormatted(string.Join(", ", changing.Select((string text2) => "`" + text2 + "`")));
						handler2.AppendLiteral(". These are the first host-readable trace of the physical brightness step and deserve a dedicated follow-up.");
						stringBuilder18.AppendLine(ref handler2);
					}
					if (capturedLines.Length == 0)
					{
						report2.AppendLine("- No collection outside the keyboard interfaces reported anything, which further supports full in-controller handling of the chord.");
					}
				}
				else
				{
					report2.AppendLine("- No round completed.");
				}
				WriteReport();
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-brightness-signal-hunt-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}


		static void RunEffectPaletteTest()
		{
			(byte, string, byte, string, string)[] steps = new(byte, string, byte, string, string)[5]
			{
				(1, "Static", 1, "Red", "Wird die Tastatur rot?"),
				(1, "Static", 4, "Blue", "Wird sie blau?"),
				(2, "Breathing", 1, "Red", "Pulsiert sie rot, oder steht sie nur rot?"),
				(3, "Wave", 8, "Random", "Bewegt sich etwas zwischen den Zonen?"),
				(8, "Neon", 8, "Random", "Wechseln die Farben von selbst?")
			};
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - Effekt- und Palettentest");
			Console.WriteLine();
			Console.WriteLine("Der vorige Lauf sendete in jedem Schritt Farbbyte 0. Das ist in Gigabytes");
			Console.WriteLine("Enum die Farbe Schwarz, deshalb wurde die Tastatur dunkel. Dieser Test");
			Console.WriteLine("verwendet kraeftige Farben, die sich nicht mit dem Vorzustand verwechseln");
			Console.WriteLine("lassen. Vor jedem Schritt werden alle Zonen auf Weiss gesetzt und geprueft.");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS effect and palette test");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88`, global effect `0x08` selector 0");
			report2.AppendLine("- Picture-matrix commands `0x12` / `0x92` used: **no**");
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) touched: **no**");
			report2.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			report2.AppendLine("- Baseline before every step: all three zones written to `#FFFFFF` at brightness `50` and verified");
			report2.AppendLine("- Correction under test: the previous isolation run sent palette byte `0` = `FusionLightColor.Black` in every step, which makes its blackout uninformative about the effect engine");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				Console.Error.WriteLine("Die exakt zugelassene RGB-Schnittstelle wurde nicht gefunden.");
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			try
			{
				Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
				try
				{
					report2.AppendLine("## Captured original zone state");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] state = QueryZone2(zone2);
						originalZones.Add(zone2, state);
						stringBuilder6 = report2;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(state[3], "X2");
						handler2.AppendFormatted(state[4], "X2");
						handler2.AppendFormatted(state[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(state[6]);
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
					}
					report2.AppendLine();
					report2.AppendLine("## Steps");
					report2.AppendLine();
					for (int index = 0; index < steps.Length; index++)
					{
						(byte, string, byte, string, string) tuple = steps[index];
						byte effect = tuple.Item1;
						string effectName = tuple.Item2;
						byte palette = tuple.Item3;
						string paletteName = tuple.Item4;
						string question = tuple.Item5;
						bool baselineVerified = true;
						for (byte zone3 = 1; zone3 <= 3; zone3++)
						{
							WriteZone2(zone3, byte.MaxValue, byte.MaxValue, byte.MaxValue, 50);
							Thread.Sleep(65);
							byte[] readback = QueryZone2(zone3);
							if (readback[3] != byte.MaxValue || readback[4] != byte.MaxValue || readback[5] != byte.MaxValue || readback[6] != 50)
							{
								baselineVerified = false;
							}
						}
						Console.WriteLine($"{index + 1}/{steps.Length}: Effekt {effect} ({effectName}), Farbe {palette} ({paletteName})");
						Console.WriteLine(baselineVerified ? "  Ausgangslage: alle drei Zonen weiss und geprueft." : "  ACHTUNG: die weisse Ausgangslage konnte nicht verifiziert werden.");
						byte[] request = new byte[9];
						request[1] = 8;
						request[2] = 0;
						request[3] = effect;
						request[4] = 5;
						request[5] = 50;
						request[6] = palette;
						request[7] = 1;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(2000);
						byte[] globalReadback = QueryZone2(0);
						Console.WriteLine("  " + question);
						Console.Write("  Beobachtung: ");
						string observation = Console.ReadLine()?.Trim() ?? string.Empty;
						stringBuilder6 = report2;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(37, 5, stringBuilder6);
						handler2.AppendLiteral("### ");
						handler2.AppendFormatted(index + 1);
						handler2.AppendLiteral(". Effect `");
						handler2.AppendFormatted(effect);
						handler2.AppendLiteral("` (");
						handler2.AppendFormatted(effectName);
						handler2.AppendLiteral(") with palette `");
						handler2.AppendFormatted(palette);
						handler2.AppendLiteral("` (");
						handler2.AppendFormatted(paletteName);
						handler2.AppendLiteral(")");
						stringBuilder9.AppendLine(ref handler2);
						report2.AppendLine();
						stringBuilder6 = report2;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 1, stringBuilder6);
						handler2.AppendLiteral("- White baseline verified: **");
						handler2.AppendFormatted(baselineVerified ? "yes" : "no");
						handler2.AppendLiteral("**");
						stringBuilder10.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
						handler2.AppendLiteral("- Request: `");
						handler2.AppendFormatted(Convert.ToHexString(request));
						handler2.AppendLiteral("`");
						stringBuilder11.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder12 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
						handler2.AppendLiteral("- Global readback: `");
						handler2.AppendFormatted(Convert.ToHexString(globalReadback));
						handler2.AppendLiteral("`");
						stringBuilder12.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
						handler2.AppendLiteral("- Owner observation: ");
						handler2.AppendFormatted(Escape((observation.Length == 0) ? "(keine Beschreibung eingegeben)" : observation));
						stringBuilder13.AppendLine(ref handler2);
						report2.AppendLine();
						if (observation.Equals("/stop", StringComparison.OrdinalIgnoreCase))
						{
							Console.WriteLine("Test wird beendet.");
							break;
						}
						Console.WriteLine();
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Testfehler: " + ex.Message);
					stringBuilder6 = report2;
					StringBuilder stringBuilder14 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder14.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					report2.AppendLine("## Restoration");
					report2.AppendLine();
					foreach (var (zone4, original) in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
					{
						try
						{
							WriteZone2(zone4, original[3], original[4], original[5], original[6]);
							Thread.Sleep(65);
							byte[] restored = QueryZone2(zone4);
							bool exact = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
							stringBuilder6 = report2;
							StringBuilder stringBuilder15 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(": `#");
							handler2.AppendFormatted(restored[3], "X2");
							handler2.AppendFormatted(restored[4], "X2");
							handler2.AppendFormatted(restored[5], "X2");
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(restored[6]);
							handler2.AppendLiteral("`, exact match: **");
							handler2.AppendFormatted(exact ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder15.AppendLine(ref handler2);
							if (!exact)
							{
								Environment.ExitCode = 6;
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = report2;
							StringBuilder stringBuilder16 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(" restore failed: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder16.AppendLine(ref handler2);
							Environment.ExitCode = 6;
						}
					}
					Console.WriteLine("Die vorherigen drei RGB-Zonen wurden wiederhergestellt.");
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] QueryZone2(byte selector)
			{
				byte[] query = new byte[9];
				query[1] = 136;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(65);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-effect-palette-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine("Report written to: " + outputPath2);
			}
			void WriteZone2(byte b2, byte red, byte green, byte blue, byte brightness = 50)
			{
				byte[] request2 = new byte[9];
				request2[1] = 8;
				request2[2] = b2;
				request2[3] = red;
				request2[4] = green;
				request2[5] = blue;
				request2[6] = brightness;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
			}
		}


		static void RunEffectSelectionIsolation()
		{
			(byte, string, string)[] steps = new(byte, string, string)[5]
			{
				(51, "Custom 1", "Wird die Tastatur dunkel, so wie beim letzten Test?"),
				(1, "Static", "Bleibt sie unveraendert, oder aendert sich etwas?"),
				(52, "Custom 2", "Wird sie wieder dunkel? Custom 2 ist ebenfalls leer."),
				(2, "Breathing", "Passiert etwas, oder bleibt alles unveraendert?"),
				(8, "Neon", "Passiert etwas, oder bleibt alles unveraendert?")
			};
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - Isolation der globalen Effektauswahl");
			Console.WriteLine();
			Console.WriteLine("Vor jedem Schritt werden alle drei Zonen auf Weiss gesetzt und geprueft,");
			Console.WriteLine("damit du eine eindeutige Ausgangslage siehst. Danach wird genau ein");
			Console.WriteLine("Effektpaket gesendet. Es wird KEIN Picture-Matrix-Kommando verwendet.");
			Console.WriteLine("Beschreibe nach jedem Schritt, was du siehst, und druecke Enter.");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS global effect selection isolation");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88`, global effect `0x08` selector 0");
			report2.AppendLine("- Picture-matrix commands `0x12` / `0x92` used: **no**");
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) touched: **no**");
			report2.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			report2.AppendLine("- Baseline before every step: all three zones written to `#FFFFFF` at brightness `50` and verified");
			report2.AppendLine("- Purpose: separate the blackout cause — effect selection `51` versus the failed picture-matrix output reports");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				Console.Error.WriteLine("Die exakt zugelassene RGB-Schnittstelle wurde nicht gefunden.");
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			try
			{
				Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
				try
				{
					report2.AppendLine("## Captured original zone state");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] state = QueryZone2(zone2);
						originalZones.Add(zone2, state);
						stringBuilder6 = report2;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(state[3], "X2");
						handler2.AppendFormatted(state[4], "X2");
						handler2.AppendFormatted(state[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(state[6]);
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
					}
					report2.AppendLine();
					report2.AppendLine("## Steps");
					report2.AppendLine();
					for (int index = 0; index < steps.Length; index++)
					{
						(byte, string, string) tuple = steps[index];
						byte effect = tuple.Item1;
						string name = tuple.Item2;
						string question = tuple.Item3;
						bool baselineVerified = true;
						for (byte zone3 = 1; zone3 <= 3; zone3++)
						{
							WriteZone2(zone3, byte.MaxValue, byte.MaxValue, byte.MaxValue, 50);
							Thread.Sleep(65);
							byte[] readback = QueryZone2(zone3);
							if (readback[3] != byte.MaxValue || readback[4] != byte.MaxValue || readback[5] != byte.MaxValue || readback[6] != 50)
							{
								baselineVerified = false;
							}
						}
						Console.WriteLine($"{index + 1}/{steps.Length}: Effekt {effect} ({name})");
						Console.WriteLine(baselineVerified ? "  Ausgangslage: alle drei Zonen weiss und geprueft. Die Tastatur sollte jetzt hell leuchten." : "  ACHTUNG: die weisse Ausgangslage konnte nicht verifiziert werden.");
						Console.WriteLine("  Sende Effektpaket ...");
						byte[] request = new byte[9];
						request[1] = 8;
						request[2] = 0;
						request[3] = effect;
						request[4] = 5;
						request[5] = 50;
						request[6] = 0;
						request[7] = 1;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(1500);
						byte[] globalReadback = QueryZone2(0);
						byte[] zoneAfter = QueryZone2(1);
						Console.WriteLine("  " + question);
						Console.Write("  Beobachtung: ");
						string observation = Console.ReadLine()?.Trim() ?? string.Empty;
						stringBuilder6 = report2;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 3, stringBuilder6);
						handler2.AppendLiteral("### ");
						handler2.AppendFormatted(index + 1);
						handler2.AppendLiteral(". Effect `");
						handler2.AppendFormatted(effect);
						handler2.AppendLiteral("` (");
						handler2.AppendFormatted(name);
						handler2.AppendLiteral(")");
						stringBuilder9.AppendLine(ref handler2);
						report2.AppendLine();
						stringBuilder6 = report2;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 1, stringBuilder6);
						handler2.AppendLiteral("- White baseline verified: **");
						handler2.AppendFormatted(baselineVerified ? "yes" : "no");
						handler2.AppendLiteral("**");
						stringBuilder10.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
						handler2.AppendLiteral("- Request: `");
						handler2.AppendFormatted(Convert.ToHexString(request));
						handler2.AppendLiteral("`");
						stringBuilder11.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder12 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
						handler2.AppendLiteral("- Global readback: `");
						handler2.AppendFormatted(Convert.ToHexString(globalReadback));
						handler2.AppendLiteral("`");
						stringBuilder12.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(54, 4, stringBuilder6);
						handler2.AppendLiteral("- Zone 1 readback after the effect: `#");
						handler2.AppendFormatted(zoneAfter[3], "X2");
						handler2.AppendFormatted(zoneAfter[4], "X2");
						handler2.AppendFormatted(zoneAfter[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(zoneAfter[6]);
						handler2.AppendLiteral("`");
						stringBuilder13.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder14 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
						handler2.AppendLiteral("- Owner observation: ");
						handler2.AppendFormatted(Escape((observation.Length == 0) ? "(keine Beschreibung eingegeben)" : observation));
						stringBuilder14.AppendLine(ref handler2);
						report2.AppendLine();
						if (observation.Equals("/stop", StringComparison.OrdinalIgnoreCase))
						{
							Console.WriteLine("Test wird beendet.");
							break;
						}
						Console.WriteLine();
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Testfehler: " + ex.Message);
					stringBuilder6 = report2;
					StringBuilder stringBuilder15 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder15.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					report2.AppendLine("## Restoration");
					report2.AppendLine();
					foreach (var (zone4, original) in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
					{
						try
						{
							WriteZone2(zone4, original[3], original[4], original[5], original[6]);
							Thread.Sleep(65);
							byte[] restored = QueryZone2(zone4);
							bool exact = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
							stringBuilder6 = report2;
							StringBuilder stringBuilder16 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(": `#");
							handler2.AppendFormatted(restored[3], "X2");
							handler2.AppendFormatted(restored[4], "X2");
							handler2.AppendFormatted(restored[5], "X2");
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(restored[6]);
							handler2.AppendLiteral("`, exact match: **");
							handler2.AppendFormatted(exact ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder16.AppendLine(ref handler2);
							if (!exact)
							{
								Environment.ExitCode = 6;
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = report2;
							StringBuilder stringBuilder17 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(" restore failed: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder17.AppendLine(ref handler2);
							Environment.ExitCode = 6;
						}
					}
					Console.WriteLine("Die vorherigen drei RGB-Zonen wurden wiederhergestellt.");
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] QueryZone2(byte selector)
			{
				byte[] query = new byte[9];
				query[1] = 136;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(65);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-effect-isolation-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine("Report written to: " + outputPath2);
			}
			void WriteZone2(byte b2, byte red, byte green, byte blue, byte brightness = 50)
			{
				byte[] request2 = new byte[9];
				request2[1] = 8;
				request2[2] = b2;
				request2[3] = red;
				request2[4] = green;
				request2[5] = blue;
				request2[6] = brightness;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
			}
		}


		static void RunInteractiveHostEffectTest()
		{
			(string, string, Func<double, (byte, byte, byte)[]>)[] effects = new(string, string, Func<double, (byte, byte, byte)[]>)[10]
			{
				("Static", "Sollte ruhig und unveraenderlich in einer Farbe leuchten.", (double _) => Uniform(0, byte.MaxValue, 0)),
				("Breathing", "Sollte langsam heller und dunkler werden, ohne Farbwechsel.", (double elapsed) => Uniform(Scale(0, Ramp(elapsed, 3.0)), Scale(byte.MaxValue, Ramp(elapsed, 3.0)), Scale(0, Ramp(elapsed, 3.0)))),
				("Pulse", "Sollte deutlich schneller und harter blinken als Breathing.", (double elapsed) => (!(elapsed % 0.7 < 0.35)) ? Uniform(10, 2, 0) : Uniform(byte.MaxValue, 40, 0)),
				("Colour cycle", "Alle drei Zonen sollten gemeinsam durch das ganze Farbspektrum wandern.", delegate(double elapsed)
				{
					(byte, byte, byte) tuple2 = HostHueToRgb(elapsed / 6.0 % 1.0);
					return new(byte, byte, byte)[3] { tuple2, tuple2, tuple2 };
				}),
				("Rainbow marquee", "Die drei Zonen sollten unterschiedliche Farben zeigen, die nach rechts wandern.", (double elapsed) => new(byte, byte, byte)[3]
				{
					HostHueToRgb((elapsed / 5.0 + 0.0) % 1.0),
					HostHueToRgb((elapsed / 5.0 + 0.33) % 1.0),
					HostHueToRgb((elapsed / 5.0 + 0.66) % 1.0)
				}),
				("Wave", "Eine helle Zone sollte weich von links nach rechts laufen, Rest gedimmt.", (double elapsed) => Travelling(elapsed, 0.5, (r: 0, g: byte.MaxValue, b: 120), (r: 0, g: 30, b: 15), pingPong: false)),
				("Marquee", "Wie Wave, aber schneller und mit hartem Wechsel.", (double elapsed) => Travelling(elapsed, 0.18, (r: byte.MaxValue, g: byte.MaxValue, b: byte.MaxValue), (r: 0, g: 0, b: 0), pingPong: false)),
				("Rotate", "Die helle Zone sollte hin und zurueck pendeln, nicht nur in eine Richtung.", (double elapsed) => Travelling(elapsed, 0.4, (r: 120, g: 0, b: byte.MaxValue), (r: 12, g: 0, b: 25), pingPong: true)),
				("Raindrop", "Einzelne Zonen sollten unregelmaessig kurz aufblitzen.", delegate(double elapsed)
				{
					(byte, byte, byte)[] obj = new(byte, byte, byte)[3]
					{
						(0, 10, 30),
						(0, 10, 30),
						(0, 10, 30)
					};
					int num3 = (int)(Math.Abs(Math.Sin((double)(int)(elapsed / 0.25) * 12.9898) * 43758.5453) % 3.0);
					obj[num3] = (120, 200, byte.MaxValue);
					return obj;
				}),
				("Fade sweep", "Die Zonen sollten nacheinander aufleuchten und langsam ausklingen.", delegate(double elapsed)
				{
					(byte, byte, byte)[] array3 = new(byte, byte, byte)[3];
					for (int i = 0; i < 3; i++)
					{
						double num3 = (elapsed / 1.2 - (double)i * 0.33) % 1.0;
						if (num3 < 0.0)
						{
							num3++;
						}
						double factor = Math.Max(0.0, 1.0 - num3 * 1.6);
						array3[i] = (Scale(byte.MaxValue, factor), Scale(120, factor), Scale(0, factor));
					}
					return array3;
				})
			};
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - interaktiver Test host-gerenderter RGB-Effekte");
			Console.WriteLine("Jeder Effekt laeuft dauerhaft, bis du Enter drueckst.");
			Console.WriteLine("Enter = naechster Effekt | Text = Beobachtung speichern und weiter | /stop = beenden");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS interactive host-rendered RGB-effect test");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88` only");
			report2.AppendLine("- Global effect command `0x08` selector 0 used: **no**");
			report2.AppendLine("- Picture-matrix commands `0x12` / `0x92` used: **no**");
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) touched: **no**");
			report2.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(38, 1, stringBuilder6);
			handler2.AppendLiteral("- Frame interval per zone write: `");
			handler2.AppendFormatted(5);
			handler2.AppendLiteral(" ms`");
			stringBuilder8.AppendLine(ref handler2);
			report2.AppendLine("- Animation is rendered in the RGB values; the brightness byte stays at `50`, because raw brightness is a proven off/on gate on this firmware.");
			report2.AppendLine("- Advancement: owner-controlled; no fixed timeout");
			report2.AppendLine("- Restore policy: capture all zones; restore and verify in `finally`");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				Console.Error.WriteLine("Die exakt zugelassene RGB-Schnittstelle wurde nicht gefunden.");
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			try
			{
				Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
				try
				{
					report2.AppendLine("## Captured original zone state");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] state = QueryZone2(zone2);
						originalZones.Add(zone2, state);
						stringBuilder6 = report2;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(state[3], "X2");
						handler2.AppendFormatted(state[4], "X2");
						handler2.AppendFormatted(state[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(state[6]);
						handler2.AppendLiteral("`");
						stringBuilder9.AppendLine(ref handler2);
					}
					report2.AppendLine();
					report2.AppendLine("## Effects and owner observations");
					report2.AppendLine();
					for (int index = 0; index < effects.Length; index++)
					{
						string name;
						string hint;
						Func<double, (byte r, byte g, byte b)[]> frameAt;
						(name, hint, frameAt) = effects[index];
						Console.WriteLine($"{index + 1}/{effects.Length}: {name}");
						Console.WriteLine("  " + hint);
						Console.Write("  Beobachtung: ");
						CancellationTokenSource cancellation = new CancellationTokenSource();
						try
						{
							long renderedFrames = 0L;
							Stopwatch clock = Stopwatch.StartNew();
							Thread thread = new Thread((ThreadStart)delegate
							{
								while (!cancellation.IsCancellationRequested)
								{
									(byte, byte, byte)[] array3 = frameAt(clock.Elapsed.TotalSeconds);
									byte b2 = 1;
									while (b2 <= 3 && !cancellation.IsCancellationRequested)
									{
										var (red, green, blue) = array3[b2 - 1];
										WriteZone2(b2, red, green, blue, 50);
										Thread.Sleep(5);
										b2++;
									}
									Interlocked.Increment(ref renderedFrames);
								}
							});
							thread.IsBackground = true;
							thread.Name = $"aorus-host-effect-{index}";
							thread.Start();
							string observation = Console.ReadLine() ?? string.Empty;
							cancellation.Cancel();
							thread.Join();
							clock.Stop();
							bool num2 = observation.Trim().Equals("/stop", StringComparison.OrdinalIgnoreCase);
							string recorded = ((num2 || observation.Trim().Length == 0) ? "(keine Beschreibung eingegeben)" : observation.Trim());
							double achieved = (double)renderedFrames / Math.Max(0.001, clock.Elapsed.TotalSeconds);
							stringBuilder6 = report2;
							StringBuilder stringBuilder10 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(8, 2, stringBuilder6);
							handler2.AppendLiteral("### ");
							handler2.AppendFormatted(index + 1);
							handler2.AppendLiteral(". `");
							handler2.AppendFormatted(name);
							handler2.AppendLiteral("`");
							stringBuilder10.AppendLine(ref handler2);
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder11 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder6);
							handler2.AppendLiteral("- Expected appearance: ");
							handler2.AppendFormatted(Escape(hint));
							stringBuilder11.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder12 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(49, 3, stringBuilder6);
							handler2.AppendLiteral("- Ran for `");
							handler2.AppendFormatted(clock.Elapsed.TotalSeconds, "F1");
							handler2.AppendLiteral("` s, `");
							handler2.AppendFormatted(renderedFrames);
							handler2.AppendLiteral("` three-zone frames, `");
							handler2.AppendFormatted(achieved, "F1");
							handler2.AppendLiteral("` frames/s");
							stringBuilder12.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder13 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
							handler2.AppendLiteral("- Owner observation: ");
							handler2.AppendFormatted(Escape(recorded));
							stringBuilder13.AppendLine(ref handler2);
							report2.AppendLine();
							if (num2)
							{
								Console.WriteLine("Test wird beendet und die vorherigen Zonen werden wiederhergestellt.");
								break;
							}
							Console.WriteLine();
						}
						finally
						{
							if (cancellation != null)
							{
								((IDisposable)cancellation).Dispose();
							}
						}
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Testfehler: " + ex.Message);
					stringBuilder6 = report2;
					StringBuilder stringBuilder14 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder14.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					report2.AppendLine("## Restoration");
					report2.AppendLine();
					if (originalZones.Count == 3)
					{
						try
						{
							foreach (var (zone3, original) in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
							{
								WriteZone2(zone3, original[3], original[4], original[5], original[6]);
								Thread.Sleep(65);
								byte[] restored = QueryZone2(zone3);
								bool verified = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
								stringBuilder6 = report2;
								StringBuilder stringBuilder15 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(42, 6, stringBuilder6);
								handler2.AppendLiteral("- Zone ");
								handler2.AppendFormatted(zone3);
								handler2.AppendLiteral(": `#");
								handler2.AppendFormatted(restored[3], "X2");
								handler2.AppendFormatted(restored[4], "X2");
								handler2.AppendFormatted(restored[5], "X2");
								handler2.AppendLiteral("`, ");
								handler2.AppendLiteral("brightness `");
								handler2.AppendFormatted(restored[6]);
								handler2.AppendLiteral("`, verified **");
								handler2.AppendFormatted(verified ? "yes" : "no");
								handler2.AppendLiteral("**");
								stringBuilder15.AppendLine(ref handler2);
								if (!verified)
								{
									Environment.ExitCode = 6;
								}
							}
							Console.WriteLine("Die vorherigen drei RGB-Zonen wurden wiederhergestellt.");
						}
						catch (Exception ex2)
						{
							stringBuilder6 = report2;
							StringBuilder stringBuilder16 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
							handler2.AppendLiteral("- RESTORE ERROR: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder16.AppendLine(ref handler2);
							Console.Error.WriteLine("Wiederherstellungsfehler: " + ex2.Message);
							Environment.ExitCode = 7;
						}
					}
					else
					{
						report2.AppendLine("- Restore unavailable because not all three original zones were captured.");
						Environment.ExitCode = 7;
					}
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] QueryZone2(byte b2)
			{
				byte[] query = new byte[9];
				query[1] = 136;
				query[2] = b2;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(10);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-host-effect-interactive-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine("Report written to: " + outputPath2);
			}
			void WriteZone2(byte b2, byte red, byte green, byte blue, byte brightness = 50)
			{
				byte[] request = new byte[9];
				request[1] = 8;
				request[2] = b2;
				request[3] = red;
				request[4] = green;
				request[5] = blue;
				request[6] = brightness;
				request[8] = CalculateGigabyteChecksum(request);
				stream.SetFeature(request);
			}
		}


		static void RunInteractiveKeyboardEffectTest()
		{
			(byte, string, byte, string)[] effects = new(byte, string, byte, string)[10]
			{
				(1, "Static", 2, "Sollte dauerhaft grün leuchten."),
				(2, "Breathing", 2, "Sollte grün pulsieren/atmen."),
				(3, "Wave", 8, "Achte auf eine wandernde Welle zwischen den drei Zonen."),
				(4, "Fade-on-keypress", 2, "Drücke mehrere Tasten und achte auf ein Nachleuchten."),
				(5, "Marquee", 8, "Achte auf ein laufendes Farbmuster."),
				(6, "Ripple", 2, "Drücke mehrere Tasten und achte auf eine auslaufende Reaktion."),
				(8, "Neon", 8, "Achte auf automatisch wechselnde Farben."),
				(10, "Raindrop", 8, "Achte auf einzelne zufällige Lichtimpulse."),
				(12, "Hedge", 8, "Achte auf ein gerichtetes, wanderndes Muster."),
				(13, "Rotate", 8, "Achte auf eine rotierende/umlaufende Bewegung.")
			};
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE – interaktiver RGB-Effekttest");
			Console.WriteLine("Enter = nächster Effekt | Text = Beobachtung speichern und weiter | /stop = beenden");
			Console.WriteLine();
			StringBuilder interactiveReport = new StringBuilder();
			interactiveReport.AppendLine("# AORUS interactive visible RGB-effect test");
			interactiveReport.AppendLine();
			StringBuilder stringBuilder6 = interactiveReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			interactiveReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			interactiveReport.AppendLine("- Tested list: Gigabyte's ten compact ITE UI effects");
			interactiveReport.AppendLine("- Shared parameters: raw speed `5`, brightness `50`, direction `1`");
			interactiveReport.AppendLine("- Advancement: user-controlled; no fixed timeout");
			interactiveReport.AppendLine("- Restore policy: capture all zones; restore and verify in `finally`");
			interactiveReport.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				Console.Error.WriteLine("Die freigegebene RGB-Schnittstelle wurde nicht gefunden.");
				interactiveReport.AppendLine("- Exact approved RGB interface was not found; no packet was sent.");
				WriteInteractiveKeyboardEffectReport(interactiveReport);
				Environment.ExitCode = 2;
				return;
			}
			Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
			HidStream stream = device2.Open();
			try
			{
				try
				{
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						originalZones.Add(zone2, Query3(136, zone2, 65));
					}
					interactiveReport.AppendLine("## Effect observations");
					interactiveReport.AppendLine();
					for (int index = 0; index < effects.Length; index++)
					{
						(byte, string, byte, string) tuple = effects[index];
						byte id = tuple.Item1;
						string name = tuple.Item2;
						byte palette = tuple.Item3;
						string item = tuple.Item4;
						byte[] request = new byte[9];
						request[1] = 8;
						request[2] = 0;
						request[3] = id;
						request[4] = 5;
						request[5] = 50;
						request[6] = palette;
						request[7] = 1;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(500);
						byte[] readback = Query3(136, 0, 500);
						Console.WriteLine($"[{index + 1}/{effects.Length}] {name} (ID {id})");
						Console.WriteLine(item);
						Console.Write("Beobachtung oder Enter: ");
						string observation = Console.ReadLine();
						int num2;
						object obj;
						if (observation != null)
						{
							num2 = (observation.Trim().Equals("/stop", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
							if (num2 == 0)
							{
								obj = (string.IsNullOrWhiteSpace(observation) ? "No note entered." : observation.Trim());
								goto IL_03db;
							}
						}
						else
						{
							num2 = 1;
						}
						obj = "Test stopped by user before an observation was recorded.";
						goto IL_03db;
						IL_03db:
						string recordedObservation = (string)obj;
						stringBuilder6 = interactiveReport;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(16, 3, stringBuilder6);
						handler2.AppendLiteral("### ");
						handler2.AppendFormatted(index + 1);
						handler2.AppendLiteral(". `");
						handler2.AppendFormatted(name);
						handler2.AppendLiteral("` (ID `");
						handler2.AppendFormatted(id);
						handler2.AppendLiteral("`)");
						stringBuilder8.AppendLine(ref handler2);
						interactiveReport.AppendLine();
						stringBuilder6 = interactiveReport;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
						handler2.AppendLiteral("- Request: `");
						handler2.AppendFormatted(Convert.ToHexString(request));
						handler2.AppendLiteral("`");
						stringBuilder9.AppendLine(ref handler2);
						stringBuilder6 = interactiveReport;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
						handler2.AppendLiteral("- Global readback: `");
						handler2.AppendFormatted(Convert.ToHexString(readback));
						handler2.AppendLiteral("`");
						stringBuilder10.AppendLine(ref handler2);
						stringBuilder6 = interactiveReport;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder6);
						handler2.AppendLiteral("- User observation: ");
						handler2.AppendFormatted(Escape(recordedObservation));
						stringBuilder11.AppendLine(ref handler2);
						interactiveReport.AppendLine();
						if (num2 != 0)
						{
							Console.WriteLine("Test wird beendet und die vorherigen Zonen werden wiederhergestellt.");
							break;
						}
						Console.WriteLine();
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Testfehler: " + ex.Message);
					stringBuilder6 = interactiveReport;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder12.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					interactiveReport.AppendLine("## Restoration");
					interactiveReport.AppendLine();
					if (originalZones.Count == 3)
					{
						try
						{
							foreach (var (zone3, original) in originalZones.OrderBy((KeyValuePair<byte, byte[]> keyValuePair2) => keyValuePair2.Key))
							{
								SetZone(zone3, original[3], original[4], original[5], original[6]);
								byte[] restored = Query3(136, zone3, 65);
								bool verified = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
								stringBuilder6 = interactiveReport;
								StringBuilder stringBuilder13 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(42, 6, stringBuilder6);
								handler2.AppendLiteral("- Zone ");
								handler2.AppendFormatted(zone3);
								handler2.AppendLiteral(": `#");
								handler2.AppendFormatted(restored[3], "X2");
								handler2.AppendFormatted(restored[4], "X2");
								handler2.AppendFormatted(restored[5], "X2");
								handler2.AppendLiteral("`, ");
								handler2.AppendLiteral("brightness `");
								handler2.AppendFormatted(restored[6]);
								handler2.AppendLiteral("`, verified **");
								handler2.AppendFormatted(verified ? "yes" : "no");
								handler2.AppendLiteral("**");
								stringBuilder13.AppendLine(ref handler2);
								if (!verified)
								{
									Environment.ExitCode = 6;
								}
							}
							Console.WriteLine("Die vorherigen drei RGB-Zonen wurden wiederhergestellt.");
						}
						catch (Exception ex2)
						{
							stringBuilder6 = interactiveReport;
							StringBuilder stringBuilder14 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
							handler2.AppendLiteral("- RESTORE ERROR: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder14.AppendLine(ref handler2);
							Console.Error.WriteLine("Wiederherstellungsfehler: " + ex2.Message);
							Environment.ExitCode = 7;
						}
					}
					else
					{
						interactiveReport.AppendLine("- Restore unavailable because not all three original zones were captured.");
						Environment.ExitCode = 7;
					}
				}
				WriteInteractiveKeyboardEffectReport(interactiveReport);
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] Query3(byte command, byte selector, int delayMilliseconds)
			{
				byte[] query = new byte[9];
				query[1] = command;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(delayMilliseconds);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void SetZone(byte b2, byte red, byte green, byte blue, byte brightness)
			{
				byte[] request2 = new byte[9];
				request2[1] = 8;
				request2[2] = b2;
				request2[3] = red;
				request2[4] = green;
				request2[5] = blue;
				request2[6] = brightness;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
				Thread.Sleep(65);
			}
		}


		static void RunKeyboardBreathingTest()
		{
			StringBuilder effectReport = new StringBuilder();
			effectReport.AppendLine("# AORUS guarded Breathing-effect test");
			effectReport.AppendLine();
			StringBuilder stringBuilder6 = effectReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			effectReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			effectReport.AppendLine("- Temporary request: Breathing `2`, raw speed `5` (official UI 50/100), brightness `50`, Green `2`, direction `1`");
			effectReport.AppendLine("- Visible hold: 10 seconds");
			effectReport.AppendLine("- Restore policy: capture all three zones first; restore and verify them in `finally`");
			effectReport.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			effectReport.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				effectReport.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteKeyboardEffectTestReport(effectReport);
				return;
			}
			Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
			HidStream stream = device2.Open();
			try
			{
				try
				{
					effectReport.AppendLine("## Captured zone state");
					effectReport.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] original = Query3(136, zone2, 65);
						originalZones.Add(zone2, original);
						stringBuilder6 = effectReport;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(original[3], "X2");
						handler2.AppendFormatted(original[4], "X2");
						handler2.AppendFormatted(original[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(original[6]);
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
					}
					byte[] request = new byte[9];
					request[1] = 8;
					request[2] = 0;
					request[3] = 2;
					request[4] = 5;
					request[5] = 50;
					request[6] = 2;
					request[7] = 1;
					request[8] = CalculateGigabyteChecksum(request);
					stream.SetFeature(request);
					Thread.Sleep(500);
					byte[] globalReadback = Query3(136, 0, 500);
					effectReport.AppendLine();
					effectReport.AppendLine("## Effect request and readback");
					effectReport.AppendLine();
					stringBuilder6 = effectReport;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
					handler2.AppendLiteral("- Request: `");
					handler2.AppendFormatted(Convert.ToHexString(request));
					handler2.AppendLiteral("`");
					stringBuilder9.AppendLine(ref handler2);
					stringBuilder6 = effectReport;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
					handler2.AppendLiteral("- Global readback: `");
					handler2.AppendFormatted(Convert.ToHexString(globalReadback));
					handler2.AppendLiteral("`");
					stringBuilder10.AppendLine(ref handler2);
					stringBuilder6 = effectReport;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(78, 5, stringBuilder6);
					handler2.AppendLiteral("- Decoded readback: effect `");
					handler2.AppendFormatted(globalReadback[3]);
					handler2.AppendLiteral("`, speed `");
					handler2.AppendFormatted(globalReadback[4]);
					handler2.AppendLiteral("`, ");
					handler2.AppendLiteral("brightness `");
					handler2.AppendFormatted(globalReadback[5]);
					handler2.AppendLiteral("`, color `");
					handler2.AppendFormatted(globalReadback[6]);
					handler2.AppendLiteral("`, direction `");
					handler2.AppendFormatted(globalReadback[7]);
					handler2.AppendLiteral("`");
					stringBuilder11.AppendLine(ref handler2);
					effectReport.AppendLine("- The effect was then left visible for ten seconds for direct observation.");
					Thread.Sleep(10000);
				}
				catch (Exception ex)
				{
					effectReport.AppendLine();
					stringBuilder6 = effectReport;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder12.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					if (originalZones.Count == 3)
					{
						effectReport.AppendLine();
						effectReport.AppendLine("## Restoration");
						effectReport.AppendLine();
						try
						{
							foreach (var (zone3, original2) in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
							{
								SetZone(zone3, original2[3], original2[4], original2[5], original2[6]);
								byte[] restored = Query3(136, zone3, 65);
								bool verified = restored[3] == original2[3] && restored[4] == original2[4] && restored[5] == original2[5] && restored[6] == original2[6];
								stringBuilder6 = effectReport;
								StringBuilder stringBuilder13 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(42, 6, stringBuilder6);
								handler2.AppendLiteral("- Zone ");
								handler2.AppendFormatted(zone3);
								handler2.AppendLiteral(": `#");
								handler2.AppendFormatted(restored[3], "X2");
								handler2.AppendFormatted(restored[4], "X2");
								handler2.AppendFormatted(restored[5], "X2");
								handler2.AppendLiteral("`, ");
								handler2.AppendLiteral("brightness `");
								handler2.AppendFormatted(restored[6]);
								handler2.AppendLiteral("`, verified **");
								handler2.AppendFormatted(verified ? "yes" : "no");
								handler2.AppendLiteral("**");
								stringBuilder13.AppendLine(ref handler2);
								if (!verified)
								{
									Environment.ExitCode = 6;
								}
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = effectReport;
							StringBuilder stringBuilder14 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
							handler2.AppendLiteral("- RESTORE ERROR: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder14.AppendLine(ref handler2);
							Environment.ExitCode = 7;
						}
					}
				}
				WriteKeyboardEffectTestReport(effectReport);
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] Query3(byte command, byte selector, int delayMilliseconds)
			{
				byte[] query = new byte[9];
				query[1] = command;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(delayMilliseconds);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void SetZone(byte b2, byte red, byte green, byte blue, byte brightness)
			{
				byte[] request2 = new byte[9];
				request2[1] = 8;
				request2[2] = b2;
				request2[3] = red;
				request2[4] = green;
				request2[5] = blue;
				request2[6] = brightness;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
				Thread.Sleep(65);
			}
		}


		static void RunKeyboardBrightnessCycle()
		{
			byte[] brightnessSteps = new byte[5] { 0, 1, 25, 49, 50 };
			StringBuilder cycleReport = new StringBuilder();
			cycleReport.AppendLine("# AORUS keyboard software-brightness cycle");
			cycleReport.AppendLine();
			StringBuilder stringBuilder6 = cycleReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			cycleReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			cycleReport.AppendLine("- Boundary steps: raw `0`, `1`, `25`, `49`, `50` (approximately 0%, 2%, 50%, 98%, 100%)");
			cycleReport.AppendLine("- Hold time: five seconds per step");
			cycleReport.AppendLine("- Final requested state: raw `50` / on");
			cycleReport.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				cycleReport.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteBrightnessCycleReport(cycleReport);
			}
			else
			{
				try
				{
					HidStream stream = device2.Open();
					try
					{
						byte[][] zoneStates = new byte[3][];
						for (byte zone2 = 1; zone2 <= 3; zone2++)
						{
							zoneStates[zone2 - 1] = QueryZone2(zone2);
						}
						stringBuilder6 = cycleReport;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(54, 9, stringBuilder6);
						handler2.AppendLiteral("- Colors preserved: zone 1 `#");
						handler2.AppendFormatted(zoneStates[0][3], "X2");
						handler2.AppendFormatted(zoneStates[0][4], "X2");
						handler2.AppendFormatted(zoneStates[0][5], "X2");
						handler2.AppendLiteral("`, ");
						handler2.AppendLiteral("zone 2 `#");
						handler2.AppendFormatted(zoneStates[1][3], "X2");
						handler2.AppendFormatted(zoneStates[1][4], "X2");
						handler2.AppendFormatted(zoneStates[1][5], "X2");
						handler2.AppendLiteral("`, ");
						handler2.AppendLiteral("zone 3 `#");
						handler2.AppendFormatted(zoneStates[2][3], "X2");
						handler2.AppendFormatted(zoneStates[2][4], "X2");
						handler2.AppendFormatted(zoneStates[2][5], "X2");
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
						cycleReport.AppendLine();
						byte[] array2 = brightnessSteps;
						foreach (byte brightness in array2)
						{
							for (byte zone3 = 1; zone3 <= 3; zone3++)
							{
								byte[] saved = zoneStates[zone3 - 1];
								SetZone(zone3, saved[3], saved[4], saved[5], brightness);
							}
							bool verified = true;
							List<string> readbacks = new List<string>();
							for (byte zone4 = 1; zone4 <= 3; zone4++)
							{
								byte[] readback = QueryZone2(zone4);
								verified &= readback[6] == brightness;
								readbacks.Add($"Z{zone4}={readback[6]}");
							}
							stringBuilder6 = cycleReport;
							StringBuilder stringBuilder9 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 3, stringBuilder6);
							handler2.AppendLiteral("- Raw candidate ");
							handler2.AppendFormatted(brightness);
							handler2.AppendLiteral(": ");
							handler2.AppendFormatted(string.Join(", ", readbacks));
							handler2.AppendLiteral("; stored value verified **");
							handler2.AppendFormatted(verified ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder9.AppendLine(ref handler2);
							Thread.Sleep(5000);
						}
						byte[] array3 = QueryZone2(1);
						byte[] finalZone2 = QueryZone2(2);
						byte[] finalZone3 = QueryZone2(3);
						bool finalVerified = array3[6] == 50 && finalZone2[6] == 50 && finalZone3[6] == 50;
						stringBuilder6 = cycleReport;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(47, 1, stringBuilder6);
						handler2.AppendLiteral("- Final on-state (`50`) readback verified: **");
						handler2.AppendFormatted(finalVerified ? "yes" : "no");
						handler2.AppendLiteral("**");
						stringBuilder10.AppendLine(ref handler2);
					}
					finally
					{
						if (stream != null)
						{
							((IDisposable)stream).Dispose();
						}
					}
					byte[] QueryZone2(byte b)
					{
						byte[] request = new byte[9];
						request[1] = 136;
						request[2] = b;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(65);
						byte[] response = new byte[9];
						stream.GetFeature(response);
						return response;
					}
					void SetZone(byte b, byte red, byte green, byte blue, byte b2)
					{
						byte[] request = new byte[9];
						request[1] = 8;
						request[2] = b;
						request[3] = red;
						request[4] = green;
						request[5] = blue;
						request[6] = b2;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(65);
					}
				}
				catch (Exception ex)
				{
					stringBuilder6 = cycleReport;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder6);
					handler2.AppendLiteral("- Cycle failed: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder11.AppendLine(ref handler2);
				}
				WriteBrightnessCycleReport(cycleReport);
			}
		}


		void RunKeyboardBrightnessMonitor()
		{
			int durationSeconds = Math.Clamp(ReadPositiveIntArgument("--seconds", 25), 5, 120);
			StringBuilder monitorReport = new StringBuilder();
			monitorReport.AppendLine("# AORUS Fn+Space brightness-channel monitor");
			monitorReport.AppendLine();
			StringBuilder stringBuilder6 = monitorReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			stringBuilder6 = monitorReport;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder6);
			handler2.AppendLiteral("- Duration: ");
			handler2.AppendFormatted(durationSeconds);
			handler2.AppendLiteral(" seconds");
			stringBuilder8.AppendLine(ref handler2);
			monitorReport.AppendLine("- State-changing command sent: **no**");
			monitorReport.AppendLine("- Standard keyboard interfaces captured: **no**");
			monitorReport.AppendLine("- Vendor interfaces observed: `MI_01` and `MI_03` only");
			monitorReport.AppendLine();
			ConcurrentQueue<string> events = new ConcurrentQueue<string>();
			CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
			try
			{
				Task[] readers = (from hidDevice in DeviceList.Local.GetHidDevices(4164, 31297).Where(delegate(HidDevice hidDevice)
					{
						string text3 = GetInterfaceLabel(hidDevice.DevicePath);
						return (text3.Equals("MI_01", StringComparison.OrdinalIgnoreCase) || text3.Equals("MI_03", StringComparison.OrdinalIgnoreCase)) && hidDevice.GetMaxInputReportLength() == 65;
					}).ToArray()
					select Task.Run(delegate
					{
						string value2 = GetInterfaceLabel(hidDevice.DevicePath);
						try
						{
							using HidStream hidStream = hidDevice.Open();
							hidStream.ReadTimeout = 150;
							byte[] array2 = new byte[hidDevice.GetMaxInputReportLength()];
							string value3 = null;
							while (!cancellation.IsCancellationRequested)
							{
								try
								{
									int num2 = hidStream.Read(array2, 0, array2.Length);
									if (num2 > 0)
									{
										string text3 = Convert.ToHexString(array2.AsSpan(0, num2));
										if (!text3.Equals(value3, StringComparison.Ordinal))
										{
											events.Enqueue($"{DateTimeOffset.Now:HH:mm:ss.fff} HID {value2} `{text3}`");
											value3 = text3;
										}
									}
								}
								catch (TimeoutException)
								{
								}
							}
						}
						catch (Exception ex5)
						{
							events.Enqueue($"{DateTimeOffset.Now:HH:mm:ss.fff} HID {value2} unavailable: {ex5.Message}");
						}
					}, cancellation.Token)).ToArray();
				ManagementObject wmiInstance = null;
				try
				{
					ManagementClass getClass = new ManagementClass("root\\WMI", "GB_WMIACPI_Get", null);
					try
					{
						getClass.Get();
						using ManagementObjectCollection instances = getClass.GetInstances();
						wmiInstance = instances.Cast<ManagementObject>().FirstOrDefault();
					}
					finally
					{
						((IDisposable)getClass)?.Dispose();
					}
				}
				catch (Exception ex)
				{
					events.Enqueue($"{DateTimeOffset.Now:HH:mm:ss.fff} WMI unavailable: {ex.Message}");
				}
				string previousWmi = null;
				while (!cancellation.IsCancellationRequested)
				{
					if (wmiInstance != null)
					{
						try
						{
							InvokeMethodOptions options = new InvokeMethodOptions
							{
								Timeout = TimeSpan.FromSeconds(2L)
							};
							ManagementBaseObject output = wmiInstance.InvokeMethod("GetKeyBoardBackLight", null, options);
							try
							{
								string value = Convert.ToString(output["Data"], CultureInfo.InvariantCulture) ?? string.Empty;
								if (!value.Equals(previousWmi, StringComparison.Ordinal))
								{
									events.Enqueue($"{DateTimeOffset.Now:HH:mm:ss.fff} EC KBLL `{value}`");
									previousWmi = value;
								}
							}
							finally
							{
								((IDisposable)output)?.Dispose();
							}
						}
						catch (Exception ex2)
						{
							events.Enqueue($"{DateTimeOffset.Now:HH:mm:ss.fff} WMI read failed: {ex2.Message}");
							wmiInstance.Dispose();
							wmiInstance = null;
						}
					}
					Thread.Sleep(250);
				}
				try
				{
					Task.WaitAll(readers, TimeSpan.FromSeconds(2L));
				}
				catch (AggregateException)
				{
				}
				finally
				{
					wmiInstance?.Dispose();
				}
				monitorReport.AppendLine("## Observed changes");
				monitorReport.AppendLine();
				if (events.IsEmpty)
				{
					monitorReport.AppendLine("- No EC or vendor-HID changes observed.");
				}
				else
				{
					string observedEvent;
					while (events.TryDequeue(out observedEvent))
					{
						stringBuilder6 = monitorReport;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder6);
						handler2.AppendLiteral("- ");
						handler2.AppendFormatted(Escape(observedEvent));
						stringBuilder9.AppendLine(ref handler2);
					}
				}
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-brightness-monitor-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, monitorReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine(monitorReport);
				Console.WriteLine("Report written to: " + outputPath2);
			}
			finally
			{
				if (cancellation != null)
				{
					((IDisposable)cancellation).Dispose();
				}
			}
		}


		static void RunKeyboardEffectBatch()
		{
			(byte, string, byte, string)[] effects = new(byte, string, byte, string)[3]
			{
				(2, "Breathing", 2, "The whole keyboard should pulse green."),
				(3, "Wave", 8, "A moving/rainbow wave should cross the three zones."),
				(4, "Fade-on-keypress", 2, "Press several keys; their zone should react/fade if supported.")
			};
			StringBuilder batchReport = new StringBuilder();
			batchReport.AppendLine("# AORUS visible RGB-effect test — batch 1");
			batchReport.AppendLine();
			StringBuilder stringBuilder6 = batchReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			batchReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			batchReport.AppendLine("- Sequence: Breathing → Wave → Fade-on-keypress");
			batchReport.AppendLine("- Hold per effect: 8 seconds");
			batchReport.AppendLine("- Shared parameters: raw speed `5`, brightness `50`, direction `1`");
			batchReport.AppendLine("- Restore policy: capture all zones; restore and verify in `finally`");
			batchReport.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				batchReport.AppendLine("- Exact approved RGB interface was not found; no packet was sent.");
				WriteKeyboardEffectBatchReport(batchReport);
				return;
			}
			Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
			HidStream stream = device2.Open();
			try
			{
				try
				{
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						originalZones.Add(zone2, Query3(136, zone2, 65));
					}
					batchReport.AppendLine("## Requests");
					batchReport.AppendLine();
					(byte, string, byte, string)[] array2 = effects;
					for (int num2 = 0; num2 < array2.Length; num2++)
					{
						(byte, string, byte, string) tuple = array2[num2];
						byte id = tuple.Item1;
						string name = tuple.Item2;
						byte palette = tuple.Item3;
						string observation = tuple.Item4;
						byte[] request = new byte[9];
						request[1] = 8;
						request[2] = 0;
						request[3] = id;
						request[4] = 5;
						request[5] = 50;
						request[6] = palette;
						request[7] = 1;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(500);
						byte[] readback = Query3(136, 0, 500);
						stringBuilder6 = batchReport;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(66, 5, stringBuilder6);
						handler2.AppendLiteral("- `");
						handler2.AppendFormatted(name);
						handler2.AppendLiteral("` ID `");
						handler2.AppendFormatted(id);
						handler2.AppendLiteral("`: request `");
						handler2.AppendFormatted(Convert.ToHexString(request));
						handler2.AppendLiteral("`, ");
						handler2.AppendLiteral("global readback `");
						handler2.AppendFormatted(Convert.ToHexString(readback));
						handler2.AppendLiteral("`. Expected observation: ");
						handler2.AppendFormatted(observation);
						stringBuilder8.AppendLine(ref handler2);
						Console.WriteLine("Now visible for 8 seconds: " + name);
						Thread.Sleep(8000);
					}
				}
				catch (Exception ex)
				{
					stringBuilder6 = batchReport;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder9.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					batchReport.AppendLine();
					batchReport.AppendLine("## Restoration");
					batchReport.AppendLine();
					if (originalZones.Count == 3)
					{
						try
						{
							foreach (var (zone3, original) in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
							{
								SetZone(zone3, original[3], original[4], original[5], original[6]);
								byte[] restored = Query3(136, zone3, 65);
								bool verified = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
								stringBuilder6 = batchReport;
								StringBuilder stringBuilder10 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(42, 6, stringBuilder6);
								handler2.AppendLiteral("- Zone ");
								handler2.AppendFormatted(zone3);
								handler2.AppendLiteral(": `#");
								handler2.AppendFormatted(restored[3], "X2");
								handler2.AppendFormatted(restored[4], "X2");
								handler2.AppendFormatted(restored[5], "X2");
								handler2.AppendLiteral("`, ");
								handler2.AppendLiteral("brightness `");
								handler2.AppendFormatted(restored[6]);
								handler2.AppendLiteral("`, verified **");
								handler2.AppendFormatted(verified ? "yes" : "no");
								handler2.AppendLiteral("**");
								stringBuilder10.AppendLine(ref handler2);
								if (!verified)
								{
									Environment.ExitCode = 6;
								}
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = batchReport;
							StringBuilder stringBuilder11 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
							handler2.AppendLiteral("- RESTORE ERROR: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder11.AppendLine(ref handler2);
							Environment.ExitCode = 7;
						}
					}
					else
					{
						batchReport.AppendLine("- Restore unavailable because not all three original zones were captured.");
						Environment.ExitCode = 7;
					}
				}
				batchReport.AppendLine();
				batchReport.AppendLine("## Observation status");
				batchReport.AppendLine();
				batchReport.AppendLine("- Visible behavior requires the user's physical observation; HID global readback is known to return zeros on this firmware.");
				WriteKeyboardEffectBatchReport(batchReport);
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] Query3(byte command, byte selector, int delayMilliseconds)
			{
				byte[] query = new byte[9];
				query[1] = command;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(delayMilliseconds);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void SetZone(byte b2, byte red, byte green, byte blue, byte brightness)
			{
				byte[] request2 = new byte[9];
				request2[1] = 8;
				request2[2] = b2;
				request2[3] = red;
				request2[4] = green;
				request2[5] = blue;
				request2[6] = brightness;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
				Thread.Sleep(65);
			}
		}


		void RunKeyboardHostEffectTest()
		{
			int effectSeconds = Math.Clamp(ReadPositiveIntArgument("--seconds", 8), 3, 30);
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS host-rendered keyboard effect test");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88` only");
			report2.AppendLine("- Global effect command `0x08` selector 0 used: **no**");
			report2.AppendLine("- Picture-matrix commands `0x12` / `0x92` used: **no**");
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) touched: **no**");
			report2.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 1, stringBuilder6);
			handler2.AppendLiteral("- Seconds per effect: `");
			handler2.AppendFormatted(effectSeconds);
			handler2.AppendLiteral("`");
			stringBuilder8.AppendLine(ref handler2);
			report2.AppendLine("- Zone brightness byte held at `50` throughout; animation is rendered in the RGB values, because raw brightness is a proven off/on gate on this firmware.");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice hidDevice) => GetInterfaceLabel(hidDevice.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && hidDevice.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			int writeInterval;
			try
			{
				Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
				writeInterval = 65;
				try
				{
					report2.AppendLine("## Captured original zone state");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] state = QueryZone2(zone2);
						originalZones.Add(zone2, state);
						stringBuilder6 = report2;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(state[3], "X2");
						handler2.AppendFormatted(state[4], "X2");
						handler2.AppendFormatted(state[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(state[6]);
						handler2.AppendLiteral("`");
						stringBuilder9.AppendLine(ref handler2);
					}
					report2.AppendLine();
					report2.AppendLine("## Minimum reliable write interval");
					report2.AppendLine();
					report2.AppendLine("| Interval | Verified writes | Result |");
					report2.AppendLine("|---|---|---|");
					int[] array2 = new int[6] { 65, 40, 25, 15, 10, 5 };
					foreach (int candidate in array2)
					{
						int verified = 0;
						for (int attempt = 0; attempt < 6; attempt++)
						{
							byte value = (byte)(40 + attempt * 30);
							WriteZone2(1, value, 0, value, 50);
							Thread.Sleep(candidate);
							byte[] readback = QueryZone2(1);
							if (readback[3] == value && readback[5] == value)
							{
								verified++;
							}
						}
						bool reliable = verified == 6;
						stringBuilder6 = report2;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 3, stringBuilder6);
						handler2.AppendLiteral("| `");
						handler2.AppendFormatted(candidate);
						handler2.AppendLiteral(" ms` | `");
						handler2.AppendFormatted(verified);
						handler2.AppendLiteral(" / 6` | ");
						handler2.AppendFormatted(reliable ? "reliable" : "unreliable");
						handler2.AppendLiteral(" |");
						stringBuilder10.AppendLine(ref handler2);
						if (!reliable)
						{
							break;
						}
						writeInterval = candidate;
					}
					int zoneFramesPerSecond = 1000 / (writeInterval * 3);
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(67, 1, stringBuilder6);
					handler2.AppendLiteral("- Fastest interval verified on every attempt: `");
					handler2.AppendFormatted(writeInterval);
					handler2.AppendLiteral(" ms` per zone write.");
					stringBuilder11.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(58, 1, stringBuilder6);
					handler2.AppendLiteral("- Resulting full three-zone frame rate: about `");
					handler2.AppendFormatted(zoneFramesPerSecond);
					handler2.AppendLiteral("` frames/s.");
					stringBuilder12.AppendLine(ref handler2);
					Console.WriteLine();
					Console.WriteLine($"Schnellstes verifiziertes Intervall: {writeInterval} ms pro Zone (~{zoneFramesPerSecond} Bilder/s).");
					Console.WriteLine("Bitte jetzt auf die Tastatur schauen.");
					Console.WriteLine();
					report2.AppendLine();
					report2.AppendLine("## Rendered effects");
					report2.AppendLine();
					RenderEffect("Breathing", "all three zones share one hue whose RGB values follow a sine ramp", delegate(double elapsed)
					{
						byte b2 = (byte)Math.Round((1.0 - Math.Cos(elapsed * 2.0 * Math.PI / 3.0)) / 2.0 * 255.0);
						return new(byte, byte, byte)[3]
						{
							(b2, 0, b2),
							(b2, 0, b2),
							(b2, 0, b2)
						};
					});
					RenderEffect("Colour cycle", "one hue rotating through the full spectrum on all three zones", delegate(double elapsed)
					{
						(byte, byte, byte) tuple = HueToRgb(elapsed / 6.0 % 1.0);
						return new(byte, byte, byte)[3] { tuple, tuple, tuple };
					});
					RenderEffect("Wave", "a bright zone travelling left to right over a dim base", delegate(double elapsed)
					{
						int num3 = (int)(elapsed / 0.35) % 3;
						(byte, byte, byte)[] array4 = new(byte, byte, byte)[3];
						for (int i = 0; i < 3; i++)
						{
							array4[i] = ((byte, byte, byte))((i == num3) ? (0, 255, 120) : (0, 25, 12));
						}
						return array4;
					});
				}
				catch (Exception ex)
				{
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder13 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder13.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					report2.AppendLine();
					report2.AppendLine("## Restore of the original zone state");
					report2.AppendLine();
					foreach (var (zone3, state2) in originalZones)
					{
						try
						{
							WriteZone2(zone3, state2[3], state2[4], state2[5], state2[6]);
							Thread.Sleep(65);
							byte[] restored = QueryZone2(zone3);
							bool exact = restored[3] == state2[3] && restored[4] == state2[4] && restored[5] == state2[5] && restored[6] == state2[6];
							stringBuilder6 = report2;
							StringBuilder stringBuilder14 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone3);
							handler2.AppendLiteral(": `#");
							handler2.AppendFormatted(restored[3], "X2");
							handler2.AppendFormatted(restored[4], "X2");
							handler2.AppendFormatted(restored[5], "X2");
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(restored[6]);
							handler2.AppendLiteral("`, exact match: **");
							handler2.AppendFormatted(exact ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder14.AppendLine(ref handler2);
							if (!exact)
							{
								Environment.ExitCode = 6;
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = report2;
							StringBuilder stringBuilder15 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(26, 2, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone3);
							handler2.AppendLiteral(": restore failed — ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder15.AppendLine(ref handler2);
							Environment.ExitCode = 6;
						}
					}
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] QueryZone2(byte b2)
			{
				byte[] query = new byte[9];
				query[1] = 136;
				query[2] = b2;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(10);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void RenderEffect(string name, string description, Func<double, (byte r, byte g, byte b)[]> frameAt)
			{
				Console.WriteLine("Effekt laeuft: " + name);
				Stopwatch clock = Stopwatch.StartNew();
				int frames = 0;
				while (clock.Elapsed.TotalSeconds < (double)effectSeconds)
				{
					(byte, byte, byte)[] frame = frameAt(clock.Elapsed.TotalSeconds);
					for (byte zone4 = 1; zone4 <= 3; zone4++)
					{
						var (r, g, b2) = frame[zone4 - 1];
						WriteZone2(zone4, r, g, b2, 50);
						Thread.Sleep(writeInterval);
					}
					frames++;
				}
				clock.Stop();
				double achieved = (double)frames / clock.Elapsed.TotalSeconds;
				StringBuilder stringBuilder16 = report2;
				StringBuilder.AppendInterpolatedStringHandler handler3 = new StringBuilder.AppendInterpolatedStringHandler(60, 5, stringBuilder16);
				handler3.AppendLiteral("- **");
				handler3.AppendFormatted(name);
				handler3.AppendLiteral("**: ");
				handler3.AppendFormatted(description);
				handler3.AppendLiteral(". Frames rendered: `");
				handler3.AppendFormatted(frames);
				handler3.AppendLiteral("` in `");
				handler3.AppendFormatted(clock.Elapsed.TotalSeconds, "F1");
				handler3.AppendLiteral("` s, achieved `");
				handler3.AppendFormatted(achieved, "F1");
				handler3.AppendLiteral("` frames/s.");
				stringBuilder16.AppendLine(ref handler3);
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-host-effects-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine(report2);
				Console.WriteLine("Report written to: " + outputPath2);
			}
			void WriteZone2(byte b2, byte red, byte green, byte blue, byte brightness = 50)
			{
				byte[] request = new byte[9];
				request[1] = 8;
				request[2] = b2;
				request[3] = red;
				request[4] = green;
				request[5] = blue;
				request[6] = brightness;
				request[8] = CalculateGigabyteChecksum(request);
				stream.SetFeature(request);
			}
		}


		static void RunKeyboardOldDefaultPulse()
		{
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS exact old-default Pulse request");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Source: signed `GBT_Keyboard 23.03.10.01` defaults and `IteKeyBoard.SetLightEffect`");
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Request: Pulse/Breathing `2`, speed `5`, brightness `50`, Orange palette `5`, direction `1`");
			report2.AppendLine("- Expected bytes: `0008000205320501B8`");
			report2.AppendLine("- Persistence: deliberately left active for visual observation; static zone values were not overwritten");
			report2.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			try
			{
				try
				{
					report2.AppendLine("## Static zone state before request");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] zoneState = Query3(136, zone2, 65);
						stringBuilder6 = report2;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(zoneState[3], "X2");
						handler2.AppendFormatted(zoneState[4], "X2");
						handler2.AppendFormatted(zoneState[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(zoneState[6]);
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
					}
					byte[] request = new byte[9] { 0, 8, 0, 2, 5, 50, 5, 1, 0 };
					request[8] = CalculateGigabyteChecksum(request);
					if (Convert.ToHexString(request) != "0008000205320501B8")
					{
						throw new InvalidOperationException("Generated packet did not match the decompiled old Gigabyte packet.");
					}
					stream.SetFeature(request);
					Thread.Sleep(750);
					byte[] readback = Query3(136, 0, 500);
					report2.AppendLine();
					report2.AppendLine("## Request and immediate global readback");
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder6);
					handler2.AppendLiteral("- Request sent: `");
					handler2.AppendFormatted(Convert.ToHexString(request));
					handler2.AppendLiteral("`");
					stringBuilder9.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Readback: `");
					handler2.AppendFormatted(Convert.ToHexString(readback));
					handler2.AppendLiteral("`");
					stringBuilder10.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(69, 5, stringBuilder6);
					handler2.AppendLiteral("- Decoded: effect `");
					handler2.AppendFormatted(readback[3]);
					handler2.AppendLiteral("`, speed `");
					handler2.AppendFormatted(readback[4]);
					handler2.AppendLiteral("`, brightness `");
					handler2.AppendFormatted(readback[5]);
					handler2.AppendLiteral("`, color `");
					handler2.AppendFormatted(readback[6]);
					handler2.AppendLiteral("`, direction `");
					handler2.AppendFormatted(readback[7]);
					handler2.AppendLiteral("`");
					stringBuilder11.AppendLine(ref handler2);
				}
				catch (Exception ex)
				{
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder12.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] Query3(byte command, byte selector, int delayMilliseconds)
			{
				byte[] query = new byte[9];
				query[1] = command;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(delayMilliseconds);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-old-default-pulse-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine(report2);
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}


		static void RunKeyboardSlowColorCycle()
		{
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS slow full-color-cycle request");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Combined request: Neon/Cycle effect `8`, slowest raw speed `9`, brightness `50`, Random palette `8`, direction `1`");
			report2.AppendLine("- Persistence: effect request deliberately left active for visual observation; static zone values were not overwritten");
			report2.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			try
			{
				try
				{
					report2.AppendLine("## Static zone state before request");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] zoneState = Query3(136, zone2, 65);
						stringBuilder6 = report2;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(zoneState[3], "X2");
						handler2.AppendFormatted(zoneState[4], "X2");
						handler2.AppendFormatted(zoneState[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(zoneState[6]);
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
					}
					byte[] request = new byte[9];
					request[1] = 8;
					request[2] = 0;
					request[3] = 8;
					request[4] = 9;
					request[5] = 50;
					request[6] = 8;
					request[7] = 1;
					request[8] = CalculateGigabyteChecksum(request);
					stream.SetFeature(request);
					Thread.Sleep(750);
					byte[] readback = Query3(136, 0, 500);
					report2.AppendLine();
					report2.AppendLine("## Request and immediate global readback");
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
					handler2.AppendLiteral("- Request: `");
					handler2.AppendFormatted(Convert.ToHexString(request));
					handler2.AppendLiteral("`");
					stringBuilder9.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Readback: `");
					handler2.AppendFormatted(Convert.ToHexString(readback));
					handler2.AppendLiteral("`");
					stringBuilder10.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(69, 5, stringBuilder6);
					handler2.AppendLiteral("- Decoded: effect `");
					handler2.AppendFormatted(readback[3]);
					handler2.AppendLiteral("`, speed `");
					handler2.AppendFormatted(readback[4]);
					handler2.AppendLiteral("`, brightness `");
					handler2.AppendFormatted(readback[5]);
					handler2.AppendLiteral("`, color `");
					handler2.AppendFormatted(readback[6]);
					handler2.AppendLiteral("`, direction `");
					handler2.AppendFormatted(readback[7]);
					handler2.AppendLiteral("`");
					stringBuilder11.AppendLine(ref handler2);
				}
				catch (Exception ex)
				{
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder12.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] Query3(byte command, byte selector, int delayMilliseconds)
			{
				byte[] query = new byte[9];
				query[1] = command;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(delayMilliseconds);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-slow-color-cycle-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine(report2);
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}


		void RunPictureMatrixWriteTest()
		{
			bool num2 = args.Any((string argument) => argument.Equals("--confirm-picture-matrix-write", StringComparison.OrdinalIgnoreCase));
			int slot = Math.Clamp(ReadPositiveIntArgument("--slot", 1) - 1, 0, 4);
			byte customEffect = (byte)(51 + slot);
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS picture-matrix write test");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature / 65-byte Input and Output report`");
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(105, 2, stringBuilder6);
			handler2.AppendLiteral("- Official commands used: getter `0x");
			handler2.AppendFormatted((byte)146, "X2");
			handler2.AppendLiteral("`, setter `0x");
			handler2.AppendFormatted((byte)18, "X2");
			handler2.AppendLiteral("`, effect selector `0x08` selector 0, zone getter `0x88`");
			stringBuilder8.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(50, 3, stringBuilder6);
			handler2.AppendLiteral("- Target custom slot: `");
			handler2.AppendFormatted(slot);
			handler2.AppendLiteral("` (effect enum `");
			handler2.AppendFormatted(customEffect);
			handler2.AppendLiteral("`, Custom ");
			handler2.AppendFormatted(slot + 1);
			handler2.AppendLiteral(")");
			stringBuilder9.AppendLine(ref handler2);
			report2.AppendLine("- Written memory: LED profile storage only. Firmware code, key matrix, macros, BIOS, EC, and battery: **not written**");
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) touched: **no**");
			report2.AppendLine("- Rollback: the slot is read and saved first, then rewritten and verified in `finally`; the three zone colours are restored the same way");
			report2.AppendLine();
			HidStream stream;
			if (!num2)
			{
				Console.Error.WriteLine("Dieser Test schreibt erstmals mit Kommandobyte 0x12 in den LED-Profilspeicher.");
				Console.Error.WriteLine("Er verlangt zusaetzlich --confirm-picture-matrix-write.");
				report2.AppendLine("- Refused before any device access: `--confirm-picture-matrix-write` was not supplied.");
				WriteReport();
				Environment.ExitCode = 2;
			}
			else
			{
				HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice hidDevice) => GetInterfaceLabel(hidDevice.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && hidDevice.GetMaxFeatureReportLength() == 9 && hidDevice.GetMaxInputReportLength() == 65 && hidDevice.GetMaxOutputReportLength() == 65);
				if (device2 != null)
				{
					stream = device2.Open();
					try
					{
						stream.ReadTimeout = 2500;
						byte[] savedMatrix = null;
						Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
						try
						{
							report2.AppendLine("## Saved original state");
							report2.AppendLine();
							savedMatrix = ReadMatrix(slot);
							int savedNonZero = 512 - ((ReadOnlySpan<byte>)savedMatrix).Count((byte)0);
							stringBuilder6 = report2;
							StringBuilder stringBuilder10 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(42, 3, stringBuilder6);
							handler2.AppendLiteral("- Slot ");
							handler2.AppendFormatted(slot);
							handler2.AppendLiteral(" matrix read: `");
							handler2.AppendFormatted(512);
							handler2.AppendLiteral("` bytes, `");
							handler2.AppendFormatted(savedNonZero);
							handler2.AppendLiteral("` non-zero");
							stringBuilder10.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder11 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(19, 2, stringBuilder6);
							handler2.AppendLiteral("- Slot ");
							handler2.AppendFormatted(slot);
							handler2.AppendLiteral(" SHA-256: `");
							handler2.AppendFormatted(Convert.ToHexString(SHA256.HashData(savedMatrix)));
							handler2.AppendLiteral("`");
							stringBuilder11.AppendLine(ref handler2);
							for (byte zone2 = 1; zone2 <= 3; zone2++)
							{
								byte[] state = QueryFeature(136, zone2);
								originalZones.Add(zone2, state);
								stringBuilder6 = report2;
								StringBuilder stringBuilder12 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
								handler2.AppendLiteral("- Zone ");
								handler2.AppendFormatted(zone2);
								handler2.AppendLiteral(": `#");
								handler2.AppendFormatted(state[3], "X2");
								handler2.AppendFormatted(state[4], "X2");
								handler2.AppendFormatted(state[5], "X2");
								handler2.AppendLiteral("`, brightness `");
								handler2.AppendFormatted(state[6]);
								handler2.AppendLiteral("`");
								stringBuilder12.AppendLine(ref handler2);
							}
							byte[] candidate = new byte[512];
							for (int index = 0; index < 128; index++)
							{
								candidate[index * 4] = 0;
								candidate[index * 4 + 1] = byte.MaxValue;
								candidate[index * 4 + 2] = 0;
								candidate[index * 4 + 3] = 0;
							}
							report2.AppendLine();
							report2.AppendLine("## Written matrix");
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder13 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(65, 1, stringBuilder6);
							handler2.AppendLiteral("- Pattern: all `");
							handler2.AppendFormatted(128);
							handler2.AppendLiteral("` four-byte slots set to `00 FF 00 00` (pure red)");
							stringBuilder13.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder14 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder6);
							handler2.AppendLiteral("- Candidate SHA-256: `");
							handler2.AppendFormatted(Convert.ToHexString(SHA256.HashData(candidate)));
							handler2.AppendLiteral("`");
							stringBuilder14.AppendLine(ref handler2);
							WriteMatrix(slot, candidate);
							byte[] verification = ReadMatrix(slot);
							bool matrixVerified = ((ReadOnlySpan<byte>)verification.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)candidate);
							int verifiedNonZero = 512 - ((ReadOnlySpan<byte>)verification).Count((byte)0);
							stringBuilder6 = report2;
							StringBuilder stringBuilder15 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(22, 1, stringBuilder6);
							handler2.AppendLiteral("- Readback SHA-256: `");
							handler2.AppendFormatted(Convert.ToHexString(SHA256.HashData(verification)));
							handler2.AppendLiteral("`");
							stringBuilder15.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder16 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(32, 2, stringBuilder6);
							handler2.AppendLiteral("- Readback non-zero bytes: `");
							handler2.AppendFormatted(verifiedNonZero);
							handler2.AppendLiteral(" / ");
							handler2.AppendFormatted(512);
							handler2.AppendLiteral("`");
							stringBuilder16.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder17 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder6);
							handler2.AppendLiteral("- Exact readback match: **");
							handler2.AppendFormatted(matrixVerified ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder17.AppendLine(ref handler2);
							Console.WriteLine(matrixVerified ? $"Die Picture-Matrix in Slot {slot} wurde geschrieben und exakt zurueckgelesen." : $"Die Picture-Matrix wurde geschrieben, aber NICHT exakt zurueckgelesen (nur {verifiedNonZero} von {512} Byte ungleich null).");
							byte[] activation = new byte[9];
							activation[1] = 8;
							activation[2] = 0;
							activation[3] = customEffect;
							activation[4] = 5;
							activation[5] = 50;
							activation[6] = 0;
							activation[7] = 1;
							activation[8] = CalculateGigabyteChecksum(activation);
							stream.SetFeature(activation);
							Thread.Sleep(750);
							byte[] globalReadback = QueryFeature(136, 0);
							report2.AppendLine();
							report2.AppendLine("## Custom effect activation");
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder18 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
							handler2.AppendLiteral("- Request: `");
							handler2.AppendFormatted(Convert.ToHexString(activation));
							handler2.AppendLiteral("`");
							stringBuilder18.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder19 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
							handler2.AppendLiteral("- Global readback: `");
							handler2.AppendFormatted(Convert.ToHexString(globalReadback));
							handler2.AppendLiteral("`");
							stringBuilder19.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder20 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(70, 5, stringBuilder6);
							handler2.AppendLiteral("- Decoded: effect `");
							handler2.AppendFormatted(globalReadback[3]);
							handler2.AppendLiteral("`, speed `");
							handler2.AppendFormatted(globalReadback[4]);
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(globalReadback[5]);
							handler2.AppendLiteral("`, colour `");
							handler2.AppendFormatted(globalReadback[6]);
							handler2.AppendLiteral("`, direction `");
							handler2.AppendFormatted(globalReadback[7]);
							handler2.AppendLiteral("`");
							stringBuilder20.AppendLine(ref handler2);
							Console.WriteLine();
							Console.WriteLine($"Custom {slot + 1} (Effekt {customEffect}) ist angefordert. Bitte jetzt auf die Tastatur schauen.");
							Console.WriteLine("Erwartung, falls der Pfad funktioniert: alle Tasten leuchten rot, unabhaengig von den Zonenfarben.");
							Console.Write("Beobachtung: ");
							string observation = Console.ReadLine()?.Trim() ?? string.Empty;
							report2.AppendLine();
							report2.AppendLine("## Owner observation");
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder21 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder6);
							handler2.AppendLiteral("- ");
							handler2.AppendFormatted(Escape((observation.Length == 0) ? "(keine Beschreibung eingegeben)" : observation));
							stringBuilder21.AppendLine(ref handler2);
						}
						catch (Exception ex)
						{
							Console.Error.WriteLine("Testfehler: " + ex.Message);
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder22 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
							handler2.AppendLiteral("- Test error: ");
							handler2.AppendFormatted(Escape(ex.Message));
							stringBuilder22.AppendLine(ref handler2);
							Environment.ExitCode = 5;
						}
						finally
						{
							report2.AppendLine();
							report2.AppendLine("## Rollback");
							report2.AppendLine();
							if (savedMatrix != null)
							{
								try
								{
									WriteMatrix(slot, savedMatrix);
									bool exact = ((ReadOnlySpan<byte>)ReadMatrix(slot).AsSpan()).SequenceEqual((ReadOnlySpan<byte>)savedMatrix);
									stringBuilder6 = report2;
									StringBuilder stringBuilder23 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(43, 2, stringBuilder6);
									handler2.AppendLiteral("- Slot ");
									handler2.AppendFormatted(slot);
									handler2.AppendLiteral(" matrix rewritten, exact match: **");
									handler2.AppendFormatted(exact ? "yes" : "no");
									handler2.AppendLiteral("**");
									stringBuilder23.AppendLine(ref handler2);
									if (!exact)
									{
										Environment.ExitCode = 6;
									}
								}
								catch (Exception ex2)
								{
									stringBuilder6 = report2;
									StringBuilder stringBuilder24 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 1, stringBuilder6);
									handler2.AppendLiteral("- MATRIX RESTORE ERROR: ");
									handler2.AppendFormatted(Escape(ex2.Message));
									stringBuilder24.AppendLine(ref handler2);
									Environment.ExitCode = 6;
								}
							}
							else
							{
								report2.AppendLine("- Matrix restore not required; the original slot was never read.");
							}
							foreach (var (zone3, original) in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
							{
								try
								{
									byte[] request = new byte[9];
									request[1] = 8;
									request[2] = zone3;
									request[3] = original[3];
									request[4] = original[4];
									request[5] = original[5];
									request[6] = original[6];
									request[8] = CalculateGigabyteChecksum(request);
									stream.SetFeature(request);
									Thread.Sleep(65);
									byte[] restored = QueryFeature(136, zone3);
									bool exact2 = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
									stringBuilder6 = report2;
									StringBuilder stringBuilder25 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder6);
									handler2.AppendLiteral("- Zone ");
									handler2.AppendFormatted(zone3);
									handler2.AppendLiteral(": `#");
									handler2.AppendFormatted(restored[3], "X2");
									handler2.AppendFormatted(restored[4], "X2");
									handler2.AppendFormatted(restored[5], "X2");
									handler2.AppendLiteral("`, brightness `");
									handler2.AppendFormatted(restored[6]);
									handler2.AppendLiteral("`, exact match: **");
									handler2.AppendFormatted(exact2 ? "yes" : "no");
									handler2.AppendLiteral("**");
									stringBuilder25.AppendLine(ref handler2);
									if (!exact2)
									{
										Environment.ExitCode = 6;
									}
								}
								catch (Exception ex3)
								{
									stringBuilder6 = report2;
									StringBuilder stringBuilder26 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder6);
									handler2.AppendLiteral("- Zone ");
									handler2.AppendFormatted(zone3);
									handler2.AppendLiteral(" restore failed: ");
									handler2.AppendFormatted(Escape(ex3.Message));
									stringBuilder26.AppendLine(ref handler2);
									Environment.ExitCode = 6;
								}
							}
						}
						WriteReport();
						return;
					}
					finally
					{
						if (stream != null)
						{
							((IDisposable)stream).Dispose();
						}
					}
				}
				Console.Error.WriteLine("Die exakt zugelassene Schnittstelle wurde nicht gefunden; es wurde nichts gesendet.");
				report2.AppendLine("- Exact approved interface was not found; no command was sent.");
				WriteReport();
				Environment.ExitCode = 4;
			}
			byte[] QueryFeature(byte command, byte selector)
			{
				byte[] query = new byte[9];
				query[1] = command;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(65);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			byte[] ReadMatrix(int targetSlot)
			{
				byte[] request2 = new byte[9];
				request2[1] = 146;
				request2[2] = 0;
				request2[3] = (byte)targetSlot;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
				byte[] handshake = new byte[9];
				stream.GetFeature(handshake);
				byte[] matrix = new byte[512];
				for (int block = 0; block < 8; block++)
				{
					byte[] input = new byte[65];
					int received = stream.Read(input, 0, input.Length);
					if (received != 65)
					{
						throw new InvalidOperationException($"Matrix block {block + 1} returned {received} bytes instead of {65}.");
					}
					input.AsSpan(1, 64).CopyTo(matrix.AsSpan(block * 64, 64));
					Thread.Sleep(25);
				}
				return matrix;
			}
			void WriteMatrix(int targetSlot, byte[] matrix)
			{
				byte[] request2 = new byte[9];
				request2[1] = 18;
				request2[2] = 0;
				request2[3] = (byte)targetSlot;
				request2[4] = 8;
				request2[5] = 0;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
				Thread.Sleep(65);
				for (int block = 0; block < 8; block++)
				{
					byte[] output = new byte[65];
					output[0] = 0;
					matrix.AsSpan(block * 64, 64).CopyTo(output.AsSpan(1, 64));
					stream.Write(output, 0, output.Length);
					Thread.Sleep(100);
				}
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-picture-matrix-write-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine(report2);
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}


		static void RunZoneBrightnessSweep()
		{
			byte[] levels = new byte[10] { 0, 25, 50, 51, 60, 75, 100, 150, 200, 255 };
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - Sweep des Zonen-Helligkeitsbytes");
			Console.WriteLine();
			Console.WriteLine("Gigabytes Oberfläche sendet nie mehr als 50, weil sie 0-100 Prozent halbiert.");
			Console.WriteLine("Alles oberhalb von 50 ist deshalb ungetestet. Die Farbe bleibt konstant weiss,");
			Console.WriteLine("nur das Helligkeitsbyte aendert sich.");
			Console.WriteLine("Enter = weiter | Text = Beobachtung speichern | /stop = beenden");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS zone brightness byte sweep");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Commands used: zone setter `0x08` selector 1-3 and zone getter `0x88` only");
			report2.AppendLine("- Global effect command, picture matrix, WMI, and EC: **not used**");
			report2.AppendLine("- Colour held constant at `#FFFFFF` on all three zones; only the brightness byte varies");
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder6);
			handler2.AppendLiteral("- Levels tested: `");
			handler2.AppendFormatted(string.Join("`, `", levels));
			handler2.AppendLiteral("`");
			stringBuilder8.AppendLine(ref handler2);
			report2.AppendLine("- Context: the earlier boundary test covered only 0, 1, 25, 49, and 50 and concluded the byte is an off/on gate");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				Console.Error.WriteLine("Die exakt zugelassene RGB-Schnittstelle wurde nicht gefunden.");
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			try
			{
				Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
				byte[] value;
				try
				{
					report2.AppendLine("## Captured original zone state");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] state = QueryZone2(zone2);
						originalZones.Add(zone2, state);
						stringBuilder6 = report2;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(state[3], "X2");
						handler2.AppendFormatted(state[4], "X2");
						handler2.AppendFormatted(state[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(state[6]);
						handler2.AppendLiteral("`");
						stringBuilder9.AppendLine(ref handler2);
					}
					report2.AppendLine();
					report2.AppendLine("## Levels");
					report2.AppendLine();
					report2.AppendLine("| Requested | Stored readback | Owner observation |");
					report2.AppendLine("|---|---|---|");
					value = levels;
					foreach (byte level in value)
					{
						for (byte zone3 = 1; zone3 <= 3; zone3++)
						{
							WriteZone2(zone3, byte.MaxValue, byte.MaxValue, byte.MaxValue, level);
							Thread.Sleep(65);
						}
						byte[] readback = QueryZone2(1);
						Console.WriteLine($"Helligkeitsbyte {level} gesetzt, gespeichert gelesen als {readback[6]}.");
						Console.Write("  Beobachtung: ");
						string observation = Console.ReadLine()?.Trim() ?? string.Empty;
						stringBuilder6 = report2;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 3, stringBuilder6);
						handler2.AppendLiteral("| `");
						handler2.AppendFormatted(level);
						handler2.AppendLiteral("` | `");
						handler2.AppendFormatted(readback[6]);
						handler2.AppendLiteral("` | ");
						handler2.AppendFormatted(Escape((observation.Length == 0) ? "(keine Beschreibung)" : observation));
						handler2.AppendLiteral(" |");
						stringBuilder10.AppendLine(ref handler2);
						if (observation.Equals("/stop", StringComparison.OrdinalIgnoreCase))
						{
							Console.WriteLine("Sweep wird beendet.");
							break;
						}
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Testfehler: " + ex.Message);
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder11.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					report2.AppendLine();
					report2.AppendLine("## Restoration");
					report2.AppendLine();
					foreach (KeyValuePair<byte, byte[]> item11 in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
					{
						item11.Deconstruct(out var key2, out value);
						byte zone4 = key2;
						byte[] original = value;
						try
						{
							WriteZone2(zone4, original[3], original[4], original[5], original[6]);
							Thread.Sleep(65);
							byte[] restored = QueryZone2(zone4);
							bool exact = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
							stringBuilder6 = report2;
							StringBuilder stringBuilder12 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(": `#");
							handler2.AppendFormatted(restored[3], "X2");
							handler2.AppendFormatted(restored[4], "X2");
							handler2.AppendFormatted(restored[5], "X2");
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(restored[6]);
							handler2.AppendLiteral("`, exact match: **");
							handler2.AppendFormatted(exact ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder12.AppendLine(ref handler2);
							if (!exact)
							{
								Environment.ExitCode = 6;
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = report2;
							StringBuilder stringBuilder13 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(" restore failed: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder13.AppendLine(ref handler2);
							Environment.ExitCode = 6;
						}
					}
					Console.WriteLine("Die vorherigen drei RGB-Zonen wurden wiederhergestellt.");
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] QueryZone2(byte b)
			{
				byte[] query = new byte[9];
				query[1] = 136;
				query[2] = b;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(10);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-zone-brightness-sweep-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine("Report written to: " + outputPath2);
			}
			void WriteZone2(byte b, byte red, byte green, byte blue, byte brightness)
			{
				byte[] request = new byte[9];
				request[1] = 8;
				request[2] = b;
				request[3] = red;
				request[4] = green;
				request[5] = blue;
				request[6] = brightness;
				request[8] = CalculateGigabyteChecksum(request);
				stream.SetFeature(request);
			}
		}


		static byte Scale(byte value, double factor)
		{
			return (byte)Math.Clamp(Math.Round((double)(int)value * factor), 0.0, 255.0);
		}


		static (byte r, byte g, byte b)[] Travelling(double elapsed, double secondsPerZone, (byte r, byte g, byte b) lit, (byte r, byte g, byte b) dim, bool pingPong)
		{
			int step = (int)(elapsed / secondsPerZone);
			int active = (pingPong ? Math.Abs(2 - step % 4) : (step % 3));
			(byte, byte, byte)[] frame = new(byte, byte, byte)[3];
			for (int i = 0; i < 3; i++)
			{
				frame[i] = ((i == active) ? lit : dim);
			}
			return frame;
		}


		static string TryRead(Func<string> operation)
		{
			try
			{
				return operation();
			}
			catch (Exception ex)
			{
				return "unavailable (" + ex.Message + ")";
			}
		}


		static (byte r, byte g, byte b)[] Uniform(byte red, byte green, byte blue)
		{
			return new(byte, byte, byte)[3]
			{
				(red, green, blue),
				(red, green, blue),
				(red, green, blue)
			};
		}


		static void WriteBrightnessCycleReport(StringBuilder cycleReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-brightness-cycle-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, cycleReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(cycleReport);
			Console.WriteLine("Report written to: " + outputPath2);
		}


		static void WriteInteractiveKeyboardEffectReport(StringBuilder interactiveReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-effect-interactive-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, interactiveReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine("Bericht gespeichert: " + outputPath2);
		}


		static void WriteKeyboardEffectBatchReport(StringBuilder batchReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-effect-batch1-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, batchReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(batchReport);
			Console.WriteLine("Report written to: " + outputPath2);
		}


		static void WriteKeyboardEffectTestReport(StringBuilder effectReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-effect-breathing-test-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, effectReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(effectReport);
			Console.WriteLine("Report written to: " + outputPath2);
		}


		static void WriteZone(HidStream stream, byte b, byte red, byte green, byte blue, byte brightness)
		{
			byte[] request = new byte[9];
			request[1] = 8;
			request[2] = b;
			request[3] = red;
			request[4] = green;
			request[5] = blue;
			request[6] = brightness;
			request[8] = CalculateGigabyteChecksum(request);
			stream.SetFeature(request);
		}


#nullable restore warnings
#pragma warning restore CS8321

/// <summary>
/// Writes single curve points through the firmware setter directly, bypassing the app's own
/// 57-229 validation - which is the entire point of the probe: that range is what this test
/// is trying to establish, so it cannot be assumed while establishing it.
/// </summary>
sealed class CurvePointWriter : IDisposable
{
    private readonly ManagementClass _setterClass;
    private readonly ManagementObject _setter;

    public CurvePointWriter()
    {
        _setterClass = new ManagementClass(
            new ManagementScope(AorusDeviceProfile.FirmwareNamespace),
            new ManagementPath(AorusDeviceProfile.SetterClass),
            null);
        using ManagementObjectCollection instances = _setterClass.GetInstances();
        _setter = instances.Cast<ManagementObject>().FirstOrDefault()
            ?? throw new InvalidOperationException("Keine Instanz der Gigabyte-Schreibklasse gefunden.");
    }

    public void Write(byte index, byte temperature, byte value)
    {
        using ManagementBaseObject input = _setter.GetMethodParameters("SetFanIndexValue");
        input["Index"] = index;
        input["Temperture"] = temperature;
        input["Value"] = value;
        using ManagementBaseObject output = _setter.InvokeMethod(
            "SetFanIndexValue",
            input,
            new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) });
    }

    public void Dispose()
    {
        _setter.Dispose();
        _setterClass.Dispose();
    }
}
