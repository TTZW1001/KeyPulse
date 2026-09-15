using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace KeyPulse.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly IApplicationLifecycle _lifecycle;
    private readonly IStatisticsReader _reader;
    private readonly IStatisticsAggregator _aggregator;
    private readonly IStatisticsRepository _repository;
    private readonly IStartupService _startup;
    private readonly ILogger<TrayService> _logger;
    private readonly Icon _colorIcon;
    private readonly Icon _pausedIcon;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _keysItem;
    private readonly ToolStripMenuItem _clicksItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _startupItem;
    private bool _disposed;

    public TrayService(
        IApplicationLifecycle lifecycle,
        IStatisticsReader reader,
        IStatisticsAggregator aggregator,
        IStatisticsRepository repository,
        IStartupService startup,
        ILogger<TrayService> logger)
    {
        _lifecycle = lifecycle;
        _reader = reader;
        _aggregator = aggregator;
        _repository = repository;
        _startup = startup;
        _logger = logger;

        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "tray-icon.png");
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
        menu.Items.Add("设置", null, (_, _) => _lifecycle.ShowMainWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => _lifecycle.RequestExit());
        menu.Opening += (_, _) => RefreshMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = _colorIcon,
            Visible = true,
            Text = TooltipFor(_aggregator.State),
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => _lifecycle.ShowMainWindow();
        RefreshMenu();
        _logger.LogInformation("Tray icon created");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _colorIcon.Dispose();
        _pausedIcon.Dispose();
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
        RefreshMenu();
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

    private void RefreshMenu()
    {
        var state = _aggregator.State;
        var (keys, clicks) = ReadTodayTotals();
        _keysItem.Text = "今日：" + keys.ToString("N0", CultureInfo.CurrentCulture) + " 次按键";
        _clicksItem.Text = "      " + clicks.ToString("N0", CultureInfo.CurrentCulture) + " 次点击";
        _pauseItem.Text = state == TrackingState.Paused ? "恢复统计" : "暂停统计";
        _startupItem.Checked = _startup.IsEnabled;
        _notifyIcon.Text = TooltipFor(state);
        _notifyIcon.Icon = state == TrackingState.Paused ? _pausedIcon : _colorIcon;
    }

    private (long Keys, long Clicks) ReadTodayTotals()
    {
        var snap = _reader.CaptureSnapshot();
        var memoryKeys = snap.KeyCounts.Values.Sum();
        var memoryClicks = snap.Mouse.Left + snap.Mouse.Right + snap.Mouse.Middle +
                           snap.Mouse.XButton1 + snap.Mouse.XButton2;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var persistedKeys = _repository.GetKeyStatsAsync(today, today).GetAwaiter().GetResult()
                .Sum(row => row.PressCount);
            var mouseRows = _repository.GetMouseStatsAsync(today, today).GetAwaiter().GetResult();
            var persistedClicks = mouseRows.Sum(row =>
                row.Mouse.Left + row.Mouse.Right + row.Mouse.Middle +
                row.Mouse.XButton1 + row.Mouse.XButton2);
            return (memoryKeys + persistedKeys, memoryClicks + persistedClicks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read persisted totals for tray");
            return (memoryKeys, memoryClicks);
        }
    }

    private static string TooltipFor(TrackingState state) =>
        state == TrackingState.Paused ? "KeyPulse · 已暂停" : "KeyPulse · 正在统计";
}
