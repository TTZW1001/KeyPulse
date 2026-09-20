using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
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
using Media = System.Windows.Media;

namespace KeyPulse.App.ViewModels;

public sealed partial class MouseViewModel : ObservableObject
{
    private const double ShareBarMax = 180;
    private const int PreviewHeatmapMaxDimension = 480;
    private static readonly SKColor Accent = new(0x4E, 0x6E, 0x9E);

    private readonly IMouseQuery _query;
    private readonly IFlushService _flush;
    private readonly ThemeService _theme;
    private readonly IPointerHeatmapQuery _pointerQuery;
    private readonly IUserSettings _settings;
    private readonly PointerHeatmapRenderer _heatmapRenderer;
    private readonly ScreenImageService _screenImages;
    private readonly Dispatcher _dispatcher;
    private readonly object _gate = new();
    private bool _busy;
    private bool _fullRefreshPending;
    private bool _chartsStale = true;
    private DateOnly _chartsDate;
    private KeyboardRange _range = KeyboardRange.Last7Days;
    private KeyboardRange _heatmapRange = KeyboardRange.Last7Days;
    private PointerHeatmapResult? _pointerResult;
    private DateTime? _lastSuccessfulUpdate;
    private bool _isActive;

    public MouseViewModel(
        IMouseQuery query,
        IPointerHeatmapQuery pointerQuery,
        IFlushService flush,
        ThemeService theme,
        IUserSettings settings,
        PointerHeatmapRenderer heatmapRenderer,
        ScreenImageService screenImages)
    {
        _query = query;
        _pointerQuery = pointerQuery;
        _flush = flush;
        _theme = theme;
        _settings = settings;
        _heatmapRenderer = heatmapRenderer;
        _screenImages = screenImages;
        _range = settings.MouseRange;
        _heatmapRange = settings.MouseHeatmapRange;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _flush.Flushed += () =>
        {
            _chartsStale = true;
            RefreshPointerData();
        };
        _theme.Changed += () =>
        {
            _chartsStale = true;
            RefreshPointerData();
        };
        _screenImages.Changed += RefreshPointerData;
        BuildChart(Array.Empty<MouseDayPoint>());
        SelectedButton = ButtonOptions[0];
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
    [ObservableProperty] private string _coveragePanelTitle = "覆盖挑战";
    [ObservableProperty] private string _exportStatus = string.Empty;
    [ObservableProperty] private string _dataStateText = "正在加载…";
    [ObservableProperty] private HeatmapFilterOption? _selectedMonitor;
    [ObservableProperty] private HeatmapFilterOption? _selectedButton;
    [ObservableProperty] private bool _useRelativeIntensity = true;
    [ObservableProperty] private bool _useLogScale;
    [ObservableProperty] private bool _showClickLayer = true;
    [ObservableProperty] private bool _showTrajectoryLayer = true;
    [ObservableProperty] private bool _showCoverageLayer = true;
    [ObservableProperty] private string _heatmapLegendText = "暂无样本";

    public ObservableCollection<HeatmapFilterOption> MonitorOptions { get; } = [];
    public ObservableCollection<HeatmapFilterOption> ButtonOptions { get; } =
    [
        new("全部按键", null),
        new("左键", "Left"),
        new("右键", "Right"),
        new("中键", "Middle"),
        new("侧键 X1", "XButton1"),
        new("侧键 X2", "XButton2")
    ];

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

    public bool IsLast30DaysRange
    {
        get => _range == KeyboardRange.Last30Days;
        set { if (value) SetRange(KeyboardRange.Last30Days); }
    }

    public bool IsAllRange
    {
        get => _range == KeyboardRange.All;
        set { if (value) SetRange(KeyboardRange.All); }
    }

    public bool IsHeatmapTodayRange
    {
        get => _heatmapRange == KeyboardRange.Today;
        set { if (value) SetHeatmapRange(KeyboardRange.Today); }
    }

    public bool IsHeatmapLast7DaysRange
    {
        get => _heatmapRange == KeyboardRange.Last7Days;
        set { if (value) SetHeatmapRange(KeyboardRange.Last7Days); }
    }

    public bool IsHeatmapLast30DaysRange
    {
        get => _heatmapRange == KeyboardRange.Last30Days;
        set { if (value) SetHeatmapRange(KeyboardRange.Last30Days); }
    }

    public bool IsHeatmapAllRange
    {
        get => _heatmapRange == KeyboardRange.All;
        set { if (value) SetHeatmapRange(KeyboardRange.All); }
    }

    public bool IsRelativeIntensity
    {
        get => UseRelativeIntensity;
        set { if (value) UseRelativeIntensity = true; }
    }

    public bool IsGlobalIntensity
    {
        get => !UseRelativeIntensity;
        set { if (value) UseRelativeIntensity = false; }
    }

    public bool IsLinearScale
    {
        get => !UseLogScale;
        set { if (value) UseLogScale = false; }
    }

    public bool IsLogarithmicScale
    {
        get => UseLogScale;
        set { if (value) UseLogScale = true; }
    }

    public void Refresh()
    {
        QueueRefresh(includePointer: false);
    }

    public void SetActive(bool active)
    {
        _isActive = active;
        if (active) RefreshPointerData();
    }

    public Task RefreshAsync() => RefreshAsync(includePointer: true);

    private void RefreshPointerData() => QueueRefresh(includePointer: true);

    private void QueueRefresh(bool includePointer)
    {
        if (!_isActive) return;
        OnPropertyChanged(nameof(ScreenPositionStatsEnabled));
        _ = RefreshAsync(includePointer);
    }

    private async Task RefreshAsync(bool includePointer)
    {
        lock (_gate)
        {
            if (_busy)
            {
                _fullRefreshPending |= includePointer;
                return;
            }

            _busy = true;
        }

        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var totals = await _query.GetMouseTotalsAsync(_range.GetStartDate(today), today).ConfigureAwait(false);
            var pointer = includePointer
                ? await _pointerQuery.GetAsync(_heatmapRange.GetStartDate(today), today).ConfigureAwait(false)
                : null;
            var needChart = _chartsStale || _chartsDate != today;
            IReadOnlyList<MouseDayPoint>? days = null;
            if (needChart)
            {
                days = await _query.GetLast7DaysAsync(today).ConfigureAwait(false);
            }

            await _dispatcher.InvokeAsync(() =>
            {
                ApplyTotals(totals);
                if (includePointer)
                {
                    ApplyPointer(pointer);
                }
                if (days is not null)
                {
                    BuildChart(days);
                    _chartsDate = today;
                    _chartsStale = false;
                }
                _lastSuccessfulUpdate = DateTime.Now;
                DataStateText = "更新于 " + _lastSuccessfulUpdate.Value.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            });
        }
        catch
        {
            await _dispatcher.InvokeAsync(() => DataStateText = _lastSuccessfulUpdate is { } updated
                ? "刷新失败，当前为 " + updated.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + " 的数据"
                : "首次加载失败，请重试");
        }
        finally
        {
            var runPendingFullRefresh = false;
            lock (_gate)
            {
                _busy = false;
                if (_fullRefreshPending)
                {
                    _fullRefreshPending = false;
                    runPendingFullRefresh = _isActive;
                }
            }

            if (runPendingFullRefresh)
            {
                _ = RefreshAsync(includePointer: true);
            }
        }
    }

