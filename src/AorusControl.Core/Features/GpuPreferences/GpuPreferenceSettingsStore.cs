using System.Text.Json;
using System.Text.Json.Serialization;

namespace AorusControl.Core.Features.GpuPreferences;

public interface IGpuPreferenceSettingsStore
{
    IReadOnlyList<ManagedProgram> Load();
    void Save(IReadOnlyList<ManagedProgram> programs);
}

/// <summary>
/// Which programs the user put under automatic switching, and what Windows had set for each
/// before this app first touched it.
///
/// That second half is the whole reason this file exists rather than just a list of paths:
/// taking a program off the list has to be able to hand its original setting back, and after
/// a restart the app would otherwise have nothing to hand back.
/// </summary>
public sealed class GpuPreferenceSettingsStore(string filePath) : IGpuPreferenceSettingsStore
{
    private readonly string _path = Path.GetFullPath(filePath);
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    public IReadOnlyList<ManagedProgram> Load()
    {
        if (!File.Exists(_path)) return [];
        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 64 * 1024) throw new InvalidDataException("Die Datei mit den Grafikzuordnungen ist zu groß.");
            Envelope? envelope = JsonSerializer.Deserialize<Envelope>(stream, Options);
            if (envelope is null || envelope.Version != 1 || envelope.Programs is null)
                throw new InvalidDataException("Dateiversion der Grafikzuordnungen wird nicht unterstützt.");
            foreach (Entry entry in envelope.Programs)
            {
                if (string.IsNullOrWhiteSpace(entry.Name) || !GpuPreferenceText.IsProgramEntry(entry.Name))
                    throw new InvalidDataException("Die Datei enthält einen ungültigen Programmeintrag.");
                if (entry.Original is { } original && !Enum.IsDefined(original))
                    throw new InvalidDataException("Die Datei enthält eine unbekannte Grafikeinstellung.");
            }
            return envelope.Programs.Select(entry => new ManagedProgram(entry.Name, entry.Original)).ToArray();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Die Datei mit den Grafikzuordnungen ist beschädigt.", exception);
        }
    }

    public void Save(IReadOnlyList<ManagedProgram> programs)
    {
        ArgumentNullException.ThrowIfNull(programs);
        string directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".gpu-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream,
                    new Envelope(1, programs.Select(program => new Entry(program.Name, program.Original)).ToArray()), Options);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".bak");
            else File.Move(temporary, _path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private sealed record Entry(string Name, GpuPreference? Original);
    private sealed record Envelope(int Version, IReadOnlyList<Entry>? Programs);
}
