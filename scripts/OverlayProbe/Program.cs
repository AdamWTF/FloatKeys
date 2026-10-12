using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FloatKeys.Controls;
using FloatKeys.Input;
using FloatKeys.Platform;
using FloatKeys.Services;
using FloatKeys.Windows;

internal static class Program
{
    [DllImport("user32.dll")] private static extern nint SendMessageW(nint hwnd, int message, nint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int RegisterWindowMessageW(string message);
    [STAThread]
    private static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var backend = new NoInput();
        var directory = Path.GetFullPath("artifacts/overlay-probe");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings-" + Guid.NewGuid().ToString("N") + ".json");
        nint foreground = NativeMethods.GetForegroundWindow();
        using (var overlay = new OverlayService(backend, new AppLog(), new SettingsService(path)))
        {
            overlay.Show(); Pump();
            var launcher = Field<LauncherWindow>(overlay, "launcher");
            var keyboard = Field<KeyboardWindow>(overlay, "keyboard");
            var visibility = Field<OverlayVisibility>(overlay, "visibility");
            int shutdowns = 0;
            var lifetime = new ShutdownCoordinator(message => throw new Exception(message));
            lifetime.Add("probe shutdown", () => shutdowns++);
            var trayController = new TrayController(overlay, new StartupService(new ProbeStartupStore(), null, _ => { }),
                () => lifetime.Stopping, lifetime.Stop, message => throw new Exception(message));
            using var tray = new TrayService(trayController, message => throw new Exception(message));
            var notifyIcon = Field<System.Windows.Forms.NotifyIcon>(tray, "icon");
            Check(notifyIcon.Visible, "tray icon available");
            var trayWindow = Field<System.Windows.Forms.NativeWindow>(notifyIcon, "_window");
            nint trayHwnd = trayWindow.Handle;
            int explorerMessage = RegisterWindowMessageW("TaskbarCreated");
            Check(explorerMessage != 0, "Explorer restart message available");
            SendMessageW(trayHwnd, explorerMessage, 0, 0); Pump();
            Check(notifyIcon.Visible && trayWindow.Handle == trayHwnd && Field<bool>(notifyIcon, "_added"), "tray restored after simulated Explorer restart");
            nint lh = new WindowInteropHelper(launcher).Handle, kh = new WindowInteropHelper(keyboard).Handle;
            Check(launcher.IsVisible && !keyboard.IsVisible, "launcher-only startup");
            foreach (nint hwnd in new[] { lh, kh })
            {
                long style = NativeMethods.GetWindowLongPtr(hwnd, -20).ToInt64();
                Check((style & 0x08000088) == 0x08000088 && (style & 0x40000) == 0, "native styles");
                Check(SendMessageW(hwnd, WindowStyles.MouseActivate, 0, 0) == 3, "mouse nonactivation response");
                // WM_POINTERACTIVATE needs a genuine Windows pointer ID; its policy is covered by xUnit.
            }
            NativeMethods.GetWindowRect(kh, out var before);
            Gesture(launcher.Handle, new(0, 0), new(0, 0)); Pump();
            Check(keyboard.IsVisible, "launcher tap opens keyboard");
            Gesture(launcher.Handle, new(0, 0), new(0, 0)); Pump();
            Check(!keyboard.IsVisible && launcher.IsVisible, "launcher tap hides only keyboard");
            var savedTime = File.GetLastWriteTimeUtc(path);
            for (int i = 0; i < 50; i++)
            {
                visibility.Toggle(); Pump();
                Check(keyboard.IsVisible && launcher.IsVisible, "show visibility");
                Check(new WindowInteropHelper(keyboard).Handle == kh, "same keyboard HWND");
                Check(NativeMethods.GetForegroundWindow() == foreground, "show foreground preservation");
                visibility.Toggle(); Pump();
                Check(!keyboard.IsVisible && launcher.IsVisible, "hide visibility");
                Check(NativeMethods.GetForegroundWindow() == foreground, "hide foreground preservation");
            }
            NativeMethods.GetWindowRect(kh, out var after);
            Check(before.Pixels == after.Pixels, "bounds survive show/hide");
            Check(File.GetLastWriteTimeUtc(path) == savedTime, "show/hide avoids settings writes");
            Check(ReferenceEquals(notifyIcon, Field<System.Windows.Forms.NotifyIcon>(tray, "icon")), "one tray object across visibility cycles");
            overlay.ShowKeyboard(); Pump();
            keyboard.Close(); Pump();
            Check(!keyboard.IsVisible && launcher.IsVisible, "keyboard native close hides only keyboard");
            launcher.Close(); Pump();
            Check(!keyboard.IsVisible && !launcher.IsVisible, "both native windows can hide without shutdown");
            Check(notifyIcon.Visible && !lifetime.Stopping, "tray remains available with both overlays hidden");
            var trayItems = Field<Dictionary<TrayCommand, System.Windows.Forms.ToolStripMenuItem>>(tray, "items");
            trayItems[TrayCommand.Settings].PerformClick(); Pump();
            Check(keyboard.IsVisible && !launcher.IsVisible && !((Grid)keyboard.FindName("KeyRows")).IsEnabled,
                "tray settings restores hidden keyboard and opens panel");
            overlay.HideKeyboard();
            overlay.ShowKeyboard(); overlay.ShowLauncher(); Pump();
            Check(new WindowInteropHelper(keyboard).Handle == kh && new WindowInteropHelper(launcher).Handle == lh, "close-hide preserves HWNDs");
            Check(NativeMethods.GetForegroundWindow() == foreground, "restore foreground preservation");
            overlay.SetStartupPreference(true);
            overlay.ShowSettings();
            Check(!((Grid)keyboard.FindName("KeyRows")).IsEnabled, "tray settings opens inline panel");
            Check(new SettingsService(path).Load().StartWithWindows, "startup preference shares geometry settings");
            overlay.HideKeyboard(); Pump();
            visibility.Show(); Pump();
            // Drive the service's pointer callbacks without moving the real cursor or injecting input.
            Gesture(launcher.Handle, new(0, 0), new(-20, 0));
            Gesture(keyboard.DragHandle, new(0, 0), new(-20, -20));
            Gesture(keyboard.ResizeHandle, new(0, 0), new(-10000, -10000));
            Pump();
            NativeMethods.GetWindowRect(kh, out var minimum);
            double dpi = NativeMethods.GetDpiForWindow(kh) / 96d;
            Check(Math.Abs(minimum.Pixels.Width / dpi - 720) < 1, "native minimum width");
            Check(Math.Abs(minimum.Pixels.Height / dpi - 360) < 1, "native minimum height");
            Check(NativeMethods.GetForegroundWindow() == foreground, "movement/resize foreground preservation");
            Check(IsAbove(lh, kh), "launcher remains above keyboard");
            var saved = new SettingsService(path).Load();
            Check(Math.Abs(saved.Keyboard.WidthDip - 720) < 1, "geometry saved on gesture completion");
            var header = (StackPanel)keyboard.FindName("HeaderKeys");
            Released((KeyboardKey)header.Children[1]);
            Check(!((Grid)keyboard.FindName("KeyRows")).IsEnabled, "settings disables covered keys");
            Released((KeyboardKey)((StackPanel)keyboard.FindName("SizeSettings")).Children[2]);
            Released((KeyboardKey)((StackPanel)keyboard.FindName("OpacitySettings")).Children[2]);
            saved = new SettingsService(path).Load();
            Check(Math.Abs(saved.Launcher.WidthDip - 64) < 1 && Math.Abs(saved.LauncherOpacity - .9) < .000001,
                $"settings step buttons persist: size={saved.Launcher.WidthDip:R} opacity={saved.LauncherOpacity:R}");
            Released((KeyboardKey)header.Children[1]);
            Check(((Grid)keyboard.FindName("KeyRows")).IsEnabled, "settings restores keys");
            Check(header.Children.OfType<KeyboardKey>().All(key => key.Label != "Exit"), "keyboard has no Exit control");
            Check(Equals(((KeyboardKey)header.Children[2]).ToolTip, "Minimise keyboard"), "minimise tooltip");
            var keyboardController = Field<KeyboardController>(keyboard, "controller");
            keyboardController.Begin(10, KeyboardLayout.Main[2][0]); keyboardController.End(10, true);
            keyboardController.Begin(11, KeyboardLayout.Main[3][1]); keyboardController.End(11, true);
            keyboardController.Begin(12, KeyboardLayout.Main[3][2]); keyboardController.End(12, true);
            backend.AllowChords = true; // Records calls only; never sends native input.
            var repeatKey = KeyboardLayout.Main.SelectMany(row => row).First(key => key.Repeat);
            Check(keyboardController.Begin(13, repeatKey), "active repeat before minimise");
            Check(keyboardController.Begin(14, KeyboardLayout.Main[0][0]), "pending touch before minimise");
            keyboardController.Begin(15, KeyboardLayout.Main[3][1]); keyboardController.End(15, true);
            int cleanupsBeforeMinimise = backend.Cleanups, pressesBeforeMinimise = backend.Presses;
            Released((KeyboardKey)header.Children[2]);
            Pump();
            Check(!keyboard.IsVisible && launcher.IsVisible && notifyIcon.Visible && !lifetime.Stopping,
                "header minimise leaves launcher and tray without shutdown");
            Check(keyboardController.InteractionCount == 0 && !keyboardController.Shift && !keyboardController.Ctrl && !keyboardController.Alt,
                "minimise clears pending touches, repeat and modifier latches");
            keyboardController.End(14, true); Pump();
            Check(backend.Cleanups > cleanupsBeforeMinimise && backend.Presses == pressesBeforeMinimise,
                "minimise releases input and cancelled touch cannot type");
            backend.AllowChords = false;
            Gesture(launcher.Handle, new(0, 0), new(0, 0)); Pump();
            Check(keyboard.IsVisible && new WindowInteropHelper(keyboard).Handle == kh, "launcher restores minimised keyboard");
            trayItems[TrayCommand.Exit].PerformClick();
            trayItems[TrayCommand.Exit].PerformClick();
            Check(lifetime.Stopping && shutdowns == 1, "tray Exit routes to application lifetime once");
            Console.WriteLine($"PASS lifecycle architecture={RuntimeInformation.ProcessArchitecture} cycles=50 closeHides=true sameHWND=true foregroundPreserved=true cleanups={backend.Cleanups}");
        }
        Pump();
        Check(NativeMethods.GetForegroundWindow() == foreground, "shutdown foreground preservation");
        Check(new SettingsService(path).Load().StartWithWindows, "shutdown preserves startup preference in final settings snapshot");
        File.Delete(path);

        foreach (bool symbols in new[] { false, true })
        foreach (var size in new[] { new Size(840, 360), new Size(720, 360), new Size(1000, 480) })
        {
            var window = new KeyboardWindow(backend, new AppLog(), symbols);
            Check(((StackPanel)window.FindName("HeaderKeys")).Children.OfType<KeyboardKey>().All(key => key.Label != "Exit"),
                "neither keyboard layout exposes Exit");
            var root = (FrameworkElement)window.Content;
            root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
            var keys = Descendants(root).OfType<KeyboardKey>().ToArray();
            foreach (var key in keys)
            {
                var bounds = key.TransformToAncestor(root).TransformBounds(new Rect(0, 0, key.ActualWidth, key.ActualHeight));
                Check(key.ActualWidth >= 44 && key.ActualHeight >= 44 && bounds.Right <= size.Width && bounds.Bottom <= size.Height, $"key bounds {key.Label}: {bounds}");
            }
            foreach (int dpi in new[] { 96, 144, 192 }) Render(root, size, dpi, Path.Combine(directory, $"{(symbols ? "symbols" : "qwerty")}-{size.Width}-{dpi}.png"));
            Console.WriteLine($"PASS render layout={(symbols ? "symbols" : "qwerty")} size={size} minKey={keys.Min(k => k.ActualWidth):F1}x{keys.Min(k => k.ActualHeight):F1} dpi=96,144,192");
            window.Close();
        }
        var settingsWindow = new KeyboardWindow(backend, new AppLog());
        var panel = (UIElement)settingsWindow.FindName("SettingsPanel");
        panel.Visibility = Visibility.Visible;
        var settingsRoot = (FrameworkElement)settingsWindow.Content;
        settingsRoot.Measure(new(720, 360)); settingsRoot.Arrange(new Rect(0, 0, 720, 360)); settingsRoot.UpdateLayout();
        Render(settingsRoot, new(720, 360), 192, Path.Combine(directory, "settings.png"));
        settingsWindow.Close();
        var small = new KeyboardWindow(backend, new AppLog());
        var smallRoot = (FrameworkElement)small.Content;
        smallRoot.Measure(new(600, 300)); smallRoot.Arrange(new Rect(0, 0, 600, 300)); smallRoot.UpdateLayout();
        var scroll = Descendants(smallRoot).OfType<ScrollViewer>().First();
        Check(scroll.ScrollableWidth > 0 && scroll.ScrollableHeight > 0, "small display retains scrollable content");
        Render(smallRoot, new(600, 300), 192, Path.Combine(directory, "small-display.png"));
        small.Close();
        var icon = new LauncherWindow();
        var iconRoot = (FrameworkElement)icon.Content;
        iconRoot.Measure(new(56, 56)); iconRoot.Arrange(new Rect(0, 0, 56, 56)); iconRoot.UpdateLayout();
        Render(iconRoot, new(56, 56), 192, Path.Combine(directory, "launcher.png"));
        icon.Close();
        app.Shutdown();
    }
    private static void Gesture(PointerHandle handle, Point start, Point end)
    {
        Field<Action<Point>>(handle, "Started")(start);
        Field<Action<Point>>(handle, "Moved")(end);
        Field<Action<bool, bool>>(handle, "Completed")(true, false);
    }
    private static void Released(KeyboardKey key) => Field<Action<long, bool>>(key, "Released")(1, true);
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
    private static void Check(bool success, string message) { if (!success) throw new Exception(message); }
    private static bool IsAbove(nint launcher, nint keyboard)
    {
        for (nint above = NativeMethods.GetWindow(keyboard, 3); above != 0; above = NativeMethods.GetWindow(above, 3))
            if (above == launcher) return true;
        return false;
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is UIElement e && e.Visibility == Visibility.Collapsed) continue;
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Render(Visual root, Size size, int dpi, string path)
    {
        var image = new RenderTargetBitmap((int)(size.Width * dpi / 96), (int)(size.Height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        image.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private sealed class NoInput : IInputBackend
    {
        internal int Cleanups;
        internal int Presses;
        internal bool AllowChords;
        public InputResult KeyPressChord(InputKey key, IReadOnlyList<InputKey> modifiers)
        {
            if (!AllowChords) throw new Exception("Unexpected probe input call.");
            Presses++;
            return new(0, 0, 0, false, null);
        }
        public InputResult KeyDown(InputKey key) => throw new Exception();
        public InputResult KeyUp(InputKey key) => throw new Exception();
        public InputResult KeyPress(InputKey key) => throw new Exception();
        public InputResult SendUnicodeText(string text) => throw new Exception();
        public InputResult ReleaseAllKeys() { Cleanups++; return new(0, 0, 0, false, null); }
        public void Dispose() { }
    }
    private sealed class ProbeStartupStore : IStartupStore
    {
        public string? Read() => null;
        public void Write(string command) => throw new Exception("Probe cannot modify startup registration.");
        public void Remove() => throw new Exception("Probe cannot modify startup registration.");
    }
}
