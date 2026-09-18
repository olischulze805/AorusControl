using System.Text.Json;
using System.Text.Json.Serialization;

namespace AorusControl.Core.Features.PowerProfiles;

/// <param name="LeftoverScheme">A scheme this app created and did not get to delete, because
/// it was killed while the saver was on. Kept so the next start can clean it up: a stray entry
/// in the user's plan list is harmless but untidy, and untidy is how people lose trust in a
/// tool that edits system settings.</param>
public sealed record BatterySaverSettings(bool Enabled, uint ProcessorCap, uint Brightness, Guid? LeftoverScheme = null)
{
    public static readonly BatterySaverSettings Default = new(false, 50, 30);
}

public interface IBatterySaverSettingsStore
{
    BatterySaverSettings Load();
    void Save(BatterySaverSettings settings);
}

/// <summary>Versioned JSON beside the other stores, with the same rule: a file that cannot be
/// read is not an error, it is "no opinion" - and the defaults are the opinion then.</summary>
public sealed class BatterySaverSettingsStore(string filePath) : IBatterySaverSettingsStore
{
    private readonly string _path = Path.GetFullPath(filePath);
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    public BatterySaverSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return BatterySaverSettings.Default;
            Envelope? envelope = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(_path), Options);
            if (envelope is null || envelope.Version != 1) return BatterySaverSettings.Default;
            return new BatterySaverSettings(
                envelope.Enabled,
                BatterySaverPlan.Clamp(envelope.ProcessorCap, BatterySaverPlan.LowestProcessorCap),
                BatterySaverPlan.Clamp(envelope.Brightness, BatterySaverPlan.LowestBrightness),
                envelope.LeftoverScheme);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return BatterySaverSettings.Default;
        }
    }

    public void Save(BatterySaverSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(
            new Envelope(1, settings.Enabled, settings.ProcessorCap, settings.Brightness, settings.LeftoverScheme), Options));
    }

    private sealed record Envelope(int Version, bool Enabled, uint ProcessorCap, uint Brightness, Guid? LeftoverScheme);
}
