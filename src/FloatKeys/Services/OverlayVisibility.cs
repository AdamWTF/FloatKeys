namespace FloatKeys.Services;

internal sealed class OverlayVisibility(Action show, Action cleanup, Action hide)
{
    internal bool Visible { get; private set; }
    internal void Toggle() { if (Visible) Hide(); else Show(); }
    internal void Show() { if (Visible) return; show(); Visible = true; }
    internal void Hide() { if (!Visible) return; cleanup(); hide(); Visible = false; }
}
