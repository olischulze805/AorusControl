using AorusControl.App.Infrastructure;
using AorusControl.Core.Features.Diagnostics;
using AorusControl.Core.Features.Startup;
using AorusControl.Core.Services;

namespace AorusControl.App.Features.Platform;

/// <summary>
/// The Windows-side settings: the power-mode overlay and autostart. Grouped because neither
/// touches the laptop's own hardware - they are the operating system's state, read back the
/// same way every device setting in this app is.
/// </summary>
public sealed class WindowsSettingsViewModel : ObservableObject, IFeatureModule
{
    private readonly WindowsPowerOverlayController _overlay;
    private readonly IStartupManager _startup;

    private bool _powerBusy, _powerControlsEnabled, _startupBusy, _startWithWindows;
    private string _powerStatus = "Windows-Leistungsmodus wird gelesen …";
    private string _startupStatus = "Autostart wird geprüft …";
    private string _powerSource = "Stromquelle wird gelesen …";
    private string? _activeMode;

    public WindowsSettingsViewModel(WindowsPowerOverlayController overlay, IStartupManager startup)
    {
        _overlay = overlay;
        _startup = startup;
        SetPowerModeCommand = new AsyncRelayCommand<string>(name =>
            Enum.TryParse(name, ignoreCase: true, out WindowsPowerOverlayMode mode)
                ? SetPowerModeAsync(mode)
                : Task.CompletedTask);
        ToggleStartWithWindowsCommand = new AsyncRelayCommand(() => SetStartWithWindowsAsync(!StartWithWindows));
    }

    public AsyncRelayCommand<string> SetPowerModeCommand { get; }
    public AsyncRelayCommand ToggleStartWithWindowsCommand { get; }

    public bool IsBusy => _powerBusy || _startupBusy;
    public bool PowerControlsEnabled { get => _powerControlsEnabled; private set => SetProperty(ref _powerControlsEnabled, value); }
    public string PowerStatus { get => _powerStatus; private set => SetProperty(ref _powerStatus, value); }

    /// <summary>Netz- oder Akkubetrieb. Shown on the dashboard because it explains the rest:
    /// the power modes are only verified on AC, and the charge limit only matters off it.</summary>
    public string PowerSource { get => _powerSource; private set => SetProperty(ref _powerSource, value); }

    /// <summary>Highlighted power-mode chip, from Windows' own readback rather than from the
    /// last click.</summary>
    public string? ActivePowerMode
    {
        get => _activeMode;
        private set
        {
            if (!SetProperty(ref _activeMode, value)) return;
            OnPropertyChanged(nameof(ActivePowerModeLabel));
            OnPropertyChanged(nameof(PowerModeEffect));
        }
    }

    /// <summary>The running mode in the same words as its chip, for the dashboard tile.</summary>
    public string ActivePowerModeLabel => _activeMode switch
    {
        nameof(WindowsPowerOverlayMode.BestEfficiency) => "Energieeffizienz",
        nameof(WindowsPowerOverlayMode.BestPerformance) => "Beste Leistung",
        nameof(WindowsPowerOverlayMode.Balanced) => "Ausbalanciert",
        _ => "Noch nicht gelesen"
    };

    /// <summary>
    /// Spells out what the running mode actually changes. Tools in this category routinely
    /// imply that a "performance mode" also drives the fans; here it does not - the fan curve
    /// is ours, on the EC - and saying so plainly is worth more than a louder label.
    /// </summary>
    public string PowerModeEffect => _activeMode switch
    {
        nameof(WindowsPowerOverlayMode.BestEfficiency) =>
            "Niedrigerer Takt, Last bevorzugt auf sparsamen Kernen. Weniger Abwärme, dadurch drehen die Lüfter meist niedriger.",
        nameof(WindowsPowerOverlayMode.BestPerformance) =>
            "Boost länger erlaubt, Taktziele hoch. Mehr Abwärme, dadurch erreicht die Kurve ihre höheren Stufen früher.",
        nameof(WindowsPowerOverlayMode.Balanced) =>
            "Takt und Boost nach Last. Die Lüfter folgen nur der Temperatur, die sich daraus ergibt.",
        _ => "Noch kein Modus gelesen."
    } + " Die Lüfterkurve bleibt unverändert.";

    /// <summary>
    /// What this app does NOT claim about the power mode, shown at the section's info dot.
    ///
    /// It used to be appended to every one of the sentences above, which meant reading the
    /// same caveat three times. It is a property rather than a string in the XAML so it stays
    /// under test: the promise that the app never overstates where a mode was verified is
    /// exactly the kind of sentence that quietly disappears in a layout change.
    /// </summary>
    public string PowerModeScope =>
        "Unabhängig vom Lüfterprofil und nur für den getesteten Netzbetrieb. " +
        "GPU-Limits und EC-Einstellungen fasst Windows dabei nicht an.";

    public bool StartWithWindows { get => _startWithWindows; private set => SetProperty(ref _startWithWindows, value); }
    public string StartWithWindowsButtonText => StartWithWindows ? "Autostart deaktivieren" : "Autostart aktivieren";
    public string StartupStatus { get => _startupStatus; private set => SetProperty(ref _startupStatus, value); }

    /// <summary>Where the log files are, so "look in the log" is an actionable instruction
    /// rather than a scavenger hunt.</summary>
    public string LogDirectory => AppLog.Directory;

    public async Task StartAsync()
    {
        await LoadPowerModeAsync();
        await LoadStartupStateAsync();
    }

    /// <summary>Re-reads the power source on the telemetry tick; it changes under the app
    /// whenever someone plugs the charger in.</summary>
    public void RefreshPowerSource() => PowerSource = _overlay.IsOnAcPower() ? "Netzbetrieb" : "Akkubetrieb";

