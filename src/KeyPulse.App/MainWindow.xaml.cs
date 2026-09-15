using System.Windows;
using System.Windows.Threading;
using KeyPulse.App.Services;

namespace KeyPulse.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IApplicationLifecycle _lifecycle;
    private readonly DispatcherTimer _timer;

    public MainWindow(MainWindowViewModel viewModel, IApplicationLifecycle lifecycle)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _lifecycle = lifecycle;
        DataContext = viewModel;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => _viewModel.Refresh();
        Loaded += (_, _) => _timer.Start();
        Closed += (_, _) => _timer.Stop();
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_lifecycle.IsExiting)
        {
            return;
        }

        e.Cancel = true;
        _lifecycle.HideToTray();
    }
}
