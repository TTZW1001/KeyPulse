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

    void Save();
}
