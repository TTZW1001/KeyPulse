using System.IO;
using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Spike.Tray;

public partial class MainWindow : Window
{
    private static readonly string StatusDirectory =
        Path.Combine(Path.GetTempPath(), "KeyPulseSpike");

    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Drawing.Icon _icon;
    private readonly EventWaitHandle _exitEvent;
    private readonly CancellationTokenSource _cts = new();
    private bool _reallyExiting;

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(StatusDirectory);

        _icon = LoadTrayIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Visible = true,
            Text = "KeyPulse Spike Tray",
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();

        _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\KeyPulseSpike.Tray.Exit");
        _ = Task.Run(WaitForExitSignalAsync);

        Closing += OnClosing;
        Closed += (_, _) => Cleanup();

        WriteStatus("running");
        File.WriteAllText(
            Path.Combine(StatusDirectory, "tray-ready.txt"),
            Environment.ProcessId.ToString());
        StatusText.Text = "Tray icon created. Process Id: " + Environment.ProcessId;
    }

    private async Task WaitForExitSignalAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                if (_exitEvent.WaitOne(TimeSpan.FromMilliseconds(400)))
                {
                    await Dispatcher.InvokeAsync(ExitApp);
                    return;
                }

                var signalPath = Path.Combine(StatusDirectory, "tray-exit.signal");
                if (File.Exists(signalPath))
                {
                    File.Delete(signalPath);
                    await Dispatcher.InvokeAsync(ExitApp);
                    return;
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // shutting down
        }
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        return menu;
    }

    private void HideButton_OnClick(object sender, RoutedEventArgs e) => HideToTray();

    private void ExitButton_OnClick(object sender, RoutedEventArgs e) => ExitApp();

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_reallyExiting)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        WriteStatus("hidden");
        StatusText.Text = "Hidden to tray. Process still running.";
    }

    private void ShowWindow()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
        WriteStatus("visible");
        StatusText.Text = "Window restored from tray.";
    }

    private void ExitApp()
    {
        if (_reallyExiting)
        {
            return;
        }

        _reallyExiting = true;
        WriteStatus("exiting");
        Cleanup();
        System.Windows.Application.Current.Shutdown();
    }

    private bool _cleaned;

    private void Cleanup()
    {
        if (_cleaned)
        {
            return;
        }

        _cleaned = true;
        _cts.Cancel();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
        _exitEvent.Dispose();
        _cts.Dispose();
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "tray-icon.png");
        using var source = new Drawing.Bitmap(path);
        using var sized = new Drawing.Bitmap(source, new Drawing.Size(32, 32));
        return Drawing.Icon.FromHandle(sized.GetHicon());
    }

    private static void WriteStatus(string state)
    {
        File.WriteAllText(
            Path.Combine(StatusDirectory, "tray-status.txt"),
            $"ts={DateTimeOffset.Now:o}{Environment.NewLine}state={state}{Environment.NewLine}pid={Environment.ProcessId}{Environment.NewLine}");
    }
}
