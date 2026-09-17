using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;

namespace KeyPulse.App.ViewModels;

public sealed partial class AppsViewModel : ObservableObject
{
    private readonly IAppQuery _query;
    private readonly IFlushService _flush;
    private readonly IExcludedAppList _exclusions;
    private readonly Dispatcher _dispatcher;
    private readonly object _gate = new();
    private bool _busy;
    private KeyboardRange _range = KeyboardRange.Last7Days;
    private DateTime? _lastSuccessfulUpdate;
    private bool _isActive;

    public AppsViewModel(IAppQuery query, IFlushService flush, IExcludedAppList exclusions)
    {
        _query = query;
        _flush = flush;
        _exclusions = exclusions;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _flush.Flushed += Refresh;
        _exclusions.Changed += Refresh;
        ExcludedSummary = FormatExcluded(_exclusions.Snapshot());
    }

    public ObservableCollection<AppRankItem> Rows { get; } = [];

    [ObservableProperty]
    private bool _hasRows;

    [ObservableProperty]
    private string _excludedSummary = string.Empty;

    [ObservableProperty]
    private string _dataStateText = "正在加载…";

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

    public void SetActive(bool active)
    {
        _isActive = active;
        if (active) Refresh();
    }

    public void Refresh()
    {
        if (_isActive) _ = RefreshAsync();
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
            var from = _range.GetStartDate(today);
            var ranking = await _query.GetRankingAsync(from, today).ConfigureAwait(false);
            var excluded = _exclusions.Snapshot();
            await _dispatcher.InvokeAsync(() =>
            {
                Apply(ranking, excluded);
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
            lock (_gate)
            {
                _busy = false;
            }
        }
    }

    [RelayCommand]
    private Task Retry() => RefreshAsync();

    [RelayCommand]
    private void Exclude(AppRankItem? item)
    {
        if (item is null || !item.CanExclude)
        {
            return;
        }

        _exclusions.Exclude(item.ProcessName);
    }

    private void SetRange(KeyboardRange range)
    {
        if (_range == range)
        {
            return;
        }

        _range = range;
        OnPropertyChanged(nameof(IsTodayRange));
        OnPropertyChanged(nameof(IsLast7DaysRange));
        OnPropertyChanged(nameof(IsLast30DaysRange));
        OnPropertyChanged(nameof(IsAllRange));
        Refresh();
    }

    private void Apply(IReadOnlyList<AppRankRow> ranking, IReadOnlyList<string> excluded)
    {
        var excludedSet = new HashSet<string>(excluded, StringComparer.OrdinalIgnoreCase);
        Rows.Clear();
        foreach (var row in ranking)
        {
            Rows.Add(AppRankItem.From(row, !excludedSet.Contains(row.ProcessName)));
        }

        HasRows = Rows.Count > 0;
        ExcludedSummary = FormatExcluded(excluded);
    }

    private static string FormatExcluded(IReadOnlyList<string> excluded)
    {
        if (excluded.Count == 0)
        {
            return "没有排除的应用。排除后不再记入本页，全局键鼠统计仍会累计。";
        }

        return "已排除 " + string.Join("、", excluded) + "。排除后不再记入本页，全局键鼠统计仍会累计。";
    }
}

public sealed class AppRankItem
{
    public required string ProcessName { get; init; }
    public required string DisplayLabel { get; init; }
    public required string ProcessHint { get; init; }
    public long KeyPressCount { get; init; }
    public long MouseClickCount { get; init; }
    public long WheelEventCount { get; init; }
    public double MouseDistancePixels { get; init; }
    public long ActiveSeconds { get; init; }
    public bool CanExclude { get; init; }
    public string ExcludeLabel => CanExclude ? "排除" : "已排除";
    public string KeysText { get; init; } = "0";
    public string ClicksText { get; init; } = "0";
    public string WheelsText { get; init; } = "0";
    public string DistanceText { get; init; } = "0";
    public string ActiveText { get; init; } = "—";

    public static AppRankItem From(AppRankRow row, bool canExclude)
    {
        var culture = CultureInfo.CurrentCulture;
        return new AppRankItem
        {
            ProcessName = row.ProcessName,
            DisplayLabel = row.DisplayLabel,
            ProcessHint = row.ProcessName,
            KeyPressCount = row.KeyPressCount,
            MouseClickCount = row.MouseClickCount,
            WheelEventCount = row.WheelEventCount,
            MouseDistancePixels = row.MouseDistancePixels,
            ActiveSeconds = row.ActiveSeconds,
            CanExclude = canExclude,
            KeysText = row.KeyPressCount.ToString("N0", culture),
            ClicksText = row.MouseClickCount.ToString("N0", culture),
            WheelsText = row.WheelEventCount.ToString("N0", culture),
            DistanceText = row.MouseDistancePixels.ToString("N0", culture),
            ActiveText = FormatActive(row.ActiveSeconds)
        };
    }

    internal static string FormatActive(long seconds)
    {
        if (seconds <= 0)
        {
            return "—";
        }

        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours >= 1)
        {
            return ((int)span.TotalHours).ToString(CultureInfo.InvariantCulture) + "h " +
                   span.Minutes.ToString(CultureInfo.InvariantCulture) + "m";
        }

        if (span.TotalMinutes >= 1)
        {
            return ((int)span.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m";
        }

        return seconds.ToString(CultureInfo.InvariantCulture) + "s";
    }
}
