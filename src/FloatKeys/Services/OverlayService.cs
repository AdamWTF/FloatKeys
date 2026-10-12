using System.Windows;
using System.Windows.Interop;
using FloatKeys.Input;
using FloatKeys.Platform;
using FloatKeys.Windows;

namespace FloatKeys.Services;

internal sealed class OverlayService : IDisposable
{
    private readonly KeyboardWindow window;
    private readonly PrototypeLog log;
    private HwndSource? source;

    internal OverlayService(IInputBackend input, PrototypeLog log)
    {
        this.log = log;
        window = new KeyboardWindow(input, log);
        window.Closed += (_, _) => Dispose();
    }

    internal void Show()
    {
        Rect workArea = SystemParameters.WorkArea;
        window.Left = Math.Max(workArea.Left, workArea.Right - window.Width - 20);
        window.Top = Math.Max(workArea.Top, workArea.Bottom - window.Height - 20);
        nint before = NativeMethods.GetForegroundWindow();
        // EnsureHandle initializes an invisible HWND; styles and hook precede the first Show.
        nint hwnd = new WindowInteropHelper(window).EnsureHandle();
        source = HwndSource.FromHwnd(hwnd) ?? throw new InvalidOperationException("Overlay HWND unavailable.");
        source.AddHook(WindowHook);
        WindowStyles.Apply(hwnd);
        Application.Current.MainWindow = window;
        window.Show();
        log.Write($"show overlay=0x{hwnd:X} foregroundBefore=0x{before:X} foregroundAfter=0x{NativeMethods.GetForegroundWindow():X}");
    }

    private nint WindowHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        nint? response = WindowStyles.ActivationResponse(message);
        if (response is null) return 0;
        log.Write($"activation suppressed message=0x{message:X} foreground=0x{NativeMethods.GetForegroundWindow():X}");
        handled = true;
        return response.Value;
    }

    public void Dispose()
    {
        window.CancelInteractions();
        source?.RemoveHook(WindowHook);
        source = null;
    }
}
