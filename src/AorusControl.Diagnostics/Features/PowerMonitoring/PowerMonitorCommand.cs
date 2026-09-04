using System.Diagnostics;
using System.Globalization;
using System.Text;
using AorusControl.Core.Features.PowerMonitoring;

namespace AorusControl.Diagnostics.Features.PowerMonitoring;

internal static class PowerMonitorCommand
{
    public static void Run(int durationSeconds, int intervalMilliseconds, string repositoryRoot)
    {
        using var sampler = new WindowsPowerSampler();
        using var cancellation = new CancellationTokenSource();
        using Process process = Process.GetCurrentProcess();
        TimeSpan cpuBefore = process.TotalProcessorTime;
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        var samples = new List<PowerSnapshot>();
        var clock = Stopwatch.StartNew();
        string? failure = null;
        Console.WriteLine("Verbrauchsmonitor: nur lesend, kein nvidia-smi. -- bedeutet nicht verfügbar/Basisprobe.");
        Console.WriteLine("GPU-IDs sind dynamisch; 0 % Aktivität beweist keinen abgeschalteten Adapter.");
        try
        {
            while (clock.Elapsed.TotalSeconds < durationSeconds && !cancellation.IsCancellationRequested)
            {
                PowerSnapshot sample = sampler.Read();
                samples.Add(sample);
                Console.WriteLine($"{sample.CapturedAt:HH:mm:ss}  Akku {Number(sample.BatteryDischargeWatts)} W  CPU {Number(sample.CpuPercent)} %  {GpuText(sample)}");
                foreach (string note in sample.Notes) Console.WriteLine($"  {note}");
                cancellation.Token.WaitHandle.WaitOne(intervalMilliseconds);
            }
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            Environment.ExitCode = 5;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }

        process.Refresh();
        double ownCpu = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
        var report = new StringBuilder("# Systemverbrauch – überarbeiteter Monitor\n\n");
        report.AppendLine($"- Zeitpunkt: {DateTimeOffset.Now:O}");
        report.AppendLine("- Nur lesend: Akku-WMI, Windows CPU-Zeitdifferenzen und persistente GPU-Leistungszähler; kein nvidia-smi/NVML.");
        report.AppendLine("- Akkuentladung ist Gesamtverbrauch, nicht GPU-Leistung. Netzbetrieb/ungültige Werte werden als nicht verfügbar gezeigt.");
        report.AppendLine("- GPU: am stärksten ausgelastete Engine je dynamisch erkannter Adapter-ID. Keine ungesicherte Intel/NVIDIA-Zuordnung; kein D3-Nachweis.");
        report.AppendLine("- Erste CPU/GPU-Proben und unvollständige Intervalle sind nicht verfügbar, nicht 0 %.");
        report.AppendLine($"- Laufzeit {clock.Elapsed.TotalSeconds:F1} s; Intervall {intervalMilliseconds} ms; eigene CPU-Zeit {ownCpu:F0} ms; Working Set {process.WorkingSet64 / 1048576.0:F1} MiB.");
        report.AppendLine("- Eigenverbrauchswerte gelten für diesen kurzen Diagnoselauf, nicht für die spätere App. Pro Probe ein gemeinsamer GPU-Kategoriesnapshot; neue Instanzen brauchen zunächst eine Basisprobe.");
        if (failure is not null) report.AppendLine($"- Fehler: {Escape(failure)}");
        report.AppendLine("\n| Zeitpunkt | Entladung W | CPU % | GPU-Aktivität | Abfrage ms | Hinweise |\n|---|---:|---:|---|---:|---|");
        foreach (PowerSnapshot sample in samples)
            report.AppendLine($"| {sample.CapturedAt:HH:mm:ss} | {Number(sample.BatteryDischargeWatts)} | {Number(sample.CpuPercent)} | {Escape(GpuText(sample))} | {sample.SamplingDuration.TotalMilliseconds:F0} | {Escape(string.Join("; ", sample.Notes))} |");
        report.AppendLine("\nKeine automatische Ursachenzuordnung: Aus Auslastungswerten allein lässt sich die Leistungsaufnahme einzelner Komponenten nicht bestimmen.");
        string directory = Path.Combine(repositoryRoot, "research", "runs");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"power-monitor-v2-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"Bericht: {path}");
    }

    private static string Number(double? value) => value?.ToString("F1", CultureInfo.InvariantCulture) ?? "--";
    private static string GpuText(PowerSnapshot sample) => sample.Gpus.Count == 0 ? "GPU --"
        : string.Join("; ", sample.Gpus.Select(x => $"{x.AdapterId}: {Number(x.BusiestEnginePercent)} %"));
    private static string Escape(string text) => text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
