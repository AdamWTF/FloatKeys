namespace FloatKeys.Input;

// A press is committed only on release inside its original control. Leaving cancels it.
internal sealed class TapGesture
{
    internal bool IsPressed { get; private set; }
    private bool cancelled;
    internal bool Begin()
    {
        if (IsPressed) return false;
        IsPressed = true;
        cancelled = false;
        return true;
    }
    internal void Move(bool inside) { if (!inside) cancelled = true; }
    internal bool End(bool inside)
    {
        bool commit = IsPressed && !cancelled && inside;
        Cancel();
        return commit;
    }
    internal void Cancel() { IsPressed = false; cancelled = true; }
}
