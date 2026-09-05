using System.Runtime.InteropServices;

namespace AorusControl.Core.Features.Diagnostics;

/// <summary>
/// Lets a windowless (WinExe) process still write to the console it was started from.
///
/// The worker is a WinExe on purpose: it runs elevated for as long as Fixed mode is held,
/// and as a console Exe that meant a black window sitting in the user's way the whole
/// time - the single most-reported annoyance of tools in this category. WinExe alone would
/// silence its CLI modes, so this reattaches to the parent console when there is one.
/// With no console (the normal case, launched by the app) every write simply goes nowhere,
/// which is correct: those messages also go to <see cref="AppLog"/>.
/// </summary>
public static class ConsoleAttach
{
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    public static void ToParentIfAny()
    {
        // Redirected output (a test harness reading stdout) already works and must not be
        // replaced by a console handle.
        if (Console.IsOutputRedirected || !AttachConsole(AttachParentProcess)) return;
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }
}
