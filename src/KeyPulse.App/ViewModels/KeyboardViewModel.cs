using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private readonly Dispatcher _dispatcher;
    private readonly object _gate = new();
    private bool _busy;
    private KeyboardRange _range = KeyboardRange.Last7Days;

    public KeyboardViewModel(IKeyboardQuery query, IFlushService flush, ThemeService theme)
    {
        _query = query;
        _flush = flush;
        _theme = theme;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _flush.Flushed += OnFlushed;
        _theme.Changed += OnThemeChanged;

        var keys = new List<HeatmapKeyItem>(KeyboardLayoutDefinition.Keys.Count);
        double maxRight = 0;
        double maxBottom = 0;
        foreach (var def in KeyboardLayoutDefinition.Keys)
        {
            var left = def.X * (Unit + Gap);
            var top = def.Y * (Unit + Gap);
            var width = def.Width * Unit + Math.Max(0, def.Width - 1) * Gap;
            var height = def.Height * Unit + Math.Max(0, def.Height - 1) * Gap;
            keys.Add(new HeatmapKeyItem(def.KeyCode, def.Label, left, top, width, height));
            maxRight = Math.Max(maxRight, left + width);
            maxBottom = Math.Max(maxBottom, top + height);
        }

        Keys = keys;
        CanvasWidth = maxRight + 1;
        CanvasHeight = maxBottom + 1;
        ApplyZeroFills();
    }

    public IReadOnlyList<HeatmapKeyItem> Keys { get; }

    public double CanvasWidth { get; }

    public double CanvasHeight { get; }

    public ObservableCollection<OtherKeyRow> OtherKeys { get; } = [];

    public ObservableCollection<OtherKeyRow> TopKeys { get; } = [];

    [ObservableProperty]
    private IReadOnlyList<Media.Brush> _legendFills = [];

    public bool HasOtherKeys => OtherKeys.Count > 0;

    public bool HasTopKeys => TopKeys.Count > 0;

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
            var counts = await _query.GetKeyCountsAsync(from, today).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() => ApplyCounts(counts));
        }
        catch
        {
            // keep last painted keys
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
        OnPropertyChanged(nameof(IsTodayRange));
        OnPropertyChanged(nameof(IsLast7DaysRange));
        Refresh();
    }

    private void ApplyCounts(IReadOnlyDictionary<string, long> counts)
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
            key.LabelBrush = t >= 0.45 || (dark && t >= 0.28) ? DarkLabel : LightLabel;
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
                     .Where(p => p.Value > 0 && !KeyboardLayoutDefinition.MappedKeyCodes.Contains(p.Key))
                     .OrderByDescending(p => p.Value)
                     .ThenBy(p => p.Key, StringComparer.Ordinal)
                     .Take(12))
        {
            OtherKeys.Add(new OtherKeyRow(pair.Key, pair.Value.ToString("N0", culture)));
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

        OnPropertyChanged(nameof(HasOtherKeys));
        OnPropertyChanged(nameof(HasTopKeys));
    }

    private void ApplyZeroFills()
    {
        ApplyCounts(new Dictionary<string, long>(StringComparer.Ordinal));
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
}

public sealed record OtherKeyRow(string Name, string CountText);
