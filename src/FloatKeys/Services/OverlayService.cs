using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using FloatKeys.Controls;
using FloatKeys.Input;
using FloatKeys.Models;
using FloatKeys.Platform;
using FloatKeys.Windows;

namespace FloatKeys.Services;

internal sealed class OverlayService : IDisposable, IOverlayCommands
{
    private readonly KeyboardWindow keyboard;
    private readonly LauncherWindow launcher;
    private readonly AppLog log;
    private readonly SettingsService storage;
    private readonly OverlayVisibility visibility;
    private readonly List<(HwndSource Source, HwndSourceHook Hook)> hooks = [];
    private AppSettings settings;
    private IReadOnlyList<DisplayArea> displays = [];
    private nint launcherHwnd, keyboardHwnd;
    private bool disposed, displayQueued, orderQueued, started;
    private bool interruptDisplayGestures;

    public bool KeyboardVisible => visibility.Visible;
    public bool LauncherVisible => launcher.IsVisible;
    public void ShowKeyboard() { if (!disposed) visibility.Show(); }
    public void HideKeyboard() { if (!disposed) visibility.Hide(); }
    public void ShowLauncher()
    {
        if (disposed || launcher.IsVisible) return;
        RepairBounds(); launcher.Show(); OrderLauncher();
    }
    public void HideLauncher()
    {
        if (disposed || !launcher.IsVisible) return;
        launcher.Handle.Cancel(); Save(); launcher.Hide();
    }
    public void ShowSettings() { ShowKeyboard(); keyboard.ShowSettings(); }
    public void SetStartupPreference(bool enabled)
    {
        settings = settings with { StartWithWindows = enabled };
        Save();
    }

    internal OverlayService(IInputBackend input, AppLog log, SettingsService? settingsService = null)
    {
        this.log = log;
        storage = settingsService ?? new(log: log.Write);
        settings = storage.Load();
        launcher = new();
        keyboard = new(input, log, settings.Symbols);
        visibility = new(() =>
        {
            RepairBounds();
            keyboard.Show();
            OrderLauncher();
            Save();
        }, () => { keyboard.CancelGeometry(); keyboard.CancelInteractions(); }, keyboard.Hide);
        keyboard.CollapseRequested += visibility.Hide;
        keyboard.LayoutChanged += Save;
        keyboard.SizeStep += step => ChangePreferences(step * 8, 0);
        keyboard.OpacityStep += step => ChangePreferences(0, step * .05);
        launcher.Closing += LauncherClosing;
        keyboard.DismissRequested += HideKeyboard;
        BindGesture(launcher.Handle, launcher, false, visibility.Toggle);
        BindGesture(keyboard.DragHandle, keyboard, false);
        BindGesture(keyboard.ResizeHandle, keyboard, true);
    }

    private void LauncherClosing(object? sender, CancelEventArgs e)
    {
        if (disposed) return;
        e.Cancel = true;
        HideLauncher();
    }

    internal void Show()
    {
        if (started) return;
        displays = DisplayService.Read();
        launcherHwnd = Prepare(launcher);
        keyboardHwnd = Prepare(keyboard);
        Apply(launcherHwnd, OverlayGeometry.Restore(settings.Launcher, displays, true));
        Apply(keyboardHwnd, OverlayGeometry.Restore(settings.Keyboard, displays, false));
        launcher.SetBackgroundOpacity(settings.LauncherOpacity);
        keyboard.RefreshSettings(settings.Launcher.WidthDip, settings.LauncherOpacity);
        Application.Current.MainWindow = launcher;
        nint before = NativeMethods.GetForegroundWindow();
        launcher.Show();
        OrderLauncher();
        started = true;
        Save();
        log.Write("overlays initialised");
    }

    private nint Prepare(Window window)
    {
        nint hwnd = new WindowInteropHelper(window).EnsureHandle();
        var source = HwndSource.FromHwnd(hwnd) ?? throw new InvalidOperationException("Overlay HWND unavailable.");
        HwndSourceHook hook = WindowHook;
        source.AddHook(hook);
        hooks.Add((source, hook));
        WindowStyles.Apply(hwnd);
        return hwnd;
    }

