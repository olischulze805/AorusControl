using System.Text.Json;
using System.Text.Json.Serialization;
using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Cooling;

public interface IFanCurveStore
{
    IReadOnlyList<FanCurvePoint>? Load();
    void Save(IReadOnlyList<FanCurvePoint> curve);
}

/// <summary>
/// Persists exactly one user-edited 15-point curve so "Dynamic" mode has a known,
/// user-chosen shape to activate instead of whatever the EC's curve table happened to
/// hold last (e.g. from an earlier vendor tool). Same safe-write/versioned pattern as
/// KeyboardSettingsStore: atomic replace with a .bak, strict versioned JSON, never
/// silently accepts an out-of-range or malformed file.
/// </summary>
public sealed class FanCurveStore(string filePath) : IFanCurveStore
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

    public IReadOnlyList<FanCurvePoint>? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 16 * 1024) throw new InvalidDataException("Kurvendatei ist zu groß.");
            Envelope? envelope = JsonSerializer.Deserialize<Envelope>(stream, Options);
            if (envelope is null || envelope.Version != 1 || envelope.Device != DeviceKey || envelope.Curve is null)
                throw new InvalidDataException("Kurvendateiversion oder Gerätezuordnung nicht unterstützt.");
            FanCurveValidation.Validate(envelope.Curve);
            return envelope.Curve;
        }
        catch (JsonException exception) { throw new InvalidDataException("Kurvendatei ist beschädigt oder enthält unbekannte Felder.", exception); }
        catch (ArgumentException exception) { throw new InvalidDataException("Kurvendatei enthält ungültige Werte.", exception); }
    }

    public void Save(IReadOnlyList<FanCurvePoint> curve)
    {
        FanCurveValidation.Validate(curve);
        string directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".curve-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new Envelope(1, DeviceKey, curve), Options);
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

    private sealed record Envelope(int Version, string Device, IReadOnlyList<FanCurvePoint>? Curve);
}
