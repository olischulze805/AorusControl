using System.IO;
using AorusControl.Core.Features.Diagnostics;

/// <summary>
/// Where the app keeps its files. This one is not cosmetic: sharing a folder with the
/// installer made a fresh machine that had merely *run* the app refuse to install it, because
/// the folder already existed and therefore looked like an installation.
/// </summary>
internal static class AppDataTests
{
    public static void Run()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Check(AppData.Directory.StartsWith(roaming, StringComparison.OrdinalIgnoreCase),
            "settings belong under %AppData%, next to every other application's");
        Check(!AppData.Directory.StartsWith(Path.Combine(local, "AorusControl"), StringComparison.OrdinalIgnoreCase),
            @"and never inside %LocalAppData%\AorusControl, which is the installer's own folder");
        Check(AppLog.Directory.StartsWith(AppData.Directory, StringComparison.OrdinalIgnoreCase),
            "the logs live with the settings, so \"open the log folder\" is one place");
        Check(AppData.File("fan-curve-v1.json") == Path.Combine(AppData.Directory, "fan-curve-v1.json"),
            "a named file sits directly in that folder");

        // The move out of the old folder, exercised on temporary folders rather than on the
        // real profile - a test has no business relocating someone's saved fan curve.
        string root = Path.Combine(Path.GetTempPath(), "AorusControlAppDataTests-" + Guid.NewGuid().ToString("N"));
        string from = Path.Combine(root, "old"), to = Path.Combine(root, "new");
        try
        {
            Directory.CreateDirectory(from);
            Directory.CreateDirectory(to);
            File.WriteAllText(Path.Combine(from, "fan-curve-v1.json"), "alt");
            File.WriteAllText(Path.Combine(from, "keyboard-v1.json"), "alt");
            File.WriteAllText(Path.Combine(from, "keyboard-v1.json.bak"), "kein json");
            File.WriteAllText(Path.Combine(to, "keyboard-v1.json"), "neu");

            AppData.Migrate(from, to);

            Check(File.ReadAllText(Path.Combine(to, "fan-curve-v1.json")) == "alt", "a saved curve is carried over, not lost");
            Check(File.ReadAllText(Path.Combine(to, "keyboard-v1.json")) == "neu", "a file already at the destination is the newer one and wins");
            Check(!File.Exists(Path.Combine(from, "keyboard-v1.json")), "the old copy is cleared out either way");
            Check(File.Exists(Path.Combine(from, "keyboard-v1.json.bak")), "only settings move; backups and logs stay where they are");

            // Safe to run at every start, including when there is nothing left to move.
            AppData.Migrate(from, to);
            AppData.Migrate(Path.Combine(root, "gibtsnicht"), to);
        }
        finally { Directory.Delete(root, recursive: true); }

        Console.WriteLine("PASS: app data lives beside the user's other settings, not inside the install folder");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
