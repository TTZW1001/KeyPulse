namespace KeyPulse.Infrastructure.Input;

internal static class ProcessNameGuard
{
    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Contains("://", StringComparison.Ordinal))
        {
            return null;
        }

        var name = Path.GetFileName(trimmed);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (name.IndexOfAny(['/', '\\', ':']) >= 0)
        {
            return null;
        }

        return name;
    }

    public static string? SanitizeDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Contains("://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.IndexOfAny(['/', '\\']) >= 0)
        {
            return null;
        }

        return trimmed;
    }
}
