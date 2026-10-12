namespace FloatKeys.Input;

public enum InputKeyKind { ScanCode, VirtualKey, Unicode }

public readonly record struct InputKey
{
    public InputKeyKind Kind { get; }
    public ushort Code { get; }
    public bool Extended { get; }

    private InputKey(InputKeyKind kind, ushort code, bool extended)
    {
        Kind = kind;
        Code = code;
        Extended = extended;
    }

    public static InputKey ScanCode(ushort code, bool extended = false) =>
        code is > 0 and <= 0x7F ? new(InputKeyKind.ScanCode, code, extended) :
        throw new ArgumentOutOfRangeException(nameof(code));

    public static InputKey VirtualKey(ushort code, bool extended = false) =>
        code is > 0 and < 255 ? new(InputKeyKind.VirtualKey, code, extended) :
        throw new ArgumentOutOfRangeException(nameof(code));

    internal static InputKey Unicode(char code) => new(InputKeyKind.Unicode, code, false);
    internal bool IsValid => Kind == InputKeyKind.Unicode || Code != 0;
}

public static class PrototypeKeys
{
    public static InputKey A { get; } = InputKey.ScanCode(0x1E);
    public static InputKey Backspace { get; } = InputKey.ScanCode(0x0E);
    public static InputKey Enter { get; } = InputKey.ScanCode(0x1C);
}
