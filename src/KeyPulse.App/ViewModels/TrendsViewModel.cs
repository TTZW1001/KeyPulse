using System.Globalization;
using System.Windows.Threading;
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

namespace KeyPulse.App.ViewModels;

public sealed partial class TrendsViewModel : ObservableObject
{
    private static readonly SKColor Accent = new(0x4E, 0x6E, 0x9E);
    private static readonly SKColor AccentFill = new(0x4E, 0x6E, 0x9E, 0xC8);

    private readonly ITrendQuery _query;
    private readonly IFlushService _flush;
    private readonly ThemeService _theme;
    private readonly Dispatcher _dispatcher;
    private readonly object _gate = new();
    private bool _busy;
    private bool _suspendCustom;

    public TrendsViewModel(ITrendQuery query, IFlushService flush, ThemeService theme)
    {
        _query = query;
        _flush = flush;
        _theme = theme;
        _dispatcher = Dispatcher.CurrentDispatcher;
        var today = DateTime.Today;
        _customFromDate = today.AddDays(-6);
        _customToDate = today;
        _selectedRange = RangeOptions[2];
        _flush.Flushed += Refresh;
        _theme.Changed += Refresh;
        ApplyEmptyCharts();
    }

    public IReadOnlyList<TrendRangeOption> RangeOptions { get; } =
    [
        new(TrendRangeKind.Today, "今日"),
        new(TrendRangeKind.Yesterday, "昨日"),
        new(TrendRangeKind.Last7Days, "最近 7 天"),
        new(TrendRangeKind.Last30Days, "最近 30 天"),
        new(TrendRangeKind.ThisMonth, "本月"),
        new(TrendRangeKind.All, "全部"),
        new(TrendRangeKind.Custom, "自定义")
    ];

    [ObservableProperty]
    private TrendRangeOption _selectedRange;

    [ObservableProperty]
    private DateTime? _customFromDate;

    [ObservableProperty]
    private DateTime? _customToDate;

    [ObservableProperty]
    private bool _isCustomRange;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _rangeCaption = string.Empty;

    [ObservableProperty] private ISeries[] _keySeries = [];
    [ObservableProperty] private Axis[] _keyXAxes = [];
    [ObservableProperty] private Axis[] _keyYAxes = [];
    [ObservableProperty] private ISeries[] _clickSeries = [];
    [ObservableProperty] private Axis[] _clickXAxes = [];
    [ObservableProperty] private Axis[] _clickYAxes = [];
    [ObservableProperty] private ISeries[] _wheelSeries = [];
    [ObservableProperty] private Axis[] _wheelXAxes = [];
    [ObservableProperty] private Axis[] _wheelYAxes = [];
    [ObservableProperty] private ISeries[] _hourlySeries = [];
    [ObservableProperty] private Axis[] _hourlyXAxes = [];
    [ObservableProperty] private Axis[] _hourlyYAxes = [];

    public void Refresh() => _ = RefreshAsync();

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

        await _dispatcher.InvokeAsync(() => IsLoading = true);
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var earliest = await _query.GetEarliestDateAsync().ConfigureAwait(false);
            DateOnly? customFrom = CustomFromDate is { } fromDt ? DateOnly.FromDateTime(fromDt) : null;
            DateOnly? customTo = CustomToDate is { } toDt ? DateOnly.FromDateTime(toDt) : null;
            var (from, to) = TrendRangeResolver.Resolve(
                SelectedRange.Kind,
                today,
                earliest,
                customFrom,
                customTo);

            var daily = await _query.GetDailyAsync(from, to).ConfigureAwait(false);
            var hourly = await _query.GetHourlyAsync(from, to).ConfigureAwait(false);

