namespace FloatKeys.Input;

internal enum PrimaryKeyboardLayout { Chat, Compact }
internal enum KeyAction { Input, Shift, Ctrl, Alt, Caps, SwitchLayout }
internal enum KeyStyle { Character, Special, Modifier, Enter, Slash }
internal sealed record KeyDefinition(string Id, string Label, KeyAction Action,
    InputKey? Mapping = null, string? ShiftedLabel = null, InputKey? ShiftedMapping = null,
    bool Letter = false, bool Repeat = false, KeyStyle Style = KeyStyle.Character, double Width = 1,
    bool FixedCharacter = false, bool RequiredShift = false);

internal static class KeyboardLayout
{
    internal static readonly InputKey ShiftKey = InputKey.ScanCode(0x2A);
    internal static readonly InputKey CtrlKey = InputKey.ScanCode(0x1D);
    internal static readonly InputKey AltKey = InputKey.ScanCode(0x38);
    internal static readonly KeyDefinition Caps = new("caps", "Caps Lock", KeyAction.Caps, Style: KeyStyle.Modifier);
    private static KeyDefinition Modifier(string id, KeyAction action, double width = 1) =>
        new(id, id, action, Style: KeyStyle.Modifier, Width: width);
    private static KeyDefinition Special(string id, string label, ushort scan, bool extended = false, bool repeat = false, double width = 1) =>
        new(id, label, KeyAction.Input, InputKey.ScanCode(scan, extended), Repeat: repeat, Style: id == "enter" ? KeyStyle.Enter : KeyStyle.Special, Width: width);
    private static IReadOnlyList<KeyDefinition> Letters(string letters, ushort[] scans) =>
        Array.AsReadOnly(letters.Select((c, i) => new KeyDefinition(c.ToString(), c.ToString(), KeyAction.Input,
            InputKey.ScanCode(scans[i]), c.ToString().ToUpperInvariant(), InputKey.ScanCode(scans[i]), Letter: true)).ToArray());
    private static IReadOnlyList<KeyDefinition> Bottom(bool symbols, bool slash = false)
    {
        var keys = new List<KeyDefinition> {
            new("layout", symbols ? "ABC" : "?123", KeyAction.SwitchLayout, Style: KeyStyle.Special, Width: 1.4),
            Modifier("Ctrl", KeyAction.Ctrl), Modifier("Alt", KeyAction.Alt) };
        if (slash) keys.Add(Character("/", 0x35));
        keys.AddRange([Special("space", "Space", 0x39, width: 3.2),
            Special("left", "←", 0x4B, true, true), Special("right", "→", 0x4D, true, true),
            Special("enter", "Enter", 0x1C, width: 1.6)]);
        return keys.AsReadOnly();
    }
    private static KeyDefinition Character(string label, ushort scan, bool shift = false) =>
        new(label, label, KeyAction.Input, InputKey.ScanCode(scan),
            Style: label == "/" ? KeyStyle.Slash : KeyStyle.Character, FixedCharacter: true, RequiredShift: shift);
    private static KeyDefinition Digit(string label, string shifted, ushort scan) =>
        new(label, label, KeyAction.Input, InputKey.ScanCode(scan), shifted, InputKey.ScanCode(scan));
    private static readonly IReadOnlyList<KeyDefinition> Numbers = Array.AsReadOnly(new[] {
        Digit("1","!",0x02), Digit("2","\"",0x03), Digit("3","£",0x04), Digit("4","$",0x05),
        Digit("5","%",0x06), Digit("6","^",0x07), Digit("7","&",0x08), Digit("8","*",0x09),
        Digit("9","(",0x0A), Digit("0",")",0x0B) });
    private static readonly IReadOnlyList<KeyDefinition> Qwerty = Letters("qwertyuiop", [0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x18,0x19]);
    private static readonly IReadOnlyList<KeyDefinition> Home = Letters("asdfghjkl", [0x1E,0x1F,0x20,0x21,0x22,0x23,0x24,0x25,0x26]);
    private static readonly IReadOnlyList<KeyDefinition> Lower = Array.AsReadOnly(new[] { Modifier("Shift", KeyAction.Shift, 1.6) }
        .Concat(Letters("zxcvbnm", [0x2C,0x2D,0x2E,0x2F,0x30,0x31,0x32]))
        .Append(Special("backspace", "Bksp", 0x0E, repeat: true, width: 1.8)).ToArray());
    private static readonly IReadOnlyList<KeyDefinition> Punctuation = Array.AsReadOnly(new[] {
        Character("/",0x35), Character(":",0x27,true), Character(".",0x34), Character(",",0x33),
        Character("?",0x35,true), Character("!",0x02,true), Character("'",0x28), Character("-",0x0C) });
    internal static readonly IReadOnlyList<IReadOnlyList<KeyDefinition>> Chat = Array.AsReadOnly(new[] {
        Numbers, Qwerty, Home, Lower, Punctuation, Bottom(false) });
    internal static readonly IReadOnlyList<IReadOnlyList<KeyDefinition>> Compact = Array.AsReadOnly(new[] {
        Numbers, Qwerty, Home, Lower, Bottom(false, slash: true) });
    internal static IReadOnlyList<IReadOnlyList<KeyDefinition>> Main => Chat;
    internal static IReadOnlyList<IReadOnlyList<KeyDefinition>> Primary(PrimaryKeyboardLayout layout) =>
        layout == PrimaryKeyboardLayout.Compact ? Compact : Chat;
    internal static readonly IReadOnlyList<IReadOnlyList<KeyDefinition>> Symbols = Array.AsReadOnly(new IReadOnlyList<KeyDefinition>[] {
        Array.AsReadOnly(Punctuation.Concat(new[] { Character("@",0x28,true), Character("\"",0x03,true), Character("£",0x04,true) }).ToArray()),
        Array.AsReadOnly(new[] { Character("#",0x2B), Character("$",0x05,true), Character("%",0x06,true), Character("&",0x08,true),
            Character("*",0x09,true), Character("(",0x0A,true), Character(")",0x0B,true), Character("[",0x1A), Character("]",0x1B),
            Character("{",0x1A,true), Character("}",0x1B,true) }),
        Array.AsReadOnly(new[] { Character("\\",0x56), Character("|",0x56,true), Character("_",0x0C,true), Character("=",0x0D),
            Character("+",0x0D,true), Character(";",0x27), Character("`",0x29), Character("¬",0x29,true), Character("^",0x07,true),
            Character("~",0x2B,true), Character("<",0x33,true), Character(">",0x34,true) }),
        Array.AsReadOnly(new[] { Modifier("Shift", KeyAction.Shift, 1.6), Special("escape","Esc",0x01),
            Special("tab","Tab",0x0F), Special("delete","Delete",0x53,true,true),
            Special("up","↑",0x48,true,true), Special("down","↓",0x50,true,true) }), Bottom(true) });
}
