namespace KeyPulse.Infrastructure.System;

public interface IRunKeyStore
{
    string? GetValue(string name);

    void SetValue(string name, string value);

    void DeleteValue(string name);
}
