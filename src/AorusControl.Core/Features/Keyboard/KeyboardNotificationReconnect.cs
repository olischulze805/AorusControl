using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Keyboard;

public static class KeyboardNotificationReconnect
{
    public static async Task RunAsync(
        Func<Action<KeyboardBrightnessLevel>, CancellationToken, Task> listen,
        Action<KeyboardBrightnessLevel> onBrightness,
        Action<Exception, TimeSpan> onRetry,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= Task.Delay;
        int failures = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await listen(level => { failures = 0; onBrightness(level); }, cancellationToken).ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested) return;
                    throw new IOException("Helligkeits-Ereignisleser unerwartet beendet.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
                catch (Exception error)
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    TimeSpan pause = TimeSpan.FromSeconds(Math.Min(30, 1 << Math.Min(failures++, 5)));
                    onRetry(error, pause);
                    // Await disposal/completion of the old source before delaying and reopening.
                    await delay(pause, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }
}
