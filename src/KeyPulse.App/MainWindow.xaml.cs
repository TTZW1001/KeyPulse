using System.Windows;
using System.Windows.Threading;
using KeyPulse.App.Services;
using KeyPulse.Core;

namespace KeyPulse.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IApplicationLifecycle _lifecycle;
    private readonly ThemeService _theme;
    private readonly DispatcherTimer _timer;

    public MainWindow(
        MainWindowViewModel viewModel,
        IApplicationLifecycle lifecycle,
        ThemeService theme)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _lifecycle = lifecycle;
        _theme = theme;
        DataContext = viewModel;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => _viewModel.Refresh();
        Loaded += OnLoaded;
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                _timer.Start();
                _viewModel.SetWindowVisible(true);
            }
            else
            {
                _timer.Stop();
                _viewModel.SetWindowVisible(false);
            }
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            _theme.Changed -= OnThemeChanged;
        };
        SourceInitialized += (_, _) => _theme.ApplyCaption(this);
        Closing += OnClosing;
    }

    public void Navigate(AppPage page) => _viewModel.Navigate(page);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _timer.Start();
        _viewModel.SetWindowVisible(true);
        _theme.Changed += OnThemeChanged;
        _theme.ApplyCaption(this);
    }

    private void OnThemeChanged()
    {
        Dispatcher.Invoke(() => _theme.ApplyCaption(this));
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
