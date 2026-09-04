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
using AorusControl.Core.Features.PowerProfiles;
using AorusControl.Core.Models;
using AorusControl.Core.Services;

Exception? failure = null;
var thread = new Thread(() =>
{
    try
    {
        // Real compiled window + production resource styles; no App startup or hardware objects.
        var app = new Application();
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var source = XDocument.Load("src/AorusControl.App/App.xaml");
        var resources = new XElement(ns + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            source.Root!.Element(ns + "Application.Resources")!.Nodes());
        app.Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString());
        var points = Enumerable.Range(0, 15).Select(i => new FanCurvePoint((byte)i, (byte)(30 + i * 4), (byte)(i == 14 ? 229 : 57 + i * 10))).ToArray();
        var profile = new LaptopProfile(Guid.NewGuid(), "Testprofil – Eigene Lüfterkurve", WindowsPowerOverlayMode.Balanced, ProfileCoolingMode.CustomCurve, curve: points);
        var vm = new ProfileEditorViewModel(() => new([profile], new(profile.Id, null)), _ => throw new Exception("No writes permitted in render test"));
        vm.Initialization.GetAwaiter().GetResult();
        vm.Selected = vm.Profiles.Single();
        vm.LoadSelectedCommand.Execute(null);
        var window = new ProfileWindow(vm);
        var content = (FrameworkElement)window.Content;
        // Explicit inheritance for offscreen content layout, without opening an OS window.
        content.DataContext = vm;
        var output = Path.GetFullPath("research/runs/profile-ui");
        Directory.CreateDirectory(output);
        foreach (int width in new[] { 760, 600 })
        {
            content.Width = width; content.Height = 750;
            content.Measure(new Size(width, 750));
            content.Arrange(new Rect(0, 0, width, 750));
            content.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
            var scroll = (ScrollViewer)content;
            scroll.ScrollToTop(); content.UpdateLayout();
            Render($"profile-{width}-top.png");
            scroll.ScrollToEnd(); content.UpdateLayout();
            Render($"profile-{width}-bottom.png");
            void Render(string filename)
            {
                var bitmap = new RenderTargetBitmap(width, 750, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, filename));
                encoder.Save(file);
            }
        }
        Console.WriteLine("Rendered real profile window at 760 and 600 pixels; no native window or hardware started.");
        app.Shutdown();
    }
    catch (Exception error) { failure = error; }
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start(); thread.Join();
if (failure is not null) throw failure;
