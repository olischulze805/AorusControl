using System.Runtime.InteropServices;

namespace AorusControl.Core.Features.PowerProfiles;

/// <summary>One setting inside a power scheme, named the way Windows names it.</summary>
public sealed record PowerSetting(Guid Subgroup, Guid Setting)
{
    /// <summary>Maximum processor state, in per cent.</summary>
    public static readonly PowerSetting MaximumProcessorState =
        new(new("54533251-82be-4824-96c1-47b60b740d00"), new("bc5038f7-23e0-4960-96da-33abaf5935ec"));

    /// <summary>Display brightness, in per cent.</summary>
    public static readonly PowerSetting DisplayBrightness =
        new(new("7516b95f-f776-4464-8c53-06167f40cc99"), new("aded5e82-b909-4619-9949-f5d71dac0bcb"));
}

/// <summary>
/// Windows power schemes: duplicate one, write into the copy, switch to it, switch back.
///
/// Gigabyte's own battery saver wrote its two values straight into the user's Balanced plan
/// and kept the old ones in the registry, which means a crash between setting and restoring
/// leaves the machine capped with nobody to say why. This works on a copy instead. The user's
/// plans are never touched, and the worst a crash can leave behind is an extra entry in a
/// list that does nothing unless it is active.
///
/// Every write is read back, like everywhere else in this project: PowerWriteDCValueIndex
/// returns success for a setting the current scheme does not actually expose.
/// </summary>
public sealed class PowerSchemeWriter
{
    private const uint Ok = 0;

    /// <summary>The scheme that is in force right now.</summary>
    public static Guid Active()
    {
        IntPtr pointer = IntPtr.Zero;
        try
        {
            uint result = PowerGetActiveScheme(IntPtr.Zero, ref pointer);
            if (result != Ok || pointer == IntPtr.Zero)
                throw new InvalidOperationException($"Der aktive Energiesparplan war nicht lesbar (Code {result}).");
            return Marshal.PtrToStructure<Guid>(pointer);
        }
        finally { if (pointer != IntPtr.Zero) LocalFree(pointer); }
    }

    /// <summary>Copies a scheme and gives the copy a name.</summary>
    public static Guid Duplicate(Guid source, string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        IntPtr copy = IntPtr.Zero;
        try
        {
            uint result = PowerDuplicateScheme(IntPtr.Zero, ref source, ref copy);
            if (result != Ok || copy == IntPtr.Zero)
                throw new InvalidOperationException($"Der Energiesparplan liess sich nicht kopieren (Code {result}).");
            Guid created = Marshal.PtrToStructure<Guid>(copy);
            // Both are cosmetic, and both are the only thing the user will see of this in
            // Windows' own list - so a failure here is not worth undoing the copy over.
            PowerWriteFriendlyName(IntPtr.Zero, ref created, IntPtr.Zero, IntPtr.Zero, name, (uint)((name.Length + 1) * 2));
            PowerWriteDescription(IntPtr.Zero, ref created, IntPtr.Zero, IntPtr.Zero, description, (uint)((description.Length + 1) * 2));
            return created;
        }
        finally { if (copy != IntPtr.Zero) LocalFree(copy); }
    }

    public static void Delete(Guid scheme)
    {
        uint result = PowerDeleteScheme(IntPtr.Zero, ref scheme);
        if (result != Ok) throw new InvalidOperationException($"Der Energiesparplan liess sich nicht löschen (Code {result}).");
    }

    public static void Activate(Guid scheme)
    {
        uint result = PowerSetActiveScheme(IntPtr.Zero, ref scheme);
        if (result != Ok) throw new InvalidOperationException($"Der Energiesparplan liess sich nicht aktivieren (Code {result}).");
    }

    /// <summary>Reads one setting's battery-side value.</summary>
    public static uint ReadOnBattery(Guid scheme, PowerSetting setting)
    {
        Guid subgroup = setting.Subgroup, item = setting.Setting;
        uint value = 0;
        uint result = PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref item, ref value);
        if (result != Ok) throw new InvalidOperationException($"Der Wert war nicht lesbar (Code {result}).");
        return value;
    }

    /// <summary>
    /// Writes one setting's battery-side value and reads it back.
    ///
    /// The readback is the point. A scheme that does not expose a setting - a hidden one, or
    /// one the platform has no say over - takes the write and reports success, and the only
    /// way to tell that apart from a write that landed is to ask again.
    /// </summary>
    public static void WriteOnBattery(Guid scheme, PowerSetting setting, uint value)
    {
        Guid subgroup = setting.Subgroup, item = setting.Setting;
        uint result = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref item, value);
        if (result != Ok) throw new InvalidOperationException($"Der Wert liess sich nicht schreiben (Code {result}).");

        uint written = ReadOnBattery(scheme, setting);
        if (written != value)
            throw new InvalidOperationException($"Rücklesen ergab {written} statt {value}; der Plan hat den Wert nicht übernommen.");
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, ref IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerDuplicateScheme(IntPtr rootPowerKey, ref Guid sourceSchemeGuid, ref IntPtr destinationSchemeGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerDeleteScheme(IntPtr rootPowerKey, ref Guid schemeGuid);

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerWriteFriendlyName(IntPtr rootPowerKey, ref Guid schemeGuid,
        IntPtr subgroupOfPowerSettingsGuid, IntPtr powerSettingGuid, string buffer, uint bufferSize);

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerWriteDescription(IntPtr rootPowerKey, ref Guid schemeGuid,
        IntPtr subgroupOfPowerSettingsGuid, IntPtr powerSettingGuid, string buffer, uint bufferSize);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid,
        ref Guid subgroupOfPowerSettingsGuid, ref Guid powerSettingGuid, uint dcValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid,
        ref Guid subgroupOfPowerSettingsGuid, ref Guid powerSettingGuid, ref uint dcValueIndex);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
