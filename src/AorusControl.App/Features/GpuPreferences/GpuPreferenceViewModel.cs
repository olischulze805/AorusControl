using System.Collections.ObjectModel;
using AorusControl.App.Infrastructure;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.Diagnostics;
using AorusControl.Core.Features.GpuPreferences;
using AorusControl.Core.Features.PowerProfiles;

namespace AorusControl.App.Features.GpuPreferences;

/// <summary>One of the three settings, as the drop-down offers it.</summary>
public sealed record GpuChoice(GpuPreference Value, string Text)
{
    public static IReadOnlyList<GpuChoice> All { get; } =
        GpuSwitchPlan.Choices.Select(value => new GpuChoice(value, Name(value))).ToArray();

    /// <summary>The one place the three settings are put into words. Both the drop-downs and
    /// the line under each program read from here, so they cannot come to disagree.</summary>
    public static string Name(GpuPreference preference) => preference switch
    {
        GpuPreference.Nvidia => "RTX 3070",
        GpuPreference.Integrated => "Intel-Grafik",
        _ => "Windows entscheidet"
    };
}

/// <summary>One managed program, as the list shows it.</summary>
public sealed class GpuProgramViewModel : ObservableObject
{
    private readonly Func<GpuPreference, GpuPreference, Task>? _change;
    private GpuPreference _onAc, _onBattery;
    private string _state;

    /// <summary>A row without rules: a suggestion, which nothing manages yet.</summary>
    public GpuProgramViewModel(string name, string state)
    {
        Name = name;
        _state = state;
    }

    /// <param name="change">Called when the user picks a different chip for one of the two
    /// supplies. The current values are taken in through the constructor rather than through
    /// the setters, so building a row cannot look like a change and write one.</param>
    public GpuProgramViewModel(ManagedProgram program, string state,
        Func<GpuPreference, GpuPreference, Task> change)
        : this(program.Name, state)
    {
        _onAc = program.OnAc;
        _onBattery = program.OnBattery;
        _change = change;
    }

    public string Name { get; }
    public string DisplayName => Name.Contains('\\') ? System.IO.Path.GetFileName(Name) : Name;
    public string Path => Name;
    public string State { get => _state; set => SetProperty(ref _state, value); }

    /// <summary>False for a suggestion, which has nothing to configure yet.</summary>
    public bool HasRules => _change is not null;
    public IReadOnlyList<GpuChoice> Choices => GpuChoice.All;

    public GpuPreference OnAc
    {
        get => _onAc;
        set { if (SetProperty(ref _onAc, value)) Changed(); }
    }

    public GpuPreference OnBattery
    {
        get => _onBattery;
        set { if (SetProperty(ref _onBattery, value)) Changed(); }
    }

    private void Changed() => _ = _change?.Invoke(_onAc, _onBattery);
}

/// <summary>One program that is using a graphics chip right now.</summary>
public sealed class GpuUsageViewModel(GpuUser user, bool managed)
{
    public string Name { get; } = user.Program;
    public string Path { get; } = user.Path;
    public bool IsNvidia { get; } = user.IsNvidia;
    public IReadOnlyList<int> ProcessIds { get; } = user.ProcessIds;

    /// <summary>Closing is offered only where it would achieve something: a program on the
    /// Intel chip is not what is keeping the RTX awake, so ending it buys nothing and only
    /// risks somebody's unsaved work.</summary>
    public bool CanClose { get; } = user.IsNvidia;
    public string Chip { get; } = user.IsNvidia ? "RTX 3070" : "Intel-Grafik";

    /// <summary>The line under the name. Deliberately without the path: a Store app's path
    /// runs to three wrapped lines at the narrowest window width and buries everything worth
    /// reading. It is on the row's tooltip, where looking for it costs nothing.</summary>
    public string Detail { get; } = string.Join(" · ", new[]
    {
        user.Processes > 1 ? $"{user.Processes} Prozesse" : null,
        managed ? "wird schon verwaltet" : null,
        // The registry keeps a Store app's preference under its package id, so writing this
        // path would leave an entry nothing ever reads. The program search knows the ids.
        user.IsStoreApp && !managed ? "Store-App · über „Programm suchen“ hinzufügen" : null
    }.Where(part => part is { Length: > 0 }));

