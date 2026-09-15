using KeyPulse.Core.Events;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using Microsoft.Extensions.Logging;

namespace KeyPulse.Infrastructure.Aggregation;

public sealed class InputAggregator : IStatisticsAggregator, IStatisticsReader, IDisposable
{
    private readonly IInputCapture _capture;
    private readonly ILogger<InputAggregator> _logger;
    private readonly object _gate = new();
    private StatisticsBuffer _active = new();
    private StatisticsBuffer _flush = new();
    private TrackingState _state = TrackingState.Running;
    private DateTimeOffset? _lastInputTime;
    private bool _disposed;

    public InputAggregator(IInputCapture capture, ILogger<InputAggregator> logger)
    {
        _capture = capture;
        _logger = logger;
        _capture.InputReceived += OnInputReceived;
    }

    public TrackingState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public void SetState(TrackingState state)
    {
        lock (_gate)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
        }

        switch (state)
        {
            case TrackingState.Paused:
                _logger.LogInformation("Tracking paused");
                break;
            case TrackingState.Running:
                _logger.LogInformation("Tracking running");
                break;
            case TrackingState.Error:
                _logger.LogWarning("Tracking entered error state");
                break;
        }
    }

    public void Record(InputEvent inputEvent)
    {
        lock (_gate)
        {
            if (_state != TrackingState.Running)
            {
                return;
            }

            _active.Add(inputEvent);
            _lastInputTime = inputEvent.Timestamp;
        }
    }

    public StatisticsSnapshot CaptureSnapshot()
    {
        lock (_gate)
        {
            var snapshot = _active.ToSnapshot(_state);
            return snapshot with { LastInputTime = _lastInputTime ?? snapshot.LastInputTime };
        }
    }

    public StatisticsBatch SwapForFlush()
    {
        lock (_gate)
        {
            (_active, _flush) = (_flush, _active);
            _active.Clear();
            return _flush.ToBatch();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _capture.InputReceived -= OnInputReceived;
    }

    private void OnInputReceived(object? sender, InputEvent inputEvent)
    {
        Record(inputEvent);
    }
}
