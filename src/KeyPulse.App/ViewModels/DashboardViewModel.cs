using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

namespace KeyPulse.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    public event Action<DateOnly>? TrendDetailRequested;
    private static readonly SKColor Accent = new(0x4E, 0x6E, 0x9E);
    private static readonly SKColor AccentFill = new(0x4E, 0x6E, 0x9E, 0xC8);
    private static readonly SKColor ClickFill = new(0x5F, 0x99, 0x98, 0xD8);
    private static readonly SKColor WheelFill = new(0xA7, 0xB3, 0xC5, 0xE0);

    private readonly IDashboardQuery _query;
    private readonly IFlushService _flush;
    private readonly ThemeService _theme;
    private readonly Dispatcher _dispatcher;
    private readonly IUserSettings _settings;
    private readonly object _gate = new();
    private bool _busy;
    private bool _chartsStale = true;
    private DateOnly _chartsDate;
    private IReadOnlyList<DailyTrendPoint> _trendPoints = [];
    private IReadOnlyList<HourlyPoint> _hourlyPoints = [];
    private DateTime? _lastSuccessfulUpdate;
    private bool _isActive;

    public DashboardViewModel(IDashboardQuery query, IFlushService flush, ThemeService theme, IUserSettings settings)
    {
        _query = query;
        _flush = flush;
        _theme = theme;
        _settings = settings;
        _trendMetric = settings.DashboardTrendMetric;
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
    private string _distanceEstimateText = "约 0 m（估算）";

    [ObservableProperty]
    private string _mostUsedText = "暂无";

    [ObservableProperty]
    private string _statusText = "正在统计";

    [ObservableProperty]
    private string _dataStateText = "正在加载…";

    [ObservableProperty]
    private string _peakHourText = "暂无足够数据";

    [ObservableProperty]
    private string _topShortcutText = "暂无足够数据";

    [ObservableProperty]
    private string _topAppText = "暂无足够数据";

    [ObservableProperty]
    private string _comparisonText = "暂无历史基线";

    [ObservableProperty]
    private string _recordText = "暂无纪录";

    [ObservableProperty]
    private string _effectiveTimeText = "0 分钟";

    [ObservableProperty]
    private string _sessionCountText = "0 次";

    [ObservableProperty]
    private string _effectiveTimeHint = "有效时长从 1.3 起开始统计";

    public bool ShowInsights => _settings.ShowInsights;

    [ObservableProperty]
    private bool _hasDataWarning;

    [ObservableProperty]
    private DashboardTrendMetric _trendMetric = DashboardTrendMetric.Keys;

    public bool IsKeysTrend
    {
        get => TrendMetric == DashboardTrendMetric.Keys;
        set { if (value) TrendMetric = DashboardTrendMetric.Keys; }
    }

    public bool IsClicksTrend
    {
        get => TrendMetric == DashboardTrendMetric.Clicks;
        set { if (value) TrendMetric = DashboardTrendMetric.Clicks; }
    }

    public bool IsWheelTrend
    {
        get => TrendMetric == DashboardTrendMetric.Wheel;
        set { if (value) TrendMetric = DashboardTrendMetric.Wheel; }
    }

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
        if (_dispatcher.CheckAccess()) OnPropertyChanged(nameof(ShowInsights));
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
            DashboardInsights? insights = null;
            if (needCharts)
            {
                days = await _query.GetLast7DaysAsync(today.Date).ConfigureAwait(false);
                hours = await _query.GetTodayHourlyAsync(today.Date).ConfigureAwait(false);
                insights = await _query.GetInsightsAsync(today.Date).ConfigureAwait(false);
            }

            await _dispatcher.InvokeAsync(() =>
            {
                ApplyToday(today);
                if (days is not null && hours is not null)
                {
                    _trendPoints = days;
                    _hourlyPoints = hours;
                    BuildCharts(days, hours);
                    _chartsDate = today.Date;
                    _chartsStale = false;
                }
                if (insights is not null) ApplyInsights(insights);

                _lastSuccessfulUpdate = DateTime.Now;
                DataStateText = "更新于 " + _lastSuccessfulUpdate.Value.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
                HasDataWarning = false;
            });
        }
        catch
        {
            await _dispatcher.InvokeAsync(() =>
            {
                DataStateText = _lastSuccessfulUpdate is { } updated
                    ? "刷新失败，当前为 " + updated.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + " 的数据"
                    : "首次加载失败，请重试";
                HasDataWarning = true;
            });
        }
        finally
        {
            lock (_gate)
            {
                _busy = false;
            }
        }
    }

    public void SetActive(bool active)
    {
        _isActive = active;
        if (active) Refresh();
    }

    public void Refresh()
    {
        if (_isActive) _ = RefreshAsync();
    }

    private void ApplyToday(DashboardToday today)
    {
        var culture = CultureInfo.CurrentCulture;
        KeyCountText = today.KeyPressCount.ToString("N0", culture);
        ClickCountText = today.MouseClickCount.ToString("N0", culture);
        WheelCountText = today.WheelEventCount.ToString("N0", culture);
        DistanceText = FormatDistance(today.DistancePixels);
        DistanceEstimateText = FormatEstimatedDistance(today.EstimatedDistanceMeters);
        EffectiveTimeText = FormatDuration(today.EffectiveActiveSeconds);
        SessionCountText = today.ActivitySessionCount.ToString("N0", culture) + " 次";
        EffectiveTimeHint = today.HasEffectiveTime
            ? $"超过 {_settings.AfkThresholdMinutes} 分钟无输入后停止累计"
            : "有效时长从 1.3 起开始统计，不回填旧数据";
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
                trendValues[i] = TrendValue(days[i]);
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
            hourLabels[hour] = hour.ToString("00", CultureInfo.InvariantCulture);
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
                Name = TrendMetric switch
                {
                    DashboardTrendMetric.Clicks => "点击",
                    DashboardTrendMetric.Wheel => "滚轮",
                    _ => "按键"
                },
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
                MinStep = 3,
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

    private static string FormatHourlyPoint(ChartPoint point) =>
        point.Coordinate.PrimaryValue.ToString("N0", CultureInfo.CurrentCulture);

    private static string FormatDistance(double pixels)
    {
        var culture = CultureInfo.CurrentCulture;
        return pixels.ToString("N0", culture) + " px";
    }

    private static string FormatEstimatedDistance(double meters)
    {
        var culture = CultureInfo.CurrentCulture;
        return meters >= 1000
            ? "约 " + (meters / 1000).ToString("0.00", culture) + " km（估算）"
            : "约 " + meters.ToString(meters >= 10 ? "0" : "0.0", culture) + " m（估算）";
    }

    private static string FormatDuration(long seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours} 小时 {span.Minutes} 分钟"
            : $"{span.Minutes} 分钟";
    }

    private long TrendValue(DailyTrendPoint point) => TrendMetric switch
    {
        DashboardTrendMetric.Clicks => point.MouseClickCount,
        DashboardTrendMetric.Wheel => point.WheelEventCount,
        _ => point.KeyPressCount
    };

    private void ApplyInsights(DashboardInsights insights)
    {
        var culture = CultureInfo.CurrentCulture;
        PeakHourText = insights.PeakHour is { } hour
            ? $"{hour:00}:00–{hour:00}:59 · {insights.PeakActivity.ToString("N0", culture)} 次活动"
            : "暂无足够数据";
        TopShortcutText = insights.TopShortcut is null
            ? "暂无足够数据"
            : insights.TopShortcut + " · " + insights.TopShortcutCount.ToString("N0", culture) + " 次";
        TopAppText = insights.TopApp ?? "暂无足够数据";
        ComparisonText = insights.PreviousDailyAverage <= 0
            ? "暂无历史基线"
            : "较历史日均 " + ((insights.TodayActivity / insights.PreviousDailyAverage - 1) * 100)
                .ToString("+0;-0;0", culture) + "%";
        RecordText = insights.RecordDate is null
            ? "暂无纪录"
            : $"{insights.RecordDate:yyyy-MM-dd} · {insights.RecordActivity.ToString("N0", culture)} 次活动";
    }

    partial void OnTrendMetricChanged(DashboardTrendMetric value)
    {
        OnPropertyChanged(nameof(IsKeysTrend));
        OnPropertyChanged(nameof(IsClicksTrend));
        OnPropertyChanged(nameof(IsWheelTrend));
        _settings.DashboardTrendMetric = value;
        _settings.Save();
        if (_trendPoints.Count > 0)
        {
            BuildCharts(_trendPoints, _hourlyPoints);
        }
    }

    [RelayCommand]
    private Task Retry() => RefreshAsync();

    [RelayCommand]
    private void OpenTrendDetail(ChartPoint? point)
    {
        var index = point is null ? -1 : (int)Math.Round(point.Coordinate.SecondaryValue);
        var date = index >= 0 && index < _trendPoints.Count
            ? _trendPoints[index].Date
            : DateOnly.FromDateTime(DateTime.Now);
        TrendDetailRequested?.Invoke(date);
    }

    [RelayCommand]
    private void OpenHourlyDetail(ChartPoint? point) =>
        TrendDetailRequested?.Invoke(DateOnly.FromDateTime(DateTime.Now));

    private void OnFlushed()
    {
        _chartsStale = true;
        Refresh();
    }

    private void OnThemeChanged()
    {
        _chartsStale = true;
        Refresh();
    }
}
