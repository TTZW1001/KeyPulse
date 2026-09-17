using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyPulse.App.Services;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Infrastructure.Persistence;
using Media = System.Windows.Media;

namespace KeyPulse.App.ViewModels;

public sealed partial class KeyboardViewModel : ObservableObject
{
    private const double Unit = 32;
    private const double Gap = 3;
    private static readonly Media.Color Accent = Media.Color.FromRgb(0x4E, 0x6E, 0x9E);
    private static readonly Media.Color LightUnused = Media.Color.FromRgb(0xE6, 0xE6, 0xE2);
    private static readonly Media.Color DarkUnused = Media.Color.FromRgb(0x2C, 0x2C, 0x2C);
    private static readonly Media.SolidColorBrush LightLabel = NewBrush(Media.Color.FromRgb(0x20, 0x20, 0x20));
    private static readonly Media.SolidColorBrush DarkLabel = NewBrush(Media.Color.FromRgb(0xF2, 0xF2, 0xF2));

    private readonly IKeyboardQuery _query;
    private readonly IFlushService _flush;
    private readonly ThemeService _theme;
    private readonly IUserSettings _settings;
    private readonly Dispatcher _dispatcher;
    private readonly object _gate = new();
    private bool _busy;
    private KeyboardRange _range = KeyboardRange.Last7Days;
    private DateTime? _lastSuccessfulUpdate;
    private bool _isActive;

    public KeyboardViewModel(
        IKeyboardQuery query,
        IFlushService flush,
        ThemeService theme,
        IUserSettings settings)
    {
        _query = query;
        _flush = flush;
        _theme = theme;
        _settings = settings;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _flush.Flushed += OnFlushed;
        _theme.Changed += OnThemeChanged;

        KeyboardLayoutOptions = KeyboardLayoutDefinition.Presets
            .Select(layout => new KeyboardLayoutOption(layout.Kind, layout.DisplayName))
            .ToList();
        _selectedLayout = KeyboardLayoutOptions.First(option => option.Kind == settings.KeyboardLayout);
        BuildLayout(_selectedLayout.Kind);
        ApplyZeroFills();
    }

    public ObservableCollection<HeatmapKeyItem> Keys { get; } = [];

    public IReadOnlyList<KeyboardLayoutOption> KeyboardLayoutOptions { get; }

    public KeyboardLayoutOption SelectedLayout
    {
        get => _selectedLayout;
        set
        {
            if (value is null || Equals(_selectedLayout, value)) return;
            _selectedLayout = value;
            OnPropertyChanged();
            _settings.KeyboardLayout = value.Kind;
            _settings.KeyboardLayoutExplicitlyChosen = true;
            _settings.Save();
            ShowFullSizeSuggestion = false;
            BuildLayout(value.Kind);
            Refresh();
        }
    }

    private KeyboardLayoutOption _selectedLayout;

    [ObservableProperty]
    private double _canvasWidth;

    [ObservableProperty]
    private double _canvasHeight;

    [ObservableProperty]
    private bool _showFullSizeSuggestion;

    private IReadOnlySet<string> _mappedKeyCodes = new HashSet<string>();

    public ObservableCollection<OtherKeyRow> ShortcutRows { get; } = [];

    private void BuildLayout(KeyboardLayoutKind kind)
    {
        var layout = KeyboardLayoutDefinition.Get(kind);
        Keys.Clear();
        double maxRight = 0;
        double maxBottom = 0;
        foreach (var def in layout.Keys)
        {
            var left = def.X * (Unit + Gap);
            var top = def.Y * (Unit + Gap);
            var width = def.Width * Unit + Math.Max(0, def.Width - 1) * Gap;
            var height = def.Height * Unit + Math.Max(0, def.Height - 1) * Gap;
            Keys.Add(new HeatmapKeyItem(def.KeyCode, def.Label, left, top, width, height));
            maxRight = Math.Max(maxRight, left + width);
            maxBottom = Math.Max(maxBottom, top + height);
        }

        CanvasWidth = maxRight + 1;
        CanvasHeight = maxBottom + 1;
        _mappedKeyCodes = layout.Keys.Select(key => key.KeyCode).ToHashSet(StringComparer.Ordinal);
    }

    public ObservableCollection<OtherKeyRow> OtherKeys { get; } = [];

    public ObservableCollection<OtherKeyRow> TopKeys { get; } = [];

