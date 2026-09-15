using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyPulse.App.ViewModels;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;

namespace KeyPulse.App;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IStatisticsAggregator _aggregator;
    private readonly IInputCapture _capture;
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

    public MainWindowViewModel(
        IStatisticsAggregator aggregator,
        IInputCapture capture,
        DashboardViewModel dashboard,
        KeyboardViewModel keyboard,
        MouseViewModel mouse,
        TrendsViewModel trends,
        AppsViewModel apps,
        SettingsViewModel settings)
    {
        _aggregator = aggregator;
        _capture = capture;
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

    public void Refresh()
    {
        _dashboard.Refresh();
        _keyboard.Refresh();
        if (SelectedItem.Page == AppPage.Mouse)
        {
            _mouse.Refresh();
        }

        if (SelectedItem.Page == AppPage.Apps)
        {
            _apps.Refresh();
        }

        var state = _aggregator.State;
        if (_capture.Error is not null)
        {
            state = TrackingState.Error;
        }

        IsPaused = state == TrackingState.Paused;
        ShowPauseBanner = state is TrackingState.Paused or TrackingState.Error;
        StatusText = state switch
        {
            TrackingState.Paused => "已暂停",
            TrackingState.Error => "监听失败",
            _ => "正在统计"
        };
    }

    [RelayCommand]
    private void Resume()
    {
        if (_aggregator.State == TrackingState.Error)
        {
            return;
        }

        _aggregator.SetState(TrackingState.Running);
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
    }
}
