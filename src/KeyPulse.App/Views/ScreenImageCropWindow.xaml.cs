using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeyPulse.Core;
using KeyPulse.Core.Models;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace KeyPulse.App.Views;

public partial class ScreenImageCropWindow : Window
{
    private readonly BitmapSource _source;
    private readonly DisplayLayout _layout;
    private Point _dragStart;
    private double _dragLeft;
    private double _dragTop;
    private double _baseScale;
    private bool _dragging;
    private bool _initialized;

    public ScreenImageCropWindow(BitmapSource source, DisplayLayout layout, ScreenImageCropSettings? existing = null)
    {
        InitializeComponent();
        _source = source;
        _layout = layout;
        ExistingCrop = existing;
        BlurredImage.Source = source;
        ClearImage.Source = source;
        Loaded += (_, _) => InitializeViewport();
    }

    public ScreenImageCropSettings? Result { get; private set; }
    private ScreenImageCropSettings? ExistingCrop { get; }

    private void InitializeViewport()
    {
        var availableWidth = Math.Max(320, ViewportBorder.ActualWidth);
        var availableHeight = Math.Max(220, ViewportBorder.ActualHeight);
        var aspect = _layout.VirtualWidth / (double)Math.Max(1, _layout.VirtualHeight);
        if (availableWidth / availableHeight > aspect) availableWidth = availableHeight * aspect;
        else availableHeight = availableWidth / aspect;
        ViewportBorder.Width = availableWidth;
        ViewportBorder.Height = availableHeight;
        ImageCanvas.Width = availableWidth;
        ImageCanvas.Height = availableHeight;
        MonitorOverlay.Width = availableWidth;
        MonitorOverlay.Height = availableHeight;
        DrawMonitorOverlay();

        _baseScale = Math.Max(availableWidth / _source.PixelWidth, availableHeight / _source.PixelHeight);
        _initialized = true;
        if (ExistingCrop?.IsValid == true)
        {
            var zoom = Math.Clamp(1 / Math.Max(ExistingCrop.Width * _source.PixelWidth * _baseScale / availableWidth,
                                               ExistingCrop.Height * _source.PixelHeight * _baseScale / availableHeight), 1, 4);
            ZoomSlider.Value = zoom;
            UpdateImageSize();
            Canvas.SetLeft(BlurredImage, -ExistingCrop.X * BlurredImage.Width);
            Canvas.SetTop(BlurredImage, -ExistingCrop.Y * BlurredImage.Height);
            SyncClearImage();
            ClampPosition();
        }
        else ResetPosition();
    }

    private void DrawMonitorOverlay()
    {
        MonitorOverlay.Children.Clear();
        foreach (var monitor in _layout.Monitors)
        {
            var left = (monitor.Left - _layout.VirtualLeft) * MonitorOverlay.Width / _layout.VirtualWidth;
            var top = (monitor.Top - _layout.VirtualTop) * MonitorOverlay.Height / _layout.VirtualHeight;
            var width = monitor.Width * MonitorOverlay.Width / _layout.VirtualWidth;
            var height = monitor.Height * MonitorOverlay.Height / _layout.VirtualHeight;
            var border = new Border
            {
                Width = width,
                Height = height,
                BorderThickness = new Thickness(2),
                BorderBrush = System.Windows.Media.Brushes.White,
                Background = System.Windows.Media.Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = monitor.IsPrimary ? "主屏" : "显示器",
                    Foreground = System.Windows.Media.Brushes.White,
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 0, 0, 0)),
                    Padding = new Thickness(5, 2, 5, 2),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top
                }
            };
            Canvas.SetLeft(border, left); Canvas.SetTop(border, top);
            MonitorOverlay.Children.Add(border);
        }
    }

    private void UpdateImageSize()
    {
        if (!_initialized) return;
        var scale = _baseScale * ZoomSlider.Value;
        var width = _source.PixelWidth * scale;
        var height = _source.PixelHeight * scale;
        BlurredImage.Width = ClearImage.Width = width;
        BlurredImage.Height = ClearImage.Height = height;
    }

    private void ResetPosition()
    {
        if (!_initialized) return;
        ZoomSlider.Value = 1;
        UpdateImageSize();
        Canvas.SetLeft(BlurredImage, (ImageCanvas.Width - BlurredImage.Width) / 2);
        Canvas.SetTop(BlurredImage, (ImageCanvas.Height - BlurredImage.Height) / 2);
        SyncClearImage();
    }

    private void ClampPosition()
    {
        var left = Math.Clamp(Canvas.GetLeft(BlurredImage), ImageCanvas.Width - BlurredImage.Width, 0);
        var top = Math.Clamp(Canvas.GetTop(BlurredImage), ImageCanvas.Height - BlurredImage.Height, 0);
        Canvas.SetLeft(BlurredImage, left); Canvas.SetTop(BlurredImage, top);
        SyncClearImage();
    }

    private void SyncClearImage()
    {
        Canvas.SetLeft(ClearImage, Canvas.GetLeft(BlurredImage));
        Canvas.SetTop(ClearImage, Canvas.GetTop(BlurredImage));
    }

    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true; _dragStart = e.GetPosition(ImageCanvas);
        _dragLeft = Canvas.GetLeft(BlurredImage); _dragTop = Canvas.GetTop(BlurredImage);
        ViewportBorder.CaptureMouse();
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var point = e.GetPosition(ImageCanvas);
        Canvas.SetLeft(BlurredImage, _dragLeft + point.X - _dragStart.X);
        Canvas.SetTop(BlurredImage, _dragTop + point.Y - _dragStart.Y);
        ClampPosition();
    }

    private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false; ViewportBorder.ReleaseMouseCapture();
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomSlider.Value = Math.Clamp(ZoomSlider.Value + (e.Delta > 0 ? 0.12 : -0.12), 1, 4);
        e.Handled = true;
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;
        var centerX = (-Canvas.GetLeft(BlurredImage) + ImageCanvas.Width / 2) / Math.Max(1, BlurredImage.Width);
        var centerY = (-Canvas.GetTop(BlurredImage) + ImageCanvas.Height / 2) / Math.Max(1, BlurredImage.Height);
        UpdateImageSize();
        Canvas.SetLeft(BlurredImage, (ImageCanvas.Width / 2) - centerX * BlurredImage.Width);
        Canvas.SetTop(BlurredImage, (ImageCanvas.Height / 2) - centerY * BlurredImage.Height);
        ClampPosition();
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetPosition();
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var left = Canvas.GetLeft(BlurredImage);
        var top = Canvas.GetTop(BlurredImage);
        Result = new ScreenImageCropSettings(
            Math.Clamp(-left / BlurredImage.Width, 0, 1),
            Math.Clamp(-top / BlurredImage.Height, 0, 1),
            Math.Clamp(ImageCanvas.Width / BlurredImage.Width, 0, 1),
            Math.Clamp(ImageCanvas.Height / BlurredImage.Height, 0, 1),
            _layout.VirtualWidth / (double)Math.Max(1, _layout.VirtualHeight),
            _layout.Signature);
        DialogResult = true;
        Close();
    }
}
