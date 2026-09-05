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
        AppLog.Initialize("app");
        // Must come before anything else draws: the installer relaunches the app with its
        // own arguments for first run, update and uninstall, and this is what answers them.
        Velopack.VelopackApp.Build()
            .OnFirstRun(_ => AppLog.Info("update", "Erste Ausführung nach der Installation."))
            .Run();
        // A crash the user only sees as a closing window is a crash nobody can report;
        // both of these paths put it on disk before anything else happens.
        DispatcherUnhandledException += (_, args) =>
            AppLog.Error("crash", "Unbehandelter UI-Fehler.", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("crash", "Unbehandelter Fehler.", args.ExceptionObject as Exception);
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
                System.Windows.MessageBox.Show("Lüfter auf Normal zurückgestellt und rückgelesen.", "AORUS Control");
            }
            catch (Exception exception)
            {
                exitCode = 1;
                System.Windows.MessageBox.Show($"Rückstellung fehlgeschlagen: {exception.Message}", "AORUS Control");
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
            _trayMenu.Items.Add("Öffnen", null, (_, _) => ShowWindow());
            _trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            _trayMenu.Items.Add("Lüfter auf Normal", null, (_, _) =>
                _ = window.ViewModel.Cooling.SetProfileCommand.ExecuteAsync("Normal"));
            _trayMenu.Items.Add("Beleuchtung umschalten", null, (_, _) =>
                _ = window.ViewModel.Keyboard.TogglePowerCommand.ExecuteAsync());
            _trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            _trayMenu.Items.Add("Beenden", null, (_, _) => { ShowWindow(); window.RequestExit(); });
            _tray = new System.Windows.Forms.NotifyIcon
            {
                // Reuses the same icon embedded into the exe via <ApplicationIcon> rather
                // than shipping/loading a second copy, so tray, taskbar and title bar are
                // always visually consistent.
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
                    ?? System.Drawing.SystemIcons.Application,
                Text = "AORUS Control · Öffnen oder Beenden per Rechtsklick",
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
            _tray.DoubleClick += (_, _) => ShowWindow();
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
            // Started by the logon task: stay in the tray. The app is there to hold the
            // lighting and the fan settings, which it does without a window - and a tool
            // that opens itself at every login is a tool people turn off.
            if (e.Args.Contains(AorusControl.Core.Features.Startup.StartupManager.BackgroundStartArgument))
                AppLog.Info("start", "Autostart: läuft im Infobereich, ohne Fenster.");
            else
                ShowWindow();
        }
        catch (Exception exception)
        {
            AppLog.Error("start", "Start fehlgeschlagen.", exception);
            System.Windows.MessageBox.Show($"AORUS Control konnte nicht gestartet werden: {exception.Message}");
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
