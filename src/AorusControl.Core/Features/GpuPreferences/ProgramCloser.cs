using System.Diagnostics;

namespace AorusControl.Core.Features.GpuPreferences;

/// <summary>What became of an attempt to close a program.</summary>
/// <param name="Closed">Processes that are gone.</param>
/// <param name="Forced">Of those, the ones that had to be terminated because they had no
/// window to ask or did not answer in time.</param>
/// <param name="Refused">Processes still running - no rights, or a refusal this app respects.</param>
public sealed record CloseOutcome(int Closed, int Forced, int Refused);

public interface IProgramCloser
{
    Task<CloseOutcome> CloseAsync(IReadOnlyList<int> processIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Closes the processes of one program, politely first.
///
/// The standing decision in GPU-AC-BATTERY-PREFERENCES.md is that this app never ends a
/// process *on its own*. This is the other case: the user reads a list of what is holding the
/// discrete card awake, names one, and confirms. That is their machine and their call, and
/// refusing it would only send them to the Task Manager to do the same thing with less
/// information.
///
/// What it does not do is pretend the two ways of closing a program are the same. A program
/// with a window is asked, which lets it save and object; a background service has nothing to
/// ask, so it is terminated. The result says which of the two happened, because "closed" and
/// "killed" are different news.
/// </summary>
public sealed class ProgramCloser(TimeSpan? grace = null) : IProgramCloser
{
    /// <summary>How long a program that was asked politely gets to finish. Long enough for a
    /// save prompt to appear - at which point the program is still running and this reports it
    /// as refused, which is the truth.</summary>
    private readonly TimeSpan _grace = grace ?? TimeSpan.FromSeconds(4);

    public async Task<CloseOutcome> CloseAsync(IReadOnlyList<int> processIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processIds);
        int closed = 0, forced = 0, refused = 0;

        // Asked first, all of them, before anything is forced: a browser's tabs share one
        // window, and terminating half of them while the rest are still deciding is how a
        // program loses what it was about to save.
        var waiting = new List<Process>();
        foreach (int id in processIds)
        {
            Process? process = Find(id);
            if (process is null) { closed++; continue; }
            try
            {
                // A program without a window has nothing to be asked; it goes straight to the
                // wait below and is terminated when that runs out.
                if (process.MainWindowHandle != IntPtr.Zero) process.CloseMainWindow();
                waiting.Add(process);
            }
            catch (Exception exception) when (Expected(exception)) { refused++; process.Dispose(); }
        }

        foreach (Process process in waiting)
        {
            using (process)
            {
                try
                {
                    await process.WaitForExitAsync(cancellationToken).WaitAsync(_grace, cancellationToken).ConfigureAwait(false);
                    closed++;
                }
                catch (TimeoutException)
                {
                    // It has no window to ask, or it asked the user something and is waiting.
                    // Either way the request stands: the point of pressing the button was to
                    // get the card back.
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        closed++;
                        forced++;
                    }
                    catch (Exception exception) when (Expected(exception)) { refused++; }
                }
                catch (Exception exception) when (Expected(exception)) { refused++; }
            }
        }
        return new CloseOutcome(closed, forced, refused);
    }

    private static Process? Find(int id)
    {
        try { return Process.GetProcessById(id); }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    /// <summary>The ways a process can slip away or refuse. Anything else is a fault worth
    /// seeing rather than counting as "refused".</summary>
    private static bool Expected(Exception exception) =>
        exception is InvalidOperationException or System.ComponentModel.Win32Exception
            or NotSupportedException or ArgumentException;
}
