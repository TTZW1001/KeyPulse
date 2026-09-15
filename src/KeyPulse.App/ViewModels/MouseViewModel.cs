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

public sealed partial class MouseViewModel : ObservableObject
{
    private const double ShareBarMax = 180;
    private static readonly SKColor Accent = new(0x4E, 0x6E, 0x9E);

    private readonly IMouseQuery _query;
    private readonly IFlushService _flush;
    private readonly ThemeService _theme;
    private readonly Dispatcher _dispatcher;
    private readonly object _gate = new();
    private bool _busy;
    private bool _chartsStale = true;
    private DateOnly _chartsDate;
    private KeyboardRange _range = KeyboardRange.Last7Days;

    public MouseViewModel(IMouseQuery query, IFlushService flush, ThemeService theme)
    {
        _query = query;
        _flush = flush;
        _theme = theme;
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

        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var from = _range == KeyboardRange.Today ? today : today.AddDays(-6);
            var totals = await _query.GetMouseTotalsAsync(from, today).ConfigureAwait(false);
            var needChart = _chartsStale || _chartsDate != today;
            IReadOnlyList<MouseDayPoint>? days = null;
            if (needChart)
            {
                days = await _query.GetLast7DaysAsync(today).ConfigureAwait(false);
            }

            await _dispatcher.InvokeAsync(() =>
            {
                ApplyTotals(totals);
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
        DistancePixelsText = mouse.DistancePixels.ToString("N0", culture) + " px";
        var converted = FormatConverted(mouse.DistancePixels, culture);
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

    internal static string? FormatConverted(double pixels, CultureInfo culture)
    {
        var meters = pixels / 96.0 * 0.0254;
        if (meters >= 1000)
        {
            return "约 " + (meters / 1000.0).ToString("0.00", culture) + " km";
        }

        if (meters >= 1)
        {
            return "约 " + meters.ToString("0.0", culture) + " m";
        }

        return null;
    }
}
