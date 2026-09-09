namespace AorusControl.Core.Features.GpuPreferences;

/// <summary>A program worth offering, and why it is worth offering.</summary>
public sealed record GpuSuggestion(string Name, string Reason)
{
    public string DisplayName => Name.Contains('\\') ? Path.GetFileName(Name) : Name;
}

/// <summary>What the registry holds beyond what is being managed: candidates, and leftovers.</summary>
public sealed record GpuSuggestionResult(IReadOnlyList<GpuSuggestion> Suggestions, int MissingPrograms);

/// <summary>
/// Finds the programs worth putting under automatic switching, out of the ones Windows already
/// has a graphics preference for.
///
/// Deliberately not a scan of everything installed. This machine has 234 start-menu entries
/// and 303 installed programs; a list that long is not a suggestion, it is homework. The
/// interesting set is much smaller and needs no guessing at all: the programs somebody has
/// already set to high performance. Those are exactly the ones that will wake the RTX on
/// battery - on this laptop that was a game and, less obviously, Netflix.
///
/// Programs set to power saving are left alone: they already do the right thing on battery,
/// and taking them over would only mean this app switches them to the RTX on mains, which
/// nobody asked for.
/// </summary>
public static class GpuSuggestions
{
    public static GpuSuggestionResult From(
        IEnumerable<GpuPreferenceProgram> programs,
        IEnumerable<ManagedProgram> managed,
        Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(managed);
        ArgumentNullException.ThrowIfNull(exists);

        var known = managed.Select(program => program.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var suggestions = new List<GpuSuggestion>();
        int missing = 0;

        foreach (GpuPreferenceProgram program in programs)
        {
            if (known.Contains(program.Name)) continue;
            // A program whose file is gone is a leftover of an uninstall, not a candidate. It
            // is counted, because a pile of them is worth mentioning once.
            if (!exists(program.Name)) { missing++; continue; }
            if (!program.IsManageable) continue;
            if (program.Preference != GpuPreference.Nvidia) continue;

            suggestions.Add(new GpuSuggestion(program.Name,
                "steht in Windows auf Höchstleistung und weckt im Akkubetrieb die RTX"));
        }

        return new GpuSuggestionResult(
            suggestions.OrderBy(suggestion => suggestion.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            missing);
    }

    /// <summary>Whether this entry still points at something. Store apps are named by their id
    /// rather than a path, and there is nothing to look up on disk for those.</summary>
    public static bool StillInstalled(string name) => !name.Contains('\\') || File.Exists(name);
}
