using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;

namespace KeyPulse.App;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IStatisticsReader _reader;
    private readonly IStatisticsAggregator _aggregator;
    private readonly IInputCapture _capture;
    private long _uiTicks;

    public MainWindowViewModel(
        IStatisticsReader reader,
        IStatisticsAggregator aggregator,
        IInputCapture capture)
    {
        _reader = reader;
        _aggregator = aggregator;
        _capture = capture;
        Refresh();
    }

    public string Title => ProductInfo.Name;

    public string PrivacyNotice => ProductInfo.PrivacyNotice;

    [ObservableProperty]
    private string _statusText = "正在统计";

    [ObservableProperty]
    private string _pauseLabel = "暂停";

    [ObservableProperty]
    private string _statsText = string.Empty;

    [RelayCommand]
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
        Refresh();
    }

    public void Refresh()
    {
        _uiTicks++;
        var snap = _reader.CaptureSnapshot();
        PauseLabel = snap.State == TrackingState.Paused ? "恢复" : "暂停";
        StatusText = snap.State switch
        {
            TrackingState.Paused => "已暂停",
            TrackingState.Error => "监听失败",
            _ => "正在统计"
        };

        var builder = new StringBuilder();
        if (_capture.Error is not null)
        {
            builder.AppendLine("Error: " + _capture.Error);
        }

        builder.AppendLine("State: " + snap.State);
        builder.AppendLine("UI refresh: 1s");
        builder.AppendLine("UI ticks: " + _uiTicks);
        if (snap.LastInputTime is not null)
        {
            builder.AppendLine("Last input: " + snap.LastInputTime.Value.ToLocalTime().ToString("HH:mm:ss"));
        }

        builder.AppendLine();
        foreach (var pair in snap.KeyCounts.OrderByDescending(p => p.Value).ThenBy(p => p.Key).Take(24))
        {
            builder.AppendLine($"{pair.Key}: {pair.Value}");
        }

        var mouse = snap.Mouse;
        builder.AppendLine();
        builder.AppendLine($"Left: {mouse.Left}   Right: {mouse.Right}   Middle: {mouse.Middle}");
        builder.AppendLine($"XButton1: {mouse.XButton1}   XButton2: {mouse.XButton2}");
        builder.AppendLine($"WheelUp: {mouse.WheelUp}   WheelDown: {mouse.WheelDown}");
        builder.AppendLine($"Distance: {mouse.DistancePixels:0} px");

        var now = DateTimeOffset.Now.ToLocalTime();
        var currentHour = new HourBucket(DateOnly.FromDateTime(now.DateTime), now.Hour);
        if (snap.HourlyCounts.TryGetValue(currentHour, out var hourly))
        {
            builder.AppendLine();
            builder.AppendLine(
                $"Hour {currentHour.Hour:00}: keys {hourly.KeyPressCount}  clicks {hourly.MouseClickCount}  wheel {hourly.WheelEventCount}");
        }

        StatsText = builder.ToString();
    }
}