    [RelayCommand]
    private Task Retry() => RefreshAsync(includePointer: true);

    private void SetRange(KeyboardRange range)
    {
        if (_range == range)
        {
            return;
        }

        _range = range;
        _settings.MouseRange = range;
        _settings.Save();
        _chartsStale = true;
        OnPropertyChanged(nameof(IsTodayRange));
        OnPropertyChanged(nameof(IsLast7DaysRange));
        OnPropertyChanged(nameof(IsLast30DaysRange));
        OnPropertyChanged(nameof(IsAllRange));
        Refresh();
    }

    private void SetHeatmapRange(KeyboardRange range)
    {
        if (_heatmapRange == range) return;
        _heatmapRange = range;
        _settings.MouseHeatmapRange = range;
        _settings.Save();
        OnPropertyChanged(nameof(IsHeatmapTodayRange));
        OnPropertyChanged(nameof(IsHeatmapLast7DaysRange));
        OnPropertyChanged(nameof(IsHeatmapLast30DaysRange));
        OnPropertyChanged(nameof(IsHeatmapAllRange));
        RefreshPointerData();
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
        var accent = HeatmapPaletteService.Accent(_settings.HeatmapPalette);
        LeftHeatBrush = NewBrush(Lerp(unused, accent, HeatmapScale.Normalize(mouse.Left, maxButton)));
        RightHeatBrush = NewBrush(Lerp(unused, accent, HeatmapScale.Normalize(mouse.Right, maxButton)));
        MiddleHeatBrush = NewBrush(Lerp(unused, accent, HeatmapScale.Normalize(mouse.Middle, maxButton)));
        X1HeatBrush = NewBrush(Lerp(unused, accent, HeatmapScale.Normalize(mouse.XButton1, maxButton)));
        X2HeatBrush = NewBrush(Lerp(unused, accent, HeatmapScale.Normalize(mouse.XButton2, maxButton)));
    }

