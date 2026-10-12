namespace FloatKeys.Input;

internal sealed class MoveGesture
{
    internal bool Dragging { get; private set; }
    private double x, y, scale;
    private bool active;
    internal void Begin(double x, double y, double scale)
    {
        this.x = x; this.y = y; this.scale = scale;
        active = true; Dragging = false;
    }
    internal (double X, double Y) Move(double currentX, double currentY)
    {
        double dx = currentX - x, dy = currentY - y;
        if (active && dx * dx + dy * dy >= 64 * scale * scale) Dragging = true;
        return (dx, dy);
    }
    internal bool End(bool inside, bool cancelled = false)
    {
        bool tap = active && !Dragging && inside && !cancelled;
        active = false;
        return tap;
    }
}
