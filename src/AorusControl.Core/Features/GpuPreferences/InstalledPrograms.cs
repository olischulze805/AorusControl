namespace AorusControl.Core.Features.GpuPreferences;

/// <summary>A program the user could pick.</summary>
/// <param name="Name">What to show: the name Windows shows in the start menu.</param>
/// <param name="Identity">What to write: a full path for a normal program, an app id for a
/// Store app - exactly the two forms the graphics preferences are keyed by.</param>
public sealed record InstalledProgram(string Name, string Identity)
{
    public bool IsStoreApp => !Identity.Contains('\\');
}

/// <summary>
/// The list behind the program picker.
///
/// It exists because a file dialog cannot pick half of what is installed here: Netflix and 41
/// other Store apps have no reachable executable at all, and Windows addresses them by an id.
/// So the list is assembled from the two places Windows itself keeps this - the Apps folder
/// for Store apps, the start menu for everything else - and each entry carries the identity
/// the registry actually wants.
///
/// The scanning talks to the shell and is therefore only as testable as the shell is; the
/// merging and searching below are separate and pure, because those are the parts that decide
/// what the user gets to see.
/// </summary>
public static class InstalledPrograms
{
    private const string AppsFolder = "shell:::{4234d49b-0245-4df3-b780-3893943456e1}";

    /// <summary>Everything installed, both kinds, sorted and free of duplicates.</summary>
    public static IReadOnlyList<InstalledProgram> Scan() => Merge(StoreApps(), StartMenuPrograms());

    /// <summary>One list out of two, without duplicates: a program that appears in both is the
    /// same program, and the start menu's name is usually the friendlier one.</summary>
    public static IReadOnlyList<InstalledProgram> Merge(params IEnumerable<InstalledProgram>[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var byIdentity = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);
        foreach (InstalledProgram program in sources.SelectMany(source => source))
        {
            if (string.IsNullOrWhiteSpace(program.Name) || string.IsNullOrWhiteSpace(program.Identity)) continue;
            if (!GpuPreferenceText.IsProgramEntry(program.Identity)) continue;
            byIdentity.TryAdd(program.Identity, program);
        }
        return byIdentity.Values
            .OrderBy(program => program.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Filters by what the user typed. Every word has to appear somewhere in the name or the
    /// path, in any order - "chrome 32" finds the 32-bit one, and typing part of a folder
    /// works as well as typing part of the name.
    /// </summary>
    public static IReadOnlyList<InstalledProgram> Search(IEnumerable<InstalledProgram> programs, string? query)
    {
        ArgumentNullException.ThrowIfNull(programs);
        string[] words = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return programs.ToArray();

        return programs
            .Where(program => words.All(word =>
                program.Name.Contains(word, StringComparison.CurrentCultureIgnoreCase) ||
                program.Identity.Contains(word, StringComparison.OrdinalIgnoreCase)))
            // A hit in the name beats a hit somewhere in the path: that is what was typed at.
            .OrderByDescending(program => program.Name.Contains(words[0], StringComparison.CurrentCultureIgnoreCase))
            .ThenBy(program => program.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <summary>Store apps, by the id Windows keys them under - the only way to reach them.</summary>
    private static IEnumerable<InstalledProgram> StoreApps()
    {
        foreach ((string name, string path) in ShellItems(AppsFolder))
        {
            // Only the real app ids: the folder also holds desktop programs, and those are
            // taken from the start menu below where their target is a path we can use.
            if (path.Contains('!')) yield return new InstalledProgram(name, path);
        }
    }

    /// <summary>Normal programs, resolved from their start-menu shortcuts to the executable
    /// the preference has to be keyed by.</summary>
    private static IEnumerable<InstalledProgram> StartMenuPrograms()
    {
        // One shell object for all of them: creating it per shortcut turned a list of a few
        // hundred entries into seconds of COM overhead.
        object? shell = CreateShell();
        if (shell is null) yield break;

        foreach (Environment.SpecialFolder folder in new[]
                 { Environment.SpecialFolder.CommonStartMenu, Environment.SpecialFolder.StartMenu })
        {
            string root = Environment.GetFolderPath(folder);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;

            // IgnoreInaccessible, because this throws while walking rather than when called:
            // there is a start-menu folder on this machine that denies access, and one
            // unreadable folder must not cost the whole list.
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            IEnumerable<string> shortcuts;
            try { shortcuts = Directory.EnumerateFiles(root, "*.lnk", options); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            foreach (string shortcut in shortcuts)
            {
                string? target = ResolveShortcut(shell, shortcut);
                if (target is null || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(target)) continue;
                yield return new InstalledProgram(Path.GetFileNameWithoutExtension(shortcut), target);
            }
        }
    }

    // ---- the shell ------------------------------------------------------------------
    // Through IDispatch rather than a COM reference: two calls do not justify an interop
    // assembly, and everything here is read-only.
    private static IEnumerable<(string Name, string Path)> ShellItems(string folder)
    {
        object? shell = CreateShell();
        if (shell is null) yield break;
        object? items;
        try
        {
            object? space = Invoke(shell, "NameSpace", folder);
            items = space is null ? null : Invoke(space, "Items");
        }
        catch (Exception) { yield break; }
        if (items is null) yield break;

        int count = Invoke(items, "Count") as int? ?? 0;
        for (int index = 0; index < count; index++)
        {
            string name, path;
            try
            {
                object? item = Invoke(items, "Item", index);
                if (item is null) continue;
                name = Invoke(item, "Name") as string ?? string.Empty;
                path = Invoke(item, "Path") as string ?? string.Empty;
            }
            catch (Exception) { continue; }
            if (name.Length > 0 && path.Length > 0) yield return (name, path);
        }
    }

    private static string? ResolveShortcut(object shell, string shortcutPath)
    {
        try
        {
            object? folder = Invoke(shell, "NameSpace", Path.GetDirectoryName(shortcutPath)!);
            object? item = folder is null ? null : Invoke(folder, "ParseName", Path.GetFileName(shortcutPath));
            if (item is null) return null;

            // The shortcut's own Path is empty for most of them - 157 of the 234 on this
            // machine, Chrome and the Epic launcher included, because the shell only fills it
            // in for plain links. The parsing path is what those actually point at.
            if (Invoke(item, "ExtendedProperty", "System.Link.TargetParsingPath") is string parsed &&
                parsed.Length > 0)
            {
                return parsed;
            }

            object? link = Invoke(item, "GetLink");
            return link is null ? null : Invoke(link, "Path") as string;
        }
        // Reflection wraps everything the shell throws, and a shortcut that cannot be read is
        // simply one the picker does without.
        catch (Exception) { return null; }
    }

    private static object? CreateShell()
    {
        Type? type = Type.GetTypeFromProgID("Shell.Application");
        return type is null ? null : Activator.CreateInstance(type);
    }

    private static object? Invoke(object target, string member, params object?[] arguments) =>
        target.GetType().InvokeMember(member,
            System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.GetProperty,
            binder: null, target, arguments);
}
