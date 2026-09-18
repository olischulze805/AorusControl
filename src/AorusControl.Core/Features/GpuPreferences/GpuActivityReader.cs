using System.Diagnostics;
using System.Globalization;

namespace AorusControl.Core.Features.GpuPreferences;

/// <summary>One process holding a graphics context on one chip - the raw fact, before the
/// list is made readable.</summary>
/// <param name="Path">The executable, when it could be read. Null for a process this app may
/// not open, which on this machine means a protected Windows component.</param>
public sealed record GpuContext(int ProcessId, string Program, string? Path, bool IsNvidia);

/// <summary>One program, and the chip it is actually using.</summary>
/// <param name="ProcessIds">Every process of it that holds a context. Browsers and Electron
/// apps run several, and listing each one separately would bury everything else - but closing
/// the program means closing all of them, so the ids are kept rather than just counted.</param>
public sealed record GpuUser(string Program, string Path, bool IsNvidia, IReadOnlyList<int> ProcessIds)
{
    public int Processes => ProcessIds.Count;

    /// <summary>Store apps are addressed by package id, not by path. Windows keeps their
    /// graphics preference under that id, so handing this path to the registry would write an
    /// entry nothing ever reads - the program search is the way in for these.</summary>
    public bool IsStoreApp => Path.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase);
}

public interface IGpuActivityReader
{
    /// <summary>
    /// Which programs are on which graphics chip right now, or null when that cannot be
    /// read at all. The distinction is the point: an empty list means nothing outside
    /// Windows is using a graphics chip, which is an answer, and null means there is none.
    /// Never throws.
    /// </summary>
    IReadOnlyList<GpuUser>? Read();
}

/// <summary>
/// Turns the raw contexts into the list a person can act on.
///
/// Separate from the reading because this is the part with opinions in it, and opinions are
/// what tests are for. A single machine produced 29 raw rows, of which 21 were Windows' own
/// shell and kernel: csrss, dwm, System, the lock screen, the text input host. None of them
/// can be assigned a graphics chip and none of them is why the card is awake.
/// </summary>
public static class GpuActivitySummary
{
    /// <param name="ownPath">This app's own executable. It uses the Intel chip and appears in
    /// the list like anything else, which would be an odd thing to offer to close from inside
    /// itself.</param>
    public static IReadOnlyList<GpuUser> From(IEnumerable<GpuContext> contexts,
        string? windowsDirectory = null, string? ownPath = null)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        string windows = windowsDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string? own = ownPath ?? Environment.ProcessPath;

