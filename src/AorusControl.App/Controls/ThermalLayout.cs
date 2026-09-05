using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace AorusControl.App.Controls;

/// <summary>
/// This laptop's cooling system as the live picture of the cooling page: the machine seen
/// from below, both blowers turning, and the heat pipes lighting up with what the fans are
/// actually doing.
///
/// The picture itself is a still of the AORUS 5's own thermal assembly (Assets/README.md says
/// where it comes from and how the three files were made). Drawing it by hand had been tried
/// first and looked like a diagram of the machine rather than the machine; taking the real
/// frame and animating the parts that really move is both more honest and far less code.
///
/// Three pieces are stacked here, all from the same frame so they cannot fall out of
/// alignment: the body, the two fan discs cut out of it and rotated by <see cref="FanRotor"/>
/// at the measured speed, and a mask of the heat pipes through which a warm pulse is drawn.
/// The pulse travels outwards from the chips at the speed the fans are working, which is what
/// makes switching a profile visible rather than merely readable - and with no live reading
/// at all it stops and the whole assembly dims, so the picture never suggests it knows
/// something it has not been told.
/// </summary>
public sealed class ThermalLayout : Panel
{
    // The design space is the asset's own pixel space, so every coordinate here can be read
    // straight off the image.
    private const double DesignWidth = 1218, DesignHeight = 600;
    private static readonly Point LeftFan = new(198, 255.5), RightFan = new(1017.5, 266.5);
    private const double RotorSize = 194;

    /// <summary>Where the pulse starts: the chips, under the crossed pipes.</summary>
    private static readonly Point HeatSource = new(0.52, 0.42);
    private static readonly Color GlowColor = Color.FromRgb(0xFF, 0xE4, 0xB0);

    private static readonly BitmapImage Body = Load("thermal-body.jpg");
    private static readonly BitmapImage Pipes = Load("thermal-pipes.png");

    private readonly FanRotor _left = new() { Blades = Load("thermal-fan-left.png") };
    private readonly FanRotor _right = new() { Blades = Load("thermal-fan-right.png") };
    private double _phase;
    private bool _hooked;
    private long _lastTick;

    private static BitmapImage Load(string file)
    {
        var image = new BitmapImage();
        image.BeginInit();
        // The assembly is named explicitly rather than relying on the entry application's
        // own resources: the offscreen render checks load this control from their own host,
        // where a bare "/Assets/..." would look in the wrong assembly.
        image.UriSource = new Uri($"pack://application:,,,/AorusControl;component/Assets/{file}", UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static readonly DependencyProperty CpuRpmProperty = Forwarded(nameof(CpuRpm));
    public static readonly DependencyProperty GpuRpmProperty = Forwarded(nameof(GpuRpm));
    public static readonly DependencyProperty CpuDutyProperty = Forwarded(nameof(CpuDuty));
    public static readonly DependencyProperty GpuDutyProperty = Forwarded(nameof(GpuDuty));
    public static readonly DependencyProperty CpuTemperatureProperty = Forwarded(nameof(CpuTemperature), double.NaN);
    public static readonly DependencyProperty GpuTemperatureProperty = Forwarded(nameof(GpuTemperature), double.NaN);

    public static readonly DependencyProperty IsLiveProperty = DependencyProperty.Register(
        nameof(IsLive), typeof(bool), typeof(ThermalLayout),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender,
            (layout, _) => ((ThermalLayout)layout).OnLiveChanged()));

    private static DependencyProperty Forwarded(string name, double fallback = 0) =>
        DependencyProperty.Register(name, typeof(double), typeof(ThermalLayout),
            new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender,
                (layout, args) => ((ThermalLayout)layout).Forward(name, args.NewValue)));

    public double CpuRpm { get => (double)GetValue(CpuRpmProperty); set => SetValue(CpuRpmProperty, value); }
    public double GpuRpm { get => (double)GetValue(GpuRpmProperty); set => SetValue(GpuRpmProperty, value); }
    public double CpuDuty { get => (double)GetValue(CpuDutyProperty); set => SetValue(CpuDutyProperty, value); }
    public double GpuDuty { get => (double)GetValue(GpuDutyProperty); set => SetValue(GpuDutyProperty, value); }
    public double CpuTemperature { get => (double)GetValue(CpuTemperatureProperty); set => SetValue(CpuTemperatureProperty, value); }
    public double GpuTemperature { get => (double)GetValue(GpuTemperatureProperty); set => SetValue(GpuTemperatureProperty, value); }
    public bool IsLive { get => (bool)GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }

    public ThermalLayout()
    {
        Children.Add(_left);
        Children.Add(_right);
        Loaded += (_, _) => Hook(true);
        Unloaded += (_, _) => Hook(false);
        IsVisibleChanged += (_, _) => Hook(IsVisible);
    }

