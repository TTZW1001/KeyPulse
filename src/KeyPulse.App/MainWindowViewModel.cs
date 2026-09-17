using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyPulse.App.ViewModels;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;

namespace KeyPulse.App;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IStatisticsAggregator _aggregator;
    private readonly IInputCapture _capture;
    private readonly IFlushService _flush;
    private readonly DashboardViewModel _dashboard;
    private readonly KeyboardViewModel _keyboard;
    private readonly MouseViewModel _mouse;
    private readonly TrendsViewModel _trends;
    private readonly AppsViewModel _apps;
    private readonly SettingsViewModel _settings;
    private readonly object _dashboardPage;
    private readonly object _keyboardPage;
    private readonly object _mousePage;
    private readonly object _trendsPage;
    private readonly object _appsPage;
    private readonly object _settingsPage;
    private bool _windowVisible;

    public MainWindowViewModel(
        IStatisticsAggregator aggregator,
        IInputCapture capture,
        IFlushService flush,
        DashboardViewModel dashboard,
        KeyboardViewModel keyboard,
        MouseViewModel mouse,
        TrendsViewModel trends,
        AppsViewModel apps,
        SettingsViewModel settings)
    {
        _aggregator = aggregator;
        _capture = capture;
        _flush = flush;
        _dashboard = dashboard;
        _keyboard = keyboard;
        _mouse = mouse;
        _trends = trends;
        _apps = apps;
        _settings = settings;
        _dashboardPage = dashboard;
        _keyboardPage = keyboard;
        _mousePage = mouse;
        _trendsPage = trends;
        _appsPage = apps;
        _settingsPage = settings;
        _dashboard.TrendDetailRequested += date =>
        {
            _trends.ShowDate(date);
            Navigate(AppPage.Trends);
        };

        NavigationItems = NavigationCatalog.Items;
        SelectedItem = NavigationItems[0];
        PageTitle = SelectedItem.Title;
        CurrentPage = _dashboardPage;
        Refresh();
    }

    public IReadOnlyList<NavigationItem> NavigationItems { get; }

    [ObservableProperty]
    private NavigationItem _selectedItem = NavigationCatalog.Items[0];

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _pageTitle = NavigationCatalog.Items[0].Title;

    [ObservableProperty]
    private string _statusText = "正在统计";

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _showPauseBanner;

    public void Navigate(AppPage page)
    {
        var item = NavigationItems.First(i => i.Page == page);
        SelectedItem = item;
    }

    public void SetWindowVisible(bool visible)
    {
        _windowVisible = visible;
        UpdatePageActivation();
    }

    public void Refresh()
    {
        if (SelectedItem.Page == AppPage.Dashboard)
        {
            _dashboard.Refresh();
        }
        else if (SelectedItem.Page == AppPage.Keyboard)
        {
            _keyboard.Refresh();
        }

        if (SelectedItem.Page == AppPage.Mouse)
        {
            _mouse.Refresh();
        }

        if (SelectedItem.Page == AppPage.Apps)
        {
            _apps.Refresh();
        }

        var state = _aggregator.State;
        var captureDown = _capture.Error is not null ||
                          (!_capture.IsListening && state == TrackingState.Error);
        if (captureDown)
        {
            state = TrackingState.Error;
        }

        IsPaused = state == TrackingState.Paused;
        var writeError = _flush.HasWriteError;
        ShowPauseBanner = state is TrackingState.Paused or TrackingState.Error || writeError;
        StatusText = state switch
        {
            TrackingState.Error => StatusCopy.CaptureError,
            TrackingState.Paused => StatusCopy.Paused,
            _ when writeError => StatusCopy.WriteError,
            _ => StatusCopy.Running
        };
    }

    [RelayCommand]
    private async Task Resume()
    {
        if (!_capture.IsListening || _capture.Error is not null)
        {
            await _capture.StartAsync().ConfigureAwait(true);
            if (_capture.IsListening && _capture.Error is null)
            {
                _aggregator.SetState(TrackingState.Running);
            }
            else
            {
                _aggregator.SetState(TrackingState.Error);
            }
        }
        else if (_aggregator.State is TrackingState.Paused or TrackingState.Error)
        {
            _aggregator.SetState(TrackingState.Running);
        }

        if (_flush.HasWriteError)
        {
            await _flush.FlushNowAsync().ConfigureAwait(true);
        }

        Refresh();
    }

    partial void OnSelectedItemChanged(NavigationItem value)
    {
        PageTitle = value.Title;
        CurrentPage = value.Page switch
        {
            AppPage.Dashboard => _dashboardPage,
            AppPage.Keyboard => _keyboardPage,
            AppPage.Mouse => _mousePage,
            AppPage.Trends => _trendsPage,
            AppPage.Apps => _appsPage,
            AppPage.Settings => _settingsPage,
            _ => _dashboardPage
        };
        if (value.Page == AppPage.Dashboard)
        {
            _dashboard.Refresh();
        }
        else if (value.Page == AppPage.Keyboard)
        {
            _keyboard.Refresh();
        }

        if (value.Page == AppPage.Mouse)
        {
            _mouse.Refresh();
        }

        if (value.Page == AppPage.Trends)
        {
            _trends.Refresh();
        }

        if (value.Page == AppPage.Apps)
        {
            _apps.Refresh();
        }

        if (value.Page == AppPage.Settings)
        {
            _settings.Refresh();
        }
        UpdatePageActivation();
    }

    private void UpdatePageActivation()
    {
        _dashboard.SetActive(_windowVisible && SelectedItem.Page == AppPage.Dashboard);
        _keyboard.SetActive(_windowVisible && SelectedItem.Page == AppPage.Keyboard);
        _mouse.SetActive(_windowVisible && SelectedItem.Page == AppPage.Mouse);
        _trends.SetActive(_windowVisible && SelectedItem.Page == AppPage.Trends);
        _apps.SetActive(_windowVisible && SelectedItem.Page == AppPage.Apps);
    }
}
