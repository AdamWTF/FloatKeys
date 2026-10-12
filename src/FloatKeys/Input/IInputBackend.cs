namespace FloatKeys.Input;

public readonly record struct InputResult(int Requested, int Submitted, int NativeError,
    bool CleanupFailed, string? Error)
{
    // Success means accepted by the Windows input queue, not delivered to an application.
    public bool Success => Error is null && Submitted == Requested && !CleanupFailed;
    internal static InputResult Rejected(string message) => new(0, 0, 0, false, message);
}

public interface IInputBackend : IDisposable
{
    InputResult KeyDown(InputKey key);
    InputResult KeyUp(InputKey key);
    InputResult KeyPress(InputKey key);
    InputResult KeyPressChord(InputKey key, IReadOnlyList<InputKey> modifiers);
    InputResult SendUnicodeText(string text);
    InputResult ReleaseAllKeys();
}