    /// <summary>Each fan's own values go to its own rotor, so the two fans in the picture sit
    /// where their readings do - left is the CPU side on this machine, right the GPU.</summary>
    private void Forward(string name, object value)
    {
        FanRotor rotor = name.StartsWith("Cpu", StringComparison.Ordinal) ? _left : _right;
        rotor.SetValue(name[3..] switch
        {
            "Rpm" => FanRotor.RpmProperty,
            "Duty" => FanRotor.DutyProperty,
            _ => FanRotor.TemperatureProperty
        }, value);
    }

    private void OnLiveChanged()
    {
        _left.IsLive = _right.IsLive = IsLive;
        Hook(IsVisible && IsLoaded);
    }

    // ---- the pulse ------------------------------------------------------------------
    private double FlowSpeed => IsLive ? Math.Clamp(Math.Max(CpuDuty, GpuDuty), 0, 100) / 100 : 0;

    private void Hook(bool wanted)
    {
        bool needed = wanted && FlowSpeed > 0.01;
        if (needed == _hooked) return;
        _hooked = needed;
        if (needed)
        {
            _lastTick = Environment.TickCount64;
            CompositionTarget.Rendering += OnFrame;
        }
        else
        {
            CompositionTarget.Rendering -= OnFrame;
        }
    }

    private void OnFrame(object? sender, EventArgs eventArgs)
    {
        long now = Environment.TickCount64;
        double elapsed = Math.Clamp((now - _lastTick) / 1000.0, 0, 0.25);
        _lastTick = now;
        // A crawl at the bottom of the range, a little over one pulse a second at full duty.
        _phase = (_phase + elapsed * (0.18 + FlowSpeed * 0.95)) % 1;
        InvalidateVisual();
    }

    // ---- layout ---------------------------------------------------------------------
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? DesignWidth : availableSize.Width;
        double scale = width / DesignWidth;
        foreach (UIElement child in InternalChildren) child.Measure(new Size(RotorSize * scale, RotorSize * scale));
        return new Size(width, DesignHeight * scale);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double scale = finalSize.Width / DesignWidth, size = RotorSize * scale;
        foreach ((UIElement child, Point centre) in new[] { ((UIElement)_left, LeftFan), (_right, RightFan) })
            child.Arrange(new Rect(centre.X * scale - size / 2, centre.Y * scale - size / 2, size, size));
        return finalSize;
    }

    // ---- drawing --------------------------------------------------------------------
    protected override void OnRender(DrawingContext context)
    {
        if (ActualWidth <= 0) return;
        var area = new Rect(0, 0, ActualWidth, ActualHeight);
        double radius = 10;

        // Rounded off to sit in a card rather than to look like a photo someone pasted in.
        context.PushClip(new RectangleGeometry(area, radius, radius));
        context.PushOpacity(IsLive ? 1 : 0.72);
        context.DrawImage(Body, area);
        context.Pop();
        DrawPulse(context, area);
        context.Pop();
    }

    /// <summary>
    /// The warm light in the pipes. It is drawn as a ring travelling outwards from the chips,
    /// masked to the pipes themselves - the mask is cut from the same image, so the light can
    /// only ever appear exactly where there is metal to carry heat.
    /// </summary>
    private void DrawPulse(DrawingContext context, Rect area)
    {
        if (!IsLive) return;
        double warmth = Math.Clamp((Math.Max(CpuTemperature, GpuTemperature) - 45) / 40.0, 0, 1);
        if (double.IsNaN(warmth)) warmth = 0;

        var brush = new RadialGradientBrush
        {
            GradientOrigin = HeatSource,
            Center = HeatSource,
            RadiusX = 0.62,
            RadiusY = 1.24
        };
        // A base glow that follows the temperature, so a hot machine's pipes read as hot even
        // between two pulses.
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(0x10 + warmth * 0x38), GlowColor.R, GlowColor.G, GlowColor.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(0x06 + warmth * 0x20), GlowColor.R, GlowColor.G, GlowColor.B), 1));

        // And the pulse itself: three stops sliding outwards together.
        double centre = _phase * 1.25 - 0.12;
        byte peak = (byte)(0x30 + FlowSpeed * 0x90);
        foreach ((double offset, byte alpha) in new[]
        {
            (centre - 0.16, (byte)0), (centre, peak), (centre + 0.16, (byte)0)
        })
        {
            if (offset is <= 0 or >= 1) continue;
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, GlowColor.R, GlowColor.G, GlowColor.B), offset));
        }
        brush.Freeze();

        context.PushOpacityMask(new ImageBrush(Pipes) { Stretch = Stretch.Fill });
        context.DrawRectangle(brush, null, area);
        context.Pop();
    }
}
