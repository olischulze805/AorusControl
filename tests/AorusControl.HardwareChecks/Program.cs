using System.Diagnostics;
using System.Text;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Features.PowerMonitoring;

if (args.SequenceEqual(new[] { "--dashboard-power-read-only" }))
{
    var reader = new DashboardPowerReader();
    var lines = new StringBuilder("# Dashboard power live check\n\nWindows Energy Meter + PnP only. No NVAPI/NVML, no device setters.\n\n");
    bool sawCpu = false;
    for (int i = 0; i < 8; i++)
    {
        var watch = Stopwatch.StartNew();
        DashboardPowerReading reading = reader.Read();
        sawCpu |= reading.CpuPackageWatts is > 0;
        string line = $"{DateTimeOffset.Now:O}: CPU {reading.CpuPackageWatts:F3} W; {reading.GpuStatus}; query {watch.Elapsed.TotalMilliseconds:F2} ms";
        Console.WriteLine(line);
        lines.AppendLine(line);
        if (i < 7) await Task.Delay(2000);
    }
    lines.AppendLine("Repeated D3 is evidence that these queries did not leave the GPU in D0; it cannot exclude unobserved brief transitions. Not a battery power comparison.");
    Directory.CreateDirectory("research/runs");
    string output = $"research/runs/dashboard-power-{DateTime.Now:yyyyMMdd-HHmmss}.md";
    await File.WriteAllTextAsync(output, lines.ToString());
    Console.WriteLine(output);
    return sawCpu ? 0 : 1;
}

// Explicit opt-in, read-only hardware check. No controller setters are instantiated.
if (args.Length != 1 || args[0] != "--brightness-read-only")
{
    Console.WriteLine("Use --brightness-read-only for an 8-second passive event check.");
    return 2;
}
var report = new StringBuilder("# Live brightness listener check\n\n");
report.AppendLine($"Started: {DateTimeOffset.Now:O}");
report.AppendLine("Read-only: no RGB/fan/battery writes. Only allowlisted brightness events.");
var process = Process.GetCurrentProcess();
TimeSpan cpuBefore = process.TotalProcessorTime;
var clock = Stopwatch.StartNew();
using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8));
int events = 0;
int result = 0;
try
{
    await new KeyboardBrightnessNotifications().RunAsync(level =>
    {
        events++;
        report.AppendLine($"Event at {clock.Elapsed.TotalSeconds:F3} s: {level} ({(byte)level})");
    }, stop.Token);
    report.AppendLine("Listener returned normally after cancellation; device open/read loop did not report an error.");
}
catch (Exception error)
{
    report.AppendLine($"FAILED: {error.GetType().Name}: {error.Message}");
    result = 1;
}
clock.Stop();
report.AppendLine($"Events: {events}. No events does not verify physical Fn+Space handling.");
report.AppendLine($"Elapsed: {clock.Elapsed.TotalSeconds:F3} s; process CPU delta: {(process.TotalProcessorTime - cpuBefore).TotalMilliseconds:F1} ms.");
string directory = Path.GetFullPath("research/runs");
Directory.CreateDirectory(directory);
string path = Path.Combine(directory, $"brightness-listener-live-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.md");
using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
using (var writer = new StreamWriter(file)) writer.Write(report.ToString());
Console.WriteLine(report.ToString());
Console.WriteLine(path);
return result;
