using AorusControl.App.Infrastructure;
using AorusControl.App.Localization;
using AorusControl.Core.Features.Diagnostics;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace AorusControl.App.Features.Updates;

/// <summary>Checks, downloads and applies releases from the GitHub release feed.</summary>
public sealed class UpdateViewModel : ObservableObject
{
    private const string RepositoryUrl = "https://github.com/olischulze805/AorusControl";
    private readonly UpdateManager? _updates;
    private readonly Func<TimeSpan, CancellationToken, Task> _wait;
    private readonly CancellationTokenSource _closing = new();
    private UpdateInfo? _available;
    private bool _busy, _downloaded, _notInstalled;
    private int _downloadProgress;
    private UpdateStage _stage = UpdateStage.NotChecked;
    private string? _failureDetails;
    private bool _downloadFailure;

    private static string NotAnInstallation => Strings.Current["Upd_NotInstalled"];

    public UpdateViewModel(IUpdateSource? source = null, Func<TimeSpan, CancellationToken, Task>? wait = null)
    {
        _wait = wait ?? Task.Delay;
        try
        {
            _updates = new UpdateManager(source ?? new GithubSource(RepositoryUrl, null, prerelease: false));
        }
        catch (Exception error)
        {
            _updates = null;
            _stage = UpdateStage.Unsupported;
            AppLog.Info("update", "Kein installiertes Paket gefunden: " + error.Message);
        }

        CheckCommand = new AsyncRelayCommand(CheckAsync);
        InstallCommand = new AsyncRelayCommand(InstallAsync);
        RestartCommand = new RelayCommand(RequestRestart);
        OpenReleasePageCommand = new RelayCommand(OpenReleasePage, () => HasUpdate);
        Strings.Current.PropertyChanged += OnLanguageChanged;
    }

    /// <summary>The actual running assembly version, never a separately maintained label.</summary>
    public string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public bool IsBusy { get => _busy; private set { SetProperty(ref _busy, value); RaiseState(); } }
    public string Status => _stage switch
    {
        UpdateStage.Checking => Strings.Current["Upd_Checking"],
        UpdateStage.UpToDate => Strings.Current.Format("Upd_UpToDate", CurrentVersion),
        UpdateStage.Available => Strings.Current.Format("Upd_Available", AvailableVersion),
        UpdateStage.Downloading => Strings.Current.Format("Upd_DownloadingProgress", DownloadProgress),
        UpdateStage.Ready => Strings.Current.Format("Upd_Ready", AvailableVersion),
        UpdateStage.Unsupported => NotAnInstallation,
        UpdateStage.Failed when _downloadFailure => Strings.Current.Format("Upd_DownloadFailed", _failureDetails),
        UpdateStage.Failed => Strings.Current.Format("Upd_CheckFailed", _failureDetails),
        _ => Strings.Current["Upd_NotCheckedYet"]
    };

    public bool IsSupported => _updates is not null && !_notInstalled;
    public bool HasUpdate => _available is not null;
    public bool IsDownloaded { get => _downloaded; private set { SetProperty(ref _downloaded, value); RaiseState(); } }
    public string? AvailableVersion => _available?.TargetFullRelease.Version.ToString();
    public string VersionSummary => HasUpdate
        ? Strings.Current.Format("Upd_VersionTransition", CurrentVersion, AvailableVersion)
        : Strings.Current.Format("Upd_InstalledVersion", CurrentVersion);
    public int DownloadProgress
    {
        get => _downloadProgress;
        private set => SetProperty(ref _downloadProgress, Math.Clamp(value, 0, 100));
    }
    public bool IsDownloading => _stage == UpdateStage.Downloading;
    public bool HasReleasePage => HasUpdate;
    public bool CanCheck => IsSupported && !IsBusy;
    public bool CanInstall => IsSupported && !IsBusy && HasUpdate && !IsDownloaded;

    public AsyncRelayCommand CheckCommand { get; }
    public AsyncRelayCommand InstallCommand { get; }
    public RelayCommand RestartCommand { get; }
    public RelayCommand OpenReleasePageCommand { get; }

    public event EventHandler? RestartRequested;
    public event EventHandler? UpdateFound;

