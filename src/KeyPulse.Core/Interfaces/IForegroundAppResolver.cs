using KeyPulse.Core.Statistics;

namespace KeyPulse.Core.Interfaces;

public interface IForegroundAppResolver
{
    ForegroundApp? TryResolve();
}
