using System.Globalization;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyPulse.App.Services;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Media = System.Windows.Media;

namespace KeyPulse.App.ViewModels;

public sealed partial class MouseViewModel : ObservableObject
{
    private const double ShareBarMax = 180;
    private static readonly SKColor Accent = new(0x4E, 0x6E, 0x9E);

    private readonly IMouseQuery _query;
    private readonly IFlushService _flush;
    private readonly ThemeService _theme;
    private readonly IPointerHeatmapQuery _pointerQuery;
    private readonly IUserSettings _settings;
    private readonly Dispatcher _dispatcher;
    private readonly object _gate = new();
    private bool _busy;
    private bool _chartsStale = true;
    private DateOnly _chartsDate;
    private KeyboardRange _range = KeyboardRange.Last7Days;

    public MouseViewModel(
        IMouseQuery query,
        IPointerHeatmapQuery pointerQuery,
        IFlushService flush,
        ThemeService theme,
        IUserSettings settings)
    {
        _query = query;
        _pointerQuery = pointerQuery;
        _flush = flush;
        _theme = theme;
        _settings = settings;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _flush.Flushed += () =>
        {
            _chartsStale = true;
            Refresh();
        };
        _theme.Changed += () =>
        {
            _chartsStale = true;
            Refresh();
        };
        BuildChart(Array.Empty<MouseDayPoint>());
    }

    [ObservableProperty] private string _leftText = "0";
    [ObservableProperty] private string _rightText = "0";
    [ObservableProperty] private string _middleText = "0";
    [ObservableProperty] private string _x1Text = "0";
    [ObservableProperty] private string _x2Text = "0";
    [ObservableProperty] private string _wheelUpText = "0";
    [ObservableProperty] private string _wheelDownText = "0";
    [ObservableProperty] private string _wheelLeftText = "0";
    [ObservableProperty] private string _wheelRightText = "0";
    [ObservableProperty] private string _distancePixelsText = "0 px";
    [ObservableProperty] private string _distanceConvertedText = string.Empty;
    [ObservableProperty] private string _totalClicksText = "0";
    [ObservableProperty] private string _totalScrollText = "0";
    [ObservableProperty] private bool _hasConvertedDistance;
    [ObservableProperty] private bool _showXButtons;
    [ObservableProperty] private bool _showHorizontalWheel;
    [ObservableProperty] private bool _hasClickShare;
    [ObservableProperty] private string _leftShareText = "暂无";
    [ObservableProperty] private string _rightShareText = "暂无";
    [ObservableProperty] private string _middleShareText = "暂无";
    [ObservableProperty] private double _leftShareWidth;
    [ObservableProperty] private double _rightShareWidth;
    [ObservableProperty] private double _middleShareWidth;
    [ObservableProperty] private ISeries[] _trendSeries = [];
    [ObservableProperty] private Axis[] _trendXAxes = [];
    [ObservableProperty] private Axis[] _trendYAxes = [];
    [ObservableProperty] private Media.Brush _leftHeatBrush = NewBrush(Media.Color.FromRgb(0xE6, 0xE6, 0xE2));
    [ObservableProperty] private Media.Brush _rightHeatBrush = NewBrush(Media.Color.FromRgb(0xE6, 0xE6, 0xE2));
    [ObservableProperty] private Media.Brush _middleHeatBrush = NewBrush(Media.Color.FromRgb(0xE6, 0xE6, 0xE2));
    [ObservableProperty] private Media.Brush _x1HeatBrush = NewBrush(Media.Color.FromRgb(0xE6, 0xE6, 0xE2));
    [ObservableProperty] private Media.Brush _x2HeatBrush = NewBrush(Media.Color.FromRgb(0xE6, 0xE6, 0xE2));
    [ObservableProperty] private BitmapSource? _clickHeatmapImage;
    [ObservableProperty] private BitmapSource? _trajectoryHeatmapImage;
    [ObservableProperty] private BitmapSource? _coverageHeatmapImage;
    [ObservableProperty] private string _coverageText = "暂无轨迹数据";

