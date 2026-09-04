using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Models;

internal static class KeyboardFrameTests
{
    public static void Run()
    {
        foreach (byte raw in new byte[] { 0, 24, 32, 50 })
            Check(KeyboardBrightnessNotifications.TryParse(new byte[] { 4, 1, raw, 0 }, out var level) && (byte)level == raw, "known Fn+Space event");
        foreach (byte[] invalid in new byte[][] { [], [4, 1, 24], [4, 1, 40, 0], [4, 2, 24, 0], [3, 1, 24, 0], [4, 1, 24, 1], [4, 1, 24, 0, 0] })
            Check(!KeyboardBrightnessNotifications.TryParse(invalid, out _), "unknown event must not become brightness");
        int calls = 0;
        bool fail = false;
        var writer = new KeyboardFrameWriter((zone, color, brightness) =>
        {
            if (fail) throw new InvalidOperationException("simulated transport failure");
            calls++;
        });
        KeyboardRgbColor[] frame = [new(0, 255, 0), new(0, 255, 0), new(0, 255, 0)];
        Check(writer.WriteFrame(frame, 50, default) == 3, "first frame");
        for (int i = 0; i < 300; i++) Check(writer.WriteFrame(frame, 50, default) == 0, "held frame");
        Check(calls == 3, "no redundant writes");
        frame[1] = new(255, 0, 0);
        Check(writer.WriteFrame(frame, 50, default) == 1, "only changed zone");
        Check(writer.WriteFrame(frame, 24, default) == 3, "brightness affects every zone");
        frame[0] = new(0, 0, 255);
        fail = true;
        try { writer.WriteFrame(frame, 24, default); throw new Exception("failure swallowed"); }
        catch (InvalidOperationException) { }
        fail = false;
        Check(writer.WriteFrame(frame, 24, default) == 1, "failed zone retried");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        int before = calls;
        try { writer.WriteFrame(frame, 50, canceled.Token); throw new Exception("cancellation ignored"); }
        catch (OperationCanceledException) { }
        Check(calls == before, "canceled frame cannot write");
        try { writer.WriteFrame(frame, 25, default); throw new Exception("bad brightness allowed"); }
        catch (ArgumentOutOfRangeException) { }
        Check(calls == before, "validation before writes");
        Console.WriteLine("PASS: RGB changed-zone writes, held frames, brightness, failure retry and cancellation");
    }

    private static void Check(bool valid, string name)
    {
        if (!valid) throw new InvalidOperationException(name);
    }
}
