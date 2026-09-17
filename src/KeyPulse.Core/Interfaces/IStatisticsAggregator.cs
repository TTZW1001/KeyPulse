using KeyPulse.Core.Events;
using KeyPulse.Core.Statistics;

namespace KeyPulse.Core.Interfaces;

public interface IStatisticsAggregator
{
    TrackingState State { get; }

    void SetState(TrackingState state);

    void Record(InputEvent inputEvent);

    StatisticsSnapshot CaptureSnapshot();

    StatisticsBatch SwapForFlush();

    void Merge(StatisticsBatch batch);

    void Clear();

    void ClearPositionData();

    void ClearOccupancyData() { }
}