    /// <summary>Only a program that can actually be assigned gets the button.</summary>
    public bool CanAdd { get; } = !managed && !user.IsStoreApp;
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
    private readonly IGpuActivityReader _activity;
    private readonly IProgramCloser _closer;
    private readonly Func<string, bool> _confirm;
    private bool _visible;
    private readonly List<ManagedProgram> _managed = [];
    private bool _busy, _disposed, _started, _automatic = true, _runningRead, _runningReadable, _runningBusy;
    private int _missing;
    private string _status = "Noch nicht geprüft";
    private LaptopPowerSource _source = LaptopPowerSource.Unknown;

    public GpuPreferenceViewModel(
        IGpuPreferenceStore registry,
        IGpuPreferenceSettingsStore settings,
        Func<LaptopPowerSource> readPowerSource,
        IGpuActivityReader? activity = null,
        IProgramCloser? closer = null,
        Func<string, bool>? confirm = null)
    {
        _registry = registry;
        _settings = settings;
        _readPowerSource = readPowerSource;
        _activity = activity ?? new WindowsGpuActivityReader();
        _closer = closer ?? new ProgramCloser();
        // Ending somebody's program is not something to do on a single click, and the dialog
        // is the app's, not this class's - so it comes in from outside and tests can answer it.
        _confirm = confirm ?? (message => System.Windows.MessageBox.Show(message, "AORUS Control",
            System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.Cancel) == System.Windows.MessageBoxResult.OK);
        RemoveCommand = new AsyncRelayCommand<GpuProgramViewModel>(RemoveAsync);
        AddSuggestionCommand = new AsyncRelayCommand<GpuProgramViewModel>(
            suggestion => suggestion is null ? Task.CompletedTask : AddAsync(suggestion.Name));
        AddRunningCommand = new AsyncRelayCommand<GpuUsageViewModel>(
            running => running is null ? Task.CompletedTask : AddAsync(running.Path));
        CloseRunningCommand = new AsyncRelayCommand<GpuUsageViewModel>(CloseRunningAsync);
        RefreshRunningCommand = new AsyncRelayCommand(RefreshRunningAsync);
        ApplyNowCommand = new AsyncRelayCommand(() => ApplyAsync("Von Hand angewendet"));
    }

    /// <summary>Set by the shell when the page is on screen. Reading the graphics counters
    /// costs half a second and answers a question nobody is asking while the app sits in the
    /// notification area.</summary>
    public bool IsVisible
    {
        get => _visible;
        set
        {
            if (!SetProperty(ref _visible, value) || !value) return;
            _ = RefreshRunningAsync();
        }
    }

    public ObservableCollection<GpuProgramViewModel> Programs { get; } = [];

    /// <summary>Programs Windows already sets to the RTX, offered rather than taken over: a
    /// switch changes how a program starts, and that stays the user's decision.</summary>
    public ObservableCollection<GpuProgramViewModel> Suggestions { get; } = [];

    /// <summary>
    /// What is on which chip at this moment - the one thing the rest of this card cannot
    /// show. Everything else here is a stored preference, which is a wish; this is the
    /// answer. On battery an entry on the RTX is exactly why the card is awake.
    /// </summary>
    public ObservableCollection<GpuUsageViewModel> Running { get; } = [];

    public AsyncRelayCommand<GpuProgramViewModel> RemoveCommand { get; }
    public AsyncRelayCommand<GpuProgramViewModel> AddSuggestionCommand { get; }
    public AsyncRelayCommand<GpuUsageViewModel> AddRunningCommand { get; }
    public AsyncRelayCommand<GpuUsageViewModel> CloseRunningCommand { get; }
    public AsyncRelayCommand RefreshRunningCommand { get; }
    public AsyncRelayCommand ApplyNowCommand { get; }

