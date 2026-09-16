using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyPulse.App.Services;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace KeyPulse.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private static readonly SKColor Accent = new(0x4E, 0x6E, 0x9E);
    private static readonly SKColor AccentFill = new(0x4E, 0x6E, 0x9E, 0xC8);
    private static readonly SKColor ClickFill = new(0x5F, 0x99, 0x98, 0xD8);
    private static readonly SKColor WheelFill = new(0xA7, 0xB3, 0xC5, 0xE0);

    private readonly IDashboardQuery _query;
    private readonly IFlushService _flush;
    private readonly ThemeService _theme;
    private readonly Dispatcher _dispatcher;
    private readonly object _gate = new();
    private bool _busy;
    private bool _chartsStale = true;
    private DateOnly _chartsDate;

    public DashboardViewModel(IDashboardQuery query, IFlushService flush, ThemeService theme)
    {
        _query = query;
        _flush = flush;
        _theme = theme;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _flush.Flushed += OnFlushed;
        _theme.Changed += OnThemeChanged;
        BuildCharts(Array.Empty<DailyTrendPoint>(), Array.Empty<HourlyPoint>());
    }

    [ObservableProperty]
    private string _keyCountText = "0";

    [ObservableProperty]
    private string _clickCountText = "0";

    [ObservableProperty]
    private string _wheelCountText = "0";

    [ObservableProperty]
    private string _distanceText = "0 px";

    [ObservableProperty]
    private string _mostUsedText = "暂无";

    [ObservableProperty]
    private string _statusText = "正在统计";

    [ObservableProperty]
    private ISeries[] _trendSeries = [];

    [ObservableProperty]
    private Axis[] _trendXAxes = [];

    [ObservableProperty]
    private Axis[] _trendYAxes = [];

    [ObservableProperty]
    private ISeries[] _hourlySeries = [];

    [ObservableProperty]
    private Axis[] _hourlyXAxes = [];

    [ObservableProperty]
    private Axis[] _hourlyYAxes = [];

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
            var today = await _query.GetTodayAsync().ConfigureAwait(false);
            var needCharts = _chartsStale || _chartsDate != today.Date;
            IReadOnlyList<DailyTrendPoint>? days = null;
            IReadOnlyList<HourlyPoint>? hours = null;
            if (needCharts)
            {
                days = await _query.GetLast7DaysAsync(today.Date).ConfigureAwait(false);
                hours = await _query.GetTodayHourlyAsync(today.Date).ConfigureAwait(false);
            }

            await _dispatcher.InvokeAsync(() =>
            {
                ApplyToday(today);
                if (days is not null && hours is not null)
                {
                    BuildCharts(days, hours);
                    _chartsDate = today.Date;
                    _chartsStale = false;
                }
            });
        }
        catch
        {
            // keep last painted values; next tick retries
        }
        finally
        {
            lock (_gate)
            {
                _busy = false;
            }
        }
    }

    public void Refresh() => _ = RefreshAsync();

    private void ApplyToday(DashboardToday today)
    {
        var culture = CultureInfo.CurrentCulture;
        KeyCountText = today.KeyPressCount.ToString("N0", culture);
        ClickCountText = today.MouseClickCount.ToString("N0", culture);
        WheelCountText = today.WheelEventCount.ToString("N0", culture);
        DistanceText = FormatDistance(today.DistancePixels);
        MostUsedText = today.TopKey is null || today.TopKeyCount <= 0
            ? "暂无"
            : today.TopKey + " · " + today.TopKeyCount.ToString("N0", culture);
        StatusText = today.State switch
        {
            TrackingState.Paused => "已暂停",
            TrackingState.Error => "监听失败",
            _ => "正在统计"
        };
    }

    private void BuildCharts(IReadOnlyList<DailyTrendPoint> days, IReadOnlyList<HourlyPoint> hours)
    {
        var trendValues = new long[7];
        var trendLabels = new string[7];
        for (var i = 0; i < 7; i++)
        {
            if (i < days.Count)
            {
                trendValues[i] = days[i].KeyPressCount;
                trendLabels[i] = days[i].Date.ToString("M/d", CultureInfo.CurrentCulture);
            }
            else
            {
                trendLabels[i] = string.Empty;
            }
        }

        var hourKeys = new long[24];
        var hourClicks = new long[24];
        var hourWheels = new long[24];
        var hourLabels = new string[24];
        for (var hour = 0; hour < 24; hour++)
        {
            hourLabels[hour] = hour % 3 == 0 ? hour.ToString("00", CultureInfo.InvariantCulture) : string.Empty;
            if (hour < hours.Count)
            {
                hourKeys[hour] = hours[hour].KeyPressCount;
                hourClicks[hour] = hours[hour].MouseClickCount;
                hourWheels[hour] = hours[hour].WheelEventCount;
            }
        }

        var (text, grid) = AxisPaints();

        TrendSeries =
        [
            new LineSeries<long>
            {
                Name = "按键",
                Values = trendValues,
                Fill = null,
                Stroke = new SolidColorPaint(Accent) { StrokeThickness = 2 },
                GeometrySize = 7,
                GeometryStroke = new SolidColorPaint(Accent) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(Accent),
                LineSmoothness = 0,
                YToolTipLabelFormatter = FormatPoint
            }
        ];
        TrendXAxes =
        [
            new Axis
            {
                Labels = trendLabels,
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

        HourlySeries =
        [
            new StackedColumnSeries<long>
            {
                Name = "键盘",
                Values = hourKeys,
                Fill = new SolidColorPaint(AccentFill),
                Stroke = null,
                MaxBarWidth = 18,
                YToolTipLabelFormatter = FormatHourlyPoint
            },
            new StackedColumnSeries<long>
            {
                Name = "鼠标点击",
                Values = hourClicks,
                Fill = new SolidColorPaint(ClickFill),
                Stroke = null,
                MaxBarWidth = 18,
                YToolTipLabelFormatter = FormatHourlyPoint
            },
            new StackedColumnSeries<long>
            {
                Name = "滚轮",
                Values = hourWheels,
                Fill = new SolidColorPaint(WheelFill),
                Stroke = null,
                MaxBarWidth = 18,
                YToolTipLabelFormatter = FormatHourlyPoint
            }
        ];
        HourlyXAxes =
        [
            new Axis
            {
                Labels = hourLabels,
                TextSize = 11,
                MinStep = 1,
                ForceStepToMin = true,
                LabelsPaint = text,
                SeparatorsPaint = new SolidColorPaint(SKColors.Transparent)
            }
        ];
        HourlyYAxes =
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

    private static string FormatPoint(ChartPoint point) =>
        point.Coordinate.PrimaryValue.ToString("N0", CultureInfo.CurrentCulture);

    private static string FormatHourlyPoint(ChartPoint point)
    {
        var hour = Math.Clamp((int)Math.Round(point.Coordinate.SecondaryValue), 0, 23);
        return $"{hour:00}:00–{hour:00}:59 · " +
               point.Coordinate.PrimaryValue.ToString("N0", CultureInfo.CurrentCulture);
    }

    private static string FormatDistance(double pixels)
    {
        var culture = CultureInfo.CurrentCulture;
        var meters = pixels / 96.0 * 0.0254;
        if (meters >= 10)
        {
            return (meters / 1000.0).ToString("0.00", culture) + " km";
        }

        if (meters >= 1)
        {
            return meters.ToString("0.0", culture) + " m";
        }

        return pixels.ToString("N0", culture) + " px";
    }

    private void OnFlushed()
    {
        _chartsStale = true;
        _ = RefreshAsync();
    }

    private void OnThemeChanged()
    {
        _chartsStale = true;
        _ = RefreshAsync();
    }
}
