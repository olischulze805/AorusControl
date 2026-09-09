using AorusControl.Core.Features.GpuPreferences;

/// <summary>An in-memory stand-in for Windows' graphics preferences. The real ones belong to
/// the user, and a test suite that writes into them has done damage here before.</summary>
internal sealed class FakeGpuPreferenceStore : IGpuPreferenceStore
{
    private readonly Dictionary<string, string> _entries = new(StringComparer.OrdinalIgnoreCase);

    public int Writes { get; private set; }

    public IReadOnlyList<GpuPreferenceProgram> List() =>
        _entries.Select(entry => new GpuPreferenceProgram(entry.Key, entry.Value)).ToArray();

    public GpuPreferenceProgram? Find(string name) =>
        _entries.TryGetValue(name, out string? raw) ? new GpuPreferenceProgram(name, raw) : null;

    public void Set(string name, GpuPreference preference)
    {
        _entries.TryGetValue(name, out string? raw);
        if (GpuPreferenceText.PinsOneAdapter(raw))
            throw new InvalidOperationException("Für dieses Programm ist eine feste Grafikkarte gewählt.");
        _entries[name] = GpuPreferenceText.With(raw, preference);
        Writes++;
    }

    public void Seed(string name, string raw) => _entries[name] = raw;
}

internal sealed class FakeGpuPreferenceSettings : IGpuPreferenceSettingsStore
{
    private IReadOnlyList<ManagedProgram> _programs = [];

    public IReadOnlyList<ManagedProgram> Load() => _programs;
    public void Save(IReadOnlyList<ManagedProgram> programs) => _programs = programs.ToArray();
}
