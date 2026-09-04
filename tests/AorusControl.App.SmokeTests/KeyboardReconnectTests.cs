using System.IO;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Models;

internal static class KeyboardReconnectTests
{
    public static async Task RunAsync()
    {
        using var stop = new CancellationTokenSource();
        int attempts = 0, events = 0;
        var pauses = new List<double>();
        await KeyboardNotificationReconnect.RunAsync((callback, _) =>
        {
            attempts++;
            if (attempts == 8) callback(KeyboardBrightnessLevel.Low);
            if (attempts == 9) { stop.Cancel(); return Task.CompletedTask; }
            return Task.FromException(new IOException("Device absent"));
        }, _ => events++, (_, pause) => pauses.Add(pause.TotalSeconds), stop.Token,
            (_, _) => Task.CompletedTask);
        if (!pauses.SequenceEqual(new double[] { 1, 2, 4, 8, 16, 30, 30, 1 }) || attempts != 9 || events != 1)
            throw new Exception("Reconnect backoff/reset incorrect");

        using var cancelDuringDelay = new CancellationTokenSource();
        int reads = 0;
        await KeyboardNotificationReconnect.RunAsync((_, _) =>
        {
            reads++;
            return Task.CompletedTask; // Unexpected EOF is retried, not a successful permanent stop.
        }, _ => { }, (_, _) => { }, cancelDuringDelay.Token, async (_, token) =>
        {
            cancelDuringDelay.Cancel();
            await Task.Delay(Timeout.Infinite, token);
        });
        if (reads != 1) throw new Exception("Cancellation must prevent reopening");
        Console.WriteLine("PASS: keyboard reconnect backoff, event reset, unexpected completion and cancellation during delay");
    }
}