    public bool IsBusy => _busy;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool HasPrograms => Programs.Count > 0;
    public bool HasSuggestions => Suggestions.Count > 0;
    public bool HasRunning => Running.Count > 0;

    /// <summary>The headline over the running list: how many programs are holding the RTX.
    /// That number, and not the list, is what a glance is for.</summary>
    public string RunningSummary
    {
        get
        {
            if (!_runningRead) return "Noch nicht gelesen.";
            if (!_runningReadable) return "Die Grafikzähler von Windows geben hier nichts her.";
            int onNvidia = Running.Count(program => program.IsNvidia);
            return Running.Count == 0
                ? "Kein Programm außerhalb von Windows benutzt gerade eine Grafikkarte."
                : onNvidia == 0
                    ? "Nichts auf der RTX - sie kann schlafen."
                    : $"{onNvidia} {(onNvidia == 1 ? "Programm hält" : "Programme halten")} die RTX wach.";
        }
    }

    /// <summary>Entries Windows still keeps for programs that are no longer installed. Not
    /// worth a cleanup button on its own, worth saying once.</summary>
    public string LeftoverNote => _missing == 0
        ? string.Empty
        : $"Windows hat außerdem {_missing} Einträge für Programme, die es nicht mehr gibt.";

    /// <summary>
    /// What the card says while the automatic is off.
    ///
    /// The old wording stopped after "nothing is changed", which left the obvious next
    /// question open: a rule picked from the drop-downs while the automatic is off is saved
    /// and does nothing, and somebody watching for an effect would see none and assume the
    /// drop-down was broken.
    /// </summary>
    private const string AutomaticOff =
        "Automatik aus · nichts wird umgestellt. Geänderte Regeln werden gespeichert und gelten, sobald sie wieder an ist.";

    /// <summary>Off leaves every preference exactly where it is; the list stays, so it can be
    /// switched back on without adding everything again.</summary>
    public bool IsAutomatic
    {
        get => _automatic;
        set
        {
            if (!SetProperty(ref _automatic, value)) return;
            if (value) _ = ApplyAsync("Automatik eingeschaltet");
            else Status = AutomaticOff;
        }
    }

    /// <summary>Which of each program's two rules is the one in force, for the card's
    /// caption. Since the rules are per program, this says which column counts rather than
    /// naming a chip it can no longer speak for.</summary>
    public string SourceText => _source switch
    {
        LaptopPowerSource.Ac => "Netzbetrieb · es gilt die linke Spalte",
        LaptopPowerSource.Battery => "Akkubetrieb · es gilt die rechte Spalte",
        _ => "Stromquelle unbekannt · es wird nichts umgestellt"
    };

    public async Task StartAsync()
    {
        if (_started || _disposed) return;
        _started = true;
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
        await MarkRunningAsync();
    }

    /// <summary>
    /// Takes a new pair of rules for one program and puts it into effect.
    ///
    /// Only the one entry is replaced - the list is rebuilt from it afterwards, and a row that
    /// rebuilt itself from a stale copy would undo the change the user just made.
    /// </summary>
    private async Task ChangeRulesAsync(ManagedProgram program, GpuPreference onAc, GpuPreference onBattery)
    {
        int index = _managed.FindIndex(entry => entry.Name == program.Name);
        if (index < 0 || _disposed) return;
        if (_managed[index] is { } existing && existing.OnAc == onAc && existing.OnBattery == onBattery) return;

        // The app owns this value again from here on: the rule changed, so the value the
        // registry holds is no longer evidence that somebody else set it by hand.
        _managed[index] = _managed[index] with { OnAc = onAc, OnBattery = onBattery, LastWritten = null };
        Persist();
        await ApplyAsync($"Regel für {program.DisplayName} geändert");
    }

