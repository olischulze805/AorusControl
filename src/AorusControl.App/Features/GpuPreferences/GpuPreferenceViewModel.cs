using System.Collections.ObjectModel;
using AorusControl.App.Infrastructure;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.Diagnostics;
using AorusControl.Core.Features.GpuPreferences;
using AorusControl.Core.Features.PowerProfiles;

namespace AorusControl.App.Features.GpuPreferences;

/// <summary>One managed program, as the list shows it.</summary>
public sealed class GpuProgramViewModel(string name, string state) : ObservableObject
{
    private string _state = state;
    public string Name { get; } = name;
    public string DisplayName => Name.Contains('\\') ? System.IO.Path.GetFileName(Name) : Name;
    public string Path => Name;
    public string State { get => _state; set => SetProperty(ref _state, value); }
}

/// <summary>
/// Keeps chosen programs on the RTX while the laptop is plugged in and on the Intel chip while
/// it runs on battery.
///
/// The aim is the battery half: a program that never asks for the RTX lets it stay in its
/// sleep state, and that is worth more to battery life than anything else this app can do.
/// What makes it honest rather than magic is what it does not claim - this writes Windows' own
/// per-program graphics preference, the same value the Settings app writes, and a preference
/// only takes effect when a program starts. Nothing here moves a running program, forces a
/// program that picks its own adapter, or switches the card itself off.
///
/// The switch follows the power source through <see cref="Microsoft.Win32.SystemEvents"/>, so
/// it also happens while the window is closed and the app sits in the notification area.
/// </summary>
public sealed class GpuPreferenceViewModel : ObservableObject, IFeatureModule
{
    private readonly IGpuPreferenceStore _registry;
    private readonly IGpuPreferenceSettingsStore _settings;
    private readonly Func<LaptopPowerSource> _readPowerSource;
    private readonly Dictionary<string, GpuPreference> _lastWritten = [];
    private readonly List<ManagedProgram> _managed = [];
    private bool _busy, _disposed, _automatic = true;
    private string _status = "Noch nicht geprüft";
    private LaptopPowerSource _source = LaptopPowerSource.Unknown;

    public GpuPreferenceViewModel(
        IGpuPreferenceStore registry,
        IGpuPreferenceSettingsStore settings,
        Func<LaptopPowerSource> readPowerSource)
    {
        _registry = registry;
        _settings = settings;
        _readPowerSource = readPowerSource;
        RemoveCommand = new AsyncRelayCommand<GpuProgramViewModel>(RemoveAsync);
        ApplyNowCommand = new AsyncRelayCommand(() => ApplyAsync("Von Hand angewendet"));
    }

    public ObservableCollection<GpuProgramViewModel> Programs { get; } = [];
    public AsyncRelayCommand<GpuProgramViewModel> RemoveCommand { get; }
    public AsyncRelayCommand ApplyNowCommand { get; }

    public bool IsBusy => _busy;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool HasPrograms => Programs.Count > 0;

    /// <summary>Off leaves every preference exactly where it is; the list stays, so it can be
    /// switched back on without adding everything again.</summary>
    public bool IsAutomatic
    {
        get => _automatic;
        set
        {
            if (!SetProperty(ref _automatic, value)) return;
            if (value) _ = ApplyAsync("Automatik eingeschaltet");
            else Status = "Automatik aus · die aktuellen Einstellungen bleiben stehen.";
        }
    }

    /// <summary>What the rule would set right now, in words, for the card's caption.</summary>
    public string SourceText => _source switch
    {
        LaptopPowerSource.Ac => "Netzbetrieb · ausgewählte Programme laufen auf der RTX",
        LaptopPowerSource.Battery => "Akkubetrieb · ausgewählte Programme laufen auf der Intel-Grafik",
        _ => "Stromquelle unbekannt · es wird nichts umgestellt"
    };

    public async Task StartAsync()
    {
        try
        {
            _managed.Clear();
            _managed.AddRange(_settings.Load());
        }
        catch (Exception exception)
        {
            AppLog.Error("gpu", "Grafikzuordnungen nicht lesbar.", exception);
            Status = $"Zuordnungen nicht lesbar: {exception.Message}";
        }

        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        await ApplyAsync("Beim Start geprüft");
    }

    /// <summary>Adds a program to the list, remembering what Windows had set for it.</summary>
    public async Task AddAsync(string executablePath)
    {
        if (_busy || _disposed || string.IsNullOrWhiteSpace(executablePath)) return;
        if (_managed.Any(program => program.Name.Equals(executablePath, StringComparison.OrdinalIgnoreCase)))
        {
            Status = $"{System.IO.Path.GetFileName(executablePath)} ist schon in der Liste.";
            return;
        }

        GpuPreferenceProgram? existing = _registry.Find(executablePath);
        if (existing is { IsManageable: false })
        {
            Status = $"Für {System.IO.Path.GetFileName(executablePath)} ist in Windows eine feste Grafikkarte gewählt; das wird nicht überschrieben.";
            return;
        }

        _managed.Add(new ManagedProgram(executablePath, existing?.Preference));
        Persist();
        await ApplyAsync($"{System.IO.Path.GetFileName(executablePath)} hinzugefügt");
    }

