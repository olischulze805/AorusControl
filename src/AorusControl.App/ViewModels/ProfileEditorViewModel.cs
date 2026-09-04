using System.Globalization;
using AorusControl.App.Infrastructure;
using AorusControl.Core.Features.PowerProfiles;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

namespace AorusControl.App.ViewModels;

public sealed record ProfileOption(Guid? Id, string Name);
public sealed record ModeOption<T>(T Value, string Label);

public sealed class ProfileEditorViewModel : ObservableObject
{
    private readonly Func<ProfileCatalog?> _load;
    private readonly Action<ProfileCatalog> _save;
    private ProfileCatalog _catalog = new([], new(null, null));
    private Guid? _editingId;
    private LaptopProfile? _selected;
    private string _name = "", _curveText = "", _fixedValue = "114", _status = "";
    private WindowsPowerOverlayMode _power;
    private ProfileCoolingMode _cooling;
    private Guid? _ac, _battery;
    private bool _loaded;
    private bool _busy;
    public bool IsBusy { get => _busy; private set { SetProperty(ref _busy, value); OnPropertyChanged(nameof(CanEdit)); } }
    public bool CanEdit => !IsBusy;
    public Task Initialization { get; }
    private bool _updatingCurve;
    public IReadOnlyList<FanCurveRowViewModel> CurveRows { get; } = Array.AsReadOnly(
        Enumerable.Range(1, 15).Select(i => new FanCurveRowViewModel(i)).ToArray());
    private readonly Func<string, bool> _confirmDiscard;
    private Draft? _savedDraft;
    private sealed record Draft(string Name, string Curve, string Fixed, WindowsPowerOverlayMode Power, ProfileCoolingMode Cooling);
    private Draft CurrentDraft => new(Name, CurveText, FixedValue, PowerMode, CoolingMode);
    public bool HasDraftChanges => _savedDraft is not null && CurrentDraft != _savedDraft;
    public bool HasAssignmentChanges => AcProfile != _catalog.Assignments.AcProfile || BatteryProfile != _catalog.Assignments.BatteryProfile;
    public bool HasUnsavedChanges => HasDraftChanges || HasAssignmentChanges;

    public ProfileEditorViewModel(Func<ProfileCatalog?> load, Action<ProfileCatalog> save, Func<string, bool>? confirmDiscard = null)
    {
        _load = load; _save = save;
        _confirmDiscard = confirmDiscard ?? (_ => false);
        foreach (var row in CurveRows)
            row.PropertyChanged += (_, _) =>
            {
                if (_updatingCurve) return;
                _curveText = string.Join(Environment.NewLine, CurveRows.Select(r => $"{r.Temperature}:{r.Value}"));
                OnPropertyChanged(nameof(CurveText));
            };
        ReloadCommand = new(() => Guard(async () => { if (CanDiscard(HasUnsavedChanges)) await Reload(); }));
        NewCommand = new(() => { if (!IsBusy && CanDiscard(HasDraftChanges)) New(); });
        LoadSelectedCommand = new(() => { if (!IsBusy && Selected is { } p && CanDiscard(HasDraftChanges)) Edit(p); });
        SaveCommand = new(() => Guard(SaveDraft));
        DeleteCommand = new(() => Guard(DeleteSelected));
        AssignCommand = new(() => Guard(() => Commit(new(_catalog.Profiles, new(AcProfile, BatteryProfile)), true)));
        Initialization = Guard(Reload);
    }

