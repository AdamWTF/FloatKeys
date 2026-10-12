namespace FloatKeys.Input;

// Sizes are DIPs; native geometry converts once at the window's current DPI.
internal static class KeyboardSizing
{
    internal const double MinimumWidth = 720;
    internal const double DefaultWidth = 840;
    internal const double TouchSize = 44;
    internal static double MinimumHeight(PrimaryKeyboardLayout layout, bool symbols = false) =>
        !symbols && layout == PrimaryKeyboardLayout.Chat ? 432 : 382;
    internal static double DefaultHeight(PrimaryKeyboardLayout layout) => layout == PrimaryKeyboardLayout.Chat ? 480 : 420;
    internal static PrimaryKeyboardLayout Parse(string? value) => value == "compact" ? PrimaryKeyboardLayout.Compact : PrimaryKeyboardLayout.Chat;
    internal static string Name(PrimaryKeyboardLayout layout) => layout == PrimaryKeyboardLayout.Compact ? "compact" : "chat";
}
