using AorusControl.App.Controls;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using AorusControl.App;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.Cooling;
using AorusControl.Core.Features.PowerMonitoring;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Features.PowerProfiles;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

// Lays out the REAL windows offscreen against fakes, at several widths. Two things it is
// for: proving the layout still works when the window is narrow (nothing clipped, nothing
// pushed off the edge), and catching the XAML mistakes a compiler cannot see - a missing
// resource key, a style based on one defined later, a binding path that no longer exists.
// It opens no OS window and touches no hardware.
Exception? failure = null;
var thread = new Thread(() =>
{
    try
    {
        // Force the WPF-UI assembly to load before parsing: XAML resolves its namespace
        // from the assemblies already in the process, and nothing here has touched it yet.
        var app = new Application();
        // WPF-UI's own dictionaries are merged in code rather than parsed from the XML:
        // pack:// URIs only work once an Application exists, and touching the types here
        // is also what puts the assembly in front of the XAML parser, so the ui: prefix in
        // our own styles resolves.
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark });
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var source = XDocument.Load("src/AorusControl.App/App.xaml");
        // Application.Resources holds one ResourceDictionary; take that element itself
        // rather than wrapping its children in a second one, which WPF then rejects as a
        // keyless nested dictionary. Namespace declarations come along because App.xaml's
        // styles reference ui: (WPF-UI) types.
        var resources = new XElement(source.Root!.Element(ns + "Application.Resources")!.Element(ns + "ResourceDictionary")!);
        resources.Add(source.Root!.Attributes().Where(attribute =>
            attribute.IsNamespaceDeclaration && resources.Attribute(attribute.Name) is null));
        resources.Element(ns + "ResourceDictionary.MergedDictionaries")?.Remove();
        var parsed = (ResourceDictionary)XamlReader.Parse(resources.ToString());
        app.Resources.MergedDictionaries.Add(parsed);
        // The same call App.OnStartup makes, including updateAccent:false - without it the
        // WPF-UI controls render in their light default and the check would be judging a
        // window the user never sees.
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
            Wpf.Ui.Appearance.ApplicationTheme.Dark,
            Wpf.Ui.Controls.WindowBackdropType.Mica,
            updateAccent: false);

        string output = Path.GetFullPath("research/runs/ui");
        Directory.CreateDirectory(output);

        RenderMainWindow(output);
        RenderDashboardLive(output);
        RenderCoolingStates(output);
        RenderGraphicsPage(output);
        RenderToolTip(output);
        Console.WriteLine("PASS: main window laid out at every checked width; no native window or hardware started.");
        app.Shutdown();
    }
    catch (Exception error) { failure = error; }
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start(); thread.Join();
if (failure is not null) throw failure;

static void RenderMainWindow(string output)
{
    var vm = new MainWindowViewModel(new StubReader(), new StubKeyboard(), new StubFan(), new WindowsPowerOverlayController(),
        batteryController: new StubBattery(), fanCurveStore: new StubCurveStore(), startupManager: new StubStartup());
    // The same read a launch does, so the curve chart has real geometry to lay out rather
    // than an empty canvas.
    vm.Cooling.StartAsync().GetAwaiter().GetResult();
    var window = new MainWindow(vm);
    var content = (FrameworkElement)window.Content;
    content.DataContext = vm;

    // 720 is the narrowest the window can be dragged to; 1600 is a maximised 1080p screen.
    foreach (int width in new[] { 720, 1000, 1600 })
    {
        foreach (string section in new[] { "Dashboard", "Cooling", "Lighting", "Power", "About" })
        {
            vm.SelectedSection = section;
            // Tall enough that a section fits without scrolling: the point is to see the
            // whole page at each width, and controls further down are exactly the ones a
            // short render would keep hiding.
            Layout(content, width, 1500);
            Save(content, output, $"main-{section.ToLowerInvariant()}-{width}.png", width, 1500);
        }
    }
}

/// <summary>
/// The graphics page with both its lists filled.
///
/// The plain render above shows it empty, which is exactly the state that would hide a broken
/// binding: no rows means no drop-downs, and the two per-program rules live in drop-downs. A
/// row here that comes out without its chip selected is the failure this render is for.
/// </summary>
static void RenderGraphicsPage(string output)
{
    var vm = new MainWindowViewModel(new StubReader(), new StubKeyboard(), new StubFan(), new WindowsPowerOverlayController(),
        batteryController: new StubBattery(), fanCurveStore: new StubCurveStore(), startupManager: new StubStartup(),
        gpuPreferenceStore: new StubGpuRegistry(), gpuPreferenceSettings: new StubGpuSettings(),
        readPowerSource: () => AorusControl.Core.Features.PowerProfiles.LaptopPowerSource.Battery,
        gpuActivity: new StubGpuActivity());
    vm.Graphics.StartAsync().GetAwaiter().GetResult();
    vm.Graphics.RefreshRunningCommand.ExecuteAsync().GetAwaiter().GetResult();

    var window = new MainWindow(vm);
    var content = (FrameworkElement)window.Content;
    content.DataContext = vm;
    vm.SelectedSection = "Power";
    foreach (int width in new[] { 720, 1600 })
    {
        Layout(content, width, 1900);
        Save(content, output, $"main-power-filled-{width}.png", width, 1900);
    }
}

