namespace AorusControl.Core.Features.GpuPreferences;

/// <summary>Which graphics processor Windows should hand a program when it starts.</summary>
public enum GpuPreference
{
    /// <summary>Windows decides. On this laptop that usually means the Intel chip, but it is
    /// not a promise.</summary>
    WindowsDecides = 0,
    /// <summary>Power saving: the integrated Intel graphics.</summary>
    Integrated = 1,
    /// <summary>High performance: the NVIDIA RTX.</summary>
    Nvidia = 2
}

/// <summary>
/// Reads and rewrites one entry of Windows' own per-program graphics setting - the same value
/// the Settings app writes under Grafik / Graphics preference.
///
/// Each entry is a string of "Name=Value;" tokens, and only one of them is ours. The others
/// carry things like automatic HDR or a swap-chain upgrade, and on this machine VLC even has
/// a hard-wired adapter in there. Rewriting the whole string - the obvious shortcut - would
/// quietly throw those settings away, so the token is replaced in place and everything else
/// is left exactly as it was found, order included.
///
/// Pure string handling on purpose: the registry is somebody else's problem
/// (<see cref="WindowsGpuPreferenceStore"/>), and the rules that are easy to get wrong are
/// testable without touching it.
/// </summary>
public static class GpuPreferenceText
{
    private const string Token = "GpuPreference";

    /// <summary>An entry that pins one specific adapter. Switching such a program between
    /// chips is not ours to do: the choice is more specific than a preference, and the value
    /// beside it is a flag rather than one of the three settings below.</summary>
    public const string SpecificAdapterToken = "SpecificAdapter";

    /// <summary>Not programs: Windows keeps two housekeeping values in the same key.</summary>
    public static bool IsProgramEntry(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name is not "DirectXUserGlobalSettings" and not "GraphicsFeaturesNotificationConfig";

    public static bool PinsOneAdapter(string? raw) =>
        Tokens(raw).Any(token => token.Key.Equals(SpecificAdapterToken, StringComparison.OrdinalIgnoreCase));

    /// <summary>The preference in this entry, or null if it says nothing this app understands -
    /// including the adapter-pinning flag, which is not one of the three.</summary>
    public static GpuPreference? Read(string? raw)
    {
        foreach ((string key, string value, _) in Tokens(raw))
        {
            if (!key.Equals(Token, StringComparison.OrdinalIgnoreCase)) continue;
            return value.Trim() switch
            {
                "0" => GpuPreference.WindowsDecides,
                "1" => GpuPreference.Integrated,
                "2" => GpuPreference.Nvidia,
                _ => null
            };
        }
        return null;
    }

    /// <summary>The same entry with our token set, everything else untouched. A missing token
    /// is appended; Windows writes these strings with a trailing semicolon and so do we.</summary>
    public static string With(string? raw, GpuPreference preference)
    {
        var rebuilt = new List<string>();
        bool replaced = false;
        foreach ((string key, _, string original) in Tokens(raw))
        {
            if (key.Equals(Token, StringComparison.OrdinalIgnoreCase))
            {
                rebuilt.Add($"{Token}={(int)preference}");
                replaced = true;
            }
            else
            {
                // Carried through verbatim, not reassembled: a token this app does not
                // understand must come out of here exactly as it went in.
                rebuilt.Add(original);
            }
        }
        if (!replaced) rebuilt.Add($"{Token}={(int)preference}");
        return string.Join(";", rebuilt) + ";";
    }

    private static IEnumerable<(string Key, string Value, string Original)> Tokens(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        foreach (string part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = part.IndexOf('=');
            yield return equals < 0
                ? (part, string.Empty, part)
                : (part[..equals], part[(equals + 1)..], part);
        }
    }
}
