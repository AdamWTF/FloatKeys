using System.Runtime.InteropServices;

namespace FloatKeys.Platform;

internal static class DisplayService
{
    internal static IReadOnlyList<DisplayArea> Read()
    {
        var displays = new List<DisplayArea>();
        if (!NativeMethods.EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
        {
            var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
            if (NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                double scale = NativeMethods.GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 ? dpi / 96d : 1;
                displays.Add(new(info.Monitor.Pixels, info.Work.Pixels, scale, (info.Flags & 1) != 0));
            }
            return true;
        }, 0) || displays.Count == 0) throw new InvalidOperationException("No available displays.");
        return displays;
    }
}
