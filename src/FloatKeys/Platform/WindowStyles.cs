using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace FloatKeys.Platform;

internal static class WindowStyles
{
    internal const int MouseActivate = 0x0021;
    internal const int PointerActivate = 0x024B;
    internal const long NoActivate = 0x08000000;
    internal const long ToolWindow = 0x00000080;
    internal const long AppWindow = 0x00040000;
    private const int ExtendedStyle = -20;

    internal static long NonActivatingStyle(long existing) =>
        (existing | NoActivate | ToolWindow) & ~AppWindow;

    internal static nint? ActivationResponse(int message) => message switch
    {
        MouseActivate => 3,   // MA_NOACTIVATE: deliver the mouse message.
        PointerActivate => 3, // PA_NOACTIVATE: deliver the pointer message.
        _ => null
    };

    internal static void Apply(nint hwnd)
    {
        // Both supported RIDs are 64-bit; nint follows the host ABI.
        Marshal.SetLastPInvokeError(0);
        nint existing = NativeMethods.GetWindowLongPtr(hwnd, ExtendedStyle);
        CheckZero(existing);
        Marshal.SetLastPInvokeError(0);
        nint previous = NativeMethods.SetWindowLongPtr(hwnd, ExtendedStyle,
            (nint)NonActivatingStyle(existing.ToInt64()));
        CheckZero(previous);
        const uint flags = 0x0001 | 0x0002 | 0x0010 | 0x0020; // NOSIZE, NOMOVE, NOACTIVATE, FRAMECHANGED
        if (!NativeMethods.SetWindowPos(hwnd, -1, 0, 0, 0, 0, flags)) // HWND_TOPMOST
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    private static void CheckZero(nint value)
    {
        int error = Marshal.GetLastPInvokeError();
        if (value == 0 && error != 0) throw new Win32Exception(error);
    }
}
