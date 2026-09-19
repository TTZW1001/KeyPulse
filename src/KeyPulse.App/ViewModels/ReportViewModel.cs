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
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace KeyPulse.App.ViewModels;

public sealed partial class ReportViewModel : ObservableObject
{
    private readonly IReportQuery _query;
    private readonly IFlushService _flush;
    private readonly ReportExportService _export;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private bool _isActive;
    private bool _busy;
    private ActivityReport? _report;

    public ReportViewModel(IReportQuery query, IFlushService flush, ReportExportService export)
    {
        _query = query;
        _flush = flush;
        _export = export;
        PeriodOptions =
        [
            new(ReportPeriodKind.CurrentWeek, "本周"),
            new(ReportPeriodKind.CurrentMonth, "本月"),
            new(ReportPeriodKind.Last12Months, "最近 12 个月")
        ];
        _selectedPeriod = PeriodOptions[0];
    }

    public IReadOnlyList<ReportPeriodOption> PeriodOptions { get; }

    [ObservableProperty] private ReportPeriodOption _selectedPeriod;
    [ObservableProperty] private string _rangeText = "—";
    [ObservableProperty] private string _effectiveTimeText = "0 分钟";
    [ObservableProperty] private string _sessionText = "0 次";
    [ObservableProperty] private string _keyText = "0";
    [ObservableProperty] private string _clickText = "0";
    [ObservableProperty] private string _comparisonText = "暂无可比基线";
    [ObservableProperty] private string _topKeyText = "暂无";
    [ObservableProperty] private string _topAppText = "暂无";
    [ObservableProperty] private string _dataStateText = "正在加载…";
    [ObservableProperty] private ISeries[] _activitySeries = [];
    [ObservableProperty] private Axis[] _xAxes = [];
    [ObservableProperty] private Axis[] _yAxes = [];

    partial void OnSelectedPeriodChanged(ReportPeriodOption value) => Refresh();

    public void SetActive(bool active)
    {
        _isActive = active;
        if (active) Refresh();
    }

    public void Refresh()
    {
        if (_isActive) _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            await _flush.FlushNowAsync().ConfigureAwait(false);
            var report = await _query.GetAsync(SelectedPeriod.Kind, DateOnly.FromDateTime(DateTime.Now)).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() => Apply(report));
        }
        catch (Exception ex)
        {
            await _dispatcher.InvokeAsync(() => DataStateText = "报告生成失败：" + ex.Message);
        }
        finally { _busy = false; }
    }

    [RelayCommand]
    private async Task ExportHtmlAsync()
    {
        if (_report is null) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出离线 HTML 报告", Filter = "HTML 报告|*.html", AddExtension = true,
            FileName = $"KeyPulse-report-{_report.From:yyyyMMdd}-{_report.To:yyyyMMdd}.html"
        };
        if (dialog.ShowDialog() != true) return;
        await _export.ExportHtmlAsync(_report, dialog.FileName);
        DataStateText = "HTML 报告已导出：" + dialog.FileName;
    }

    [RelayCommand]
    private void ExportPng()
    {
        if (_report is null) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出 PNG 报告", Filter = "PNG 图片|*.png", AddExtension = true,
            FileName = $"KeyPulse-report-{_report.From:yyyyMMdd}-{_report.To:yyyyMMdd}.png"
        };
        if (dialog.ShowDialog() != true) return;
        _export.ExportPng(_report, dialog.FileName);
        DataStateText = "PNG 报告已导出：" + dialog.FileName;
    }

    private void Apply(ActivityReport report)
    {
        _report = report;
        RangeText = $"{report.From:yyyy-MM-dd} ～ {report.To:yyyy-MM-dd}";
        EffectiveTimeText = Duration(report.Current.EffectiveActiveSeconds);
        SessionText = report.Current.SessionCount.ToString("N0", CultureInfo.CurrentCulture) + " 次";
        KeyText = report.Current.KeyPressCount.ToString("N0", CultureInfo.CurrentCulture);
        ClickText = report.Current.MouseClickCount.ToString("N0", CultureInfo.CurrentCulture);
        ComparisonText = Compare(report.Current.ActivityCount, report.Comparison.ActivityCount) +
                         $" · 对比 {report.ComparisonFrom:M/d}–{report.ComparisonTo:M/d}";
        TopKeyText = report.TopKey is null ? "暂无" : $"{report.TopKey} · {report.TopKeyCount:N0} 次";
        TopAppText = report.TopApp is null ? "暂无" : $"{report.TopApp} · {Duration(report.TopAppSeconds)}";
        DataStateText = report.HasEffectiveTime ? "报告已更新" : "有效时长从 1.3 起统计，旧日期不回填";
        var labels = report.Series.Select(item => item.Label).ToArray();
        ActivitySeries =
        [
            new ColumnSeries<long>
            {
                Name = "活动次数", Values = report.Series.Select(item => item.ActivityCount).ToArray(),
                Fill = new SolidColorPaint(new SKColor(0x4E, 0x6E, 0x9E)), Stroke = null, MaxBarWidth = 28
            }
        ];
        XAxes = [new Axis { Labels = labels, TextSize = 11, MinStep = report.Series.Count > 14 ? 2 : 1 }];
        YAxes = [new Axis { MinLimit = 0, MinStep = 1, Labeler = value => value.ToString("N0", CultureInfo.CurrentCulture) }];
    }

    private static string Duration(long seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours} 小时 {span.Minutes} 分钟" : $"{span.Minutes} 分钟";
    }

    private static string Compare(long current, long previous) => previous <= 0
        ? "暂无可比基线"
        : $"活动量较上个同期 {(current / (double)previous - 1) * 100:+0;-0;0}%";
}

public sealed record ReportPeriodOption(ReportPeriodKind Kind, string Title);
