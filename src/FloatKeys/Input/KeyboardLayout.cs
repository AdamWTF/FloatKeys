namespace FloatKeys.Input;

internal enum KeyAction { Input, Shift, Ctrl, Alt, Caps, SwitchLayout }
internal enum KeyStyle { Character, Special, Modifier, Enter }
internal sealed record KeyDefinition(string Id, string Label, KeyAction Action,
    InputKey? Mapping = null, string? ShiftedLabel = null, InputKey? ShiftedMapping = null,
    bool Letter = false, bool Repeat = false, KeyStyle Style = KeyStyle.Character, double Width = 1);

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
    private static IReadOnlyList<KeyDefinition> Bottom(bool symbols) => Array.AsReadOnly(new[] {
        new KeyDefinition("layout", symbols ? "ABC" : "Symbols", KeyAction.SwitchLayout, Style: KeyStyle.Special, Width: 1.4),
        Modifier("Ctrl", KeyAction.Ctrl), Modifier("Alt", KeyAction.Alt),
        Special("space", "Space", 0x39, width: 3.2),
        Special("left", "←", 0x4B, true, true), Special("right", "→", 0x4D, true, true),
        Special("enter", "Enter", 0x1C, width: 1.6) });
    internal static readonly IReadOnlyList<IReadOnlyList<KeyDefinition>> Main = Array.AsReadOnly(new IReadOnlyList<KeyDefinition>[] {
        Letters("qwertyuiop", [0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x18,0x19]),
        Letters("asdfghjkl", [0x1E,0x1F,0x20,0x21,0x22,0x23,0x24,0x25,0x26]),
        Array.AsReadOnly(new[] { Modifier("Shift", KeyAction.Shift, 1.6) }
            .Concat(Letters("zxcvbnm", [0x2C,0x2D,0x2E,0x2F,0x30,0x31,0x32]))
            .Append(Special("backspace", "Backspace", 0x0E, repeat: true, width: 1.8)).ToArray()), Bottom(false) });
    private static KeyDefinition Symbol(string label, string shifted, ushort scan) =>
        new(label, label, KeyAction.Input, InputKey.ScanCode(scan), shifted, InputKey.ScanCode(scan));
    internal static readonly IReadOnlyList<IReadOnlyList<KeyDefinition>> Symbols = Array.AsReadOnly(new IReadOnlyList<KeyDefinition>[] {
        Array.AsReadOnly(new[] { Symbol("1","!",0x02), Symbol("2","\"",0x03), Symbol("3","£",0x04),
            Symbol("4","$",0x05), Symbol("5","%",0x06), Symbol("6","^",0x07), Symbol("7","&",0x08),
            Symbol("8","*",0x09), Symbol("9","(",0x0A), Symbol("0",")",0x0B) }),
        Array.AsReadOnly(new[] { Symbol("`","¬",0x29), Symbol("-","_",0x0C), Symbol("=","+",0x0D),
            Symbol("[","{",0x1A), Symbol("]","}",0x1B), Symbol(";",":",0x27), Symbol("'","@",0x28),
            Symbol("#","~",0x2B), Symbol("\\","|",0x56), Symbol(",","<",0x33), Symbol(".",">",0x34), Symbol("/","?",0x35) }),
        Array.AsReadOnly(new[] { Modifier("Shift", KeyAction.Shift, 1.6), Special("escape","Esc",0x01),
            Special("tab","Tab",0x0F), Special("delete","Delete",0x53,true,true),
            Special("up","↑",0x48,true,true), Special("down","↓",0x50,true,true) }), Bottom(true) });
}