    /// <summary>Brings the running list's "already managed" marks back in line after the
    /// managed list changed. Only when that list has been read at all - otherwise adding a
    /// program from the search dialog would start a counter read nobody asked for.</summary>
    private Task MarkRunningAsync() => _runningRead ? RefreshRunningAsync() : Task.CompletedTask;

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
            GpuSwitchStep? release = GpuSwitchPlan.Release(managed, _registry.Find(managed.Name));
            if (release is not null) await Task.Run(() => _registry.Set(release.Name, release.Target));

            _managed.Remove(managed);
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
        await MarkRunningAsync();
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
            if (!_automatic) { Status = AutomaticOff; return; }

            Dictionary<string, GpuPreferenceProgram> current = await Task.Run(() =>
                _registry.List().ToDictionary(program => program.Name, StringComparer.OrdinalIgnoreCase));
            IReadOnlyList<GpuSwitchStep> steps = GpuSwitchPlan.Steps(_source, _managed, current);

            int written = 0;
            foreach (GpuSwitchStep step in steps)
            {
                try
                {
                    await Task.Run(() => _registry.Set(step.Name, step.Target));
                    Remember(step.Name, step.Target);
                    written++;
                }
                catch (Exception exception)
                {
                    AppLog.Error("gpu", $"Grafikeinstellung für {step.Name} nicht geschrieben.", exception);
                }
            }
            // Only when something actually moved: the file is the record of what this app put
            // where, and it has to survive a restart or the promise not to overrule a manual
            // change would last exactly one session.
            if (written > 0) Persist();

