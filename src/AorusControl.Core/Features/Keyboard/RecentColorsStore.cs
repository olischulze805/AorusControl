using System.Text.Json;
using System.Text.Json.Serialization;
using AorusControl.Core.Models;

namespace AorusControl.Core.Features.Keyboard;

public interface IRecentColorsStore
{
    IReadOnlyList<KeyboardRgbColor> Load();
    void Save(IReadOnlyList<KeyboardRgbColor> colors);
}

/// <summary>
/// Small most-recently-used color list for the RGB color picker. Deliberately separate
/// from KeyboardSettingsStore (which holds the *active* zone colors): this is picker
/// convenience state, never applied to the device on its own and never required to be
/// present, so it can fail soft - a missing or corrupt file just means an empty list
/// rather than blocking color selection.
/// </summary>
public sealed class RecentColorsStore(string filePath, int capacity = 12) : IRecentColorsStore
{
    private readonly string _path = Path.GetFullPath(filePath);
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 4
    };

    public IReadOnlyList<KeyboardRgbColor> Load()
    {
        if (!File.Exists(_path)) return [];
        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 4 * 1024) return [];
            Envelope? envelope = JsonSerializer.Deserialize<Envelope>(stream, Options);
            if (envelope is null || envelope.Version != 1 || envelope.Colors is null) return [];
            return envelope.Colors
                .Where(hex => TryParseHex(hex, out _))
                .Select(hex => { TryParseHex(hex, out KeyboardRgbColor color); return color; })
                .Take(capacity)
                .ToArray();
        }
        catch (JsonException) { return []; } // Convenience data only: never surface a hard failure to the user.
    }

    public void Save(IReadOnlyList<KeyboardRgbColor> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        string directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".colors-{Guid.NewGuid():N}.tmp");
        var envelope = new Envelope(1, colors.Take(capacity).Select(c => c.Hex).ToArray());
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, envelope, Options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static bool TryParseHex(string? hex, out KeyboardRgbColor color)
    {
        color = default;
        if (hex is not { Length: 7 } || hex[0] != '#') return false;
        if (!byte.TryParse(hex.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r)) return false;
        if (!byte.TryParse(hex.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g)) return false;
        if (!byte.TryParse(hex.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b)) return false;
        color = new KeyboardRgbColor(r, g, b);
        return true;
    }

    private sealed record Envelope(int Version, string[]? Colors);
}
