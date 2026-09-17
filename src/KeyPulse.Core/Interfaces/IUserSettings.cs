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

    void Save();
}
