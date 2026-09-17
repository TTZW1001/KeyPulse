using System.Text.Json;
using System.Text.Json.Serialization;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;

namespace KeyPulse.Infrastructure.System;

public sealed class JsonUserSettings : IUserSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly SettingsFile _file;

    public JsonUserSettings(IAppPaths paths)
    {
        _path = paths.SettingsPath;
        _file = Load(_path, File.Exists(_path));
    }

    public bool HideToTrayHintDismissed
    {
        get => _file.HideToTrayHintDismissed;
        set => _file.HideToTrayHintDismissed = value;
    }

    public ThemeMode Theme
    {
        get => _file.Theme;
        set => _file.Theme = value;
    }

    public KeyboardLayoutKind KeyboardLayout
    {
        get => _file.KeyboardLayout;
        set => _file.KeyboardLayout = value;
    }

    public bool KeyboardLayoutExplicitlyChosen
    {
        get => _file.KeyboardLayoutExplicitlyChosen;
        set => _file.KeyboardLayoutExplicitlyChosen = value;
    }

    public bool ShortcutStatsEnabled
    {
        get => _file.ShortcutStatsEnabled;
        set => _file.ShortcutStatsEnabled = value;
    }

    public bool ScreenPositionStatsEnabled
    {
        get => _file.ScreenPositionStatsEnabled;
        set => _file.ScreenPositionStatsEnabled = value;
    }

    public int PositionRetentionDays
    {
        get => _file.PositionRetentionDays ?? 0;
        set => _file.PositionRetentionDays = value;
    }

    public bool ShowInsights
    {
        get => _file.ShowInsights;
        set => _file.ShowInsights = value;
    }

    public KeyboardRange KeyboardRange
    {
        get => ParseEnum(_file.KeyboardRange, KeyPulse.Core.KeyboardRange.Last7Days);
        set => _file.KeyboardRange = value.ToString();
    }

    public KeyboardRange MouseRange
    {
        get => ParseEnum(_file.MouseRange, KeyPulse.Core.KeyboardRange.Last7Days);
        set => _file.MouseRange = value.ToString();
    }

    public KeyboardRange MouseHeatmapRange
    {
        get => ParseEnum(_file.MouseHeatmapRange, KeyPulse.Core.KeyboardRange.Last7Days);
        set => _file.MouseHeatmapRange = value.ToString();
    }

    public KeyboardRange AppsRange
    {
        get => ParseEnum(_file.AppsRange, KeyPulse.Core.KeyboardRange.Last7Days);
        set => _file.AppsRange = value.ToString();
    }

    public TrendRangeKind TrendRange
    {
        get => ParseEnum(_file.TrendRange, TrendRangeKind.Last7Days);
        set => _file.TrendRange = value.ToString();
    }

    public DateOnly? TrendCustomFromDate
    {
        get => ParseDate(_file.TrendCustomFromDate);
        set => _file.TrendCustomFromDate = FormatDate(value);
    }

    public DateOnly? TrendCustomToDate
    {
        get => ParseDate(_file.TrendCustomToDate);
        set => _file.TrendCustomToDate = FormatDate(value);
    }

    public DashboardTrendMetric DashboardTrendMetric
    {
        get => ParseEnum(_file.DashboardTrendMetric, KeyPulse.Core.DashboardTrendMetric.Keys);
        set => _file.DashboardTrendMetric = value.ToString();
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(_file, JsonOptions));
    }

    private static SettingsFile Load(string path, bool existingInstallation)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<SettingsFile>(json, JsonOptions) ?? new SettingsFile();
            }
        }
        catch
        {
            // fall back to defaults
        }

        return new SettingsFile { PositionRetentionDays = existingInstallation ? 0 : 90 };
    }

    private static T ParseEnum<T>(string? value, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : fallback;

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", global::System.Globalization.CultureInfo.InvariantCulture,
            global::System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private static string? FormatDate(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", global::System.Globalization.CultureInfo.InvariantCulture);

    private sealed class SettingsFile
    {
        public bool HideToTrayHintDismissed { get; set; }

        public ThemeMode Theme { get; set; } = ThemeMode.System;

        public KeyboardLayoutKind KeyboardLayout { get; set; } = KeyboardLayoutKind.CompactLaptop;

        public bool KeyboardLayoutExplicitlyChosen { get; set; }

        public bool ShortcutStatsEnabled { get; set; } = true;

        public bool ScreenPositionStatsEnabled { get; set; }

        public int? PositionRetentionDays { get; set; }

        public bool ShowInsights { get; set; } = true;

        public string? KeyboardRange { get; set; }

        public string? MouseRange { get; set; }

        public string? MouseHeatmapRange { get; set; }

        public string? AppsRange { get; set; }

        public string? TrendRange { get; set; }

        public string? TrendCustomFromDate { get; set; }

        public string? TrendCustomToDate { get; set; }

        public string? DashboardTrendMetric { get; set; }
    }
}
