using FloatKeys.Input;
using FloatKeys.Platform;
using Xunit;

namespace FloatKeys.Tests;

public sealed class OverlayPolicyTests
{
    [Fact]
    public void NonActivatingStylesPreserveUnrelatedFlagsAndRemoveAppWindow()
    {
        const long unrelated = 0x00080000; // WS_EX_LAYERED, if WPF supplies it.
        long styles = WindowStyles.NonActivatingStyle(unrelated | WindowStyles.AppWindow);
        Assert.Equal(unrelated | WindowStyles.NoActivate | WindowStyles.ToolWindow, styles);
    }

    [Theory]
    [InlineData(WindowStyles.MouseActivate)]
    [InlineData(WindowStyles.PointerActivate)]
    public void ActivationPolicyPreservesPointerDeliveryWithoutActivation(int message) =>
        Assert.Equal((nint)3, WindowStyles.ActivationResponse(message));

    [Fact]
    public void UnrelatedMessagesAreNotHandled() => Assert.Null(WindowStyles.ActivationResponse(0x0201));

    [Fact]
    public void LongHoldCommitsOnlyOnReleaseAndOnlyOnce()
    {
        var tap = new TapGesture();
        Assert.True(tap.Begin());
        Assert.False(tap.Begin());
        for (int i = 0; i < 10; i++) tap.Move(true);
        Assert.True(tap.End(true));
        Assert.False(tap.End(true));
    }

    [Fact]
    public void LeavingAndReturningStillCancelsTap()
    {
        var tap = new TapGesture();
        tap.Begin();
        tap.Move(false);
        tap.Move(true);
        Assert.False(tap.End(true));
    }

    [Fact]
    public void CaptureLossCancelsAndNextTapCanSucceed()
    {
        var tap = new TapGesture();
        tap.Begin();
        tap.Cancel();
        Assert.False(tap.IsPressed);
        Assert.False(tap.End(true));
        tap.Begin();
        Assert.True(tap.End(true));
    }

    [Fact]
    public void ReleaseOutsideDoesNotCommit()
    {
        var tap = new TapGesture();
        tap.Begin();
        Assert.False(tap.End(false));
    }
}
