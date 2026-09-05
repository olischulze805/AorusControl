using System.Globalization;
using System.Text;

namespace AorusControl.Core.Features.Diagnostics;

/// <summary>
/// Always-on plain-text log, so a failure is never gone by the time someone wants to look
/// at it. Both processes write here - the app and the elevated hardware worker - into
/// separate daily files under <c>%LocalAppData%\AorusControl\logs</c>, because the worker
/// runs elevated and its console is invisible in normal use: without this, its side of a
/// failed request left no trace at all.
///
/// Two rules this must never break, since it exists precisely for the moments when things
/// are already going wrong:
/// - It never throws. A logger that can fail the operation it was meant to explain is
///   worse than no logger, so every I/O error is swallowed.
/// - It never blocks the caller on anything but a short lock, and never grows without
///   bound: files older than <see cref="RetentionDays"/> days are removed on start.
/// </summary>
public static class AppLog
{
    private const int RetentionDays = 14;
    private static readonly object Gate = new();
    private static string _role = "app";
    private static bool _prepared;
    private static bool _started;

    /// <summary>The folder shown to the user, so "where are the logs" has one answer.</summary>
    public static string Directory { get; } = Path.Combine(AppData.Directory, "logs");

    /// <param name="role">Distinguishes the writers, e.g. "app" or "worker"; each gets its
    /// own file so the two processes never contend for one handle.</param>
    public static void Initialize(string role)
    {
        lock (Gate)
        {
            _role = string.IsNullOrWhiteSpace(role) ? "app" : role;
            _prepared = false;
            _started = true;
        }
        Info("start", $"AORUS Control ({_role}) gestartet.");
    }

    public static void Info(string area, string message) => Write("INFO ", area, message, null);

    public static void Warn(string area, string message) => Write("WARN ", area, message, null);

    public static void Error(string area, string message, Exception? error = null) => Write("ERROR", area, message, error);

    private static void Write(string level, string area, string message, Exception? error)
    {
        // Nothing is written before a process says who it is. Only the app and the worker do
        // that; the test suites never do, which is what keeps their invented failures - fake
        // hardware, simulated write errors - out of the log the user reads when something
        // real goes wrong.
        lock (Gate) { if (!_started) return; }

        try
        {
            var line = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(" [").Append(level).Append("] ")
                .Append(area.PadRight(10))
                .Append(' ')
                .Append(Collapse(message));
            if (error is not null)
            {
                line.Append(" | ").Append(error.GetType().Name).Append(": ").Append(Collapse(error.Message));
                if (error.StackTrace is { } stack) line.Append(" | ").Append(Collapse(stack));
            }

            lock (Gate)
            {
                Prepare();
                File.AppendAllText(CurrentPath(), line.Append(Environment.NewLine).ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Never let logging break the thing it was logging about.
        }
    }

    private static string CurrentPath() =>
        Path.Combine(Directory, $"{_role}-{DateTime.Now:yyyy-MM-dd}.log");

    private static void Prepare()
    {
        if (_prepared) return;
        System.IO.Directory.CreateDirectory(Directory);
        DateTime cutoff = DateTime.Now.AddDays(-RetentionDays);
        foreach (string file in System.IO.Directory.EnumerateFiles(Directory, "*.log"))
        {
            try { if (File.GetLastWriteTime(file) < cutoff) File.Delete(file); }
            catch { /* A file someone else holds open stays; it will be retried tomorrow. */ }
        }

        _prepared = true;
    }

    /// <summary>Keeps one event on one line, so the file stays greppable.</summary>
    private static string Collapse(string value) =>
        value.Replace("\r", " ").Replace("\n", " ").Trim();
}
