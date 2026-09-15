using System.Windows;
using KeyPulse.Core.Interfaces;

namespace KeyPulse.App.Services;

public sealed class ApplicationLifecycleService : IApplicationLifecycle, IDisposable
{
    public const string ExitEventName = @"Local\KeyPulse.RequestExit";

    private readonly IUserSettings _settings;
    private readonly CancellationTokenSource _cts = new();
    private readonly EventWaitHandle _exitEvent;
    private readonly Task _exitListener;
    private MainWindow? _window;

    public ApplicationLifecycleService(IUserSettings settings)
    {
        _settings = settings;
        _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
        _exitListener = Task.Run(WaitForExitSignalAsync);
    }

    public bool IsExiting { get; private set; }

    public void Attach(MainWindow window)
    {
        _window = window;
    }

    public void HideToTray(bool promptIfFirst = true)
    {
        if (_window is null || IsExiting)
        {
            return;
        }

        if (promptIfFirst && !_settings.HideToTrayHintDismissed && _window.IsVisible)
        {
            var result = System.Windows.MessageBox.Show(
                _window,
                "KeyPulse 将继续在后台统计，可从系统托盘重新打开。\n\n是否不再提示？",
                "KeyPulse",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
            {
                _settings.HideToTrayHintDismissed = true;
                _settings.Save();
            }
        }

        _window.Hide();
        _window.ShowInTaskbar = false;
    }

    public void ShowMainWindow()
    {
        if (_window is null || IsExiting)
        {
            return;
        }

        _window.Show();
        _window.ShowInTaskbar = true;
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void RequestExit()
    {
        if (IsExiting)
        {
            return;
        }

        IsExiting = true;
        if (_window is not null)
        {
            _window.Close();
        }

        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _exitEvent.Set();
        _exitListener.Wait(TimeSpan.FromSeconds(1));
        _exitEvent.Dispose();
        _cts.Dispose();
    }

    private void WaitForExitSignalAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                if (_exitEvent.WaitOne(TimeSpan.FromMilliseconds(400)))
                {
                    var app = System.Windows.Application.Current;
                    app?.Dispatcher.Invoke(RequestExit);
                    return;
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // shutting down
        }
    }
}
