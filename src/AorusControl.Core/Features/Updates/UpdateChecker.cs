using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AorusControl.Core.Features.Updates;

/// <summary>
/// Checks a static JSON manifest for a newer version. Deliberately does not download or
/// install anything: no code-signing or release pipeline exists for this project yet, so
/// silently replacing the running executable would be irresponsible. This only tells the
/// user a newer version exists and hands them the link; they decide whether to fetch it.
/// </summary>
public sealed class UpdateChecker(HttpClient? httpClient = null) : IDisposable
{
    private const int MaximumManifestBytes = 16 * 1024;
    private readonly HttpClient _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    private readonly bool _ownsClient = httpClient is null;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<UpdateCheckResult> CheckAsync(
        Uri feedUrl,
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedUrl);
        ArgumentNullException.ThrowIfNull(currentVersion);
        if (feedUrl.Scheme != Uri.UriSchemeHttps)
            return UpdateCheckResult.Failed("Update-Feed muss über HTTPS erreichbar sein.");

        UpdateManifest manifest;
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(feedUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var limited = new MemoryStream();
            await body.CopyToAsync(limited, cancellationToken).ConfigureAwait(false);
            if (limited.Length > MaximumManifestBytes)
                return UpdateCheckResult.Failed("Update-Manifest ist unerwartet groß.");
            limited.Position = 0;
            manifest = JsonSerializer.Deserialize<UpdateManifest>(limited, Options)
                ?? throw new InvalidDataException("Leeres Manifest.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or IOException)
        {
            return UpdateCheckResult.Failed($"Update-Prüfung fehlgeschlagen: {exception.Message}");
        }

        if (!Version.TryParse(NormalizeForVersion(manifest.Version), out Version? remoteVersion))
            return UpdateCheckResult.Failed($"Manifest enthält keine gültige Version: „{manifest.Version}“.");

        return remoteVersion > currentVersion
            ? UpdateCheckResult.Available(manifest)
            : UpdateCheckResult.UpToDate(currentVersion.ToString());
    }

    // System.Version requires at least Major.Minor; a bare "1" or "1.0" from a hand-
    // written manifest should not just fail the whole check.
    private static string NormalizeForVersion(string version)
    {
        string trimmed = version.Trim().TrimStart('v', 'V');
        int dots = trimmed.Count(c => c == '.');
        return dots switch
        {
            0 => trimmed + ".0.0",
            1 => trimmed + ".0",
            _ => trimmed
        };
    }

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }
}
