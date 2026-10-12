namespace FloatKeys.Services;

// Every stage runs even if an earlier resource fails to clean up. Called on the UI thread.
internal sealed class ShutdownCoordinator(Action<string> log)
{
    private readonly List<(string Name, Action Cleanup)> stages = [];
    internal bool Stopping { get; private set; }
    internal void Add(string name, Action cleanup) => stages.Add((name, cleanup));
    internal void Stop()
    {
        if (Stopping) return;
        Stopping = true;
        foreach (var (name, cleanup) in stages)
            Try(name, cleanup, log);
    }
    internal static void Try(string name, Action cleanup, Action<string> log)
    {
        try { cleanup(); }
        catch (Exception e) { log($"{name} failed: {e.GetType().Name}"); }
    }
}
