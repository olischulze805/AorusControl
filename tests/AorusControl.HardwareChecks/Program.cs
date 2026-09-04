using System.Diagnostics;
using System.Text;
using AorusControl.Core.Features.Keyboard;

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
