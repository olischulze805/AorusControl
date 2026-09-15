using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace AorusControl.Core.Features.Startup;

public interface IStartupManager
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);
    Task EnableAsync(CancellationToken cancellationToken = default);
    Task DisableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings an already-enabled autostart up to the current definition, and does nothing at
    /// all when autostart is off. Returns true when it actually rewrote something.
    /// </summary>
    Task<bool> RepairAsync(CancellationToken cancellationToken = default);
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
///
/// The task is registered from <see cref="StartupTaskDefinition"/> rather than from
/// <c>schtasks</c>'s own switches - see that file for what those switches quietly decided
/// on our behalf, and why none of it suited a program that watches the fans.
/// </summary>
public sealed class StartupManager(string executablePath, string taskName = "AorusControl") : IStartupManager
{
    private readonly string _executablePath = executablePath;
    private readonly string _taskName = taskName;

    public static string[] QueryArguments(string taskName) =>
        ["/Query", "/TN", taskName];

    public static string[] ExportArguments(string taskName) =>
        ["/Query", "/TN", taskName, "/XML", "ONE"];

    /// <summary>The argument that tells the app it was started by this task, so it goes
    /// straight to the tray. An autostarting tool that throws its window in your face at
    /// every login is the reason people disable autostart.</summary>
    public const string BackgroundStartArgument = StartupTaskDefinition.BackgroundStartArgument;

    public static string[] CreateArguments(string taskName, string xmlPath) =>
        ["/Create", "/TN", taskName, "/XML", xmlPath, "/F"];

    public static string[] DeleteArguments(string taskName) =>
        ["/Delete", "/TN", taskName, "/F"];

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        (int exitCode, _) = await RunSchtasksAsync(QueryArguments(_taskName), cancellationToken).ConfigureAwait(false);
        return exitCode == 0;
    }

    public async Task EnableAsync(CancellationToken cancellationToken = default)
    {
        // A temporary file rather than a pipe: schtasks reads the definition from a path, and
        // it insists on UTF-16 - handed UTF-8 it reports a malformed task rather than a
        // charset problem, which is a confusing half hour for whoever meets it next.
        string path = Path.Combine(Path.GetTempPath(), $"aorus-autostart-{Guid.NewGuid():N}.xml");
        try
        {
            await File.WriteAllTextAsync(path,
                StartupTaskDefinition.Build(_executablePath, CurrentUserId()), Encoding.Unicode, cancellationToken)
                .ConfigureAwait(false);
            (int exitCode, string output) = await RunSchtasksAsync(CreateArguments(_taskName, path), cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
                throw new InvalidOperationException($"Autostart-Aufgabe konnte nicht angelegt werden (schtasks-Code {exitCode}). {output}".Trim());
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* A leftover temp file is not worth failing over. */ }
        }
    }

    public async Task DisableAsync(CancellationToken cancellationToken = default)
    {
        (int exitCode, _) = await RunSchtasksAsync(DeleteArguments(_taskName), cancellationToken).ConfigureAwait(false);
        // Exit code 1 from /Delete on a task that is already gone is treated as success:
        // "disabled" must be idempotent, not fail just because it was already off.
        if (exitCode != 0 && await IsEnabledAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException($"Autostart-Aufgabe konnte nicht entfernt werden (schtasks-Code {exitCode}).");
    }

    /// <summary>
    /// Replaces an autostart task that an older build wrote, or one whose executable has
    /// moved - an update does move it. Never switches autostart on by itself: a user who
    /// turned it off stays turned off, and finding it back on after an update would be the
    /// worse surprise by far.
    /// </summary>
    public async Task<bool> RepairAsync(CancellationToken cancellationToken = default)
    {
        (int exitCode, string exported) = await RunSchtasksAsync(ExportArguments(_taskName), cancellationToken).ConfigureAwait(false);
        if (exitCode != 0) return false;
        if (StartupTaskDefinition.Matches(exported, _executablePath)) return false;
        await EnableAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static string CurrentUserId() => WindowsIdentity.GetCurrent().Name;

    private static async Task<(int ExitCode, string Output)> RunSchtasksAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
            // The encoding is left alone on purpose. The exported XML declares UTF-16 in its
            // own header, but schtasks writes the bytes in the console code page - verified
            // on this machine on 2026-09-15. Forcing UTF-16 here turns the whole export into
            // mojibake; the default decoding is the one that matches what it actually writes.
        };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("schtasks.exe konnte nicht gestartet werden.");
        // Both streams must be drained concurrently with waiting for exit: schtasks output
        // is normally tiny, but an unread pipe can fill and deadlock the child process
        // against a parent that is only awaiting WaitForExitAsync.
        Task<string> stdOut = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stdErr = process.StandardError.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stdOut, stdErr).ConfigureAwait(false);
        return (process.ExitCode, (await stdOut + await stdErr).Trim());
    }
}
