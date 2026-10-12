using FloatKeys.Input;
using FloatKeys.Models;
using FloatKeys.Platform;
using FloatKeys.Services;
using Xunit;

namespace FloatKeys.Tests;

public sealed class PolishedOverlayTests
{
    private static DisplayArea Display(double x = 0, double y = 0, double w = 1920, double h = 1080, double scale = 1, bool primary = true)
        => new(new(x, y, w, h), new(x, y, w, h - 40), scale, primary);

    [Theory]
    [InlineData(7.99, true)] [InlineData(8, false)] [InlineData(30, false)]
    public void LauncherThresholdIsMeasuredInDipsAndIncludesRelease(double distance, bool tap)
    {
        var gesture = new MoveGesture(); gesture.Begin(100, 200, 2);
        gesture.Move(100 + distance * 2, 200);
        Assert.Equal(tap, gesture.End(true));
        Assert.False(gesture.End(true));
    }
    [Fact]
    public void DragDoesNotBecomeTapWhenReturningToOrigin()
    {
        var gesture = new MoveGesture(); gesture.Begin(0, 0, 1.5);
        gesture.Move(20, 20); gesture.Move(0, 0);
        Assert.False(gesture.End(true));
        gesture.Begin(0, 0, 1); Assert.True(gesture.End(true));
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, true)]
    public void CancelledOrOutsideReleaseNeverToggles(bool inside, bool cancelled)
    {
        var gesture = new MoveGesture(); gesture.Begin(0, 0, 1);
        Assert.False(gesture.End(inside, cancelled));
    }
    [Theory]
    [InlineData(1)] [InlineData(1.5)] [InlineData(2)]
    public void LauncherDefaultsUseWorkAreaAndDpi(double scale)
    {
        var d = Display(w: 2880, h: 1920, scale: scale);
        var rect = OverlayGeometry.Restore(new(WidthDip: 56, HeightDip: 56), [d], true);
        Assert.Equal(56 * scale, rect.Width);
        Assert.Equal(d.Work.Right - 76 * scale, rect.X);
        Assert.Equal(rect.Width * rect.Height, rect.Intersection(d.Work));
    }
    [Fact]
    public void LauncherCannotLeaveMonitorOrBePlacedInMonitorGap()
    {
        var left = Display(-1920, 0, primary: false); var right = Display(500, 0);
        var rect = OverlayGeometry.Recover(new(200, -500, 56, 56), [left, right], true);
        Assert.Equal(-56, rect.X); Assert.Equal(0, rect.Y);
        Assert.Equal(56 * 56, rect.Intersection(left.Work));
    }
    [Fact]
    public void ValidNegativeAndFullscreenPositionsArePreserved()
    {
        var rect = new PixelRect(-900, 720, 840, 360);
        var d = Display(-1920, 0);
        Assert.Equal(rect, OverlayGeometry.Recover(rect, [d], false));
    }
    [Theory]
    [InlineData(1280, 720)] [InlineData(960, 1440)] [InlineData(600, 300)]
    public void ResolutionAndOrientationRecoveryFitsAvailableWorkArea(double w, double h)
    {
        var d = Display(w: w, h: h);
        var rect = OverlayGeometry.Recover(new(2400, 1300, 840, 360), [d], false);
        Assert.Equal(rect.Width * rect.Height, rect.Intersection(d.Work));
        Assert.True(rect.Width <= w); Assert.True(rect.Height <= h - 40);
    }
    [Fact]
    public void DisconnectedMonitorRecoversToRemainingDisplay()
    {
        var d = Display();
        var rect = OverlayGeometry.Restore(new(-1800, 100, 840, 360), [d], false);
        Assert.Equal(0, rect.X); Assert.Equal(100, rect.Y);
    }
    [Fact]
    public void MixedDpiRestoreUsesTargetMonitorWithoutScalingDesktopOrigin()
    {
        var rect = OverlayGeometry.Restore(new(-1700, 60, 720, 360),
            [Display(), Display(-1920, 0, scale: 2, primary: false)], false);
        Assert.Equal(-1700, rect.X); Assert.Equal(60, rect.Y);
        Assert.Equal(1440, rect.Width); Assert.Equal(720, rect.Height);
    }
    [Theory]
    [InlineData(1)] [InlineData(1.5)] [InlineData(2)]
    public void ResizeEnforcesMinimumTouchDimensions(double scale)
    {
        var result = OverlayGeometry.Resize(new(0, 0, 840 * scale, 400 * scale), -1000, -1000, scale, new(0, 0, 4000, 3000));
        Assert.Equal(720 * scale, result.Width); Assert.Equal(432 * scale, result.Height);
    }
    [Fact]
    public void ResizeAtDisplayEdgeKeepsTopLeftAnchor()
    {
        var start = new PixelRect(800, 300, 840, 400);
        var resized = OverlayGeometry.Resize(start, 500, 500, 1, new(0, 0, 1920, 1080));
        Assert.Equal(800, resized.X); Assert.Equal(300, resized.Y);
        Assert.Equal(1120, resized.Width); Assert.Equal(780, resized.Height);
    }
    [Fact]
    public void ResizeFitsSmallDisplayWithoutShrinkingContentPolicy()
    {
        var result = OverlayGeometry.Resize(new(0, 0, 720, 360), 0, 0, 1, new(0, 0, 600, 300));
        Assert.Equal(new PixelRect(0, 0, 600, 300), result);
    }

    [Theory]
    [InlineData(false, false, 432)] [InlineData(true, false, 382)] [InlineData(false, true, 382)]
    [InlineData(true, true, 382)]
    public void MinimumHeightTracksVisiblePage(bool compact, bool symbols, double height)
    {
        var layout = compact ? PrimaryKeyboardLayout.Compact : PrimaryKeyboardLayout.Chat;
        foreach (double scale in new[] { 1d, 1.5, 2 })
        {
            var rect = OverlayGeometry.Resize(new(100, 50, 840 * scale, 480 * scale), -5000, -5000,
                scale, new(0, 0, 4000, 3000), layout, symbols);
            Assert.Equal(720 * scale, rect.Width); Assert.Equal(height * scale, rect.Height);
            Assert.Equal(100, rect.X); Assert.Equal(50, rect.Y);
        }
    }

    [Fact]
    public void LayoutGrowthPreservesPositionAndDoesNotShrinkExistingSize()
    {
        var area = new PixelRect(0, 0, 1920, 2880);
        var compact = new PixelRect(100, 120, 720, 382);
        var chat = OverlayGeometry.EnsureKeyboardSize(compact, 1, PrimaryKeyboardLayout.Chat, false, area);
        Assert.Equal(new PixelRect(100, 120, 720, 432), chat);
        Assert.Equal(chat, OverlayGeometry.EnsureKeyboardSize(chat, 1, PrimaryKeyboardLayout.Compact, false, area));
        Assert.Equal(new PixelRect(1200, 2448, 720, 432),
            OverlayGeometry.EnsureKeyboardSize(new(1400, 2600, 720, 382), 1, PrimaryKeyboardLayout.Chat, false, area));
    }
    [Fact]
    public void SettingsValidationRecoversFieldsIndependently()
    {
        var settings = new AppSettings { Launcher = new(double.NaN, 200, double.PositiveInfinity, 10),
            Keyboard = new(-500, double.PositiveInfinity, 20, 900), LauncherOpacity = double.NaN, Theme = "unknown", Layout = "us", Symbols = true }.Validate();
        Assert.Null(settings.Launcher.XPhysical); Assert.Equal(200, settings.Launcher.YPhysical);
        Assert.Equal(56, settings.Launcher.WidthDip); Assert.Equal(56, settings.Launcher.HeightDip);
        Assert.Equal(-500, settings.Keyboard.XPhysical); Assert.Null(settings.Keyboard.YPhysical);
        Assert.Equal(720, settings.Keyboard.WidthDip); Assert.Equal(900, settings.Keyboard.HeightDip);
        Assert.Equal(.85, settings.LauncherOpacity); Assert.True(settings.Symbols);
        Assert.Equal("uk", settings.Layout); Assert.Equal("dark", settings.Theme);
    }
    [Fact]
    public void VisibilityCleanupPrecedesHideAndDuplicateOperationsDoNothing()
    {
        var calls = new List<string>();
        var state = new OverlayVisibility(() => calls.Add("show"), () => calls.Add("cleanup"), () => calls.Add("hide"));
        Assert.False(state.Visible); state.Hide(); state.Show(); state.Show(); state.Toggle(); state.Hide();
        Assert.False(state.Visible); Assert.Equal(new[] { "show", "cleanup", "hide" }, calls);
        state.Toggle(); Assert.True(state.Visible);
    }
}
