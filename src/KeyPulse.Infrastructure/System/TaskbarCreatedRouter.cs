namespace KeyPulse.Infrastructure.System;

public sealed class TaskbarCreatedRouter
{
    private readonly uint _messageId;

    public TaskbarCreatedRouter(uint messageId)
    {
        _messageId = messageId;
    }

    public int RecreateCount { get; private set; }

    public event Action? RecreateRequested;

    public bool TryHandle(int msg)
    {
        if (_messageId == 0 || (uint)msg != _messageId)
        {
            return false;
        }

        RecreateCount++;
        RecreateRequested?.Invoke();
        return true;
    }
}
