namespace KeyPulse.Core.Interfaces;

public interface IUserSettings
{
    bool HideToTrayHintDismissed { get; set; }

    void Save();
}
