using System.Runtime.InteropServices;
using FloatKeys.Platform;

namespace FloatKeys.Input;

internal readonly record struct Submission(int Count, int Error);

internal interface IInputTransport
{
    Submission Send(NativeMethods.Input[] events);
    bool IsKeyDown(InputKey key);
}

internal sealed class NativeInputTransport : IInputTransport
{
    public Submission Send(NativeMethods.Input[] events)
    {
        Marshal.SetLastPInvokeError(0);
        uint sent = NativeMethods.SendInput((uint)events.Length, events,
            Marshal.SizeOf<NativeMethods.Input>());
        int error = Marshal.GetLastPInvokeError();
        return new((int)sent, sent == events.Length ? 0 : error);
    }

    public bool IsKeyDown(InputKey key)
    {
        if (key.Kind == InputKeyKind.Unicode) return false;
        uint vk = key.Kind == InputKeyKind.VirtualKey ? key.Code :
            NativeMethods.MapVirtualKey((uint)(key.Code | (key.Extended ? 0xE000 : 0)), 3);
        return vk != 0 && (NativeMethods.GetAsyncKeyState((int)vk) & 0x8000) != 0;
    }
}
