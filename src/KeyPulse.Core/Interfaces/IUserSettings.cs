namespace KeyPulse.Core.Interfaces;

public interface IUserSettings
{
    bool HideToTrayHintDismissed { get; set; }

    ThemeMode Theme { get; set; }

    KeyboardLayoutKind KeyboardLayout { get; set; }

    bool KeyboardLayoutExplicitlyChosen { get; set; }

    bool ShortcutStatsEnabled { get; set; }

    bool ScreenPositionStatsEnabled { get; set; }

    int PositionRetentionDays { get => 0; set { } }

    bool ShowInsights { get => true; set { } }

    KeyboardRange KeyboardRange { get => KeyboardRange.Last7Days; set { } }

    KeyboardRange MouseRange { get => KeyboardRange.Last7Days; set { } }

    KeyboardRange MouseHeatmapRange { get => KeyboardRange.Last7Days; set { } }

    KeyboardRange AppsRange { get => KeyboardRange.Last7Days; set { } }

    TrendRangeKind TrendRange { get => TrendRangeKind.Last7Days; set { } }

    DateOnly? TrendCustomFromDate { get => null; set { } }

    DateOnly? TrendCustomToDate { get => null; set { } }

    DashboardTrendMetric DashboardTrendMetric { get => DashboardTrendMetric.Keys; set { } }

    int AfkThresholdMinutes { get => 5; set { } }

    HourlyDistributionMode HourlyDistributionMode { get => HourlyDistributionMode.DailyAverage; set { } }

    HourlyMetric HourlyMetric { get => HourlyMetric.InputActivity; set { } }

    bool AutoBackupEnabled { get => false; set { } }

    BackupFrequency AutoBackupFrequency { get => BackupFrequency.Weekly; set { } }

    string? AutoBackupDirectory { get => null; set { } }

    int AutoBackupRetentionCount { get => 5; set { } }

    DateTimeOffset? LastAutoBackupAt { get => null; set { } }

    HeatmapPalette HeatmapPalette { get => HeatmapPalette.Ocean; set { } }

    string? KeyboardSkinPath { get => null; set { } }

    string? ScreenSkinPath { get => null; set { } }

    void Save();
}
