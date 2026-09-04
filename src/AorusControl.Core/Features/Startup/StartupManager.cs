using System.Diagnostics;

namespace AorusControl.Core.Features.Startup;

public interface IStartupManager
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);
    Task EnableAsync(CancellationToken cancellationToken = default);
    Task DisableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Autostart via a Scheduled Task ("At log on", "Run with highest privileges") instead of
/// the classic HKCU\...\Run registry key. The app's manifest requires administrator, so a
/// Run-key entry would show a fresh UAC prompt on every single login - exactly the kind of
/// nag well-known RGB/OC tools get criticized for. A task already marked to run elevated
/// starts silently instead, because Windows takes the elevation decision from the task's
/// own settings rather than asking again at trigger time. Creating/removing the task
/// itself needs no extra prompt: this process is already elevated (the manifest again),
/// and schtasks operating in the caller's own context does not re-elevate.
/// </summary>
public sealed class StartupManager(string executablePath, string taskName = "AorusControl") : IStartupManager
{
    private readonly string _executablePath = executablePath;
    private readonly string _taskName = taskName;

    public static string[] QueryArguments(string taskName) =>
        ["/Query", "/TN", taskName];

    public static string[] CreateArguments(string taskName, string executablePath) =>
        ["/Create", "/TN", taskName, "/TR", $"\"{executablePath}\"", "/SC", "ONLOGON", "/RL", "HIGHEST", "/F"];

    public static string[] DeleteArguments(string taskName) =>
        ["/Delete", "/TN", taskName, "/F"];

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        int exitCode = await RunSchtasksAsync(QueryArguments(_taskName), cancellationToken).ConfigureAwait(false);
        return exitCode == 0;
    }

    public async Task EnableAsync(CancellationToken cancellationToken = default)
    {
        int exitCode = await RunSchtasksAsync(CreateArguments(_taskName, _executablePath), cancellationToken).ConfigureAwait(false);
        if (exitCode != 0) throw new InvalidOperationException($"Autostart-Aufgabe konnte nicht angelegt werden (schtasks-Code {exitCode}).");
    }

    public async Task DisableAsync(CancellationToken cancellationToken = default)
    {
        int exitCode = await RunSchtasksAsync(DeleteArguments(_taskName), cancellationToken).ConfigureAwait(false);
        // Exit code 1 from /Delete on a task that is already gone is treated as success:
        // "disabled" must be idempotent, not fail just because it was already off.
        if (exitCode != 0 && await IsEnabledAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException($"Autostart-Aufgabe konnte nicht entfernt werden (schtasks-Code {exitCode}).");
    }

    private static async Task<int> RunSchtasksAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("schtasks.exe konnte nicht gestartet werden.");
        // Both streams must be drained concurrently with waiting for exit: schtasks output
        // is normally tiny, but an unread pipe can fill and deadlock the child process
        // against a parent that is only awaiting WaitForExitAsync.
        Task<string> stdOut = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stdErr = process.StandardError.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stdOut, stdErr).ConfigureAwait(false);
        return process.ExitCode;
    }
}
