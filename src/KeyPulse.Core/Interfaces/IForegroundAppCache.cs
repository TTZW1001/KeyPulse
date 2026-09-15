using KeyPulse.Core.Statistics;

namespace KeyPulse.Core.Interfaces;

public interface IForegroundAppCache
{
    ForegroundApp? Current { get; }

    event Action<ForegroundTick>? Sampled;
}
