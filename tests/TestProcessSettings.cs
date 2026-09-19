using System.Runtime.InteropServices;

/// <summary>
/// A failed unattended check must fail its terminal or CI job, never open a modal Windows
/// error box and wait forever for somebody to click it. Exceptions still reach stderr and
/// retain their non-zero process exit code.
/// </summary>
internal static class TestProcessSettings
{
    private const uint SemFailCriticalErrors = 0x0001;
    private const uint SemNoGpFaultErrorBox = 0x0002;
    private const uint SemNoOpenFileErrorBox = 0x8000;

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);

    public static void DisableNativeCrashDialogs() =>
        SetErrorMode(SemFailCriticalErrors | SemNoGpFaultErrorBox | SemNoOpenFileErrorBox);
}
