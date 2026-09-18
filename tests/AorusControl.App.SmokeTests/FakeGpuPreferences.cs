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
    public int Loads { get; private set; }

    public IReadOnlyList<ManagedProgram> Load()
    {
        Loads++;
        return _programs;
    }
    public void Save(IReadOnlyList<ManagedProgram> programs) => _programs = programs.ToArray();
}

/// <summary>A fixed answer to "who is on which chip". The real reader enumerates the graphics
/// counters of the machine the tests run on, which is neither repeatable nor the point.</summary>
internal sealed class FakeGpuActivity(params GpuUser[] users) : IGpuActivityReader
{
    public int Reads { get; private set; }

    /// <summary>Set to make the reader say it could not read at all, which is a different
    /// thing from finding nothing.</summary>
    public bool Unreadable { get; set; }

    public IReadOnlyList<GpuUser>? Read()
    {
        Reads++;
        return Unreadable ? null : users;
    }
}
