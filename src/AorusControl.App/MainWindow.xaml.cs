using System.ComponentModel;
using System.Windows;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Models;
using Wpf.Ui.Controls;

namespace AorusControl.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;

    /// <summary>For the tray menu, which offers a couple of actions without opening the
    /// window at all - the point of sitting in the tray in the first place.</summary>
    internal MainWindowViewModel ViewModel => _viewModel;
    private readonly IRecentColorsStore _recentColorsStore = new RecentColorsStore(System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AorusControl", "recent-colors-v1.json"));
    private bool _closeReady;
    private bool _closePending;
    private bool _exitRequested;
    private bool _restartForUpdate;

    public MainWindow() : this(new MainWindowViewModel()) { }

    /// <summary>Takes the ViewModel so the render checks can lay this window out against
    /// fakes. Nothing else differs - it is the same window, the same XAML, the same
    /// styles, which is the only way an offscreen render proves anything.</summary>
    internal MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
        IsVisibleChanged += (_, _) => UpdateVisibilityForViewModel();
        // IsVisible stays true while minimized, so without this the live preview and the
        // telemetry poll would keep running for a window nobody can see.
        StateChanged += (_, _) => UpdateVisibilityForViewModel();
        // The update module asks; the window is what actually closes, because the fans and
        // the lighting have to be handed back before anything replaces the executable.
        _viewModel.Updates.RestartRequested += (_, _) => { _restartForUpdate = true; RequestExit(); };
    }

    /// <summary>The chart edited the curve rows; the write itself is the ViewModel's,
    /// debounced so a burst of small drags is one device transaction.</summary>
    private void OnFanCurveEdited(object sender, EventArgs eventArgs) => _viewModel.Cooling.ScheduleCurveApply();

    /// <summary>Best-effort hardware handback for a Windows shutdown or logoff, where there
    /// is no time for the normal close sequence.</summary>
    public void RestoreHardwareBeforeShutdown() => _viewModel.RestoreFansToFirmware();

    public void RequestExit()
    {
        _exitRequested = true;
        Close();
    }

    private void OnProfilesClick(object sender, RoutedEventArgs e) => new ProfileWindow { Owner = this }.ShowDialog();

    private void OnOpenLogFolderClick(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            System.IO.Directory.CreateDirectory(AorusControl.Core.Features.Diagnostics.AppLog.Directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                AorusControl.Core.Features.Diagnostics.AppLog.Directory) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            AorusControl.Core.Features.Diagnostics.AppLog.Error("ui", "Protokollordner konnte nicht geöffnet werden.", exception);
            System.Windows.MessageBox.Show(this,
                $"Der Protokollordner konnte nicht geöffnet werden:\n{exception.Message}\n\nPfad: {AorusControl.Core.Features.Diagnostics.AppLog.Directory}",
                "Protokoll", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }

    private void UpdateVisibilityForViewModel() =>
        _viewModel.SetDashboardVisible(IsVisible && WindowState != WindowState.Minimized);

    private async void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        // The ViewModel starts on "Dashboard"; without this the pane itself shows nothing
        // selected, so the highlight and the visible section disagree on first launch.
        // SelectedItem is read-only on NavigationView - Navigate is how a selection is
        // made programmatically, and it raises SelectionChanged like a click would.
        Nav.Navigate("Dashboard");
        await _viewModel.StartAsync();
    }

    private async void OnClosing(object? sender, CancelEventArgs eventArgs)
    {
        if (_closeReady) return;
        eventArgs.Cancel = true;
        if (!_exitRequested) { Hide(); return; }
        if (_closePending) return;
        _closePending = true;
        IsEnabled = false;
        try
        {
            await _viewModel.PrepareToCloseAsync();
            _viewModel.Dispose();
            // Only now, with the hardware back under firmware control, is it safe to let the
            // updater replace the executable and start the new version.
            if (_restartForUpdate) _viewModel.Updates.ApplyDownloadedUpdateOnExit();
            _closeReady = true;
            Close();
        }
        catch (Exception exception)
        {
            _exitRequested = false;
            // The window stays open, so no updater may be left waiting for an exit that is
            // not coming.
            _restartForUpdate = false;
            System.Windows.MessageBox.Show(this,
                $"Eine Hardwareoperation konnte nicht sicher beendet werden. Das Fenster bleibt geöffnet.\n{exception.Message}\nBei Lüfterproblemen tools/Start-FanNormalRestore.cmd verwenden.",
                "Sicheres Beenden fehlgeschlagen", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            _closePending = false;
            IsEnabled = true;
        }
    }

    private void OnNavigationSelectionChanged(
        NavigationView sender,
        System.Windows.RoutedEventArgs eventArgs)
    {
        if (sender.SelectedItem is NavigationViewItem { TargetPageTag: string tag })
        {
            _viewModel.SelectedSection = tag;
        }
    }



    private async void OnZoneColorClick(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string zoneText } || !int.TryParse(zoneText, out int zone))
        {
            return;
        }

        KeyboardRgbColor current = _viewModel.Keyboard.GetZoneColor(zone);
        var picker = new ColorPickerWindow(current, _recentColorsStore) { Owner = this };
        if (picker.ShowDialog() != true || picker.Result is not { } chosen)
        {
            return;
        }

        await _viewModel.Keyboard.SetColorAsync(zone, chosen);
    }
}