    public bool ScreenPositionStatsEnabled
    {
        get => _settings.ScreenPositionStatsEnabled;
        set
        {
            if (_settings.ScreenPositionStatsEnabled == value) return;
            _settings.ScreenPositionStatsEnabled = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public bool IsTodayRange
    {
        get => _range == KeyboardRange.Today;
        set
        {
            if (value)
            {
                SetRange(KeyboardRange.Today);
            }
        }
    }

    public bool IsLast7DaysRange
    {
        get => _range == KeyboardRange.Last7Days;
        set
        {
            if (value)
            {
                SetRange(KeyboardRange.Last7Days);
            }
        }
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(ScreenPositionStatsEnabled));
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        lock (_gate)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
        }

        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var from = _range == KeyboardRange.Today ? today : today.AddDays(-6);
            var totals = await _query.GetMouseTotalsAsync(from, today).ConfigureAwait(false);
            var pointer = await _pointerQuery.GetAsync(from, today).ConfigureAwait(false);
            var needChart = _chartsStale || _chartsDate != today;
            IReadOnlyList<MouseDayPoint>? days = null;
            if (needChart)
            {
                days = await _query.GetLast7DaysAsync(today).ConfigureAwait(false);
            }

            await _dispatcher.InvokeAsync(() =>
            {
                ApplyTotals(totals);
                ApplyPointer(pointer);
                if (days is not null)
                {
                    BuildChart(days);
                    _chartsDate = today;
                    _chartsStale = false;
                }
            });
        }
        catch
        {
            // keep last values
        }
        finally
        {
            lock (_gate)
            {
                _busy = false;
            }
        }
    }

    private void SetRange(KeyboardRange range)
    {
        if (_range == range)
        {
            return;
        }

        _range = range;
        _chartsStale = true;
        OnPropertyChanged(nameof(IsTodayRange));
        OnPropertyChanged(nameof(IsLast7DaysRange));
        Refresh();
    }

    private void ApplyTotals(MouseTotals mouse)
    {
        var culture = CultureInfo.CurrentCulture;
        LeftText = mouse.Left.ToString("N0", culture);
        RightText = mouse.Right.ToString("N0", culture);
        MiddleText = mouse.Middle.ToString("N0", culture);
        X1Text = mouse.XButton1.ToString("N0", culture);
        X2Text = mouse.XButton2.ToString("N0", culture);
        WheelUpText = mouse.WheelUp.ToString("N0", culture);
        WheelDownText = mouse.WheelDown.ToString("N0", culture);
        WheelLeftText = mouse.WheelLeft.ToString("N0", culture);
        WheelRightText = mouse.WheelRight.ToString("N0", culture);
        ShowXButtons = mouse.XButton1 > 0 || mouse.XButton2 > 0;
        ShowHorizontalWheel = mouse.WheelLeft > 0 || mouse.WheelRight > 0;
        TotalClicksText = (mouse.Left + mouse.Right + mouse.Middle + mouse.XButton1 + mouse.XButton2)
            .ToString("N0", culture);
        TotalScrollText = (mouse.WheelUp + mouse.WheelDown + mouse.WheelLeft + mouse.WheelRight)
            .ToString("N0", culture);
        DistancePixelsText = mouse.CursorDistancePixels.ToString("N0", culture) + " px";
        var converted = FormatMeters(mouse.EstimatedDistanceMeters, culture);
        DistanceConvertedText = converted ?? string.Empty;
        HasConvertedDistance = converted is not null;

        var (leftPct, rightPct, middlePct) = ClickShare.Percents(mouse.Left, mouse.Right, mouse.Middle);
        HasClickShare = mouse.Left + mouse.Right + mouse.Middle > 0;
        if (HasClickShare)
        {
            LeftShareText = leftPct.ToString("0", culture) + "%";
            RightShareText = rightPct.ToString("0", culture) + "%";
            MiddleShareText = middlePct.ToString("0", culture) + "%";
            LeftShareWidth = ShareBarMax * leftPct / 100.0;
            RightShareWidth = ShareBarMax * rightPct / 100.0;
            MiddleShareWidth = ShareBarMax * middlePct / 100.0;
        }
        else
        {
            LeftShareText = "暂无";
            RightShareText = "暂无";
            MiddleShareText = "暂无";
            LeftShareWidth = 0;
            RightShareWidth = 0;
            MiddleShareWidth = 0;
        }

        var maxButton = Math.Max(1, new[] { mouse.Left, mouse.Right, mouse.Middle, mouse.XButton1, mouse.XButton2 }.Max());
        var unused = _theme.IsDarkEffective
            ? Media.Color.FromRgb(0x2C, 0x2C, 0x2C)
            : Media.Color.FromRgb(0xE6, 0xE6, 0xE2);
        var accent = Media.Color.FromRgb(0x4E, 0x6E, 0x9E);
        LeftHeatBrush = NewBrush(Lerp(unused, accent, HeatmapScale.Normalize(mouse.Left, maxButton)));
        RightHeatBrush = NewBrush(Lerp(unused, accent, HeatmapScale.Normalize(mouse.Right, maxButton)));
        MiddleHeatBrush = NewBrush(Lerp(unused, accent, HeatmapScale.Normalize(mouse.Middle, maxButton)));
        X1HeatBrush = NewBrush(Lerp(unused, accent, HeatmapScale.Normalize(mouse.XButton1, maxButton)));
        X2HeatBrush = NewBrush(Lerp(unused, accent, HeatmapScale.Normalize(mouse.XButton2, maxButton)));
    }

    private void ApplyPointer(PointerHeatmapResult? result)
    {
        if (result is null)
        {
            ClickHeatmapImage = null;
            TrajectoryHeatmapImage = null;
            CoverageHeatmapImage = null;
            CoverageText = "暂无轨迹数据";
            return;
        }

        ClickHeatmapImage = RenderHeatmap(result, HeatmapMode.Clicks);
        TrajectoryHeatmapImage = RenderHeatmap(result, HeatmapMode.Trajectory);
        CoverageHeatmapImage = RenderHeatmap(result, HeatmapMode.Coverage);
        var percent = result.TotalPixels <= 0 ? 0 : result.VisitedPixels * 100.0 / result.TotalPixels;
        CoverageText = $"光标中心路径已经过 {percent:0.00}% · 未经过 {100 - percent:0.00}%";
    }

    private void BuildChart(IReadOnlyList<MouseDayPoint> days)
    {
        var values = new long[7];
        var labels = new string[7];
        for (var i = 0; i < 7; i++)
        {
            if (i < days.Count)
            {
                values[i] = days[i].ClickCount;
                labels[i] = days[i].Date.ToString("M/d", CultureInfo.CurrentCulture);
            }
            else
            {
                labels[i] = string.Empty;
            }
        }

        var (text, grid) = AxisPaints();
        TrendSeries =
        [
            new LineSeries<long>
            {
                Name = "点击",
                Values = values,
                Fill = null,
                Stroke = new SolidColorPaint(Accent) { StrokeThickness = 2 },
                GeometrySize = 7,
                GeometryStroke = new SolidColorPaint(Accent) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(Accent),
                LineSmoothness = 0,
                YToolTipLabelFormatter = point =>
                    point.Coordinate.PrimaryValue.ToString("N0", CultureInfo.CurrentCulture)
            }
        ];
        TrendXAxes =
        [
            new Axis
            {
                Labels = labels,
                TextSize = 11,
                LabelsPaint = text,
                SeparatorsPaint = new SolidColorPaint(SKColors.Transparent)
            }
        ];
        TrendYAxes =
        [
            new Axis
            {
                MinLimit = 0,
                MinStep = 1,
                TextSize = 11,
                LabelsPaint = text,
                SeparatorsPaint = grid,
                Labeler = value => ((long)Math.Max(0, value)).ToString("N0", CultureInfo.CurrentCulture)
            }
        ];
    }

    private (SolidColorPaint Text, SolidColorPaint Grid) AxisPaints()
    {
        if (_theme.IsDarkEffective)
        {
            return (
                new SolidColorPaint(new SKColor(0xA8, 0xA8, 0xA8)),
                new SolidColorPaint(new SKColor(0x34, 0x34, 0x34)) { StrokeThickness = 1 });
        }

        return (
            new SolidColorPaint(new SKColor(0x70, 0x70, 0x70)),
            new SolidColorPaint(new SKColor(0xE4, 0xE4, 0xE1)) { StrokeThickness = 1 });
    }

    internal static string? FormatMeters(double meters, CultureInfo culture)
    {
        if (meters >= 1000) return "约 " + (meters / 1000).ToString("0.00", culture) + " km（估算）";
        if (meters >= 1) return "约 " + meters.ToString("0.0", culture) + " m（估算）";
        return null;
    }

    private static BitmapSource RenderHeatmap(PointerHeatmapResult result, HeatmapMode mode)
    {
        const int width = 720;
        var height = Math.Clamp((int)Math.Round(width * result.Layout.VirtualHeight /
            (double)Math.Max(1, result.Layout.VirtualWidth)), 120, 360);
        var pixels = new byte[width * height * 4];
        foreach (var monitor in result.Layout.Monitors)
        {
            var left = (monitor.Left - result.Layout.VirtualLeft) * width / result.Layout.VirtualWidth;
            var top = (monitor.Top - result.Layout.VirtualTop) * height / result.Layout.VirtualHeight;
            var right = (monitor.Left + monitor.Width - result.Layout.VirtualLeft) * width / result.Layout.VirtualWidth;
            var bottom = (monitor.Top + monitor.Height - result.Layout.VirtualTop) * height / result.Layout.VirtualHeight;
            FillRect(pixels, width, height, left, top, right, bottom, 0xEC, 0xEC, 0xE9, 0xFF);
        }

        if (mode == HeatmapMode.Clicks)
        {
            var max = Math.Max(1L, result.Clicks.Count == 0 ? 1 : result.Clicks.Max(point => point.Count));
            foreach (var point in result.Clicks)
            {
                var monitor = result.Layout.Monitors.FirstOrDefault(item => item.Id == point.MonitorId);
                if (monitor is null) continue;
                var x = (monitor.Left + point.X - result.Layout.VirtualLeft) * width / result.Layout.VirtualWidth;
                var y = (monitor.Top + point.Y - result.Layout.VirtualTop) * height / result.Layout.VirtualHeight;
                DrawDot(pixels, width, height, x, y, 7, Math.Log(1 + point.Count) / Math.Log(1 + max));
            }
        }
        else
        {
            var grids = mode == HeatmapMode.Trajectory
                ? result.Densities.Select(grid => (grid.MonitorId, grid.Width, grid.Height,
                    Values: grid.Cells.Select(value => (double)value).ToArray())).ToList()
                : result.Coverages.Select(grid => (grid.MonitorId, grid.Width, grid.Height,
                    Values: grid.Cells.Select(value => (double)value).ToArray())).ToList();
            var max = Math.Max(1.0, grids.SelectMany(grid => grid.Values).DefaultIfEmpty(1).Max());
            foreach (var grid in grids)
            {
                var monitor = result.Layout.Monitors.FirstOrDefault(item => item.Id == grid.MonitorId);
                if (monitor is null) continue;
                for (var gy = 0; gy < grid.Height; gy++)
                for (var gx = 0; gx < grid.Width; gx++)
                {
                    var value = grid.Values[(gy * grid.Width) + gx];
                    if (value <= 0) continue;
                    var x0 = (monitor.Left + (gx * monitor.Width / grid.Width) - result.Layout.VirtualLeft) * width / result.Layout.VirtualWidth;
                    var y0 = (monitor.Top + (gy * monitor.Height / grid.Height) - result.Layout.VirtualTop) * height / result.Layout.VirtualHeight;
                    var x1 = (monitor.Left + (((gx + 1) * monitor.Width + grid.Width - 1) / grid.Width) - result.Layout.VirtualLeft) * width / result.Layout.VirtualWidth;
                    var y1 = (monitor.Top + (((gy + 1) * monitor.Height + grid.Height - 1) / grid.Height) - result.Layout.VirtualTop) * height / result.Layout.VirtualHeight;
                    FillHeatCell(pixels, width, height, x0, y0, Math.Max(x0 + 1, x1), Math.Max(y0 + 1, y1),
                        mode == HeatmapMode.Trajectory ? Math.Log(1 + value) / Math.Log(1 + max) : value / max);
                }
            }
        }

        var image = BitmapSource.Create(width, height, 96, 96, Media.PixelFormats.Bgra32, null, pixels, width * 4);
        image.Freeze();
        return image;
    }

    private static void FillRect(byte[] pixels, int width, int height, int left, int top, int right, int bottom,
        byte r, byte g, byte b, byte a)
    {
        for (var y = Math.Max(0, top); y < Math.Min(height, bottom); y++)
        for (var x = Math.Max(0, left); x < Math.Min(width, right); x++)
        {
            var index = ((y * width) + x) * 4;
            pixels[index] = b; pixels[index + 1] = g; pixels[index + 2] = r; pixels[index + 3] = a;
        }
    }

    private static void DrawDot(byte[] pixels, int width, int height, int centerX, int centerY, int radius, double intensity)
    {
        for (var y = centerY - radius; y <= centerY + radius; y++)
        for (var x = centerX - radius; x <= centerX + radius; x++)
        {
            var distance = Math.Sqrt(((x - centerX) * (x - centerX)) + ((y - centerY) * (y - centerY)));
            if (distance <= radius) SetHeatPixel(pixels, width, height, x, y, intensity * (1 - distance / radius));
        }
    }

    private static void SetHeatPixel(byte[] pixels, int width, int height, int x, int y, double intensity)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        intensity = Math.Clamp(intensity, 0.08, 1);
        var index = ((y * width) + x) * 4;
        pixels[index] = (byte)(pixels[index] * (1 - intensity) + 0x9E * intensity);
        pixels[index + 1] = (byte)(pixels[index + 1] * (1 - intensity) + 0x6E * intensity);
        pixels[index + 2] = (byte)(pixels[index + 2] * (1 - intensity) + 0x4E * intensity);
        pixels[index + 3] = 0xFF;
    }

    private static void FillHeatCell(
        byte[] pixels, int width, int height, int left, int top, int right, int bottom, double intensity)
    {
        for (var y = Math.Max(0, top); y < Math.Min(height, bottom); y++)
        for (var x = Math.Max(0, left); x < Math.Min(width, right); x++)
        {
            SetHeatPixel(pixels, width, height, x, y, intensity);
        }
    }

    private static Media.Color Lerp(Media.Color from, Media.Color to, double t) => Media.Color.FromRgb(
        (byte)(from.R + ((to.R - from.R) * t)),
        (byte)(from.G + ((to.G - from.G) * t)),
        (byte)(from.B + ((to.B - from.B) * t)));

    private static Media.SolidColorBrush NewBrush(Media.Color color)
    {
        var brush = new Media.SolidColorBrush(color); brush.Freeze(); return brush;
    }

    private enum HeatmapMode { Clicks, Trajectory, Coverage }
}
