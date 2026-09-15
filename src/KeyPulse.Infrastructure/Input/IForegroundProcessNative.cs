namespace KeyPulse.Infrastructure.Input;

public interface IForegroundProcessNative
{
    nint GetForegroundWindow();

    uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    string? QueryImagePath(uint processId);

    string? QueryFileDescription(string imagePath);
}
