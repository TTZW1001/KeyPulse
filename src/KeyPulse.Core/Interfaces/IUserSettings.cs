namespace KeyPulse.Core.Interfaces;

public interface IUserSettings
{
    bool HideToTrayHintDismissed { get; set; }

    ThemeMode Theme { get; set; }

    void Save();
}
