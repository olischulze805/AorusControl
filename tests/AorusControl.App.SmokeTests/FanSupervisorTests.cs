using AorusControl.Core.Features.Cooling;

internal static class FanSupervisorTests
{
    public static async Task RunAsync()
    {
        var time = new TestClock();
        var reader = new FakeReader { Timestamp = time.GetUtcNow };
        var fans = new FakeFan();
        var supervisor = new FanSafetySupervisor(fans, reader, time);
        await MustFail(async () => { await supervisor.AcquireFixedAsync(114); });
        using var cancellation = new CancellationTokenSource();
        Task worker = supervisor.RunAsync(cancellation.Token);
        await supervisor.TickAsync();
        Check(fans.NormalWrites == 0, "idle supervisor does not write");
        Guid first = await supervisor.AcquireFixedAsync(114);
        Check(fans.FixedWrites == 1, "acquire fixed");
        await MustFail(() => supervisor.RenewAsync(Guid.NewGuid()));
        Check(fans.NormalWrites == 0, "wrong client must not affect active lease");
        time.Advance(8);
        await supervisor.RenewAsync(first);
        time.Advance(8);
        await supervisor.TickAsync();
        Check((await supervisor.ReadStatusAsync()).RequiresRestoration, "fresh lease still active");
        time.Advance(2);
        await supervisor.TickAsync();
        Check(!(await supervisor.ReadStatusAsync()).RequiresRestoration && fans.NormalWrites == 1, "expiry returns normal");
        await MustFail(() => supervisor.RenewAsync(first));

        Guid second = await supervisor.AcquireFixedAsync(114);
        time.Advance(10);
        await MustFail(() => supervisor.RenewAsync(second));
        Check(fans.NormalWrites == 2, "expired lease cannot be resurrected before tick");

        await supervisor.AcquireFixedAsync(114);
        reader.Temperature = 65;
        fans.FailNormal = true;
        await supervisor.TickAsync();
        FanSafetyStatus failed = await supervisor.ReadStatusAsync();
        Check(failed.RequiresRestoration && failed.Lease is null && failed.Message.Contains("ACHTUNG"), "failed recovery revokes lease, retains fault");
        fans.FailNormal = false;
        await supervisor.TickAsync();
        Check(!(await supervisor.ReadStatusAsync()).RequiresRestoration, "next tick retries restoration");

        reader.Temperature = 50;
        await supervisor.AcquireFixedAsync(114);
        reader.Timestamp = () => time.GetUtcNow().AddSeconds(-6);
        await supervisor.TickAsync();
        Check(!(await supervisor.ReadStatusAsync()).RequiresRestoration, "stale data restores normal");
        await MustFail(async () => { await supervisor.AcquireFixedAsync(114); });
        reader.Timestamp = time.GetUtcNow;
        await supervisor.AcquireFixedAsync(114);
        reader.Fail = true;
        await supervisor.TickAsync();
        Check(!(await supervisor.ReadStatusAsync()).RequiresRestoration, "telemetry failure restores normal");
        reader.Fail = false;
        Guid last = await supervisor.AcquireFixedAsync(114);
        await supervisor.ReleaseAsync(last);
        Check(!(await supervisor.ReadStatusAsync()).RequiresRestoration, "explicit release");

        await supervisor.AcquireFixedAsync(114);
        cancellation.Cancel();
        await worker;
        Check(!(await supervisor.ReadStatusAsync()).RequiresRestoration, "worker cancellation restores before exit");
        await MustFail(async () => { await supervisor.AcquireFixedAsync(114); });
        Console.WriteLine("PASS: independent fan supervisor leases, stale/hot/failed telemetry, failed restore retry and worker shutdown");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static async Task MustFail(Func<Task> action)
    {
        try { await action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected operation rejection");
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _utc = DateTimeOffset.UtcNow;
        private long _ticks;
        public override DateTimeOffset GetUtcNow() => _utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(int seconds) { _utc = _utc.AddSeconds(seconds); _ticks += TimeSpan.FromSeconds(seconds).Ticks; }
    }
}
