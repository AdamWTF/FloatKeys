using FloatKeys.Platform;

namespace FloatKeys.Input;

internal static class InputEventFactory
{
    internal static NativeMethods.Input Create(InputKey key, bool up)
    {
        if (!key.IsValid) throw new ArgumentException("Invalid key.", nameof(key));
        uint flags = up ? NativeMethods.KeyUp : 0;
        if (key.Extended) flags |= NativeMethods.ExtendedKey;
        if (key.Kind == InputKeyKind.ScanCode) flags |= NativeMethods.ScanCode;
        if (key.Kind == InputKeyKind.Unicode) flags |= NativeMethods.Unicode;
        return new NativeMethods.Input
        {
            Type = NativeMethods.KeyboardInput,
            Data = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KeyboardInputData
                {
                    VirtualKey = key.Kind == InputKeyKind.VirtualKey ? key.Code : (ushort)0,
                    Scan = key.Kind != InputKeyKind.VirtualKey ? key.Code : (ushort)0,
                    Flags = flags
                }
            }
        };
    }
}
