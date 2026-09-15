using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.System;
using Microsoft.Extensions.Logging;

namespace KeyPulse.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly IApplicationLifecycle _lifecycle;
    private readonly IDashboardQuery _dashboard;
    private readonly IStatisticsAggregator _aggregator;
    private readonly IStartupService _startup;
    private readonly ILogger<TrayService> _logger;
    private readonly Icon _colorIcon;
    private readonly Icon _pausedIcon;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _keysItem;
    private readonly ToolStripMenuItem _clicksItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly SynchronizationContext? _ui;
    private readonly TaskbarCreatedRouter _taskbar;
    private readonly TaskbarCreatedWindow _taskbarWindow;
    private int _refreshing;
    private bool _disposed;

    public TrayService(
        IApplicationLifecycle lifecycle,
        IDashboardQuery dashboard,
        IStatisticsAggregator aggregator,
        IStartupService startup,
        ILogger<TrayService> logger)
    {
        _lifecycle = lifecycle;
        _dashboard = dashboard;
        _aggregator = aggregator;
        _startup = startup;
        _logger = logger;
        _ui = SynchronizationContext.Current;

        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "tray.ico");
        _colorIcon = TrayIconLoader.Load(iconPath, grayscale: false);
        _pausedIcon = TrayIconLoader.Load(iconPath, grayscale: true);

        _keysItem = new ToolStripMenuItem { Enabled = false };
        _clicksItem = new ToolStripMenuItem { Enabled = false };
        _pauseItem = new ToolStripMenuItem("暂停统计", null, (_, _) => TogglePause());
        _startupItem = new ToolStripMenuItem("开机自启", null, (_, _) => ToggleStartup())
        {
            CheckOnClick = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开 KeyPulse", null, (_, _) => _lifecycle.ShowMainWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_keysItem);
        menu.Items.Add(_clicksItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add("设置", null, (_, _) => _lifecycle.ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => _lifecycle.RequestExit());
        menu.Opening += (_, _) => _ = RefreshMenuAsync();

        _notifyIcon = new NotifyIcon
        {
            Icon = _colorIcon,
            Visible = true,
            Text = TooltipFor(_aggregator.State),
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => _lifecycle.ShowMainWindow();
        ApplyState(_aggregator.State, 0, 0);
        _ = RefreshMenuAsync();

        _taskbar = new TaskbarCreatedRouter(TaskbarCreatedWindow.NativeMessageId);
        _taskbar.RecreateRequested += RecreateIcon;
        _taskbarWindow = new TaskbarCreatedWindow(_taskbar);
        _logger.LogInformation("Tray icon created");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _taskbar.RecreateRequested -= RecreateIcon;
        _taskbarWindow.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _colorIcon.Dispose();
        _pausedIcon.Dispose();
    }

    public void RecreateIcon()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Visible = true;
        _logger.LogInformation("Tray icon restored");
    }

    private void TogglePause()
    {
        if (_aggregator.State == TrackingState.Error)
        {
            return;
        }

        _aggregator.SetState(
            _aggregator.State == TrackingState.Paused
                ? TrackingState.Running
                : TrackingState.Paused);
        _ = RefreshMenuAsync();
    }

    private void ToggleStartup()
    {
        if (_startupItem.Checked)
        {
            _startup.Enable();
        }
        else
        {
            _startup.Disable();
        }
    }

    private async Task RefreshMenuAsync()
    {
        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
        {
            return;
        }

        try
        {
            var today = await _dashboard.GetTodayAsync().ConfigureAwait(false);
            Post(() => ApplyState(_aggregator.State, today.KeyPressCount, today.MouseClickCount));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read tray totals");
            Post(() => ApplyState(_aggregator.State, 0, 0));
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private void ApplyState(TrackingState state, long keys, long clicks)
    {
        if (_disposed)
        {
            return;
        }

        var culture = CultureInfo.CurrentCulture;
        _keysItem.Text = "今日：" + keys.ToString("N0", culture) + " 次按键";
        _clicksItem.Text = "      " + clicks.ToString("N0", culture) + " 次点击";
        _pauseItem.Text = state == TrackingState.Paused ? "恢复统计" : "暂停统计";
        _startupItem.Checked = _startup.IsEnabled;
        _notifyIcon.Text = TooltipFor(state);
        _notifyIcon.Icon = state == TrackingState.Paused ? _pausedIcon : _colorIcon;
    }

    private void Post(Action action)
    {
        if (_ui is null)
        {
            action();
            return;
        }

        _ui.Post(_ => action(), null);
    }

    private static string TooltipFor(TrackingState state) =>
        state == TrackingState.Paused ? "KeyPulse · 已暂停" : "KeyPulse · 正在统计";
}