            Status = _managed.Count == 0
                ? "Noch keine Programme ausgewählt."
                : _source == LaptopPowerSource.Unknown
                    ? "Windows meldet die Stromquelle nicht; es wurde nichts geändert."
                    : $"{reason} · {written} von {_managed.Count} umgestellt · wirkt beim nächsten Programmstart";
        }
        finally { _busy = false; Show(); }
    }

    /// <summary>
    /// Ends a program that is holding the discrete card awake, once the user has confirmed it.
    ///
    /// The lasting fix is the preference next to it - that one survives the next start, which
    /// this does not. But a preference only takes effect when a program starts, so on battery,
    /// now, with the card awake and the list naming the culprit, there is nothing else that
    /// gets it back to sleep.
    /// </summary>
    private async Task CloseRunningAsync(GpuUsageViewModel? program)
    {
        if (program is null || _disposed || _busy || !program.CanClose) return;
        string many = program.ProcessIds.Count > 1 ? $" ({program.ProcessIds.Count} Prozesse)" : string.Empty;
        string question = string.Join(Environment.NewLine + Environment.NewLine,
            $"{program.Name}{many} beenden?",
            "Das Programm wird zuerst gebeten, sich zu schließen - hat es ein Fenster, kann es " +
            "vorher noch nachfragen und speichern. Antwortet es nicht innerhalb weniger Sekunden, " +
            "wird es beendet, und nicht gespeicherte Arbeit geht dabei verloren.",
            "Dauerhaft hilft stattdessen die Regel darunter: sie greift beim nächsten Start.");
        if (!_confirm(question)) return;

        _busy = true;
        CloseOutcome outcome;
        try { outcome = await _closer.CloseAsync(program.ProcessIds); }
        catch (Exception exception)
        {
            AppLog.Error("gpu", $"{program.Name} konnte nicht beendet werden.", exception);
            Status = $"{program.Name} konnte nicht beendet werden: {exception.Message}";
            return;
        }
        finally { _busy = false; }

        Status = outcome switch
        {
            { Closed: 0 } => $"{program.Name} ließ sich nicht beenden - keine Rechte, oder es hat abgelehnt.",
            { Refused: > 0 } => $"{program.Name}: {outcome.Closed} beendet, {outcome.Refused} laufen weiter.",
            { Forced: > 0 } => $"{program.Name} beendet - {outcome.Forced} davon erzwungen, weil nichts antwortete.",
            _ => $"{program.Name} hat sich geschlossen."
        };
        AppLog.Info("gpu", Status);
        await RefreshRunningAsync();
    }

    /// <summary>
    /// Reads who is on which chip. Off the UI thread: enumerating the graphics counters and
    /// opening every process behind them took 0,6 s on this machine, which is nothing for a
    /// button and far too much for a redraw.
    /// </summary>
    private async Task RefreshRunningAsync()
    {
        // Opening the page starts one of these, and pressing the button starts another. Both
        // end in Clear-then-Add on the same collection, and two of those interleaving is how
        // a list ends up half rebuilt. The second caller has nothing to add anyway: it would
        // read the same counters a moment later.
        if (_disposed || _runningBusy) return;
        _runningBusy = true;
        try { await ReadRunningAsync(); }
        finally { _runningBusy = false; }
    }

    private async Task ReadRunningAsync()
    {
        IReadOnlyList<GpuUser>? users = await Task.Run(_activity.Read);
        if (_disposed) return;

        _runningReadable = users is not null;
        Running.Clear();
        foreach (GpuUser user in users ?? [])
            Running.Add(new GpuUsageViewModel(user,
                _managed.Any(program => program.Name.Equals(user.Path, StringComparison.OrdinalIgnoreCase))));
        _runningRead = true;
        OnPropertyChanged(nameof(HasRunning));
        OnPropertyChanged(nameof(RunningSummary));
    }

    /// <summary>Rebuilds the list from what the registry actually says, not from what was
    /// intended - a preference somebody changed elsewhere has to be visible as such.</summary>
    private void Show()
    {
        IReadOnlyList<GpuPreferenceProgram> all = SafeList();
        // The whole key was just read; asking the registry again for each managed program
        // would be one more round trip per row for an answer already in hand.
        Dictionary<string, GpuPreferenceProgram> byName =
            all.ToDictionary(program => program.Name, StringComparer.OrdinalIgnoreCase);

        Suggestions.Clear();
        GpuSuggestionResult found = GpuSuggestions.From(all, _managed, GpuSuggestions.StillInstalled);
        _missing = found.MissingPrograms;
        foreach (GpuSuggestion suggestion in found.Suggestions)
            Suggestions.Add(new GpuProgramViewModel(suggestion.Name, suggestion.Reason));

        Programs.Clear();
        foreach (ManagedProgram program in _managed.OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            byName.TryGetValue(program.Name, out GpuPreferenceProgram? entry);
            string state = entry?.Preference switch
            {
                null when entry is { IsManageable: false } => "feste Grafikkarte in Windows gewählt",
                null => "keine Einstellung in Windows",
                { } set => $"jetzt {GpuChoice.Name(set)}"
            };
            // What the rules would give it right now. A value somewhere else than that, which
            // this app did not put there, was somebody's own decision and stays.
            if (entry?.Preference is { } now && _source != LaptopPowerSource.Unknown
                && now != program.For(_source) && _automatic)
                state += " · von Hand geändert, bleibt so";
            Programs.Add(new GpuProgramViewModel(program, state,
                (onAc, onBattery) => ChangeRulesAsync(program, onAc, onBattery)));
        }
        OnPropertyChanged(nameof(HasPrograms));
        OnPropertyChanged(nameof(HasSuggestions));
        OnPropertyChanged(nameof(LeftoverNote));
    }

    /// <summary>The registry, or an empty list: a display refresh must never be the thing that
    /// takes the page down.</summary>
    private IReadOnlyList<GpuPreferenceProgram> SafeList()
    {
        try { return _registry.List(); }
        catch (Exception exception)
        {
            AppLog.Error("gpu", "Grafikeinstellungen nicht lesbar.", exception);
            return [];
        }
    }

    /// <summary>Records what was just written, on the program itself.</summary>
    private void Remember(string name, GpuPreference target)
    {
        int index = _managed.FindIndex(entry => entry.Name == name);
        if (index >= 0) _managed[index] = _managed[index] with { LastWritten = target };
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