        return contexts
            .Where(context => context.Path is { Length: > 0 } path
                && !Under(path, windows)
                && !path.Equals(own, StringComparison.OrdinalIgnoreCase))
            .GroupBy(context => context.Path!, StringComparer.OrdinalIgnoreCase)
            .Select(program => new GpuUser(
                program.First().Program,
                program.Key,
                // A program on both chips is on the discrete one as far as this list is
                // concerned: that is the state worth knowing and worth changing.
                program.Any(context => context.IsNvidia),
                program.Select(context => context.ProcessId).Distinct().ToArray()))
            // The RTX users first - they are the ones the page exists for.
            .OrderByDescending(program => program.IsNvidia)
            .ThenBy(program => program.Program, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static bool Under(string path, string directory) =>
        directory.Length > 0 && path.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The "GPU Engine" performance counters, read for their instance names alone.
///
/// Windows names one instance per process, adapter and engine:
///
///     pid_1692_luid_0x00000000_0x0001232e_phys_0_eng_0_engtype_3d
///
/// An instance exists while that process holds a graphics context on that adapter, so the
/// names by themselves answer the question this feature is about - who is on the RTX - without
/// reading a single counter value. That matters twice over: utilisation would be a second,
/// slower question, and a program sitting at 0 % on the discrete card still holds it awake.
///
/// Nothing here touches the NVIDIA driver. The counters come from the graphics kernel, which
/// is also why they can be read while the card sleeps.
/// </summary>
public sealed class WindowsGpuActivityReader(Func<IReadOnlyList<GraphicsAdapter>>? adapters = null) : IGpuActivityReader
{
    private readonly Func<IReadOnlyList<GraphicsAdapter>> _adapters = adapters ?? GraphicsAdapters.Enumerate;
    private IReadOnlyList<GraphicsAdapter>? _known;

    public IReadOnlyList<GpuUser>? Read()
    {
        try
        {
            // The adapter list is a property of the machine, not of this second, and the
            // LUIDs keep their values until the next boot. Asked once, kept.
            _known ??= _adapters();
            if (_known.Count == 0) return null;

            IReadOnlyList<GpuContext> contexts = Collect();
            // Not one instance matched a chip we know, yet the counters clearly had some. The
            // LUIDs are handed out afresh when the display driver restarts, which happens after
            // a TDR or a driver update - and a stale list would silently drop every row rather
            // than say anything. Asked once more, then believed.
            if (contexts.Count == 0)
            {
                _known = _adapters();
                contexts = Collect();
            }
            return GpuActivitySummary.From(contexts);
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            // No counter category, or no permission to read it. A missing answer, not a fault.
            return null;
        }
    }

    private IReadOnlyList<GpuContext> Collect()
    {
        var category = new PerformanceCounterCategory("GPU Engine");
        var seen = new HashSet<(int Pid, long Luid)>();
        var contexts = new List<GpuContext>();
        foreach (string instance in category.GetInstanceNames())
        {
            // One process has an instance per engine - 3D, copy, video. They say nothing
            // different about which chip it is on, so the first one of each pair wins.
            if (GpuEngineInstance.Parse(instance) is not { } engine) continue;
            if (!seen.Add((engine.ProcessId, engine.Luid))) continue;
            if (_known!.FirstOrDefault(adapter => adapter.Luid == engine.Luid) is not { } chip) continue;
            if (Describe(engine.ProcessId) is not { } program) continue;
            contexts.Add(new GpuContext(engine.ProcessId, program.Name, program.Path, chip.IsNvidia));
        }
        return contexts;
    }

    /// <summary>The process behind a counter instance, or null once it has exited. The path
    /// can fail on its own: a protected process has a name but no module this app may open.</summary>
    private static (string Name, string? Path)? Describe(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            string? path = null;
            try { path = process.MainModule?.FileName; }
            catch { /* Protected or already exiting - such a process is dropped below anyway. */ }
            return (process.ProcessName, path);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>The part of a counter instance name that says who, and on which chip.</summary>
public sealed record GpuEngineInstance(int ProcessId, long Luid)
{
    /// <summary>
    /// Reads "pid_1692_luid_0x00000000_0x0001232e_phys_0_eng_0_engtype_3d".
    ///
    /// Taken apart by hand rather than by regular expression because the shape is fixed and
    /// the failure mode matters more than the flexibility: anything that does not look exactly
    /// like this is skipped, never guessed at.
    /// </summary>
    public static GpuEngineInstance? Parse(string? instance)
    {
        if (instance is null) return null;
        ReadOnlySpan<char> rest = instance;
        if (!Take(ref rest, "pid_", out ReadOnlySpan<char> pid)) return null;
        if (!Take(ref rest, "luid_0x", out ReadOnlySpan<char> high)) return null;
        if (!Take(ref rest, "0x", out ReadOnlySpan<char> low)) return null;

        return int.TryParse(pid, out int processId)
            && int.TryParse(high, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int highPart)
            && uint.TryParse(low, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint lowPart)
                ? new GpuEngineInstance(processId, GraphicsAdapters.Key(highPart, lowPart))
                : null;
    }

    /// <summary>Steps over a prefix and hands back what stands before the next underscore.</summary>
    private static bool Take(ref ReadOnlySpan<char> text, string prefix, out ReadOnlySpan<char> value)
    {
        value = default;
        if (!text.StartsWith(prefix, StringComparison.Ordinal)) return false;
        ReadOnlySpan<char> after = text[prefix.Length..];
        int end = after.IndexOf('_');
        if (end <= 0) return false;
        value = after[..end];
        text = after[(end + 1)..];
        return true;
    }
}
