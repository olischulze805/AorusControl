namespace AorusControl.App.Infrastructure;

/// <summary>Session-local instance ownership; no arbitrary commands or payloads accepted.</summary>
public sealed class SingleInstanceGate : IDisposable
{
    private readonly Mutex _presence;
    private readonly EventWaitHandle _activation;
    public bool IsPrimary { get; }
    public WaitHandle Activation => _activation;

    public SingleInstanceGate(string name)
    {
        _presence = new Mutex(false, name + ".Presence", out bool created);
        IsPrimary = created;
        try { _activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate"); }
        catch { _presence.Dispose(); throw; }
    }

    public void RequestActivation() => _activation.Set();
    public void Dispose() { _activation.Dispose(); _presence.Dispose(); }
}
