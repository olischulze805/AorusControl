using System.Diagnostics;
using AorusControl.Core.Features.Diagnostics;

namespace AorusControl.Core.Features.Worker;

/// <summary>
/// Ensures a hardware worker is listening on this user's pipe. Starts it as a detached
/// process (no Job Object, no parent/child lifetime link), which is the entire point:
/// the worker must keep running after this process exits or crashes.
/// </summary>
public static class WorkerLauncher
{
    public static async Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default)
    {
        if (WorkerClient.IsRunning()) return true;
        string? executable = FindExecutable();
        if (executable is null)
        {
            AppLog.Error("worker", "Keine AorusControl.Worker.exe gefunden; Fixed-Modus ist ohne sie nicht absicherbar.");
            return false;
        }

        AppLog.Info("worker", $"Kein Worker aktiv, starte {executable} (erhöhte Rechte, UAC-Abfrage folgt).");
        try
        {
            // Fixed-mode writes need administrator rights (the same gate the battery
            // charge setter already uses), so the worker itself must run elevated, not
            // just the one-off request. UseShellExecute+Verb triggers the normal UAC
            // prompt; ShellExecute cannot set CreateNoWindow, which is why the worker is a
            // WinExe - it never allocates a console to begin with.
            Process.Start(new ProcessStartInfo(executable, "--serve")
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(executable)
            });
        }
        catch (Exception error)
        {
            // Most commonly: the user declined the UAC prompt.
            AppLog.Error("worker", "Worker konnte nicht gestartet werden (UAC abgelehnt?).", error);
            return false;
        }

        for (int attempt = 0; attempt < 20 && !cancellationToken.IsCancellationRequested; attempt++)
        {
            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            if (!WorkerClient.IsRunning()) continue;
            AppLog.Info("worker", $"Worker antwortet nach {(attempt + 1) * 150} ms auf der Pipe.");
            return true;
        }

        AppLog.Error("worker", "Worker wurde gestartet, hat aber innerhalb von 3 s keine Pipe geöffnet.");
        return false;
    }

    private static string? FindExecutable()
    {
        const string fileName = "AorusControl.Worker.exe";
        string appDirectory = AppContext.BaseDirectory;

        // Production layout candidates: worker deployed next to, or one folder beside, the app.
        foreach (string candidate in new[]
        {
            Path.Combine(appDirectory, fileName),
            Path.Combine(appDirectory, "..", "AorusControl.Worker", fileName)
        })
        {
            string full = Path.GetFullPath(candidate);
            if (File.Exists(full)) return full;
        }

        // Development tree: walk up to the solution and use its own build output. Take the
        // MOST RECENTLY BUILT configuration rather than preferring Release: a Release
        // worker left over from an earlier day would otherwise be launched against
        // freshly built app code, and the mismatch shows up as an unhelpful protocol
        // failure at the moment the user asks for Fixed mode. Production layouts have
        // exactly one candidate, so this only ever matters while developing.
        var directory = new DirectoryInfo(appDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AorusControl.slnx")))
            {
                return new[] { "Release", "Debug" }
                    .Select(configuration => Path.Combine(
                        directory.FullName, "src", "AorusControl.Worker", "bin", configuration,
                        "net10.0-windows", fileName))
                    .Where(File.Exists)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }

            directory = directory.Parent;
        }

        return null;
    }
}
