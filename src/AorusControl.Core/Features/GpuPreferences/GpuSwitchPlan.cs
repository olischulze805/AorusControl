using AorusControl.Core.Features.PowerProfiles;

namespace AorusControl.Core.Features.GpuPreferences;

/// <summary>A program this app assigns a graphics chip to, and what Windows had set for it
/// before this app first touched it.</summary>
/// <param name="Name">The registry value name - full path, or a Store app's id.</param>
/// <param name="Original">The preference found on adoption, so it can be handed back.</param>
/// <param name="OnAc">What it gets while the laptop is plugged in.</param>
/// <param name="OnBattery">What it gets while it runs on battery.</param>
/// <param name="LastWritten">The value this app last put there, or null if it never has.
/// Kept with the program rather than in a dictionary beside it, because it has to survive a
/// restart: it is the only way to tell a value somebody changed by hand from one we set.</param>
public sealed record ManagedProgram(
    string Name,
    GpuPreference? Original,
    GpuPreference OnAc = GpuPreference.Nvidia,
    GpuPreference OnBattery = GpuPreference.Integrated,
    GpuPreference? LastWritten = null)
{
    public string DisplayName => Name.Contains('\\') ? Path.GetFileName(Name) : Name;

    /// <summary>What this program should be set to under the given supply.</summary>
    public GpuPreference For(LaptopPowerSource source) =>
        source == LaptopPowerSource.Ac ? OnAc : OnBattery;
}

/// <summary>One intended write.</summary>
public sealed record GpuSwitchStep(string Name, GpuPreference Target);

/// <summary>
/// What to write when the power source changes.
///
/// The defaults are the RTX on mains and the Intel chip on battery, and the battery half is
/// the point of the whole feature: a program that never asks for the RTX lets it stay in its
/// sleep state, which is worth more to battery life than anything else this app can do. But
/// they are only defaults. A browser is better off on the Intel chip even on mains, and a
/// game the user wants fast is better off on the RTX even without one - so each program
/// carries its own pair rather than the rule being wired into this class.
///
/// Deciding is separate from writing because the interesting rules are all decisions: an
/// unknown power source must change nothing (better a stale preference than a wrong one), a
/// program already at its target must not be rewritten, and one whose preference somebody
/// changed by hand since we wrote it is no longer ours to manage.
/// </summary>
public static class GpuSwitchPlan
{
    /// <summary>The preferences a program can be given, in the order the interface offers
    /// them. Kept here so the two lists in the UI cannot drift apart from the rules.</summary>
    public static IReadOnlyList<GpuPreference> Choices { get; } =
        [GpuPreference.Nvidia, GpuPreference.Integrated, GpuPreference.WindowsDecides];

    /// <summary>
    /// The writes needed to bring the managed programs in line with the power source.
    /// </summary>
    /// <param name="source">Unknown leaves everything alone.</param>
    /// <param name="managed">The programs the user put under automatic control.</param>
    /// <param name="current">What the registry says right now, by value name.</param>
    public static IReadOnlyList<GpuSwitchStep> Steps(
        LaptopPowerSource source,
        IEnumerable<ManagedProgram> managed,
        IReadOnlyDictionary<string, GpuPreferenceProgram> current)
    {
        ArgumentNullException.ThrowIfNull(managed);
        ArgumentNullException.ThrowIfNull(current);
        if (source == LaptopPowerSource.Unknown) return [];

        var steps = new List<GpuSwitchStep>();
        foreach (ManagedProgram program in managed)
        {
            current.TryGetValue(program.Name, out GpuPreferenceProgram? entry);
            if (entry is { IsManageable: false }) continue;

            GpuPreference target = program.For(source);
            GpuPreference? now = entry?.Preference;
            if (now == target) continue;
            // Changed elsewhere since we last wrote it - by the user in Windows' settings, or
            // by the program's own installer. That is a decision, and it is not ours to
            // overrule. It outlives this session because it is stored with the program.
            if (program.LastWritten is { } ours && now is not null && now != ours) continue;

            steps.Add(new GpuSwitchStep(program.Name, target));
        }
        return steps;
    }

    /// <summary>What to write when the user stops managing a program: back to what Windows had
    /// before, and only if the value is still the one this app left there.</summary>
    public static GpuSwitchStep? Release(ManagedProgram program, GpuPreferenceProgram? current)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (program.Original is not { } original) return null;
        if (current?.Preference is { } now && program.LastWritten is { } ours && now != ours) return null;
        return current?.Preference == original ? null : new GpuSwitchStep(program.Name, original);
    }
}