    [ObservableProperty]
    private IReadOnlyList<Media.Brush> _legendFills = [];

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
            var keysTask = _query.GetKeyCountsAsync(from, today);
            var shortcutsTask = _query.GetShortcutCountsAsync(from, today);
            await Task.WhenAll(keysTask, shortcutsTask).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() =>
            {
                ApplyCounts(keysTask.Result, shortcutsTask.Result);
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

    private void ApplyCounts(
        IReadOnlyDictionary<string, long> counts,
        IReadOnlyDictionary<string, long>? shortcuts = null)
    {
        var max = 0L;
        foreach (var key in Keys)
        {
            counts.TryGetValue(key.KeyCode, out var count);
            max = Math.Max(max, count);
        }

        var culture = CultureInfo.CurrentCulture;
        var unused = _theme.IsDarkEffective ? DarkUnused : LightUnused;
        var dark = _theme.IsDarkEffective;
        foreach (var key in Keys)
        {
            counts.TryGetValue(key.KeyCode, out var count);
            var t = HeatmapScale.Normalize(count, max);
            key.Count = count;
            key.HoverText = key.KeyCode + " · " + count.ToString("N0", culture) + " 次";
            key.Fill = NewBrush(Lerp(unused, Accent, t));
            key.LabelBrush = dark || t >= 0.45 ? DarkLabel : LightLabel;
        }

        var legend = new Media.Brush[5];
        for (var i = 0; i < legend.Length; i++)
        {
            var t = i / (double)(legend.Length - 1);
            legend[i] = NewBrush(Lerp(unused, Accent, t));
        }

        LegendFills = legend;

        OtherKeys.Clear();
        foreach (var pair in counts
                     .Where(p => p.Value > 0 && !_mappedKeyCodes.Contains(p.Key))
                     .OrderByDescending(p => p.Value)
                     .ThenBy(p => p.Key, StringComparer.Ordinal)
                     .Take(12))
        {
            OtherKeys.Add(new OtherKeyRow(FriendlyKeyName(pair.Key), pair.Value.ToString("N0", culture)));
        }

        TopKeys.Clear();
        foreach (var key in Keys
                     .Where(k => k.Count > 0)
                     .GroupBy(k => k.KeyCode)
                     .Select(g => g.First())
                     .OrderByDescending(k => k.Count)
                     .ThenBy(k => k.KeyCode, StringComparer.Ordinal)
                     .Take(5))
        {
            TopKeys.Add(new OtherKeyRow(key.Label, key.Count.ToString("N0", culture)));
        }

        ShortcutRows.Clear();
        if (shortcuts is not null)
        {
            foreach (var pair in shortcuts.OrderByDescending(pair => pair.Value)
                         .ThenBy(pair => pair.Key, StringComparer.Ordinal).Take(10))
            {
                ShortcutRows.Add(new OtherKeyRow(pair.Key, pair.Value.ToString("N0", culture)));
            }
        }

        ShowFullSizeSuggestion = !_settings.KeyboardLayoutExplicitlyChosen &&
                                 _selectedLayout.Kind != KeyboardLayoutKind.FullSize &&
                                 counts.Any(pair => pair.Value > 0 && pair.Key.StartsWith("NumPad", StringComparison.Ordinal));
    }

    private void ApplyZeroFills()
    {
        ApplyCounts(new Dictionary<string, long>(StringComparer.Ordinal));
    }

    [RelayCommand]
    private void UseSuggestedFullSize()
    {
        SelectedLayout = KeyboardLayoutOptions.First(option => option.Kind == KeyboardLayoutKind.FullSize);
    }

    private void OnFlushed() => Refresh();

    private void OnThemeChanged() => Refresh();

    private static Media.Color Lerp(Media.Color from, Media.Color to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Media.Color.FromRgb(
            (byte)(from.R + ((to.R - from.R) * t)),
            (byte)(from.G + ((to.G - from.G) * t)),
            (byte)(from.B + ((to.B - from.B) * t)));
    }

    private static Media.SolidColorBrush NewBrush(Media.Color color)
    {
        var brush = new Media.SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static string FriendlyKeyName(string keyCode) =>
        keyCode.StartsWith("VK_", StringComparison.Ordinal)
            ? $"未识别功能键（{keyCode}）"
            : keyCode;
}

public sealed record OtherKeyRow(string Name, string CountText);

public sealed record KeyboardLayoutOption(KeyboardLayoutKind Kind, string DisplayName)
{
    public override string ToString() => DisplayName;
}
