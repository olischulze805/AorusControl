using System.Text.Json;
using System.Text.Json.Serialization;

namespace AorusControl.Core.Features.PowerProfiles;

/// <summary>Single-owner local storage; loading never applies hardware settings.</summary>
public sealed class ProfileCatalogStore(string path)
{
    private readonly string _path = Path.GetFullPath(path);
    private const int MaximumBytes = 256 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 12
    };

    public ProfileCatalog? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumBytes) throw new InvalidDataException("Profildatei zu groß.");
            var envelope = JsonSerializer.Deserialize<Envelope>(stream, Options);
            if (envelope is null || envelope.Version != 1 || envelope.Device != "AORUS-5-SE4-FB0F" || envelope.Catalog is null)
                throw new InvalidDataException("Unbekannte Profildateiversion oder Gerätezuordnung.");
            return envelope.Catalog;
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Profildatei beschädigt oder ungültig. Es wurden keine Profile übernommen.", error);
        }
    }

    public void Save(ProfileCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new Envelope(1, "AORUS-5-SE4-FB0F", catalog), Options);
        if (payload.Length > MaximumBytes) throw new InvalidDataException("Profildatei zu groß.");
        string directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".profiles-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(payload);
                stream.Flush(true);
            }
            if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".bak");
            else File.Move(temporary, _path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed record Envelope(int Version, string Device, ProfileCatalog Catalog);
}
