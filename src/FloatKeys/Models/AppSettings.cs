namespace FloatKeys.Models;

internal sealed record WindowSettings(double? XPhysical = null, double? YPhysical = null,
    double WidthDip = 840, double HeightDip = 360);

internal sealed record AppSettings
{
    public WindowSettings Launcher { get; init; } = new(WidthDip: 56, HeightDip: 56);
    public WindowSettings Keyboard { get; init; } = new();
    public double LauncherOpacity { get; init; } = .85;
    public string Layout { get; init; } = "uk";
    public bool Symbols { get; init; }
    public bool StartWithWindows { get; init; }
    public string Theme { get; init; } = "dark";

    internal AppSettings Validate()
    {
        static double Size(double value, double fallback, double min, double max) =>
            double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
        static double? Position(double? value) => value is double v && double.IsFinite(v) && Math.Abs(v) < 1_000_000 ? v : null;
        var launcher = Launcher ?? new(WidthDip: 56, HeightDip: 56);
        var keyboard = Keyboard ?? new();
        double diameter = Size(launcher.WidthDip, 56, 48, 80);
        return this with
        {
            Launcher = launcher with { XPhysical = Position(launcher.XPhysical), YPhysical = Position(launcher.YPhysical), WidthDip = diameter, HeightDip = diameter },
            Keyboard = keyboard with { XPhysical = Position(keyboard.XPhysical), YPhysical = Position(keyboard.YPhysical),
                WidthDip = Size(keyboard.WidthDip, 840, 720, 4096), HeightDip = Size(keyboard.HeightDip, 360, 360, 2160) },
            LauncherOpacity = Size(LauncherOpacity, .85, .65, 1), Layout = "uk", Theme = "dark"
        };
    }
}