    private void ApplyPointer(PointerHeatmapResult? result)
    {
        _pointerResult = result;
        if (result is null)
        {
            ClickHeatmapImage = null;
            TrajectoryHeatmapImage = null;
            CoverageHeatmapImage = null;
            CoverageText = "累计像素覆盖（全部时间）：暂无轨迹数据";
            return;
        }

        var selectedMonitorId = SelectedMonitor?.Value;
        MonitorOptions.Clear();
        MonitorOptions.Add(new HeatmapFilterOption("全部显示器", null));
        foreach (var monitor in result.Layout.Monitors)
        {
            MonitorOptions.Add(new HeatmapFilterOption(
                monitor.IsPrimary
                    ? $"主屏 · {monitor.Width}×{monitor.Height}"
                    : $"{monitor.Id} · {monitor.Width}×{monitor.Height}",
                monitor.Id));
        }
        SelectedMonitor = MonitorOptions.FirstOrDefault(item => item.Value == selectedMonitorId) ?? MonitorOptions[0];
        RenderPointerImages();
        var percent = result.TotalPixels <= 0 ? 0 : result.VisitedPixels * 100.0 / result.TotalPixels;
        CoverageText = $"累计像素覆盖（全部时间）：光标中心路径已经过 {percent:0.00}% · 未经过 {100 - percent:0.00}%";
    }

    partial void OnSelectedMonitorChanged(HeatmapFilterOption? value) => RenderPointerImages();

    partial void OnSelectedButtonChanged(HeatmapFilterOption? value) => RenderPointerImages();

    partial void OnUseRelativeIntensityChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRelativeIntensity));
        OnPropertyChanged(nameof(IsGlobalIntensity));
        RenderPointerImages();
    }

    partial void OnUseLogScaleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLinearScale));
        OnPropertyChanged(nameof(IsLogarithmicScale));
        RenderPointerImages();
    }

    private void RenderPointerImages()
    {
        if (_pointerResult is null) return;
        var monitor = SelectedMonitor?.Value;
        var accent = HeatmapPaletteService.Accent(_settings.HeatmapPalette);
        ClickHeatmapImage = _heatmapRenderer.Render(
            _pointerResult, PointerHeatmapMode.Clicks, monitor, SelectedButton?.Value, UseRelativeIntensity, UseLogScale,
            accent, PreviewHeatmapMaxDimension);
        TrajectoryHeatmapImage = _heatmapRenderer.Render(
            _pointerResult, PointerHeatmapMode.Trajectory, monitor, null, UseRelativeIntensity, UseLogScale,
            accent, PreviewHeatmapMaxDimension);
        CoverageHeatmapImage = _heatmapRenderer.Render(
            _pointerResult, PointerHeatmapMode.ImageReveal, monitor, SelectedButton?.Value, UseRelativeIntensity, UseLogScale,
            accent, PreviewHeatmapMaxDimension);
        CoveragePanelTitle = _heatmapRenderer.HasReadyScreenImage(_pointerResult)
            ? "图片揭示（累计活动）"
            : "覆盖挑战";
        var clicks = _pointerResult.Clicks.Where(point =>
            (monitor is null || point.MonitorId == monitor) &&
            (SelectedButton?.Value is null || point.ButtonCode == SelectedButton.Value)).Sum(point => point.Count);
        var samples = _pointerResult.Densities.Where(grid => monitor is null || grid.MonitorId == monitor)
            .Sum(grid => grid.Cells.Sum(cell => (long)cell));
        HeatmapLegendText = $"{RangeTitle(_heatmapRange)} · {SelectedMonitor?.Label ?? "全部显示器"} · " +
                            $"点击 {clicks:N0} · 轨迹样本 {samples:N0} · " +
                            $"{(UseRelativeIntensity ? "当前视图强度" : "全局强度")} / {(UseLogScale ? "对数色阶" : "线性色阶")}";
    }

    [RelayCommand]
    private async Task ResetCoverageAsync()
    {
        var confirm = System.Windows.MessageBox.Show(
            "只重置累计覆盖挑战；点击散点和每日轨迹数据会保留。是否继续？",
            ProductInfo.Name, System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;
        await _flush.ClearOccupancyDataAsync().ConfigureAwait(true);
        CoverageText = "累计覆盖已重置。继续移动鼠标后会重新累计。";
    }

    [RelayCommand]
    private async Task ExportHeatmapAsync()
    {
        try
        {
            ExportStatus = "正在准备图片…";
            await _flush.FlushNowAsync().ConfigureAwait(true);
            var today = DateOnly.FromDateTime(DateTime.Now);
            var result = await _pointerQuery.GetAsync(_heatmapRange.GetStartDate(today), today).ConfigureAwait(true);
            if (result is null)
            {
                ExportStatus = "暂无可导出的热力图数据";
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出屏幕位置热力图",
                Filter = "PNG 图片 (*.png)|*.png",
                FileName = $"KeyPulse-屏幕热力图-{DateTime.Now:yyyyMMdd-HHmm}.png",
                AddExtension = true,
                DefaultExt = ".png"
            };
            if (dialog.ShowDialog() != true)
            {
                ExportStatus = string.Empty;
                return;
            }

            SaveCombinedHeatmap(result, dialog.FileName, RangeTitle(_heatmapRange));
            ExportStatus = $"已导出：{Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            ExportStatus = $"导出失败：{ex.Message}";
        }
    }

    private void SaveCombinedHeatmap(PointerHeatmapResult result, string path, string rangeTitle)
    {
        const int width = 1800;
        const int height = 920;
        const int margin = 72;
        const int gap = 34;
        var panelWidth = (width - (margin * 2) - (gap * 2)) / 3;
        var visual = new Media.DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Media.Brushes.White, null, new Rect(0, 0, width, height));
            DrawText(drawing, "KeyPulse 屏幕位置热力图", 40, 72, 52, Media.Brushes.Black, true);
            DrawText(drawing, $"范围：{rangeTitle}    导出时间：{DateTime.Now:yyyy-MM-dd HH:mm}", 22, 72, 112, Media.Brushes.DimGray);
            var monitorText = string.Join("；", result.Layout.Monitors.Select(m =>
                $"{(m.IsPrimary ? "主屏" : m.Id)} {m.Width}×{m.Height} @ ({m.Left},{m.Top})"));
            DrawText(drawing, $"显示器：{monitorText}", 20, 72, 148, Media.Brushes.DimGray);

            var modes = new[]
            {
                ("点击位置", PointerHeatmapMode.Clicks),
                ("鼠标轨迹", PointerHeatmapMode.Trajectory),
                (_heatmapRenderer.HasReadyScreenImage(result) ? "图片揭示（累计活动）" : "累计像素覆盖（全部时间）",
                    PointerHeatmapMode.ImageReveal)
            };
            for (var i = 0; i < modes.Length; i++)
            {
                var x = margin + i * (panelWidth + gap);
                DrawText(drawing, modes[i].Item1, 24, x, 210, Media.Brushes.Black, true);
                drawing.DrawRectangle(Media.Brushes.WhiteSmoke, new Media.Pen(Media.Brushes.LightGray, 2),
                    new Rect(x, 255, panelWidth, 460));
                var image = _heatmapRenderer.Render(result, modes[i].Item2,
                    SelectedMonitor?.Value,
                    modes[i].Item2 == PointerHeatmapMode.Clicks || modes[i].Item2 == PointerHeatmapMode.ImageReveal
                        ? SelectedButton?.Value : null,
                    UseRelativeIntensity, UseLogScale,
                    HeatmapPaletteService.Accent(_settings.HeatmapPalette));
                var availableWidth = panelWidth - 20.0;
                const double availableHeight = 420;
                var imageScale = Math.Min(availableWidth / image.PixelWidth, availableHeight / image.PixelHeight);
                var imageWidth = image.PixelWidth * imageScale;
                var imageHeight = image.PixelHeight * imageScale;
                drawing.DrawImage(image, new Rect(
                    x + 10 + (availableWidth - imageWidth) / 2,
                    275 + (availableHeight - imageHeight) / 2,
                    imageWidth,
                    imageHeight));
            }

            var percent = result.TotalPixels <= 0 ? 0 : result.VisitedPixels * 100.0 / result.TotalPixels;
            DrawText(drawing, $"累计覆盖率：{percent:0.00}%    未经过：{100 - percent:0.00}%", 24, 72, 770, Media.Brushes.Black, true);
            DrawText(drawing, "仅包含本机坐标统计；不包含屏幕截图、窗口名称或输入内容。", 20, 72, 820, Media.Brushes.DimGray);
        }

        // DrawingVisual coordinates are device-independent pixels. Rendering at 96 DPI keeps the
        // requested 1800 x 920 canvas intact instead of scaling and clipping its right/bottom edges.
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, Media.PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void DrawText(Media.DrawingContext drawing, string text, double size, double x, double y,
        Media.Brush brush, bool bold = false)
    {
        var formatted = new Media.FormattedText(text, CultureInfo.GetCultureInfo("zh-CN"), System.Windows.FlowDirection.LeftToRight,
            new Media.Typeface(new Media.FontFamily("Microsoft YaHei UI"), System.Windows.FontStyles.Normal,
                bold ? System.Windows.FontWeights.SemiBold : System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal),
            size, brush, 1.5);
        drawing.DrawText(formatted, new System.Windows.Point(x, y));
    }

    private static string RangeTitle(KeyboardRange range) => range switch
    {
        KeyboardRange.Today => "今天",
        KeyboardRange.Last7Days => "最近 7 天",
        KeyboardRange.Last30Days => "最近 30 天",
        KeyboardRange.All => "全部",
        _ => "最近 7 天"
    };

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


    private static Media.Color Lerp(Media.Color from, Media.Color to, double t) => Media.Color.FromRgb(
        (byte)(from.R + ((to.R - from.R) * t)),
        (byte)(from.G + ((to.G - from.G) * t)),
        (byte)(from.B + ((to.B - from.B) * t)));

    private static Media.SolidColorBrush NewBrush(Media.Color color)
    {
        var brush = new Media.SolidColorBrush(color); brush.Freeze(); return brush;
    }

}

public sealed record HeatmapFilterOption(string Label, string? Value);
