using System.Runtime.InteropServices;

namespace FloatKeys.Platform;

internal static class NativeMethods
{
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