    private void RequestRestart()
    {
        if (IsDownloaded) RestartRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenReleasePage()
    {
        if (!HasUpdate) return;
        try
        {
            Process.Start(new ProcessStartInfo($"{RepositoryUrl}/releases/tag/v{AvailableVersion}") { UseShellExecute = true });
        }
        catch (Exception error)
        {
            AppLog.Error("update", "Release-Seite konnte nicht geöffnet werden.", error);
        }
    }

    /// <summary>Called after the close sequence has safely handed the hardware back.</summary>
    public void ApplyDownloadedUpdateOnExit()
    {
        if (_updates is null || !IsDownloaded) return;
        try
        {
            AppLog.Info("update", $"Update auf {AvailableVersion} wird beim Beenden übernommen; die App startet danach neu.");
            _updates.WaitExitThenApplyUpdates(_available?.TargetFullRelease, silent: false, restart: true);
        }
        catch (Exception error)
        {
            AppLog.Error("update", "Update konnte beim Beenden nicht übernommen werden.", error);
        }
    }

    public Task CheckAsync() => CheckAsync(announceFailure: true);

    /// <summary>Checks once after launch without surfacing transient network failures.</summary>
    public async Task CheckOnStartupAsync()
    {
        if (!CanCheck) return;
        try { await _wait(TimeSpan.FromSeconds(8), _closing.Token); }
        catch (OperationCanceledException) { return; }
        if (_closing.IsCancellationRequested) return;

        await CheckAsync(announceFailure: false);
        if (HasUpdate) UpdateFound?.Invoke(this, EventArgs.Empty);
    }

    private async Task CheckAsync(bool announceFailure)
    {
        if (!CanCheck) return;
        IsBusy = true;
        UpdateStage previousStage = _stage;
        if (announceFailure) SetStage(UpdateStage.Checking);
        try
        {
            _available = await _updates!.CheckForUpdatesAsync();
            _failureDetails = null;
            IsDownloaded = false;
            SetStage(_available is null ? UpdateStage.UpToDate : UpdateStage.Available);
        }
        catch (NotInstalledException)
        {
            _notInstalled = true;
            SetStage(UpdateStage.Unsupported);
            AppLog.Info("update", "Läuft nicht aus einer Installation; Update-Prüfung entfällt.");
        }
        catch (Exception error)
        {
            AppLog.Error("update", "Update-Prüfung fehlgeschlagen.", error);
            if (announceFailure)
            {
                _failureDetails = error.Message;
                _downloadFailure = false;
                SetStage(UpdateStage.Failed);
            }
            else SetStage(previousStage);
        }
        finally { IsBusy = false; RaiseState(); }
    }

    /// <summary>Stops pending update work from reaching an app that is closing.</summary>
    public void CancelStartupCheck()
    {
        _closing.Cancel();
        Strings.Current.PropertyChanged -= OnLanguageChanged;
    }

    public async Task InstallAsync()
    {
        if (!CanInstall) return;
        IsBusy = true;
        DownloadProgress = 0;
        SetStage(UpdateStage.Downloading);
        try
        {
            IProgress<int> progress = new Progress<int>(value =>
            {
                DownloadProgress = value;
                OnPropertyChanged(nameof(Status));
            });
            await _updates!.DownloadUpdatesAsync(_available!, progress.Report, _closing.Token);
            DownloadProgress = 100;
            IsDownloaded = true;
            SetStage(UpdateStage.Ready);
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested) { }
        catch (Exception error)
        {
            AppLog.Error("update", "Update-Download fehlgeschlagen.", error);
            _failureDetails = error.Message;
            _downloadFailure = true;
            SetStage(UpdateStage.Failed);
        }
        finally { IsBusy = false; }
    }

    private void SetStage(UpdateStage stage)
    {
        _stage = stage;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsDownloading));
    }

    private void RaiseState()
    {
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(AvailableVersion));
        OnPropertyChanged(nameof(VersionSummary));
        OnPropertyChanged(nameof(HasReleasePage));
        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(CanInstall));
        OpenReleasePageCommand.RaiseCanExecuteChanged();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(VersionSummary));
    }

    private enum UpdateStage { NotChecked, Checking, UpToDate, Available, Downloading, Ready, Unsupported, Failed }
}
