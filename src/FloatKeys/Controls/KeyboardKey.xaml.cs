using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FloatKeys.Input;

namespace FloatKeys.Controls;

public partial class KeyboardKey : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(KeyboardKey), new PropertyMetadata(""));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    internal event Action<long>? Captured;
    internal event Action<long, bool>? Released;
    internal event Action<long>? Cancelled;
    private readonly PointerSession pointer = new();
    private TouchDevice? touch;
    private bool mouse;
    private bool active;
    private KeyStyle style;
    private static readonly Brush Normal = Brush(52, 59, 72);
    private static readonly Brush Special = Brush(44, 52, 66);
    private static readonly Brush Modifier = Brush(58, 51, 76);
    private static readonly Brush Enter = Brush(36, 79, 75);
    private static readonly Brush Slash = Brush(43, 66, 83);
    private static readonly Brush Pressed = Brush(69, 106, 153);
    private static readonly Brush Active = Brush(54, 112, 158);
    private static Brush Brush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
    internal void SetAppearance(KeyStyle category, bool isActive)
    {
        style = category;
        active = isActive;
        Refresh();
    }
    private void Refresh() => Surface.Background = pointer.Owner is not null ? Pressed : active ? Active : style switch
    {
        KeyStyle.Modifier => Modifier, KeyStyle.Special => Special, KeyStyle.Enter => Enter, KeyStyle.Slash => Slash, _ => Normal
    };

    public KeyboardKey()
    {
        InitializeComponent();
        PreviewTouchDown += TouchDownHandler;
        PreviewTouchMove += TouchMoveHandler;
        PreviewTouchUp += TouchUpHandler;
        LostTouchCapture += (_, e) => { if (touch?.Id == e.TouchDevice.Id) Cancel(); };
        PreviewMouseDown += MouseDownHandler;
        PreviewMouseMove += MouseMoveHandler;
        PreviewMouseUp += MouseUpHandler;
        LostMouseCapture += (_, _) => { if (mouse) Cancel(); };
        Unloaded += (_, _) => Cancel();
    }

    private bool Inside(Point p) => p.X >= 0 && p.Y >= 0 && p.X <= ActualWidth && p.Y <= ActualHeight;
    private static long TouchId(TouchDevice device) => (long)device.Id + 1;

    private void TouchDownHandler(object? sender, TouchEventArgs e)
    {
        e.Handled = true;
        long id = TouchId(e.TouchDevice);
        if (!pointer.Begin(id)) return;
        touch = e.TouchDevice;
        Refresh();
        if (!touch.Capture(this)) { Cancel(); return; }
        if (pointer.Owns(id)) Captured?.Invoke(id);
    }
    private void TouchMoveHandler(object? sender, TouchEventArgs e)
    {
        e.Handled = true;
        if (pointer.Owns(TouchId(e.TouchDevice)) && !Inside(e.GetTouchPoint(this).Position)) Cancel();
    }
    private void TouchUpHandler(object? sender, TouchEventArgs e)
    {
        e.Handled = true;
        long id = TouchId(e.TouchDevice);
        if (!pointer.Owns(id)) return;
        bool inside = Inside(e.GetTouchPoint(this).Position);
        var oldTouch = touch;
        touch = null;
        pointer.Clear(); // Clear before releasing capture; lost capture is reentrant.
        Refresh();
        oldTouch?.Capture(null);
        Released?.Invoke(id, inside);
    }
    private void MouseDownHandler(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ChangedButton != MouseButton.Left || !pointer.Begin(-1, e.StylusDevice is not null)) return;
        mouse = true;
        Refresh();
        if (!CaptureMouse()) { Cancel(); return; }
        if (pointer.Owns(-1)) Captured?.Invoke(-1);
    }
    private void MouseMoveHandler(object sender, MouseEventArgs e)
    {
        if (!pointer.Owns(-1, e.StylusDevice is not null)) return;
        if (e.LeftButton != MouseButtonState.Pressed || !Inside(e.GetPosition(this))) Cancel();
    }
    private void MouseUpHandler(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ChangedButton != MouseButton.Left || !pointer.Owns(-1, e.StylusDevice is not null)) return;
        bool inside = Inside(e.GetPosition(this));
        mouse = false;
        pointer.Clear();
        Refresh();
        ReleaseMouseCapture();
        Released?.Invoke(-1, inside);
    }
    internal void Cancel()
    {
        long? oldPointer = pointer.Clear();
        var oldTouch = touch;
        touch = null;
        bool oldMouse = mouse;
        mouse = false;
        Refresh();
        oldTouch?.Capture(null);
        if (oldMouse) ReleaseMouseCapture();
        if (oldPointer is long id) Cancelled?.Invoke(id);
    }
}