    public AsyncRelayCommand ReloadCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand LoadSelectedCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand AssignCommand { get; }
    public IReadOnlyList<LaptopProfile> Profiles => _catalog.Profiles;
    public IReadOnlyList<ProfileOption> AssignmentOptions => new[] { new ProfileOption(null, "Keine automatische Zuordnung") }
        .Concat(Profiles.Select(p => new ProfileOption(p.Id, p.Name))).ToArray();
    public IReadOnlyList<ModeOption<WindowsPowerOverlayMode>> PowerOptions { get; } =
        [new(WindowsPowerOverlayMode.Balanced, "Ausbalanciert"), new(WindowsPowerOverlayMode.BestEfficiency, "Energieeffizienz"), new(WindowsPowerOverlayMode.BestPerformance, "Beste Leistung")];
    public IReadOnlyList<ModeOption<ProfileCoolingMode>> CoolingOptions { get; } =
        [new(ProfileCoolingMode.Normal, "Normal"), new(ProfileCoolingMode.Quiet, "Leise"), new(ProfileCoolingMode.Gaming, "Gaming"), new(ProfileCoolingMode.Maximum, "Maximal"), new(ProfileCoolingMode.Fixed, "Fester Wert (Testmodus)"), new(ProfileCoolingMode.Dynamic, "Gespeicherte Gerätekurve"), new(ProfileCoolingMode.CustomCurve, "Eigene Kurve")];
    public LaptopProfile? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string CurveText
    {
        get => _curveText;
        set
        {
            SetProperty(ref _curveText, value);
            _updatingCurve = true;
            try
            {
                string[] lines = value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (int i = 0; i < CurveRows.Count; i++)
                {
                    string[] pair = i < lines.Length ? lines[i].Split(':') : [];
                    CurveRows[i].Temperature = pair.Length > 0 ? pair[0] : "";
                    CurveRows[i].Value = pair.Length > 1 ? pair[1] : "";
                }
            }
            finally { _updatingCurve = false; }
        }
    }
    public string FixedValue { get => _fixedValue; set => SetProperty(ref _fixedValue, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public WindowsPowerOverlayMode PowerMode { get => _power; set => SetProperty(ref _power, value); }
    public ProfileCoolingMode CoolingMode
    {
        get => _cooling;
        set { SetProperty(ref _cooling, value); OnPropertyChanged(nameof(IsFixed)); OnPropertyChanged(nameof(IsCurve)); }
    }
    public bool IsFixed => CoolingMode == ProfileCoolingMode.Fixed;
    public bool IsCurve => CoolingMode == ProfileCoolingMode.CustomCurve;
    public Guid? AcProfile { get => _ac; set => SetProperty(ref _ac, value); }
    public Guid? BatteryProfile { get => _battery; set => SetProperty(ref _battery, value); }

    private async Task Reload()
    {
        _loaded = false; // Never overwrite a corrupt/unreadable catalog with an empty fallback.
        ProfileCatalog catalog = await Task.Run(_load) ?? new([], new(null, null));
        _loaded = true;
        Publish(catalog, true);
        New();
        Status = "Profile geladen. Keine Hardware geändert.";
    }

    private void New()
    {
        _editingId = null; Name = ""; PowerMode = WindowsPowerOverlayMode.Balanced;
        CoolingMode = ProfileCoolingMode.Normal; CurveText = ""; FixedValue = "114";
        _savedDraft = CurrentDraft;
        Status = "Neuer Entwurf – noch nicht gespeichert.";
    }

    private void Edit(LaptopProfile profile)
    {
        _editingId = profile.Id; Name = profile.Name; PowerMode = profile.PowerMode; CoolingMode = profile.CoolingMode;
        FixedValue = (profile.FixedRawValue ?? 114).ToString(CultureInfo.InvariantCulture);
        CurveText = profile.Curve is null ? "" : string.Join(Environment.NewLine, profile.Curve.Select(p => $"{p.Temperature}:{p.Value}"));
        _savedDraft = CurrentDraft;
        Status = "Entwurf geladen. Änderungen erst mit „Profil speichern“ übernehmen.";
    }

    private async Task SaveDraft()
    {
        byte? fixedValue = IsFixed ? byte.Parse(FixedValue, CultureInfo.InvariantCulture) : null;
        FanCurvePoint[]? curve = null;
        if (IsCurve)
        {
            string[] lines = CurveText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length != 15) throw new ArgumentException("Bitte genau 15 Zeilen Temperatur:Rohwert eingeben.");
            curve = lines.Select((line, index) =>
            {
                string[] pair = line.Split(':');
                if (pair.Length != 2) throw new ArgumentException($"Zeile {index + 1}: Temperatur:Rohwert erwartet.");
                if (!byte.TryParse(pair[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte temperature) ||
                    !byte.TryParse(pair[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte raw))
                    throw new ArgumentException($"Punkt {index + 1}: Temperatur und Rohwert müssen ganze Zahlen zwischen 0 und 255 sein.");
                return new FanCurvePoint((byte)index, temperature, raw);
            }).ToArray();
        }
        var profile = new LaptopProfile(_editingId ?? Guid.NewGuid(), Name, PowerMode, CoolingMode, fixedValue, curve);
        await Commit(_catalog.Upsert(profile));
        _editingId = profile.Id;
        Selected = profile;
        _savedDraft = CurrentDraft;
    }

    private async Task DeleteSelected()
    {
        if (Selected is not { } profile) throw new InvalidOperationException("Zuerst ein Profil auswählen.");
        if (_editingId == profile.Id && !CanDiscard(HasDraftChanges)) return;
        await Commit(_catalog.Remove(profile.Id));
        if (_editingId == profile.Id) { New(); Status = "Profil und betroffene Zuordnungen gelöscht. Keine Hardware geändert."; }
    }

    private async Task Commit(ProfileCatalog candidate, bool resetAssignments = false)
    {
        if (!_loaded) throw new InvalidOperationException("Profildatei zuerst erfolgreich laden. Vorhandene Datei wird nicht überschrieben.");
        await Task.Run(() => _save(candidate)); // Publish on the caller context only after confirmed disk write.
        Publish(candidate, resetAssignments);
        Status = "Gespeichert – nicht angewendet. Automatische Hardwareumschaltung noch nicht aktiv.";
    }

    private void Publish(ProfileCatalog catalog, bool resetAssignments)
    {
        Guid? pendingAc = AcProfile, pendingBattery = BatteryProfile;
        _catalog = catalog;
        Selected = null;
        OnPropertyChanged(nameof(Profiles)); OnPropertyChanged(nameof(AssignmentOptions));
        AcProfile = resetAssignments ? catalog.Assignments.AcProfile : RetainExisting(pendingAc);
        BatteryProfile = resetAssignments ? catalog.Assignments.BatteryProfile : RetainExisting(pendingBattery);
    }
    private Guid? RetainExisting(Guid? id) => _catalog.Profiles.Any(p => p.Id == id) ? id : null;
    private bool CanDiscard(bool changed) => !changed || _confirmDiscard("Ungespeicherte Änderungen verwerfen?");
    private async Task Guard(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = "Dateivorgang läuft …";
        try { await action(); }
        catch (Exception error) { Status = "Nicht gespeichert/geladen: " + error.Message; }
        finally { IsBusy = false; }
    }
}