/// <summary>
/// The dashboard with numbers on it. The plain render above shows the page before anything
/// has been measured - every value dashed, every trend line empty - which is the state that
/// says least about the design. This one feeds three minutes of readings through the live
/// model first, so the degrees, the watts, the fan bars and the trend lines all carry real
/// shapes.
/// </summary>
static void RenderDashboardLive(string output)
{
    var vm = new MainWindowViewModel(new StubReader(), new StubKeyboard(), new StubFan(), new WindowsPowerOverlayController(),
        batteryController: new StubBattery(), fanCurveStore: new StubCurveStore(), startupManager: new StubStartup());
    vm.Cooling.StartAsync().GetAwaiter().GetResult();

    // A load arriving: cool, then a climb, then the fans catching up and holding it. Ninety
    // samples is exactly the history the live model keeps.
    for (int index = 0; index < 90; index++)
    {
        double share = index / 89.0;
        double cpu = 46 + 38 * Math.Min(1, share * 1.9) - 6 * Math.Sin(share * 9);
        double gpu = 41 + 26 * Math.Min(1, share * 1.6) - 4 * Math.Sin(share * 7);
        vm.Cooling.Live.Update(new TelemetrySnapshot(DateTimeOffset.Now, (ushort)cpu, (ushort)gpu,
            (ushort)(2200 + share * 2600), (ushort)(2000 + share * 2300),
            (ushort)(70 + share * 140), (ushort)(64 + share * 120)));
    }

    // The watt figures come from the dashboard's own reader, which a render must not run:
    // it would query the real CPU counter and the real graphics card. Writing the two
    // published strings is the whole of what that reader contributes to this picture.
    Publish(vm, "CpuPower", "32,4 W");
    Publish(vm, "GpuPower", "78,5 W");
    Publish(vm, "GpuPowerStatus", "Aktiv \u00b7 Windows D0");

    // The power card runs off a battery reading rather than a string, so it is fed one - and
    // ninety of them, so its trend line carries a shape like the two cards above it.
    var publishFlow = typeof(MainWindowViewModel).GetMethod("PublishBatteryFlow",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
    for (int index = 0; index < 90; index++)
    {
        uint milliwatts = (uint)(24_000 + 14_000 * Math.Sin(index / 11.0) + index * 60);
        publishFlow.Invoke(vm, [BatteryFlowMath.From(false, 0, milliwatts, 71_500, 99_013)]);
    }

    vm.SelectedSection = "Dashboard";
    var window = new MainWindow(vm);
    var content = (FrameworkElement)window.Content;
    content.DataContext = vm;
    foreach (int width in new[] { 720, 1000, 1600 })
    {
        Layout(content, width, 900);
        Save(content, output, $"dashboard-live-{width}.png", width, 900);
    }
}

static void Publish(MainWindowViewModel vm, string property, string value) =>
    typeof(MainWindowViewModel).GetProperty(property)!.GetSetMethod(nonPublic: true)!.Invoke(vm, [value]);

/// <summary>
/// The cooling section under each fan profile. The chart is meant to show what the running
/// profile does and to be draggable only when it is the user's own curve, and that is four
/// different pictures - none of which the other renders would ever reach, since they all show
/// whatever state the stub happens to report.
/// </summary>
static void RenderCoolingStates(string output)
{
    foreach ((string name, FanControlState state) in new (string, FanControlState)[]
    {
        ("normal", new FanControlState(0, 0, 0, 0, 57, 66, StubFan.Curve)),
        ("gaming", new FanControlState(0, 0, 1, 0, 57, 66, StubFan.Curve)),
        ("maximum", new FanControlState(1, 1, 0, 0, 229, 229, StubFan.Curve)),
        ("fixed", new FanControlState(1, 0, 0, 0, 114, 114, StubFan.Curve)),
        ("dynamic", new FanControlState(0, 1, 0, 0, 57, 66, StubFan.Curve)),
    })
    {
        var vm = new MainWindowViewModel(new StubReader(), new StubKeyboard(), new StubFan(state), new WindowsPowerOverlayController(),
            batteryController: new StubBattery(), fanCurveStore: new StubCurveStore(), startupManager: new StubStartup());
        vm.Cooling.StartAsync().GetAwaiter().GetResult();
        // A reading the rotors and the live marker can be drawn from: without one they would
        // render in their "nothing measured" state, which is the one picture that says least.
        vm.Cooling.Live.Update(new TelemetrySnapshot(DateTimeOffset.Now, 62, 58, 3100, 2750, 137, 114));
        vm.SelectedSection = "Cooling";
        var window = new MainWindow(vm);
        var content = (FrameworkElement)window.Content;
        content.DataContext = vm;
        Layout(content, 1000, 1200);
        // The pulse in the heat pipes is where this picture stops being a photograph, so the
        // render is taken with the waves part-way across rather than at the one moment when
        // nothing is lit.
        int lit = 0;
        foreach (ThermalLayout layout in Descendants<ThermalLayout>(content))
        {
            layout.PulsePhase = 0.42;
            layout.SpinForCheck(2.0);
            lit++;
        }
        if (lit != 1) throw new Exception($"expected one thermal layout on the cooling page, found {lit}");
        content.UpdateLayout();

        Save(content, output, $"cooling-{name}.png", 1000, 1200);
    }
}

/// <summary>Every control of this type in the tree - used to reach into the drawing that the
/// checks want to photograph in a particular state.</summary>
static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
{
    for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
    {
        DependencyObject child = VisualTreeHelper.GetChild(root, index);
        if (child is T match) yield return match;
        foreach (T deeper in Descendants<T>(child)) yield return deeper;
    }
}

static void RenderToolTip(string output)
{
    // A ToolTip cannot be parented, so it is laid out on its own: this is only here to prove
    // the style wraps its text and paints its card.
    var tip = new System.Windows.Controls.ToolTip
    {
        Content = "Hält die Lüfter unabhängig von der Temperatur, ganz links auch komplett aus - stufenlos über den ganzen Bereich der Firmware. Läuft über den absturzsicheren Hardware-Worker, der bei 65 °C von sich aus auf Normal zurückstellt.",
        MaxWidth = 380
    };
    Layout(tip, 400, 160);
    Save(tip, output, "tooltip.png", 400, 160);
}

static void Layout(FrameworkElement content, int width, int height)
{
    content.Width = width;
    content.Height = height;
    content.Measure(new Size(width, height));
    content.Arrange(new Rect(0, 0, width, height));
    content.UpdateLayout();
    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
    content.UpdateLayout();
}

static void Save(FrameworkElement content, string output, string filename, int width, int height)
{
    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(content);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var file = File.Create(Path.Combine(output, filename));
    encoder.Save(file);
}

// ---- fakes: read-only, and every setter throws so a render can never touch hardware ----
sealed class StubReader : IAorusTelemetryReader
{
    public DeviceCompatibility CheckCompatibility() => new(true, "Test", "Test", "Test", "Test");
    public Task<TelemetrySnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new TelemetrySnapshot(DateTimeOffset.Now, 54, 47, 2400, 2200, 92, 84));
    public void Dispose() { }
}

