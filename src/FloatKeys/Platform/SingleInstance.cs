using System.Security.Principal;

namespace FloatKeys.Platform;

internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private bool owned;
    internal bool Owned => owned;
    internal SingleInstance(string? name = null)
    {
        if (name is null)
        {
            using var identity = WindowsIdentity.GetCurrent();
            name = @"Local\FloatKeys." + identity.User!.Value;
        }
        mutex = new Mutex(false, name);
        try { owned = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { owned = true; }
        catch { mutex.Dispose(); throw; }
    }
    public void Dispose()
    {
        if (owned) { owned = false; mutex.ReleaseMutex(); }
        mutex.Dispose();
    }
}
