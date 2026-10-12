namespace FloatKeys.Input;

// Device-neutral admission and duplicate filtering used by the WPF key control.
internal sealed class PointerSession
{
    internal long? Owner { get; private set; }
    internal bool Begin(long pointer, bool promotedMouse = false)
    {
        if (promotedMouse || Owner is not null) return false;
        Owner = pointer;
        return true;
    }
    internal bool Owns(long pointer, bool promotedMouse = false) => !promotedMouse && Owner == pointer;
    internal long? Clear() { var old = Owner; Owner = null; return old; }
}
