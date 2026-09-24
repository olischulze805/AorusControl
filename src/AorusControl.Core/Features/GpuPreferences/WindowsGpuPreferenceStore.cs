using Microsoft.Win32;

namespace AorusControl.Core.Features.GpuPreferences;

/// <summary>One program Windows has a graphics preference for.</summary>
/// <param name="Name">The value name Windows uses: a full path for a normal program, a package
/// and app id for a Store app.</param>
/// <param name="Raw">The whole entry, unparsed.</param>
public sealed record GpuPreferenceProgram(string Name, string Raw)
{
    public GpuPreference? Preference => GpuPreferenceText.Read(Raw);

    /// <summary>Entries with a hard-wired adapter are shown but never written: see
    /// <see cref="GpuPreferenceText.SpecificAdapterToken"/>.</summary>
    public bool IsManageable => !GpuPreferenceText.PinsOneAdapter(Raw);

    /// <summary>What to call it on screen. Store apps have no path, so their id has to do.</summary>
    public string DisplayName => Name.Contains('\\') ? Path.GetFileName(Name) : Name;
}

public interface IGpuPreferenceStore
{
    IReadOnlyList<GpuPreferenceProgram> List();
    GpuPreferenceProgram? Find(string name);
    void Set(string name, GpuPreference preference);

    /// <summary>Calls back whenever anything in the store changes, ours or not. Null where
    /// there is nothing to watch, which is every store but the real one.</summary>
    IDisposable? Watch(Action changed) => null;
}

/// <summary>
/// Windows' own per-program graphics preference, in the registry where the Settings app keeps
/// it: HKCU\Software\Microsoft\DirectX\UserGpuPreferences.
///
/// There is no documented API for setting another program's preference - the Settings UI and
/// third-party installers write this key directly, and so does this. That makes it a
/// dependency on a Windows implementation detail rather than on a contract, which is worth
/// knowing before relying on it: a preference is a hint given to a program when it starts, it
/// does not move a running one, and a program that asks for a specific adapter itself will
/// get it regardless.
///
/// Per user, not per machine, so no elevation and nothing here touches another account.
/// </summary>
public sealed class WindowsGpuPreferenceStore(string? keyPath = null) : IGpuPreferenceStore
{
    /// <summary>Overridable so the tests can work in their own key instead of the real one.</summary>
    private readonly string _keyPath = keyPath ?? @"Software\Microsoft\DirectX\UserGpuPreferences";

    public IReadOnlyList<GpuPreferenceProgram> List()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_keyPath);
        if (key is null) return [];
        return key.GetValueNames()
            .Where(GpuPreferenceText.IsProgramEntry)
            .Select(name => new GpuPreferenceProgram(name, key.GetValue(name) as string ?? string.Empty))
            .OrderBy(program => program.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public GpuPreferenceProgram? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_keyPath);
        return key?.GetValue(name) is string raw ? new GpuPreferenceProgram(name, raw) : null;
    }

    public void Set(string name, GpuPreference preference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!GpuPreferenceText.IsProgramEntry(name))
            throw new ArgumentException("Das ist kein Programmeintrag, sondern eine Windows-Einstellung.", nameof(name));

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true);
        string? raw = key.GetValue(name) as string;
        if (GpuPreferenceText.PinsOneAdapter(raw))
            throw new InvalidOperationException(
                "Für dieses Programm ist in Windows eine bestimmte Grafikkarte fest ausgewählt; diese Auswahl wird nicht überschrieben.");

        string updated = GpuPreferenceText.With(raw, preference);
        if (updated == raw) return;
        key.SetValue(name, updated, RegistryValueKind.String);

        // Read back, like every other write in this app: the registry is not hardware, but a
        // silently ignored write would leave the display claiming something untrue.
        if (key.GetValue(name) as string != updated)
            throw new InvalidOperationException("Die Grafikeinstellung wurde geschrieben, kam aber anders zurück.");
    }

    public IDisposable Watch(Action changed) =>
        new RegistryKeyWatcher(Registry.CurrentUser.CreateSubKey(_keyPath, writable: false), changed);
}

/// <summary>
/// Tells about every value written into one key, as it happens.
///
/// Needed because this app is not the only writer. NVIDIA's session service
/// (nvxdsyncplugin.dll) mirrors its own per-program GPU profiles into this key at logon, and
/// its profile puts Chrome on the Intel chip - so a rule this app wrote the evening before was
/// quietly gone again by the time the browser started. A notification rather than a timer:
/// the kernel signals an event, a pool thread wakes, nothing runs in between.
/// </summary>
internal sealed class RegistryKeyWatcher : IDisposable
{
    private const int ChangeLastSet = 0x4;
    // Lets the notification outlive the thread that asked for it; without it, the pool
    // thread that re-arms the watch exiting would end it.
    private const int ThreadAgnostic = 0x10000000;

    private readonly RegistryKey _key;
    private readonly Action _changed;
    private readonly AutoResetEvent _signal = new(false);
    private readonly RegisteredWaitHandle _wait;
    private bool _disposed;

    public RegistryKeyWatcher(RegistryKey key, Action changed)
    {
        _key = key;
        _changed = changed;
        Arm();
        _wait = ThreadPool.RegisterWaitForSingleObject(_signal, (_, _) => OnSignal(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    private void Arm()
    {
        int error = RegNotifyChangeKeyValue(_key.Handle, false, ChangeLastSet | ThreadAgnostic,
            _signal.SafeWaitHandle, true);
        if (error != 0) throw new System.ComponentModel.Win32Exception(error);
    }

    private void OnSignal()
    {
        lock (_signal)
        {
            if (_disposed) return;
            // Re-armed before the callback, so a write that lands while it runs is not missed.
            try { Arm(); } catch { /* The key went away; there is nothing left to watch. */ }
        }
        _changed();
    }

    public void Dispose()
    {
        lock (_signal)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _wait.Unregister(null);
        _key.Dispose();
        _signal.Dispose();
    }

    [System.Runtime.InteropServices.DllImport("advapi32.dll")]
    private static extern int RegNotifyChangeKeyValue(Microsoft.Win32.SafeHandles.SafeRegistryHandle key,
        bool watchSubtree, int filter, Microsoft.Win32.SafeHandles.SafeWaitHandle signal, bool asynchronous);
}
