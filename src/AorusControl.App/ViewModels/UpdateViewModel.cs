using System.Reflection;
using AorusControl.App.Infrastructure;
using AorusControl.Core.Features.Updates;

namespace AorusControl.App.ViewModels;

/// <summary>
/// Checks a static JSON manifest for a newer release. Never downloads or installs
/// anything on its own - see UpdateChecker for why - only reports and links out.
/// </summary>
public sealed class UpdateViewModel : ObservableObject, IDisposable
{
    // No release feed exists for this project yet (no signed installer, no hosting).
    // This is a placeholder that will always fail cleanly and say so, rather than
    // silently pretending to check something real. Point it at an actual manifest
    // (see UpdateManifest's doc comment for the expected shape) once one exists.
    private static readonly Uri PlaceholderFeedUrl = new("https://example.invalid/aorus-control/update-manifest.json");

    private readonly UpdateChecker _checker = new();
    private readonly Version _currentVersion;
    private bool _busy;
    private string _status = "Noch nicht geprüft.";
    private UpdateManifest? _available;

    public UpdateViewModel()
    {
        _currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        CheckCommand = new AsyncRelayCommand(CheckAsync);
    }

    public string CurrentVersion => _currentVersion.ToString(3);
    public bool IsBusy { get => _busy; private set => SetProperty(ref _busy, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public UpdateManifest? Available { get => _available; private set => SetProperty(ref _available, value); }
    public bool HasUpdate => Available is not null;
    public AsyncRelayCommand CheckCommand { get; }

    public async Task CheckAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = "Suche nach Updates …";
        try
        {
            UpdateCheckResult result = await _checker.CheckAsync(PlaceholderFeedUrl, _currentVersion);
            Available = result.Manifest;
            Status = result.Message;
            OnPropertyChanged(nameof(HasUpdate));
        }
        finally { IsBusy = false; }
    }

    public void Dispose() => _checker.Dispose();
}
