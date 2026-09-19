using Velopack;

namespace AorusControl.App;

internal static class Program
{
    /// <summary>
    /// Velopack must see its install/update hooks before WPF, logging, settings or hardware
    /// initialise. Hook launches can therefore finish immediately without starting the app.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
