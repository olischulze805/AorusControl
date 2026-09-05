namespace AorusControl.Core.Features.Diagnostics;

/// <summary>
/// Where this app keeps its own files: <c>%AppData%\AorusControl</c>.
///
/// Deliberately not <c>%LocalAppData%\AorusControl</c>, which is where the installer puts the
/// program itself. Sharing that folder had two consequences, one of them fatal: running the
/// app from a build tree created the folder, and the installer then refused to install at all
/// because it looked like an existing installation - on a machine that had never installed
/// anything. The milder one is that an uninstall would take the saved fan curve and keyboard
/// colours with it, and an update might.
///
/// Settings that survive a reinstall are the point here. Logs live alongside them so "open the
/// log folder" stays one place rather than two, and they clean themselves up after two weeks.
/// </summary>
public static class AppData
{
    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AorusControl");

    /// <summary>The old location, kept only so <see cref="MigrateFromInstallFolder"/> can empty it.</summary>
    private static string LegacyDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AorusControl");

    public static string File(string name) => Path.Combine(Directory, name);

    /// <summary>
    /// Moves settings written by an earlier version out of the install folder, once. Nothing is
    /// overwritten: a file that already exists in the new location wins, since it is the newer
    /// one. Failures are ignored on purpose - losing a saved colour is not a reason to stop the
    /// app from starting, and the device is read back on startup anyway.
    /// </summary>
    public static void MigrateFromInstallFolder() => Migrate(LegacyDirectory, Directory);

    /// <summary>The move itself, against given folders so it can be exercised without touching
    /// the real profile.</summary>
    internal static void Migrate(string from, string to)
    {
        try
        {
            if (!System.IO.Directory.Exists(from) || string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return;
            System.IO.Directory.CreateDirectory(to);
            foreach (string source in System.IO.Directory.GetFiles(from, "*.json"))
            {
                string target = Path.Combine(to, Path.GetFileName(source));
                if (System.IO.File.Exists(target)) { System.IO.File.Delete(source); continue; }
                System.IO.File.Move(source, target);
            }
        }
        catch
        {
            // Best effort: the app starts either way, reading the device rather than a file.
        }
    }
}
