using System.Windows;
using System.Windows.Input;
using FloatKeys.Input;

namespace FloatKeys.Controls;

// Captures one initiating device. Coordinates are physical screen pixels, not moving local coordinates.
internal sealed class PointerHandle
{
    private readonly FrameworkElement element;
    private readonly PointerSession session = new();
    private TouchDevice? touch;
    private bool mouse;
    internal bool Active => session.Owner is not null;
    internal event Action<Point>? Started;
    internal event Action<Point>? Moved;
    internal event Action<bool, bool>? Completed;
    internal PointerHandle(FrameworkElement element)
    {
        this.element = element;
        element.PreviewTouchDown += (_, e) =>
        {
            e.Handled = true;
            if (!session.Begin(e.TouchDevice.Id + 1L)) return;
            touch = e.TouchDevice;
            if (!touch.Capture(element)) { Cancel(); return; }
            Started?.Invoke(element.PointToScreen(e.GetTouchPoint(element).Position));
        };
        element.PreviewTouchMove += (_, e) =>
        {
            e.Handled = true;
            if (session.Owns(e.TouchDevice.Id + 1L)) Moved?.Invoke(element.PointToScreen(e.GetTouchPoint(element).Position));
        };
        element.PreviewTouchUp += (_, e) =>
        {
            e.Handled = true;
            if (session.Owns(e.TouchDevice.Id + 1L))
            {
                Moved?.Invoke(element.PointToScreen(e.GetTouchPoint(element).Position));
                Complete(Inside(e.GetTouchPoint(element).Position), false);
            }
        };
        element.LostTouchCapture += (_, e) => { if (touch?.Id == e.TouchDevice.Id) Cancel(); };
        element.PreviewMouseDown += (_, e) =>
        {
            e.Handled = true;
            if (e.ChangedButton != MouseButton.Left || !session.Begin(-1, e.StylusDevice is not null)) return;
            mouse = true;
            if (!element.CaptureMouse()) { Cancel(); return; }
            Started?.Invoke(element.PointToScreen(e.GetPosition(element)));
        };
        element.PreviewMouseMove += (_, e) =>
        {
            if (!session.Owns(-1, e.StylusDevice is not null)) return;
            if (e.LeftButton != MouseButtonState.Pressed) { Cancel(); return; }
            Moved?.Invoke(element.PointToScreen(e.GetPosition(element)));
        };
        element.PreviewMouseUp += (_, e) =>
        {
            e.Handled = true;
            if (e.ChangedButton == MouseButton.Left && session.Owns(-1, e.StylusDevice is not null))
            {
                Moved?.Invoke(element.PointToScreen(e.GetPosition(element)));
                Complete(Inside(e.GetPosition(element)), false);
            }
        };
        element.LostMouseCapture += (_, _) => { if (mouse) Cancel(); };
        element.Unloaded += (_, _) => Cancel();
    }
    private bool Inside(Point p) => p.X >= 0 && p.Y >= 0 && p.X <= element.ActualWidth && p.Y <= element.ActualHeight;
    internal void Cancel() => Complete(false, true);
    private void Complete(bool inside, bool cancelled)
    {
        if (session.Clear() is null) return;
        var oldTouch = touch; bool oldMouse = mouse;
        touch = null; mouse = false;
        oldTouch?.Capture(null);
        if (oldMouse) element.ReleaseMouseCapture();
        Completed?.Invoke(inside, cancelled);
    }
}
