using System.Windows;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.Keyboard;
using AorusControl.Core.Models;
using Wpf.Ui.Controls;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Point = System.Windows.Point;

namespace AorusControl.App;

public partial class ColorPickerWindow : FluentWindow
{
    private readonly ColorPickerViewModel _viewModel;
    private bool _draggingSv;
    private bool _draggingHue;

    public KeyboardRgbColor? Result { get; private set; }

    public ColorPickerWindow(KeyboardRgbColor initial, IRecentColorsStore recentColorsStore)
    {
        InitializeComponent();
        _viewModel = new ColorPickerViewModel(initial, recentColorsStore);
        DataContext = _viewModel;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ColorPickerViewModel.Hue)
                or nameof(ColorPickerViewModel.Saturation)
                or nameof(ColorPickerViewModel.Value))
            {
                UpdateThumbs();
            }
        };
        Loaded += (_, _) => UpdateThumbs();
    }

    private void UpdateThumbs()
    {
        double svWidth = SvCanvas.ActualWidth, svHeight = SvCanvas.ActualHeight;
        if (svWidth > 0 && svHeight > 0)
        {
            System.Windows.Controls.Canvas.SetLeft(SvThumb, _viewModel.Saturation * svWidth - SvThumb.Width / 2);
            System.Windows.Controls.Canvas.SetTop(SvThumb, (1 - _viewModel.Value) * svHeight - SvThumb.Height / 2);
        }
        double hueHeight = HueCanvas.ActualHeight;
        if (hueHeight > 0)
        {
            System.Windows.Controls.Canvas.SetTop(HueThumb, _viewModel.Hue / 360 * hueHeight - HueThumb.Height / 2);
        }
    }

    private void OnSvMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        _draggingSv = true;
        SvCanvas.CaptureMouse();
        UpdateSaturationValueFromPoint(eventArgs.GetPosition(SvCanvas));
    }

    private void OnSvMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (_draggingSv) UpdateSaturationValueFromPoint(eventArgs.GetPosition(SvCanvas));
    }

    private void OnSvMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        _draggingSv = false;
        SvCanvas.ReleaseMouseCapture();
    }

    private void UpdateSaturationValueFromPoint(Point point)
    {
        double width = SvCanvas.ActualWidth, height = SvCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;
        _viewModel.Saturation = Math.Clamp(point.X / width, 0, 1);
        _viewModel.Value = Math.Clamp(1 - point.Y / height, 0, 1);
    }

    private void OnHueMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        _draggingHue = true;
        HueCanvas.CaptureMouse();
        UpdateHueFromPoint(eventArgs.GetPosition(HueCanvas));
    }

    private void OnHueMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (_draggingHue) UpdateHueFromPoint(eventArgs.GetPosition(HueCanvas));
    }

    private void OnHueMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        _draggingHue = false;
        HueCanvas.ReleaseMouseCapture();
    }

    private void UpdateHueFromPoint(Point point)
    {
        double height = HueCanvas.ActualHeight;
        if (height <= 0) return;
        _viewModel.Hue = Math.Clamp(point.Y / height, 0, 1) * 360;
    }

    private void OnOkClick(object sender, RoutedEventArgs eventArgs)
    {
        Result = _viewModel.CurrentColor;
        _viewModel.CommitToRecentColors();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs eventArgs)
    {
        Result = null;
        DialogResult = false;
    }
}