    private async Task RemoveAsync(GpuProgramViewModel? program)
    {
        if (program is null || _busy || _disposed) return;
        ManagedProgram? managed = _managed.FirstOrDefault(entry => entry.Name == program.Name);
        if (managed is null) return;

        _busy = true;
        try
        {
            // Handing the original setting back is the polite half of taking over: what this
            // app changed, it undoes - unless somebody has changed it since, in which case
            // that decision stands.
            _lastWritten.TryGetValue(managed.Name, out GpuPreference ours);
            GpuSwitchStep? release = GpuSwitchPlan.Release(managed, _registry.Find(managed.Name),
                _lastWritten.ContainsKey(managed.Name) ? ours : null);
            if (release is not null) await Task.Run(() => _registry.Set(release.Name, release.Target));

            _managed.Remove(managed);
            _lastWritten.Remove(managed.Name);
            Persist();
            Status = release is null
                ? $"{program.DisplayName} entfernt."
                : $"{program.DisplayName} entfernt und auf die ursprüngliche Einstellung zurückgesetzt.";
        }
        catch (Exception exception)
        {
            AppLog.Error("gpu", "Programm konnte nicht freigegeben werden.", exception);
            Status = $"Entfernen fehlgeschlagen: {exception.Message}";
        }
        finally { _busy = false; Show(); }
    }

    /// <summary>Brings every managed program in line with the current power source.</summary>
    private async Task ApplyAsync(string reason)
    {
        if (_busy || _disposed) return;
        _busy = true;
        try
        {
            _source = _readPowerSource();
            OnPropertyChanged(nameof(SourceText));
            if (!_automatic) { Status = "Automatik aus · die aktuellen Einstellungen bleiben stehen."; return; }

            Dictionary<string, GpuPreferenceProgram> current = await Task.Run(() =>
                _registry.List().ToDictionary(program => program.Name, StringComparer.OrdinalIgnoreCase));
            IReadOnlyList<GpuSwitchStep> steps = GpuSwitchPlan.Steps(_source, _managed, current, _lastWritten);

            int written = 0;
            foreach (GpuSwitchStep step in steps)
            {
                try
                {
                    await Task.Run(() => _registry.Set(step.Name, step.Target));
                    _lastWritten[step.Name] = step.Target;
                    written++;
                }
                catch (Exception exception)
                {
                    AppLog.Error("gpu", $"Grafikeinstellung für {step.Name} nicht geschrieben.", exception);
                }
            }

            Status = _managed.Count == 0
                ? "Noch keine Programme ausgewählt."
                : _source == LaptopPowerSource.Unknown
                    ? "Windows meldet die Stromquelle nicht; es wurde nichts geändert."
                    : $"{reason} · {written} von {_managed.Count} umgestellt. Wirkt beim nächsten Start des Programms.";
        }
        finally { _busy = false; Show(); }
    }

    /// <summary>Rebuilds the list from what the registry actually says, not from what was
    /// intended - a preference somebody changed elsewhere has to be visible as such.</summary>
    private void Show()
    {
        Programs.Clear();
        GpuPreference target = GpuSwitchPlan.For(_source);
        foreach (ManagedProgram program in _managed.OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            GpuPreferenceProgram? entry = _registry.Find(program.Name);
            string state = entry?.Preference switch
            {
                null when entry is { IsManageable: false } => "feste Grafikkarte in Windows gewählt",
                null => "keine Einstellung in Windows",
                GpuPreference.Nvidia => "RTX 3070",
                GpuPreference.Integrated => "Intel-Grafik",
                _ => "Windows entscheidet"
            };
            if (entry?.Preference is { } now && _source != LaptopPowerSource.Unknown && now != target && _automatic)
                state += " · von Hand geändert, wird nicht überschrieben";
            Programs.Add(new GpuProgramViewModel(program.Name, state));
        }
        OnPropertyChanged(nameof(HasPrograms));
    }

    private void Persist()
    {
        try { _settings.Save(_managed); }
        catch (Exception exception)
        {
            AppLog.Error("gpu", "Grafikzuordnungen nicht gespeichert.", exception);
            Status = $"Speichern fehlgeschlagen: {exception.Message}";
        }
    }

    // SystemEvents raises this on its own thread; the ViewModel's collection is only touched
    // from the tasks started here, which the dispatcher marshals back.
    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs args)
    {
        if (_disposed || args.Mode is not (Microsoft.Win32.PowerModes.StatusChange or Microsoft.Win32.PowerModes.Resume)) return;
        _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => ApplyAsync("Stromquelle gewechselt"));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
