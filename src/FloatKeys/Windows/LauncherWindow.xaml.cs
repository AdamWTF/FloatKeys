using System.Windows;
using System.Windows.Media;
using FloatKeys.Controls;

namespace FloatKeys.Windows;

public partial class LauncherWindow : Window
{
    internal PointerHandle Handle { get; }
    public LauncherWindow()
    {
        InitializeComponent();
        Handle = new(TouchSurface);
        Handle.Started += _ => Surface.Fill = Pressed;
        Handle.Completed += (_, _) => Surface.Fill = Normal;
    }
    private static readonly Brush Normal = Frozen("#20252E");
    private static readonly Brush Pressed = Frozen("#456A99");
    private static Brush Frozen(string color) { var b = (Brush)new BrushConverter().ConvertFromString(color)!; b.Freeze(); return b; }
    internal void SetBackgroundOpacity(double opacity) => Surface.Opacity = opacity;
}
