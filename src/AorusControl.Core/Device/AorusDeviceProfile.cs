namespace AorusControl.Core.Device;

public static class AorusDeviceProfile
{
    public const string ExpectedManufacturer = "GIGABYTE";
    public const string ExpectedModel = "AORUS 5 SE";
    public const string ExpectedBios = "FB0F";
    public const string FirmwareNamespace = @"root\WMI";
    public const string GetterClass = "GB_WMIACPI_Get";
    public const string SetterClass = "GB_WMIACPI_Set";

    public static IReadOnlySet<string> BatteryGetterMethods { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GetChargePolicy",
            "GetChargeStop"
        };

    public static IReadOnlySet<string> BatterySetterMethods { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SetChargePolicy",
            "SetChargeStop"
        };

    public static IReadOnlySet<string> LiveTelemetryMethods { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "getCpuTemp",
            "getGpuTemp1",
            "getRpm1",
            "getRpm2",
            "GetCPUFanDuty",
            "GetGPUFanDuty"
        };

    public static IReadOnlySet<string> FanGetterMethods { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GetFixedFanStatus",
            "GetStepFanStatus",
            "GetAutoFanStatus",
            "GetNvThermalTarget",
            "GetFixedFanSpeed",
            "GetGPUFanDuty",
            "GetFanIndexValue"
        };

    /// <summary>
    /// The only fan setters this project may invoke. The list is a hard gate, not a
    /// preference.
    /// </summary>
    /// <remarks>
    /// Deliberately excluded: <c>TurnOffFan</c>, WMI method ID <c>0x75</c>, which the
    /// installed MOF exposes. Stopping the fans outright is never an operation this
    /// application performs, so it stays out of the allowlist by intent rather than by
    /// accident. Also excluded: <c>SetCurrentFanStep</c> and <c>SetFanModeNotify</c>,
    /// whose semantics on FB0F are unverified.
    /// </remarks>
    public static IReadOnlySet<string> FanNormalSetterMethods { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SetFixedFanStatus",
            "SetStepFanStatus",
            "SetAutoFanStatus",
            "SetNvThermalTarget",
            "SetFixedFanSpeed",
            "SetGPUFanDuty",
            "SetFanIndexValue"
        };
}
