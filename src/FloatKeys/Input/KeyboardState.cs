namespace FloatKeys.Input;

// This is ownership of injected keys, not Windows' global/physical keyboard state.
internal sealed class KeyboardState
{
    private readonly HashSet<InputKey> held = [];
    internal int Count => held.Count;
    internal bool Contains(InputKey key) => held.Contains(key);
    internal InputKey[] Snapshot() => held.ToArray();
    internal void Apply(InputKey key, bool up)
    {
        if (up) held.Remove(key);
        else held.Add(key);
    }
}
