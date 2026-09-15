using KeyPulse.Core.Statistics;

namespace KeyPulse.Core.Interfaces;

public interface IStatisticsReader
{
    TrackingState State { get; }

    StatisticsSnapshot CaptureSnapshot();
}