    private nint WindowHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        var response = WindowStyles.ActivationResponse(message);
        if (response is not null) { handled = true; return response.Value; }
        if (message == 0x0046 && lParam != 0) // WM_WINDOWPOSCHANGING: also guard WPF's DPI/visibility paths.
        {
            var position = Marshal.PtrToStructure<NativeMethods.WindowPosition>(lParam);
            position.Flags |= 0x0010; // SWP_NOACTIVATE
            Marshal.StructureToPtr(position, lParam, false);
        }
        if (message is 0x007E or 0x001A or 0x02E0) QueueDisplayRecovery(message != 0x02E0);
        if (message == 0x0047 && hwnd == keyboardHwnd) QueueOrder();
        return 0; // WPF still handles movement, size and DPI updates.
    }

    private void QueueOrder()
    {
        if (orderQueued || disposed) return;
        orderQueued = true;
        launcher.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            orderQueued = false;
            if (!disposed && started) OrderLauncher();
        }));
    }
    private void QueueDisplayRecovery(bool interrupt)
    {
        interruptDisplayGestures |= interrupt;
        if (displayQueued || disposed) return;
        displayQueued = true;
        launcher.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            displayQueued = false;
            if (disposed || !started) return;
            // Monitor-crossing DPI changes must not end an otherwise valid drag.
            // Actual desktop reconfiguration cancels gestures before recovering geometry.
            if (interruptDisplayGestures)
            {
                launcher.Handle.Cancel();
                keyboard.CancelGeometry();
            }
            interruptDisplayGestures = false;
            keyboard.CancelInteractions();
            try
            {
                displays = DisplayService.Read(); RepairBounds();
                if (!launcher.Handle.Active && !keyboard.DragHandle.Active && !keyboard.ResizeHandle.Active) Save();
            }
            catch (Exception e) { log.Write($"display recovery failed: {e.GetType().Name}"); }
        }));
    }

    private void RepairBounds()
    {
        displays = DisplayService.Read();
        foreach (var hwnd in new[] { launcherHwnd, keyboardHwnd })
        {
            var current = Bounds(hwnd);
            var valid = OverlayGeometry.Recover(current, displays, hwnd == launcherHwnd);
            if (valid != current) Apply(hwnd, valid);
        }
        OrderLauncher();
    }

    private void BindGesture(PointerHandle handle, Window window, bool resize, Action? tapped = null)
    {
        var gesture = new MoveGesture();
        PixelRect start = default;
        double scale = 1;
        bool moved = false;
        handle.Started += p =>
        {
            if (window == keyboard) keyboard.CancelInteractions();
            nint hwnd = Handle(window);
            start = Bounds(hwnd);
            scale = Scale(hwnd);
            moved = false;
            gesture.Begin(p.X, p.Y, scale);
        };
        handle.Moved += p =>
        {
            var delta = gesture.Move(p.X, p.Y);
            if (!gesture.Dragging) return;
            var candidate = start with { X = start.X + delta.X, Y = start.Y + delta.Y };
            var display = OverlayGeometry.Nearest(resize ? start : candidate, displays);
            // Use the actual current window DPI; WPF handles changes crossing display boundaries.
            nint hwnd = Handle(window);
            if (resize) candidate = OverlayGeometry.Resize(start, delta.X, delta.Y, Scale(hwnd), display.Bounds);
            else
            {
                var current = Bounds(hwnd);
                candidate = candidate with { Width = current.Width, Height = current.Height };
                candidate = OverlayGeometry.Fit(candidate, display.Bounds);
            }
            if (Bounds(hwnd) != candidate) { Apply(hwnd, candidate); moved = true; }
            OrderLauncher();
        };
        handle.Completed += (inside, cancelled) =>
        {
            bool tap = gesture.End(inside, cancelled);
            if (moved) Save();
            if (tap) tapped?.Invoke();
        };
    }

    private void ChangePreferences(double sizeDelta, double opacityDelta)
    {
        double diameter = Math.Clamp(settings.Launcher.WidthDip + sizeDelta, 48, 80);
        settings = settings with { Launcher = settings.Launcher with { WidthDip = diameter, HeightDip = diameter },
            LauncherOpacity = Math.Clamp(Math.Round(settings.LauncherOpacity + opacityDelta, 2), .65, 1) };
        var rect = Bounds(launcherHwnd);
        double scale = Scale(launcherHwnd);
        rect = rect with { Width = diameter * scale, Height = diameter * scale };
        Apply(launcherHwnd, OverlayGeometry.Recover(rect, displays, true));
        launcher.SetBackgroundOpacity(settings.LauncherOpacity);
        keyboard.RefreshSettings(diameter, settings.LauncherOpacity);
        Save();
    }

    private void Save()
    {
        if (!started || disposed) return;
        settings = settings with { Launcher = Snapshot(launcherHwnd, settings.Launcher), Keyboard = Snapshot(keyboardHwnd, settings.Keyboard), Symbols = keyboard.Symbols };
        storage.Save(settings);
    }
    private static WindowSettings Snapshot(nint hwnd, WindowSettings fallback)
    {
        // WPF can destroy HWNDs before Application.OnExit; the last completed geometry remains valid.
        if (!NativeMethods.GetWindowRect(hwnd, out var native)) return fallback;
        var rect = native.Pixels;
        double scale = Scale(hwnd);
        return new(rect.X, rect.Y, rect.Width / scale, rect.Height / scale);
    }
    private nint Handle(Window window) => window == launcher ? launcherHwnd : keyboardHwnd;
    private static double Scale(nint hwnd) => Math.Max(96, NativeMethods.GetDpiForWindow(hwnd)) / 96d;
    private static PixelRect Bounds(nint hwnd) => NativeMethods.GetWindowRect(hwnd, out var rect) ? rect.Pixels
        : throw new InvalidOperationException("Overlay bounds unavailable.");
    private static void Apply(nint hwnd, PixelRect rect)
    {
        if (!NativeMethods.SetWindowPos(hwnd, 0, (int)Math.Round(rect.X), (int)Math.Round(rect.Y),
            (int)Math.Round(rect.Width), (int)Math.Round(rect.Height), 0x0010 | 0x0004))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    private void OrderLauncher()
    {
        if (!launcher.IsVisible) return;
        if (keyboardHwnd != 0 && keyboard.IsVisible)
        {
            // Raising an already-ordered topmost window can notify the keyboard again.
            // Check the real ordering to avoid an endless dispatcher notification loop.
            for (nint above = NativeMethods.GetWindow(keyboardHwnd, 3); above != 0; above = NativeMethods.GetWindow(above, 3))
                if (above == launcherHwnd) return; // GW_HWNDPREV
        }
        else if (started) return;
        if (launcherHwnd != 0 && !NativeMethods.SetWindowPos(launcherHwnd, -1, 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    internal void PrepareForSessionEnding()
    {
        ShutdownCoordinator.Try("launcher cancellation", launcher.Handle.Cancel, log.Write);
        ShutdownCoordinator.Try("keyboard geometry cancellation", keyboard.CancelGeometry, log.Write);
        ShutdownCoordinator.Try("keyboard input cancellation", keyboard.CancelInteractions, log.Write);
        Save();
    }
    public void Dispose()
    {
        if (disposed) return;
        ShutdownCoordinator.Try("launcher cancellation", launcher.Handle.Cancel, log.Write);
        ShutdownCoordinator.Try("keyboard geometry cancellation", keyboard.CancelGeometry, log.Write);
        ShutdownCoordinator.Try("keyboard input cancellation", keyboard.CancelInteractions, log.Write);
        ShutdownCoordinator.Try("settings exit save", Save, log.Write);
        disposed = true;
        foreach (var (source, hook) in hooks)
            ShutdownCoordinator.Try("window hook removal", () => { if (!source.IsDisposed) source.RemoveHook(hook); }, log.Write);
        hooks.Clear();
        launcher.Closing -= LauncherClosing;
        keyboard.DismissRequested -= HideKeyboard;
        keyboard.LayoutChanged -= Save;
        keyboard.AllowClose = true;
        ShutdownCoordinator.Try("keyboard close", keyboard.Close, log.Write);
        ShutdownCoordinator.Try("launcher close", launcher.Close, log.Write);
    }
}
