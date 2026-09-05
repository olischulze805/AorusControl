using System.Reflection;
using AorusControl.App.Infrastructure;
using AorusControl.Core.Features.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace AorusControl.App.Features.Updates;

/// <summary>
/// Finds, downloads and applies releases published on the project's GitHub releases page.
///
/// Velopack rather than a hand-rolled downloader: it ships the installer and the updater as
/// one thing, applies deltas, and swaps the installed copy without a UAC prompt because the
/// app lives under the user's own LocalAppData. That last part is what makes automatic
/// updates acceptable here at all - an updater that needs an admin prompt every time is one
/// people learn to dismiss.
///
/// Nothing is downloaded or installed without the user asking: check is one button, install
/// is another, and the new version only takes effect on the next start unless they choose to
/// restart. An app that controls fans and lighting should not swap itself out from under a
/// running session.
/// </summary>
public sealed class UpdateViewModel : ObservableObject
{
    private readonly UpdateManager? _updates;
    private readonly string _unavailableReason;
    private UpdateInfo? _available;
    private bool _busy, _downloaded;
    private string _status = "Noch nicht geprüft.";

    public UpdateViewModel(IUpdateSource? source = null)
    {
        try
        {
            _updates = new UpdateManager(source ?? new GithubSource("https://github.com/olischulze805/AorusControl", null, prerelease: false));
            _unavailableReason = string.Empty;
        }
        catch (Exception error)
        {
            // Thrown when the app is not running from an installed copy - a development
            // build, or a folder someone unzipped. Not a failure worth an error dialog, but
            // it must be said out loud rather than silently doing nothing.
            _updates = null;
            AppLog.Info("update", "Kein installiertes Paket gefunden: " + error.Message);
            _unavailableReason = "Diese Version läuft nicht aus einer Installation - Updates gelten nur für die per Setup installierte App.";
            _status = _unavailableReason;
        }

        CheckCommand = new AsyncRelayCommand(CheckAsync);
        InstallCommand = new AsyncRelayCommand(InstallAsync);
        RestartCommand = new RelayCommand(() => _updates?.WaitExitThenApplyUpdates(_available?.TargetFullRelease));
    }

    /// <summary>The running version, from the assembly rather than a constant, so it cannot
    /// disagree with what was actually built.</summary>
    public string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public bool IsBusy { get => _busy; private set { SetProperty(ref _busy, value); Raise(); } }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool IsSupported => _updates is not null;
    public bool HasUpdate => _available is not null;
    public bool IsDownloaded { get => _downloaded; private set { SetProperty(ref _downloaded, value); Raise(); } }
    public string? AvailableVersion => _available?.TargetFullRelease.Version.ToString();
    public bool CanCheck => IsSupported && !IsBusy;
    public bool CanInstall => IsSupported && !IsBusy && HasUpdate && !IsDownloaded;

    public AsyncRelayCommand CheckCommand { get; }
    public AsyncRelayCommand InstallCommand { get; }
    public RelayCommand RestartCommand { get; }

    public async Task CheckAsync()
    {
        if (!CanCheck) return;
        IsBusy = true;
        Status = "Suche nach Updates …";
        try
        {
            _available = await _updates!.CheckForUpdatesAsync();
            Status = _available is null
                ? $"Version {CurrentVersion} ist aktuell."
                : $"Version {AvailableVersion} verfügbar.";
        }
        catch (Exception error)
        {
            AppLog.Error("update", "Update-Prüfung fehlgeschlagen.", error);
            Status = "Update-Prüfung fehlgeschlagen: " + error.Message;
        }
        finally { IsBusy = false; }
    }

    public async Task InstallAsync()
    {
        if (!CanInstall) return;
        IsBusy = true;
        Status = "Update wird geladen …";
        try
        {
            await _updates!.DownloadUpdatesAsync(_available!, progress => Status = $"Update wird geladen … {progress} %");
            IsDownloaded = true;
            Status = $"Version {AvailableVersion} ist bereit und wird beim nächsten Start aktiv.";
        }
        catch (Exception error)
        {
            AppLog.Error("update", "Update-Download fehlgeschlagen.", error);
            Status = "Update-Download fehlgeschlagen: " + error.Message;
        }
        finally { IsBusy = false; }
    }

    private void Raise()
    {
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(AvailableVersion));
        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(CanInstall));
    }
}
