using System.Diagnostics;
using System.Text;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Features.PowerMonitoring;
using AorusControl.Core.Features.Startup;

// Registers the real autostart definition under a throwaway name, reads it back out of
// Windows, and deletes it again. The unit test can only prove the file says what we meant;
// only the Task Scheduler can prove it accepts the schema and keeps the settings.
//
// One deviation, and only one: the probe asks for LeastPrivilege instead of
// HighestAvailable, because registering an elevated task needs elevation and this check is
// meant to be runnable without it. That single element is also the one the previous, working
// definition already carried, so it is the least interesting one to verify.
if (args.SequenceEqual(new[] { "--autostart-definition-check" }))
{
    const string probeTask = "AorusControlDefinitionProbe";
    string executable = Environment.ProcessPath ?? "AorusControl.exe";
    string xmlPath = Path.Combine(Path.GetTempPath(), "aorus-autostart-probe.xml");
    await File.WriteAllTextAsync(xmlPath,
        StartupTaskDefinition.Build(executable, System.Security.Principal.WindowsIdentity.GetCurrent().Name)
            .Replace("HighestAvailable", "LeastPrivilege"),
        System.Text.Encoding.Unicode);

    static async Task<(int Code, string Output)> Schtasks(string arguments)
    {
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("schtasks.exe")
        {
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        string output = await process.StandardOutput.ReadToEndAsync() + await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, output);
    }

    try
    {
        (int created, string createOutput) = await Schtasks($"/Create /TN {probeTask} /XML \"{xmlPath}\" /F");
        Console.WriteLine($"anlegen: Code {created} {createOutput.Trim()}");
        if (created != 0) return 1;

        (int _, string xml) = await Schtasks($"/Query /TN {probeTask} /XML ONE");
        bool allPresent = true;
        foreach (string expected in new[]
        {
            "<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>",
            "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>",
            "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>",
            "<Priority>5</Priority>",
            "<LogonTrigger>",
            "<Arguments>--background</Arguments>"
        })
        {
            bool found = xml.Contains(expected, StringComparison.Ordinal);
            allPresent &= found;
            Console.WriteLine($"{(found ? "OK   " : "FEHLT")} {expected}");
        }
        bool recognised = StartupTaskDefinition.Matches(xml, executable);
        Console.WriteLine($"{(recognised ? "OK   " : "FEHLT")} Matches erkennt die eigene Aufgabe wieder");
        return allPresent && recognised ? 0 : 1;
    }
    finally
    {
        await Schtasks($"/Delete /TN {probeTask} /F");
        if (File.Exists(xmlPath)) File.Delete(xmlPath);
        Console.WriteLine($"entfernt: {probeTask}");
    }
}

if (args.SequenceEqual(new[] { "--dashboard-power-read-only" }))
{
    var reader = new DashboardPowerReader();
    var lines = new StringBuilder("# Dashboard power live check\n\nWindows Energy Meter + PnP for CPU watts and the device state; NVML for GPU watts, and only on AC with the card already in D0. No device setters.\n\n");
    bool sawCpu = false;
    for (int i = 0; i < 8; i++)
    {
        var watch = Stopwatch.StartNew();
        DashboardPowerReading reading = reader.Read();
        sawCpu |= reading.CpuPackageWatts is > 0;
        string gpu = reading.GpuWatts is { } gpuWatts ? $"GPU {gpuWatts:F2} W" : "GPU --";
        BatteryFlow flow = reading.Battery ?? BatteryFlow.Unknown;
        string battery = $"Akku {flow.Direction} {(flow.Watts is { } flowWatts ? $"{flowWatts:F2} W" : "--")} bei {flow.Percent:F0} %";
        string line = $"{DateTimeOffset.Now:O}: CPU {reading.CpuPackageWatts:F3} W; {gpu}; {battery}; {reading.GpuStatus}; query {watch.Elapsed.TotalMilliseconds:F2} ms";
        Console.WriteLine(line);
        lines.AppendLine(line);
        if (i < 7) await Task.Delay(2000);
    }
    lines.AppendLine("A GPU watt column that stays empty means the gate held: either the machine is on battery or the card was not in D0. Repeated D3 is evidence that these queries did not leave the GPU in D0; it cannot exclude unobserved brief transitions. Not a battery power comparison.");
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
