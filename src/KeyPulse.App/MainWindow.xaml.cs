using System.Windows;
using System.Windows.Threading;

namespace KeyPulse.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly DispatcherTimer _timer;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => _viewModel.Refresh();
        Loaded += (_, _) => _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }
}
