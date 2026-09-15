using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyPulse.Core;
using KeyPulse.Core.Interfaces;

namespace KeyPulse.App;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly DebugInputCounters _counters;
    private readonly IInputCapture _capture;
    private long _uiTicks;

    public MainWindowViewModel(DebugInputCounters counters, IInputCapture capture)
    {
        _counters = counters;
        _capture = capture;
        _capture.InputReceived += (_, inputEvent) => _counters.Add(inputEvent);
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
        _counters.Paused = !_counters.Paused;
        Refresh();
    }

    public void Refresh()
    {
        _uiTicks++;
        var snap = _counters.Capture();
        PauseLabel = snap.Paused ? "恢复" : "暂停";
        StatusText = !_capture.IsListening && _capture.Error is not null
            ? "监听失败"
            : snap.Paused
                ? "已暂停"
                : "正在统计";

        var builder = new StringBuilder();
        if (_capture.Error is not null)
        {
            builder.AppendLine("Error: " + _capture.Error);
        }

        builder.AppendLine("UI refresh: 1s");
        builder.AppendLine("UI ticks: " + _uiTicks);
        builder.AppendLine();

        foreach (var pair in snap.Keys.OrderByDescending(p => p.Value).ThenBy(p => p.Key).Take(24))
        {
            builder.AppendLine($"{pair.Key}: {pair.Value}");
        }

        builder.AppendLine();
        builder.AppendLine($"Left: {snap.Left}   Right: {snap.Right}   Middle: {snap.Middle}");
        builder.AppendLine($"XButton1: {snap.XButton1}   XButton2: {snap.XButton2}");
        builder.AppendLine($"WheelUp: {snap.WheelUp}   WheelDown: {snap.WheelDown}");
        builder.AppendLine($"Distance: {snap.DistancePixels:0} px");
        StatsText = builder.ToString();
    }
}
