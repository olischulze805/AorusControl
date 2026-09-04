using System.Text.Json;
using System.Text.Json.Serialization;

namespace AorusControl.Core.Features.Keyboard;

public interface IKeyboardSettingsStore
{
    KeyboardLightingSettings? Load();
    void Save(KeyboardLightingSettings settings);
}

/// <summary>Versioned user intent only; never serializes live frames or firmware commands.</summary>
public sealed class KeyboardSettingsStore(string filePath) : IKeyboardSettingsStore
{
    private const string DeviceKey = "AORUS-5-SE4-FB0F";
    private readonly string _path = Path.GetFullPath(filePath);
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        IgnoreReadOnlyProperties = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };

    public KeyboardLightingSettings? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 16 * 1024) throw new InvalidDataException("RGB-Datei ist zu groß.");
            Envelope? envelope = JsonSerializer.Deserialize<Envelope>(stream, Options);
            if (envelope is null || envelope.Version != 1 || envelope.Device != DeviceKey || envelope.Settings is null)
                throw new InvalidDataException("RGB-Dateiversion oder Gerätezuordnung nicht unterstützt.");
            envelope.Settings.Validate();
            return envelope.Settings;
        }
        catch (JsonException exception) { throw new InvalidDataException("RGB-Datei ist beschädigt oder enthält unbekannte Felder.", exception); }
        catch (ArgumentOutOfRangeException exception) { throw new InvalidDataException("RGB-Datei enthält ungültige Einstellungen.", exception); }
    }

    public void Save(KeyboardLightingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        string directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".rgb-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new Envelope(1, DeviceKey, settings), Options);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".bak");
            else File.Move(temporary, _path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary); // Only this call's generated temporary file.
        }
    }

    private sealed record Envelope(int Version, string Device, KeyboardLightingSettings? Settings);
}
