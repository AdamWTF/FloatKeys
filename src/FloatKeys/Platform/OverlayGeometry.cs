using FloatKeys.Models;
using FloatKeys.Input;

namespace FloatKeys.Platform;

internal readonly record struct PixelRect(double X, double Y, double Width, double Height)
{
    internal double Right => X + Width;
    internal double Bottom => Y + Height;
    internal double Intersection(PixelRect b) => Math.Max(0, Math.Min(Right, b.Right) - Math.Max(X, b.X)) * Math.Max(0, Math.Min(Bottom, b.Bottom) - Math.Max(Y, b.Y));
}
internal sealed record DisplayArea(PixelRect Bounds, PixelRect Work, double Scale, bool Primary);

internal static class OverlayGeometry
{
    internal static DisplayArea Nearest(PixelRect rect, IReadOnlyList<DisplayArea> displays) => displays
        .OrderByDescending(d => rect.Intersection(d.Bounds))
        .ThenBy(d => Math.Pow(Math.Max(d.Bounds.X - rect.Right, Math.Max(rect.X - d.Bounds.Right, 0)), 2)
            + Math.Pow(Math.Max(d.Bounds.Y - rect.Bottom, Math.Max(rect.Y - d.Bounds.Bottom, 0)), 2)).First();

    internal static PixelRect Restore(WindowSettings settings, IReadOnlyList<DisplayArea> displays, bool launcher)
    {
        var primary = displays.FirstOrDefault(d => d.Primary) ?? displays[0];
        bool placed = settings.XPhysical.HasValue && settings.YPhysical.HasValue;
        var provisional = new PixelRect(settings.XPhysical ?? primary.Work.Right, settings.YPhysical ?? primary.Work.Bottom, 1, 1);
        var display = placed ? Nearest(provisional, displays) : primary;
        double w = settings.WidthDip * display.Scale, h = settings.HeightDip * display.Scale;
        var rect = placed ? new PixelRect(settings.XPhysical!.Value, settings.YPhysical!.Value, w, h)
            : new PixelRect(display.Work.Right - w - 20 * display.Scale, launcher ? display.Work.Y + display.Work.Height / 2 - h / 2 : display.Work.Bottom - h - 20 * display.Scale, w, h);
        return Recover(rect, displays, launcher);
    }
    internal static PixelRect Recover(PixelRect rect, IReadOnlyList<DisplayArea> displays, bool launcher)
    {
        var d = Nearest(rect, displays);
        bool fits = launcher ? rect.X >= d.Bounds.X && rect.Y >= d.Bounds.Y && rect.Right <= d.Bounds.Right && rect.Bottom <= d.Bounds.Bottom
            : rect.X >= d.Bounds.X && rect.Right <= d.Bounds.Right && rect.Y >= d.Bounds.Y && rect.Bottom <= d.Bounds.Bottom;
        return fits ? rect : Fit(rect, d.Work);
    }
    internal static PixelRect Fit(PixelRect rect, PixelRect area)
    {
        double w = Math.Min(rect.Width, area.Width), h = Math.Min(rect.Height, area.Height);
        return new(Math.Clamp(rect.X, area.X, area.Right - w), Math.Clamp(rect.Y, area.Y, area.Bottom - h), w, h);
    }
    internal static PixelRect EnsureKeyboardSize(PixelRect rect, double scale, PrimaryKeyboardLayout layout, bool symbols, PixelRect area) => Fit(rect with
    {
        Width = Math.Max(rect.Width, KeyboardSizing.MinimumWidth * scale),
        Height = Math.Max(rect.Height, KeyboardSizing.MinimumHeight(layout, symbols) * scale)
    }, area);

    internal static PixelRect Resize(PixelRect start, double dx, double dy, double scale, PixelRect area,
        PrimaryKeyboardLayout layout = PrimaryKeyboardLayout.Chat, bool symbols = false) => Fit(start with
    {
        Width = Math.Max(KeyboardSizing.MinimumWidth * scale, Math.Min(start.Width + dx, area.Right - start.X)),
        Height = Math.Max(KeyboardSizing.MinimumHeight(layout, symbols) * scale, Math.Min(start.Height + dy, area.Bottom - start.Y))
    }, area);
}