    public async Task SetPowerModeAsync(WindowsPowerOverlayMode mode)
    {
        if (_powerBusy || !PowerControlsEnabled) return;

        _powerBusy = true;
        PowerControlsEnabled = false;
        PowerStatus = $"{Describe(mode)} wird gesetzt …";
        try
        {
            if (!_overlay.IsOnAcPower())
                throw new InvalidOperationException("Bitte zuerst das Netzteil anschließen.");
            await Task.Run(() => _overlay.Set(mode));
            if (!_overlay.IsOnAcPower())
                throw new InvalidOperationException("Stromquelle während der Änderung gewechselt. Aktuellen Modus erneut prüfen.");
            Guid actual = await Task.Run(_overlay.ReadActiveForCurrentPowerSource);
            if (actual != GuidFor(mode))
                throw new InvalidOperationException($"Windows-Rücklesen stimmt nicht überein: {actual}.");
            ActivePowerMode = mode.ToString();
            PowerStatus = $"Aktiv: {Describe(mode)} (Netzbetrieb)";
        }
        catch (Exception exception)
        {
            AppLog.Error("power", $"Leistungsmodus {mode} fehlgeschlagen.", exception);
            PowerStatus = $"Leistungsmodus fehlgeschlagen: {exception.Message}";
            try
            {
                // Without this the chip keeps showing the mode that was clicked rather than
                // the one Windows is actually in.
                ActivePowerMode = KeyFor(await Task.Run(_overlay.ReadActiveForCurrentPowerSource));
            }
            catch (Exception readError)
            {
                AppLog.Warn("power", "Aktiver Modus nach Fehler nicht lesbar: " + readError.Message);
            }
        }
        finally
        {
            _powerBusy = false;
            PowerControlsEnabled = true;
            // Unconditional: the chips bind one-way and a RadioButton lights itself on click,
            // so only a notification pushes the real value back over a failed write.
            OnPropertyChanged(nameof(ActivePowerMode));
        }
    }

    /// <summary>Uses a Scheduled Task, not the registry Run key, specifically so this does not
    /// show a fresh UAC prompt at every single login - see StartupManager's own doc comment
    /// for why that distinction matters for an admin-required app like this one.</summary>
    public async Task SetStartWithWindowsAsync(bool enabled)
    {
        if (_startupBusy) return;
        _startupBusy = true;
        StartupStatus = enabled ? "Autostart wird eingerichtet …" : "Autostart wird entfernt …";
        try
        {
            if (enabled) await _startup.EnableAsync();
            else await _startup.DisableAsync();
            await ShowStartupStateAsync();
        }
        catch (Exception exception)
        {
            AppLog.Error("startup", "Autostart-Änderung fehlgeschlagen.", exception);
            StartupStatus = $"Autostart-Änderung fehlgeschlagen: {exception.Message}";
        }
        finally { _startupBusy = false; }
    }

    public void Dispose() { }

    private async Task LoadPowerModeAsync()
    {
        try
        {
            RefreshPowerSource();
            if (!_overlay.IsOnAcPower())
            {
                PowerStatus = "Akkubetrieb · Umschalten ist vorerst nur im getesteten Netzbetrieb freigegeben";
                return;
            }
            Guid current = await Task.Run(_overlay.ReadActiveForCurrentPowerSource);
            ActivePowerMode = KeyFor(current);
            PowerStatus = $"Aktiv: {DescribeGuid(current)} (Netzbetrieb)";
            PowerControlsEnabled = true;
        }
        catch (Exception exception)
        {
            AppLog.Error("power", "Windows-Leistungsmodus nicht verfügbar.", exception);
            PowerStatus = $"Windows-Leistungsmodus nicht verfügbar: {exception.Message}";
        }
    }

    private async Task LoadStartupStateAsync()
    {
        try { await ShowStartupStateAsync(); }
        catch (Exception exception) { StartupStatus = $"Autostart-Status nicht lesbar: {exception.Message}"; }
    }

    private async Task ShowStartupStateAsync()
    {
        StartWithWindows = await _startup.IsEnabledAsync();
        OnPropertyChanged(nameof(StartWithWindowsButtonText));
        StartupStatus = StartWithWindows
            ? "Startet automatisch mit Windows (angemeldet, erhöhte Rechte, kein Bestätigungsdialog nötig)."
            : "Startet aktuell nicht automatisch mit Windows.";
    }

    private static Guid GuidFor(WindowsPowerOverlayMode mode) => mode switch
    {
        WindowsPowerOverlayMode.BestEfficiency => WindowsPowerOverlayController.BestEfficiencyGuid,
        WindowsPowerOverlayMode.BestPerformance => WindowsPowerOverlayController.BestPerformanceGuid,
        _ => WindowsPowerOverlayController.BalancedGuid
    };

    private static string Describe(WindowsPowerOverlayMode mode) => mode switch
    {
        WindowsPowerOverlayMode.BestEfficiency => "Beste Energieeffizienz",
        WindowsPowerOverlayMode.BestPerformance => "Beste Leistung",
        _ => "Ausbalanciert"
    };

    private static string? KeyFor(Guid guid) =>
        guid == WindowsPowerOverlayController.BestEfficiencyGuid
            ? nameof(WindowsPowerOverlayMode.BestEfficiency)
            : guid == WindowsPowerOverlayController.BestPerformanceGuid
                ? nameof(WindowsPowerOverlayMode.BestPerformance)
                : guid == WindowsPowerOverlayController.BalancedGuid
                    ? nameof(WindowsPowerOverlayMode.Balanced)
                    : null;

    private static string DescribeGuid(Guid guid) =>
        KeyFor(guid) is { } key ? Describe(Enum.Parse<WindowsPowerOverlayMode>(key)) : $"Unbekannt ({guid})";
}
