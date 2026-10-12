using System.Runtime.InteropServices;

namespace FloatKeys.Platform;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { internal int Left, Top, Right, Bottom; internal readonly PixelRect Pixels => new(Left, Top, Right - Left, Bottom - Top); }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfo
    {
        internal int Size;
        internal Rect Monitor, Work;
        internal uint Flags;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowPosition
    {
        internal nint Hwnd, InsertAfter;
        internal int X, Y, Width, Height;
        internal uint Flags;
    }
    internal delegate bool MonitorCallback(nint monitor, nint hdc, nint rect, nint data);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")]
    internal static extern nint GetWindow(nint hwnd, uint command);
    internal const uint KeyboardInput = 1;
    internal const uint ExtendedKey = 0x0001;
    internal const uint KeyUp = 0x0002;
    internal const uint Unicode = 0x0004;
    internal const uint ScanCode = 0x0008;

    // INPUT must include the largest member (MOUSEINPUT), even for keyboard-only use.
    // Default native alignment places the union at offset 8 on ARM64/x64.
    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        internal uint Type;
        internal InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] internal MouseInput Mouse;
        [FieldOffset(0)] internal KeyboardInputData Keyboard;
        [FieldOffset(0)] internal HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        internal int X, Y;
        internal uint MouseData, Flags, Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInputData
    {
        internal ushort VirtualKey, Scan;
        internal uint Flags, Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HardwareInput
    {
        internal uint Message;
        internal ushort ParamLow, ParamHigh;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint count, [In] Input[] inputs, int size);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    internal static extern uint MapVirtualKey(uint code, uint mapType);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y,
        int width, int height, uint flags);
}
