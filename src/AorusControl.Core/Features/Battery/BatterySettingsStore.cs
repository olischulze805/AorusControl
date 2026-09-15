using System.Text.Json;
using System.Text.Json.Serialization;

namespace AorusControl.Core.Features.Battery;

/// <summary>What the user last chose: standard charging, or a custom limit in percent.</summary>
public sealed record BatterySettings(bool StandardMode, int Limit)
{
    /// <summary>The firmware's own range. A file claiming anything else is not usable.</summary>
    public bool IsValid => StandardMode || Limit is >= 60 and <= 100;
}

public interface IBatterySettingsStore
{
    BatterySettings? Load();
    void Save(BatterySettings settings);
}

/// <summary>
/// Remembers the charge limit so a restart cannot quietly undo it.
///
/// This file exists because the limit lives on the embedded controller and the controller
/// does not always keep it: after a restart the machine was found charging to 97 % again
/// with the policy back at 100 %. The app used to read that state and believe it, because it
/// had nothing of its own to compare against. Now it has.
///
/// Saved only after a write the controller confirmed on read-back, so what is stored here is
/// a setting that really took effect, not one that was merely requested.
/// </summary>
public sealed class BatterySettingsStore(string filePath) : IBatterySettingsStore
{
    private readonly string _path = Path.GetFullPath(filePath);
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 4
    };

    /// <summary>The saved choice, or null when there is none or the file cannot be trusted.
    /// A damaged file is not an error worth stopping for: without it the app simply keeps
    /// what the device reports, which is exactly where it was before.</summary>
    public BatterySettings? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 8 * 1024) return null;
            Envelope? envelope = JsonSerializer.Deserialize<Envelope>(stream, Options);
            if (envelope is null || envelope.Version != 1) return null;
            var settings = new BatterySettings(envelope.StandardMode, envelope.Limit);
            return settings.IsValid ? settings : null;
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    public void Save(BatterySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsValid) throw new ArgumentOutOfRangeException(nameof(settings), "Ladelimit außerhalb von 60 bis 100 %.");
        string directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".battery-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new Envelope(1, settings.StandardMode, settings.Limit), Options);
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

    private sealed record Envelope(int Version, bool StandardMode, int Limit);
}
