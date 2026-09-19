using System.Windows;
using AorusControl.App.Infrastructure;
using AorusControl.Core.Features.Diagnostics;
using System.Security.Principal;

namespace AorusControl.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceGate? _instance;
    private RegisteredWaitHandle? _activationWait;
    private System.Windows.Forms.NotifyIcon? _tray;
    private System.Windows.Forms.ContextMenuStrip? _trayMenu;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Velopack's install/update hooks have already run as the first instruction in Main.
        // Before any store is opened: earlier versions kept these files in what is now the
        // installer's own folder.
        AppData.MigrateFromInstallFolder();
        AppLog.Initialize("app");
        // A crash the user only sees as a closing window is a crash nobody can report;
        // both of these paths put it on disk before anything else happens.
        DispatcherUnhandledException += (_, args) =>
            AppLog.Error("crash", "Unbehandelter UI-Fehler.", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("crash", "Unbehandelter Fehler.", args.ExceptionObject as Exception);
        // A task nobody awaits - a power-source change, a background refresh, a setting
        // applying itself - throws into nothing. Since .NET 4.5 that no longer ends the
        // process, which is right for a tray app and also means such a failure leaves no
        // trace at all. It leaves one here.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("crash", "Fehler in einem nicht abgewarteten Vorgang.", args.Exception);
            args.SetObserved();
        };
        // Before the first window and the first view model: every sentence they produce on
        // the way up is already in the chosen language, and none has to be rebuilt afterwards.
        Localization.LanguageViewModel.Apply();
        // updateAccent MUST stay false: with the default (true), WPF-UI writes the user's
        // Windows accent into Application.Current.Resources at the top level, on top of
        // the accent keys App.xaml defines - so the hand-templated chips/tiles/sliders
        // stayed cyan while every WPF-UI control (Primary buttons, ToggleSwitch,
        // HyperlinkButton) silently took the system colour instead. One app, two accents.
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
            Wpf.Ui.Appearance.ApplicationTheme.Dark,
            Wpf.Ui.Controls.WindowBackdropType.Mica,
            updateAccent: false);
        if (e.Args.SequenceEqual(new[] { "--restore-fan-normal" }))
        {
            int exitCode = 0;
            try
            {
                using var controller = new AorusControl.Core.Services.GigabyteWmiFanController();
                await controller.SetNormalAsync();
                System.Windows.MessageBox.Show(Localization.Strings.Current["App_FansRestored"], "AORUS Control");
            }
            catch (Exception exception)
            {
                exitCode = 1;
                System.Windows.MessageBox.Show(Localization.Strings.Current.Format("App_RestoreFailed", exception.Message), "AORUS Control");
            }
            Shutdown(exitCode);
            return;
        }

        try
        {
            string sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Benutzer-ID fehlt.");
            _instance = new SingleInstanceGate(@"Local\AorusControl.UI." + sid);
            if (!_instance.IsPrimary)
            {
                _instance.RequestActivation();
                Shutdown();
                return;
            }
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var window = new MainWindow();
            MainWindow = window;
            window.Closed += (_, _) => Shutdown();
            // Two actions worth having without opening anything: put the fans back under
            // firmware control, and switch the lighting off. Both are things you want at the
            // moment the window is the last thing you feel like looking for.
            _trayMenu = new System.Windows.Forms.ContextMenuStrip();
            _trayMenu.Items.Add(Localization.Strings.Current["Tray_Open"], null, (_, _) => ShowWindow());
            _trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            _trayMenu.Items.Add(Localization.Strings.Current["Tray_FansNormal"], null, (_, _) =>
                _ = window.ViewModel.Cooling.SetProfileCommand.ExecuteAsync("Normal"));
            _trayMenu.Items.Add(Localization.Strings.Current["Tray_ToggleLighting"], null, (_, _) =>
                _ = window.ViewModel.Keyboard.TogglePowerCommand.ExecuteAsync());
            _trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            _trayMenu.Items.Add(Localization.Strings.Current["Tray_Exit"], null, (_, _) => { ShowWindow(); window.RequestExit(); });
            _tray = new System.Windows.Forms.NotifyIcon
            {
                // Reuses the same icon embedded into the exe via <ApplicationIcon> rather
                // than shipping/loading a second copy, so tray, taskbar and title bar are
                // always visually consistent.
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
                    ?? System.Drawing.SystemIcons.Application,
                Text = window.ViewModel.TrayText,
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
            _tray.DoubleClick += (_, _) => ShowWindow();
            // The tooltip follows the state it describes. Nothing is polled for it: both
            // halves come from modules that already announce their own changes.
            window.ViewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(AorusControl.App.ViewModels.MainWindowViewModel.TrayText) || _tray is null) return;
                try { _tray.Text = window.ViewModel.TrayText; }
                catch (ArgumentException error) { AppLog.Error("tray", "Kurzinfo zu lang für den Infobereich.", error); }
            };
            // The automatic check says nothing unless it found something; when it does, this
            // is how a user who is not looking at the window gets to hear about it. One
            // balloon per launch, and clicking it lands on the update card.
            _tray.BalloonTipClicked += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
            {
                ShowWindow();
                window.ShowUpdates();
            }));
            window.ViewModel.Updates.UpdateFound += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
            {
                string version = window.ViewModel.Updates.AvailableVersion ?? "neu";
                AppLog.Info("update", $"Version {version} verfügbar; Hinweis im Infobereich gezeigt.");
                _tray?.ShowBalloonTip(10_000, "AORUS Control",
                    Localization.Strings.Current.Format("Tray_UpdateBalloon", version),
                    System.Windows.Forms.ToolTipIcon.Info);
            }));
            // Windows shutdown and logoff never reach the window's own close path, so
            // without this the machine could come back up with the fans still pinned to a
            // Fixed or Maximum value and nothing running that knows why.
            SessionEnding += (_, args) =>
            {
                AppLog.Info("app", $"Windows beendet die Sitzung ({args.ReasonSessionEnding}); Lüfter werden zurückgestellt.");
                window.RestoreHardwareBeforeShutdown();
            };
            _activationWait = ThreadPool.RegisterWaitForSingleObject(_instance.Activation,
                (_, _) => Dispatcher.BeginInvoke(new Action(ShowWindow)), null, Timeout.Infinite, false);
            // Initialize the modules even when the logon task keeps the window hidden.
            // A hidden window never raises Loaded, but GPU power-source switching must
            // already be active before the user opens the app for the first time.
            bool background = e.Args.Contains(AorusControl.Core.Features.Startup.StartupManager.BackgroundStartArgument);
            if (background)
            {
                window.ViewModel.SetDashboardVisible(false);
                AppLog.Info("start", "Autostart: läuft im Infobereich, ohne Fenster.");
            }
            else
                ShowWindow();
            await window.ViewModel.StartAsync();
        }
        catch (Exception exception)
        {
            AppLog.Error("start", "Start fehlgeschlagen.", exception);
            System.Windows.MessageBox.Show(Localization.Strings.Current.Format("App_StartupFailed", exception.Message));
            Shutdown(1);
        }
    }

    private void ShowWindow()
    {
        if (Dispatcher.HasShutdownStarted || MainWindow is null) return;
        MainWindow.Show();
        if (MainWindow.WindowState == WindowState.Minimized) MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationWait?.Unregister(null);
        _tray?.Dispose();
        _trayMenu?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
