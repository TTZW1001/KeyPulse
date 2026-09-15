using System.Collections.Concurrent;
using KeyPulse.Core.Interfaces;
using KeyPulse.Core.Statistics;

namespace KeyPulse.Infrastructure.Input;

public sealed class ForegroundAppResolver : IForegroundAppResolver
{
    private readonly IForegroundProcessNative _native;
    private readonly ConcurrentDictionary<string, string?> _displayNames = new(StringComparer.OrdinalIgnoreCase);

    public ForegroundAppResolver(IForegroundProcessNative native)
    {
        _native = native;
    }

    public ForegroundApp? TryResolve()
    {
        var hwnd = _native.GetForegroundWindow();
        if (hwnd == 0)
        {
            return null;
        }

        _native.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
        {
            return null;
        }

        var imagePath = _native.QueryImagePath(processId);
        var processName = ProcessNameGuard.Sanitize(imagePath);
        if (processName is null)
        {
            return null;
        }

        var displayName = _displayNames.GetOrAdd(processName, _ =>
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            return ProcessNameGuard.SanitizeDisplayName(_native.QueryFileDescription(imagePath));
        });

        return new ForegroundApp(processName, displayName);
    }
}
