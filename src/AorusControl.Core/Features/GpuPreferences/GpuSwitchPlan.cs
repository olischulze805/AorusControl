using AorusControl.Core.Features.PowerProfiles;

namespace AorusControl.Core.Features.GpuPreferences;

/// <summary>A program this app switches between the two graphics chips, and what Windows had
/// set for it before we first touched it.</summary>
/// <param name="Name">The registry value name - full path, or a Store app's id.</param>
/// <param name="Original">The preference found on adoption, so it can be handed back.</param>
public sealed record ManagedProgram(string Name, GpuPreference? Original)
{
    public string DisplayName => Name.Contains('\\') ? Path.GetFileName(Name) : Name;
}

/// <summary>One intended write.</summary>
public sealed record GpuSwitchStep(string Name, GpuPreference Target);

/// <summary>
/// What to write when the power source changes: the RTX on mains, the Intel chip on battery.
///
/// The point of the whole feature is the battery case. A program that never asks for the RTX
/// lets it stay in its sleep state, and a sleeping RTX is the single biggest thing this app
/// can do for battery life. On mains there is no reason to hold it back.
///
/// Deciding is separate from writing because the interesting rules are all decisions: an
/// unknown power source must change nothing (better a stale preference than a wrong one), a
/// program already at its target must not be rewritten, and one whose preference somebody
/// changed by hand since we wrote it is no longer ours to manage.
/// </summary>
public static class GpuSwitchPlan
{
    public static GpuPreference For(LaptopPowerSource source) =>
        source == LaptopPowerSource.Ac ? GpuPreference.Nvidia : GpuPreference.Integrated;

    /// <summary>
    /// The writes needed to bring the managed programs in line with the power source.
    /// </summary>
    /// <param name="source">Unknown leaves everything alone.</param>
    /// <param name="managed">The programs the user put under automatic control.</param>
    /// <param name="current">What the registry says right now, by value name.</param>
    /// <param name="lastWritten">What this app wrote last time, by value name. A program whose
    /// current value differs from that was changed elsewhere - by the user in Windows'
    /// settings, or by the program's own installer - and is skipped rather than overruled.</param>
    public static IReadOnlyList<GpuSwitchStep> Steps(
        LaptopPowerSource source,
        IEnumerable<ManagedProgram> managed,
        IReadOnlyDictionary<string, GpuPreferenceProgram> current,
        IReadOnlyDictionary<string, GpuPreference> lastWritten)
    {
        ArgumentNullException.ThrowIfNull(managed);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(lastWritten);
        if (source == LaptopPowerSource.Unknown) return [];

        GpuPreference target = For(source);
        var steps = new List<GpuSwitchStep>();
        foreach (ManagedProgram program in managed)
        {
            current.TryGetValue(program.Name, out GpuPreferenceProgram? entry);
            if (entry is { IsManageable: false }) continue;

            GpuPreference? now = entry?.Preference;
            if (now == target) continue;
            if (lastWritten.TryGetValue(program.Name, out GpuPreference ours) && now is not null && now != ours) continue;

            steps.Add(new GpuSwitchStep(program.Name, target));
        }
        return steps;
    }

    /// <summary>What to write when the user stops managing a program: back to what Windows had
    /// before, and only if the value is still the one this app left there.</summary>
    public static GpuSwitchStep? Release(
        ManagedProgram program,
        GpuPreferenceProgram? current,
        GpuPreference? lastWritten)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (program.Original is not { } original) return null;
        if (current?.Preference is { } now && lastWritten is { } ours && now != ours) return null;
        return current?.Preference == original ? null : new GpuSwitchStep(program.Name, original);
    }
}
