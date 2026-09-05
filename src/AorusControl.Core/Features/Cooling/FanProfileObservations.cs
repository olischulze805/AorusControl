using System.Text.Json;
using System.Text.Json.Serialization;

namespace AorusControl.Core.Features.Cooling;

/// <summary>One measured pair: at this temperature, that profile was running the fans this hard.</summary>
public sealed record FanObservationPoint(byte TemperatureCelsius, byte Percent, int Samples);

/// <summary>
/// What the vendor fan profiles actually do, measured rather than read.
///
/// Quiet, Normal, Gaming and Maximum do not publish a curve: they set four status flags and
/// the firmware regulates internally. The EC's fifteen curve points stay exactly as they were
/// - that is confirmed by the write tests in research/FAN-POWER-GPU-CONTROL.md - so showing
/// those points as "what Gaming does" would be inventing an answer.
///
/// What can be known honestly is what the fans were observed doing while a profile ran. Every
/// telemetry tick contributes one temperature/duty pair, and the picture fills in as the
/// laptop gets used: sparse at first, and never claiming to be more than samples.
///
/// One entry per whole degree, keeping the most recent reading and counting how often that
/// degree has been seen. Not an average: the firmware has hysteresis, so the same degree
/// genuinely carries different duties depending on which way the temperature was moving, and
/// averaging that away would smooth a real behaviour into a fake one.
/// </summary>
public sealed class FanProfileObservations
{
    private readonly Dictionary<string, Dictionary<byte, Entry>> _byProfile = new(StringComparer.Ordinal);

    /// <summary>Temperatures a fan curve can meaningfully cover; anything outside is a bad read.</summary>
    private const byte MinimumTemperature = 20, MaximumTemperature = 105;

    public void Record(string profile, byte temperatureCelsius, byte percent)
    {
        if (string.IsNullOrWhiteSpace(profile)) return;
        if (temperatureCelsius is < MinimumTemperature or > MaximumTemperature) return;
        if (percent > 100) return;

        if (!_byProfile.TryGetValue(profile, out Dictionary<byte, Entry>? points))
            _byProfile[profile] = points = new Dictionary<byte, Entry>();
        points[temperatureCelsius] = new Entry(percent, points.TryGetValue(temperatureCelsius, out Entry? existing) ? existing.Samples + 1 : 1);
    }

    private void Restore(string profile, byte temperatureCelsius, byte percent, int samples)
    {
        if (temperatureCelsius is < MinimumTemperature or > MaximumTemperature || percent > 100 || samples < 1) return;
        if (!_byProfile.TryGetValue(profile, out Dictionary<byte, Entry>? points))
            _byProfile[profile] = points = new Dictionary<byte, Entry>();
        points[temperatureCelsius] = new Entry(percent, samples);
    }

    public IReadOnlyList<FanObservationPoint> For(string profile) =>
        _byProfile.TryGetValue(profile, out Dictionary<byte, Entry>? points)
            ? points.OrderBy(pair => pair.Key)
                .Select(pair => new FanObservationPoint(pair.Key, pair.Value.Percent, pair.Value.Samples))
                .ToArray()
            : [];

    public int SampleCount(string profile) =>
        _byProfile.TryGetValue(profile, out Dictionary<byte, Entry>? points) ? points.Values.Sum(entry => entry.Samples) : 0;

    public void Clear(string profile) => _byProfile.Remove(profile);

    public string ToJson() => JsonSerializer.Serialize(
        new Envelope(1, _byProfile.ToDictionary(
            profile => profile.Key,
            profile => profile.Value.ToDictionary(point => point.Key.ToString(), point => point.Value))),
        Options);

    /// <summary>
    /// Fail-soft on purpose: these are measurements, not settings. A corrupt or unreadable file
    /// means starting the picture again, never a failure the user has to deal with.
    /// </summary>
    public static FanProfileObservations FromJson(string? json)
    {
        var observations = new FanProfileObservations();
        if (string.IsNullOrWhiteSpace(json)) return observations;
        try
        {
            Envelope? envelope = JsonSerializer.Deserialize<Envelope>(json, Options);
            if (envelope is null || envelope.Version != 1 || envelope.Profiles is null) return observations;
            foreach ((string profile, Dictionary<string, Entry> points) in envelope.Profiles)
                foreach ((string temperature, Entry entry) in points)
                    if (byte.TryParse(temperature, out byte degrees))
                        observations.Restore(profile, degrees, entry.Percent, entry.Samples);
        }
        catch (JsonException)
        {
            return new FanProfileObservations();
        }
        return observations;
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        MaxDepth = 8
    };

    private sealed record Entry(byte Percent, int Samples);
    private sealed record Envelope(int Version, Dictionary<string, Dictionary<string, Entry>>? Profiles);
}
