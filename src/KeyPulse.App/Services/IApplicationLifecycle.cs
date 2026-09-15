namespace KeyPulse.App.Services;

public interface IApplicationLifecycle
{
    bool IsExiting { get; }

    void HideToTray(bool promptIfFirst = true);

    void ShowMainWindow();

    void RequestExit();
}