sealed class StubFan(FanControlState? state = null) : IAorusFanController
{
    public static readonly FanCurvePoint[] Curve = Enumerable.Range(0, 15)
        .Select(i => new FanCurvePoint((byte)i, (byte)(30 + i * 4), (byte)(i == 14 ? 229 : 57 + i * 12))).ToArray();
    private readonly FanControlState _state = state ?? new FanControlState(0, 1, 0, 0, 114, 120, Curve);
    public DeviceCompatibility CheckCompatibility() => new(true, "Test", "Test", "Test", "Test");
    public Task<FanControlState> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_state);
    public Task<FanProfileChangeResult> SetNormalAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<FanProfileChangeResult> SetFixedAsync(byte rawValue, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<FanProfileChangeResult> SetQuietAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<FanProfileChangeResult> SetGamingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<FanProfileChangeResult> SetMaximumAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<FanProfileChangeResult> SetDynamicAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<FanProfileChangeResult> SetCurveAsync(IReadOnlyList<FanCurvePoint> curve, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<FanProfileChangeResult> RestoreAsync(FanControlState state, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public void Dispose() { }
}

sealed class StubKeyboard : IAorusKeyboardRgbController
{
    private static readonly KeyboardRgbState Lit = new(
    [
        new KeyboardRgbZoneState(1, new KeyboardRgbColor(0, 255, 120), (byte)KeyboardBrightnessLevel.High),
        new KeyboardRgbZoneState(2, new KeyboardRgbColor(0, 200, 255), (byte)KeyboardBrightnessLevel.High),
        new KeyboardRgbZoneState(3, new KeyboardRgbColor(180, 0, 255), (byte)KeyboardBrightnessLevel.High),
    ]);
    public KeyboardRgbState ReadState() => Lit;
    public KeyboardRgbState ApplyState(KeyboardRgbState state) => state;
    public Task PlayEffectAsync(KeyboardRgbEffect effect, KeyboardEffectSpeed speed, CancellationToken cancellationToken) => Task.CompletedTask;
    public KeyboardRgbState SetLighting(bool enabled) => throw new NotSupportedException();
    public KeyboardRgbState SetBrightness(KeyboardBrightnessLevel level) => throw new NotSupportedException();
    public KeyboardRgbState SetColor(int zone, KeyboardRgbColor color, bool applyToAllZones) => throw new NotSupportedException();
    public void Dispose() { }
}

sealed class StubBattery : IAorusBatteryChargeController
{
    public DeviceCompatibility CheckCompatibility() => new(true, "Test", "Test", "Test", "Test");
    public Task<BatteryChargeState> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new BatteryChargeState(4, 80));
    public Task<BatteryChargeChangeResult> SetCustomLimitAsync(int limitPercent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<BatteryChargeChangeResult> SetStandardModeAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public void Dispose() { }
}

sealed class StubCurveStore : IFanCurveStore
{
    public IReadOnlyList<FanCurvePoint>? Load() => null;
    public void Save(IReadOnlyList<FanCurvePoint> curve) => throw new NotSupportedException();
}

sealed class StubStartup : AorusControl.Core.Features.Startup.IStartupManager
{
    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> RepairAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task EnableAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DisableAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

// The graphics card's two lists, filled from memory. Both stubs matter: with the real ones
// the render would write into this machine's own Windows graphics preferences.
sealed class StubGpuRegistry : AorusControl.Core.Features.GpuPreferences.IGpuPreferenceStore
{
    private readonly Dictionary<string, string> _entries = new(StringComparer.OrdinalIgnoreCase)
    {
        [@"C:\Program Files\Steam\steamapps\common\theHunter\theHunter.exe"] = "GpuPreference=2;",
        [@"C:\Program Files\Mozilla Firefox\firefox.exe"] = "GpuPreference=1;"
    };

    public IReadOnlyList<AorusControl.Core.Features.GpuPreferences.GpuPreferenceProgram> List() =>
        _entries.Select(entry => new AorusControl.Core.Features.GpuPreferences.GpuPreferenceProgram(entry.Key, entry.Value)).ToArray();

    public AorusControl.Core.Features.GpuPreferences.GpuPreferenceProgram? Find(string name) =>
        _entries.TryGetValue(name, out string? raw)
            ? new AorusControl.Core.Features.GpuPreferences.GpuPreferenceProgram(name, raw)
            : null;

    public void Set(string name, AorusControl.Core.Features.GpuPreferences.GpuPreference preference) =>
        _entries[name] = AorusControl.Core.Features.GpuPreferences.GpuPreferenceText.With(_entries.GetValueOrDefault(name), preference);
}

sealed class StubGpuSettings : AorusControl.Core.Features.GpuPreferences.IGpuPreferenceSettingsStore
{
    public IReadOnlyList<AorusControl.Core.Features.GpuPreferences.ManagedProgram> Load() =>
    [
        new(@"C:\Program Files\Steam\steamapps\common\theHunter\theHunter.exe", null),
        new(@"C:\Program Files\Mozilla Firefox\firefox.exe", null,
            OnAc: AorusControl.Core.Features.GpuPreferences.GpuPreference.Integrated,
            OnBattery: AorusControl.Core.Features.GpuPreferences.GpuPreference.Integrated)
    ];

    public void Save(IReadOnlyList<AorusControl.Core.Features.GpuPreferences.ManagedProgram> programs) { }
}

sealed class StubGpuActivity : AorusControl.Core.Features.GpuPreferences.IGpuActivityReader
{
    public IReadOnlyList<AorusControl.Core.Features.GpuPreferences.GpuUser>? Read() =>
    [
        new("theHunter", @"C:\Program Files\Steam\steamapps\common\theHunter\theHunter.exe", IsNvidia: true, ProcessIds: [1000]),
        new("Claude", @"C:\Program Files\WindowsApps\Claude_2.2553.1.0_x64__pzs8sxrjxfjjc\app\Claude.exe", IsNvidia: true, ProcessIds: [1000, 1001]),
        new("firefox", @"C:\Program Files\Mozilla Firefox\firefox.exe", IsNvidia: false, ProcessIds: [1000, 1001, 1002, 1003])
    ];
}