            await _dispatcher.InvokeAsync(() =>
            {
                RangeCaption = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    + "  ～  "
                    + to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                ApplyCharts(daily, hourly);
                IsLoading = false;
            });
        }
        catch
        {
            await _dispatcher.InvokeAsync(() => IsLoading = false);
        }
        finally
        {
            lock (_gate)
            {
                _busy = false;
            }
        }
    }

    partial void OnSelectedRangeChanged(TrendRangeOption value)
    {
        IsCustomRange = value.Kind == TrendRangeKind.Custom;
        Refresh();
    }

    partial void OnCustomFromDateChanged(DateTime? value)
    {
        if (_suspendCustom || !IsCustomRange)
        {
            return;
        }

        ClampCustom();
        Refresh();
    }

    partial void OnCustomToDateChanged(DateTime? value)
    {
        if (_suspendCustom || !IsCustomRange)
        {
            return;
        }

        ClampCustom();
        Refresh();
    }

    private void ClampCustom()
    {
        if (CustomFromDate is not { } fromDt || CustomToDate is not { } toDt)
        {
            return;
        }

        var from = DateOnly.FromDateTime(fromDt);
        var to = DateOnly.FromDateTime(toDt);
        var (clampedFrom, clampedTo) = TrendRangeResolver.Resolve(
            TrendRangeKind.Custom,
            DateOnly.FromDateTime(DateTime.Now),
            null,
            from,
            to);
        var newFrom = clampedFrom.ToDateTime(TimeOnly.MinValue);
        var newTo = clampedTo.ToDateTime(TimeOnly.MinValue);
        if (newFrom == CustomFromDate && newTo == CustomToDate)
        {
            return;
        }

        _suspendCustom = true;
        CustomFromDate = newFrom;
        CustomToDate = newTo;
        _suspendCustom = false;
    }

    private void ApplyEmptyCharts()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var emptyDays = new DailyTrendPoint[7];
        for (var i = 0; i < 7; i++)
        {
            emptyDays[i] = new DailyTrendPoint(today.AddDays(i - 6), 0, 0, 0);
        }

        ApplyCharts(emptyDays, Enumerable.Range(0, 24).Select(h => new HourlyPoint(h, 0)).ToArray());
    }

    private void ApplyCharts(IReadOnlyList<DailyTrendPoint> daily, IReadOnlyList<HourlyPoint> hourly)
    {
        var fullLabels = daily.Select(d => d.Date.ToString("M/d", CultureInfo.CurrentCulture)).ToArray();
        var axisLabels = ThinLabels(fullLabels);
        var keys = daily.Select(d => d.KeyPressCount).ToArray();
        var clicks = daily.Select(d => d.MouseClickCount).ToArray();
        var wheels = daily.Select(d => d.WheelEventCount).ToArray();
        var (text, grid) = AxisPaints();

        KeySeries = [Line("按键", keys, fullLabels)];
        KeyXAxes = [CategoryAxis(axisLabels, text)];
        KeyYAxes = [ValueAxis(text, grid)];
        ClickSeries = [Line("点击", clicks, fullLabels)];
        ClickXAxes = [CategoryAxis(axisLabels, text)];
        ClickYAxes = [ValueAxis(text, grid)];
        WheelSeries = [Line("滚轮", wheels, fullLabels)];
        WheelXAxes = [CategoryAxis(axisLabels, text)];
        WheelYAxes = [ValueAxis(text, grid)];

        var hourValues = new long[24];
        var hourFull = new string[24];
        var hourAxis = new string[24];
        for (var hour = 0; hour < 24; hour++)
        {
            hourValues[hour] = hour < hourly.Count ? hourly[hour].ActivityCount : 0;
            hourFull[hour] = hour.ToString("00", CultureInfo.InvariantCulture) + " 时";
            hourAxis[hour] = hour % 3 == 0 ? hour.ToString("00", CultureInfo.InvariantCulture) : string.Empty;
        }

        HourlySeries =
        [
            new ColumnSeries<long>
            {
                Name = "活动",
                Values = hourValues,
                Fill = new SolidColorPaint(AccentFill),
                Stroke = null,
                MaxBarWidth = 16,
                YToolTipLabelFormatter = point => FormatPoint(point, hourFull)
            }
        ];
        HourlyXAxes = [CategoryAxis(hourAxis, text)];
        HourlyYAxes = [ValueAxis(text, grid)];
    }

    private static LineSeries<long> Line(string name, long[] values, string[] labels) =>
        new()
        {
            Name = name,
            Values = values,
            Fill = null,
            Stroke = new SolidColorPaint(Accent) { StrokeThickness = 2 },
            GeometrySize = values.Length > 60 ? 0 : 6,
            GeometryStroke = new SolidColorPaint(Accent) { StrokeThickness = 2 },
            GeometryFill = new SolidColorPaint(Accent),
            LineSmoothness = 0,
            YToolTipLabelFormatter = point => FormatPoint(point, labels)
        };

    private static Axis CategoryAxis(string[] labels, SolidColorPaint text) =>
        new()
        {
            Labels = labels,
            TextSize = 11,
            LabelsPaint = text,
            SeparatorsPaint = new SolidColorPaint(SKColors.Transparent)
        };

    private static Axis ValueAxis(SolidColorPaint text, SolidColorPaint grid) =>
        new()
        {
            MinLimit = 0,
            MinStep = 1,
            TextSize = 11,
            LabelsPaint = text,
            SeparatorsPaint = grid,
            Labeler = value => ((long)Math.Max(0, value)).ToString("N0", CultureInfo.CurrentCulture)
        };

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

    private static string[] ThinLabels(string[] full)
    {
        var n = full.Length;
        if (n <= 10)
        {
            return full;
        }

        var step = n <= 31 ? 5 : n <= 90 ? 14 : 30;
        var labels = new string[n];
        for (var i = 0; i < n; i++)
        {
            labels[i] = i == 0 || i == n - 1 || i % step == 0 ? full[i] : string.Empty;
        }

        return labels;
    }

    private static string FormatPoint(ChartPoint point, string[] labels)
    {
        var index = (int)Math.Round(point.Coordinate.SecondaryValue);
        if (index < 0 || index >= labels.Length)
        {
            index = 0;
        }

        var stamp = labels[index];
        return stamp + " · " + point.Coordinate.PrimaryValue.ToString("N0", CultureInfo.CurrentCulture);
    }
}

public sealed record TrendRangeOption(TrendRangeKind Kind, string Title)
{
    public override string ToString() => Title;
}
