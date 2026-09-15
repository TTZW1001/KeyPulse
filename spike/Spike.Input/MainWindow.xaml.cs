using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Spike.Input;

public partial class MainWindow : Window
{
    private static readonly string StatusDirectory =
        Path.Combine(Path.GetTempPath(), "KeyPulseSpike");

    private readonly InputCounters _counters = new();
    private readonly DispatcherTimer _uiTimer;
    private RawInputService? _rawInput;
    private IntPtr _windowHandle;

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(StatusDirectory);

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += (_, _) => RefreshUi();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _rawInput = new RawInputService(_counters);
        _rawInput.Start();
        File.WriteAllText(Path.Combine(StatusDirectory, "input-ready.txt"), _rawInput.Hwnd.ToString("X"));
        _uiTimer.Start();
        RefreshUi();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _uiTimer.Stop();
        _rawInput?.Dispose();
    }

    private void PauseButton_OnClick(object sender, RoutedEventArgs e)
    {
        _counters.Paused = !_counters.Paused;
        PauseButton.Content = _counters.Paused ? "Resume" : "Pause";
        RefreshUi();
    }

    private void RefreshUi()
    {
        _counters.UiRefreshTicks++;
        var snap = _counters.Capture();
        var foreground = NativeMethods.GetForegroundWindow() == _windowHandle;
        var listening = _rawInput?.IsListening == true;

        StatusText.Text = listening
            ? (snap.Paused ? "Paused" : "Listening")
            : "Raw Input failed";

        var builder = new StringBuilder();
        builder.AppendLine("Status: " + StatusText.Text);
        if (_rawInput?.Error is not null)
        {
            builder.AppendLine("Error: " + _rawInput.Error);
        }

        builder.AppendLine("HWND: " + (_rawInput?.Hwnd.ToString("X") ?? "-"));
        builder.AppendLine("This window focused: " + (foreground ? "yes" : "no (background sink should still count)"));
        builder.AppendLine("UI refresh: 1s (MouseMove does not refresh UI)");
        builder.AppendLine("UI ticks: " + snap.UiRefreshTicks);
        builder.AppendLine("WM_INPUT messages: " + snap.WmInputMessages);
        builder.AppendLine();
        builder.AppendLine("Keyboard down events: " + snap.KeyDownEvents);
        foreach (var pair in snap.Keys.OrderByDescending(p => p.Value).ThenBy(p => p.Key).Take(40))
        {
            builder.AppendLine($"  {pair.Key}: {pair.Value}");
        }

        builder.AppendLine();
        builder.AppendLine($"Left: {snap.Left}   Right: {snap.Right}   Middle: {snap.Middle}   X1: {snap.X1}   X2: {snap.X2}");
        builder.AppendLine($"WheelUp: {snap.WheelUp}   WheelDown: {snap.WheelDown}   WheelLeft: {snap.WheelLeft}   WheelRight: {snap.WheelRight}");
        builder.AppendLine($"Distance: {snap.DistancePixels:0} px");
        builder.AppendLine($"MouseMove events (memory only): {snap.MoveEvents}");

        StatsText.Text = builder.ToString();
        WriteStatusFile(snap, foreground, listening);
    }

    private void WriteStatusFile(InputCounters.Snapshot snap, bool foreground, bool listening)
    {
        var path = Path.Combine(StatusDirectory, "input-status.txt");
        var builder = new StringBuilder();
        builder.AppendLine("ts=" + DateTimeOffset.Now.ToString("o"));
        builder.AppendLine("listening=" + listening);
        builder.AppendLine("error=" + (_rawInput?.Error ?? ""));
        builder.AppendLine("paused=" + snap.Paused);
        builder.AppendLine("foreground=" + foreground);
        builder.AppendLine("hwnd=" + (_rawInput?.Hwnd.ToString("X") ?? ""));
        builder.AppendLine("wmInput=" + snap.WmInputMessages);
        builder.AppendLine("uiTicks=" + snap.UiRefreshTicks);
        builder.AppendLine("keyDown=" + snap.KeyDownEvents);
        builder.AppendLine("left=" + snap.Left);
        builder.AppendLine("right=" + snap.Right);
        builder.AppendLine("middle=" + snap.Middle);
        builder.AppendLine("x1=" + snap.X1);
        builder.AppendLine("x2=" + snap.X2);
        builder.AppendLine("wheelUp=" + snap.WheelUp);
        builder.AppendLine("wheelDown=" + snap.WheelDown);
        builder.AppendLine("distance=" + snap.DistancePixels.ToString("0.###"));
        builder.AppendLine("moveEvents=" + snap.MoveEvents);
        foreach (var pair in snap.Keys.OrderBy(p => p.Key))
        {
            builder.AppendLine("key." + pair.Key + "=" + pair.Value);
        }

        File.WriteAllText(path, builder.ToString());
    }
}
