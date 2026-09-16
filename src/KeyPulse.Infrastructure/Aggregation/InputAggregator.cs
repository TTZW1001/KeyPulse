using KeyPulse.Core.Events;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;
using KeyPulse.Infrastructure.Input;
using Microsoft.Extensions.Logging;

namespace KeyPulse.Infrastructure.Aggregation;

public sealed class InputAggregator : IStatisticsAggregator, IStatisticsReader, IDisposable
{
    private readonly IInputCapture _capture;
    private readonly ILogger<InputAggregator> _logger;
    private readonly IForegroundAppCache? _foreground;
    private readonly IExcludedAppList? _exclusions;
    private readonly IUserSettings? _settings;
    private readonly ShortcutTracker _shortcuts = new();
    private readonly object _gate = new();
    private StatisticsBuffer _active = new();
    private StatisticsBuffer _flush = new();
    private TrackingState _state = TrackingState.Running;
    private DateTimeOffset? _lastInputTime;
    private KeyPulse.Core.Models.PointerPosition? _lastPointerPosition;
    private KeyPulse.Core.Models.PointerPosition? _lastTrajectoryPosition;
    private DateTimeOffset _lastTrajectorySample;
    private bool _disposed;

    public InputAggregator(
        IInputCapture capture,
        ILogger<InputAggregator> logger,
        IForegroundAppCache? foreground = null,
        IExcludedAppList? exclusions = null,
        IUserSettings? settings = null)
    {
        _capture = capture;
        _logger = logger;
        _foreground = foreground;
        _exclusions = exclusions;
        _settings = settings;
        _capture.InputReceived += OnInputReceived;
        if (_foreground is not null)
        {
            _foreground.Sampled += OnForegroundSampled;
        }
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
            if (state != TrackingState.Running)
            {
                _shortcuts.Reset();
                _lastPointerPosition = null;
                _lastTrajectoryPosition = null;
            }
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

            if (inputEvent is KeyPressedEvent key)
            {
                var shortcut = _shortcuts.Process(key);
                if (!key.IsKeyDown)
                {
                    return;
                }

                _active.Add(key, CurrentAppOrNull());
                if (shortcut is not null && (_settings?.ShortcutStatsEnabled ?? true))
                {
                    _active.AddShortcut(key.Timestamp, shortcut);
                }
            }
            else if (inputEvent is MouseMoveEvent move)
            {
                var enriched = EnrichDistance(move);
                _active.Add(enriched, CurrentAppOrNull());
            }
            else if (inputEvent is MouseButtonEvent button)
            {
                _active.Add((_settings?.ScreenPositionStatsEnabled ?? false)
                    ? button
                    : button with { Position = null }, CurrentAppOrNull());
            }
            else
            {
                _active.Add(inputEvent, CurrentAppOrNull());
            }

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

    public StatisticsBatch CaptureUnflushed()
    {
        lock (_gate)
        {
            var batch = _active.ToBatch();
            return batch with { LastInputTime = _lastInputTime ?? batch.LastInputTime };
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

    public void Clear()
    {
        lock (_gate)
        {
            _active.Clear();
            _flush.Clear();
            _lastInputTime = null;
            _shortcuts.Reset();
            _lastPointerPosition = null;
            _lastTrajectoryPosition = null;
        }
    }

    public void ClearPositionData()
    {
        lock (_gate)
        {
            _active.ClearPositionData();
            _flush.ClearPositionData();
            _lastTrajectoryPosition = null;
        }
    }

    public void Merge(StatisticsBatch batch)
    {
        if (batch.IsEmpty)
        {
            return;
        }

        lock (_gate)
        {
            _active.Merge(batch);
            if (batch.LastInputTime is { } time &&
                (_lastInputTime is null || time > _lastInputTime))
            {
                _lastInputTime = time;
            }
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
        if (_foreground is not null)
        {
            _foreground.Sampled -= OnForegroundSampled;
        }
    }

    private void OnInputReceived(object? sender, InputEvent inputEvent)
    {
        Record(inputEvent);
    }

    private void OnForegroundSampled(ForegroundTick tick)
    {
        if (tick.App is null)
        {
            return;
        }

        lock (_gate)
        {
            if (_state != TrackingState.Running)
            {
                return;
            }

            if (IsExcluded(tick.App.ProcessName))
            {
                return;
            }

            _active.AddActive(tick.App, tick.Elapsed, tick.Timestamp);
        }
    }

    private ForegroundApp? CurrentAppOrNull()
    {
        var app = _foreground?.Current;
        if (app is null)
        {
            return null;
        }

        return IsExcluded(app.ProcessName) ? null : app;
    }

    private bool IsExcluded(string processName) =>
        _exclusions is not null && _exclusions.IsExcluded(processName);

    private MouseMoveEvent EnrichDistance(MouseMoveEvent move)
    {
        if (move.Position is not { } current)
        {
            _lastPointerPosition = null;
            _lastTrajectoryPosition = null;
            return move;
        }

        var previous = _lastPointerPosition;
        _lastPointerPosition = current;
        if (previous is not { } prior ||
            !string.Equals(prior.Layout.Signature, current.Layout.Signature, StringComparison.Ordinal))
        {
            _lastTrajectoryPosition = (_settings?.ScreenPositionStatsEnabled ?? false) ? current : null;
            _lastTrajectorySample = move.Timestamp;
            return move;
        }

        var dx = (double)current.X - prior.X;
        var dy = (double)current.Y - prior.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        var maximumPlausible = Math.Max(current.Layout.VirtualWidth, current.Layout.VirtualHeight) * 1.5;
        if (distance <= 0 || distance > maximumPlausible)
        {
            return move;
        }

        var dpi = Math.Max(1, (current.Monitor.DpiX + current.Monitor.DpiY) / 2);
        var previousTrajectory = default(KeyPulse.Core.Models.PointerPosition?);
        if (_settings?.ScreenPositionStatsEnabled ?? false)
        {
            if (_lastTrajectoryPosition is null ||
                move.Timestamp - _lastTrajectorySample >= TimeSpan.FromMilliseconds(4))
            {
                previousTrajectory = _lastTrajectoryPosition;
                _lastTrajectoryPosition = current;
                _lastTrajectorySample = move.Timestamp;
            }
        }
        else
        {
            _lastTrajectoryPosition = null;
        }

        return move with
        {
            CursorDistancePixels = distance,
            EstimatedDistanceMeters = distance / dpi * 0.0254,
            PreviousTrajectoryPosition = previousTrajectory
        };
    }
}
