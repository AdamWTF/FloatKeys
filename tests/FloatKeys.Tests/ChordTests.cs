using FloatKeys.Input;
using FloatKeys.Platform;
using Xunit;

namespace FloatKeys.Tests;

public sealed class ChordTests
{
    private sealed class Transport : IInputTransport
    {
        internal readonly List<NativeMethods.Input[]> Calls = [];
        internal readonly HashSet<InputKey> External = [];
        internal readonly Queue<Submission> Results = new();
        internal bool ThrowInspection;
        internal bool ThrowSend;
        public bool IsKeyDown(InputKey key) => ThrowInspection ? throw new InvalidOperationException() : External.Contains(key);
        public Submission Send(NativeMethods.Input[] events)
        {
            Calls.Add(events);
            if (ThrowSend) { ThrowSend = false; throw new InvalidOperationException(); }
            return Results.TryDequeue(out var result) ? result : new(events.Length, 0);
        }
    }
    private static readonly InputKey[] Modifiers = [KeyboardLayout.CtrlKey, KeyboardLayout.AltKey, KeyboardLayout.ShiftKey];
    [Fact]
    public void ChordIsOneOrderedBatchWithReverseModifierReleases()
    {
        var transport = new Transport(); using var backend = new SendInputBackend(transport);
        Assert.True(backend.KeyPressChord(PrototypeKeys.A, Modifiers).Success);
        var events = Assert.Single(transport.Calls);
        Assert.Equal(new ushort[] { 0x1D,0x38,0x2A,0x1E,0x1E,0x2A,0x38,0x1D }, events.Select(e => e.Data.Keyboard.Scan));
        Assert.Equal(new uint[] { 8,8,8,8,10,10,10,10 }, events.Select(e => e.Data.Keyboard.Flags));
        Assert.Equal(0, backend.HeldCount);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void EveryPartialPrefixReleasesOnlyOwnedKeys(int accepted)
    {
        var transport = new Transport(); transport.Results.Enqueue(new(accepted, 5));
        using var backend = new SendInputBackend(transport);
        var result = backend.KeyPressChord(PrototypeKeys.A, Modifiers);
        Assert.False(result.Success); Assert.Equal(accepted, result.Submitted);
        Assert.False(result.CleanupFailed); Assert.Equal(0, backend.HeldCount);
        var held = new HashSet<ushort>();
        foreach (var e in transport.Calls[0].Take(accepted))
        {
            if ((e.Data.Keyboard.Flags & 2) == 0) held.Add(e.Data.Keyboard.Scan);
            else held.Remove(e.Data.Keyboard.Scan);
        }
        if (held.Count == 0) Assert.Single(transport.Calls);
        else
        {
            Assert.Equal(2, transport.Calls.Count);
            Assert.All(transport.Calls[1], e => Assert.Equal(10u, e.Data.Keyboard.Flags));
            Assert.Equal(held.Order(), transport.Calls[1].Select(e => e.Data.Keyboard.Scan).Order());
        }
    }
    [Fact]
    public void BorrowedModifiersAreNeverReleasedEvenOnFailure()
    {
        var transport = new Transport(); transport.External.Add(KeyboardLayout.CtrlKey);
        transport.Results.Enqueue(new(2, 0));
        using var backend = new SendInputBackend(transport);
        Assert.False(backend.KeyPressChord(PrototypeKeys.A, [KeyboardLayout.CtrlKey, KeyboardLayout.ShiftKey]).Success);
        Assert.All(transport.Calls.SelectMany(c => c), e => Assert.NotEqual((ushort)0x1D, e.Data.Keyboard.Scan));
        Assert.Equal(0, backend.HeldCount);
    }
    [Fact]
    public void FailedReleaseBlocksChordsUntilCleanupRetry()
    {
        var transport = new Transport(); transport.Results.Enqueue(new(1,0)); transport.Results.Enqueue(new(0,5));
        using var backend = new SendInputBackend(transport);
        Assert.True(backend.KeyPressChord(PrototypeKeys.A, [KeyboardLayout.ShiftKey]).CleanupFailed);
        Assert.False(backend.KeyPressChord(PrototypeKeys.Enter, []).Success);
        Assert.Equal(2, transport.Calls.Count);
        Assert.True(backend.ReleaseAllKeys().Success);
        Assert.True(backend.KeyPressChord(PrototypeKeys.Enter, []).Success);
    }
    [Fact]
    public void ChordTransportExceptionCleansUpPotentialDowns()
    {
        var transport = new Transport { ThrowSend = true }; using var backend = new SendInputBackend(transport);
        Assert.False(backend.KeyPressChord(PrototypeKeys.A, Modifiers).Success);
        Assert.Equal(0, backend.HeldCount); Assert.Equal(2, transport.Calls.Count);
    }
    [Fact]
    public void InvalidOrExternallyHeldPrimaryNeverSendsChord()
    {
        var transport = new Transport(); using var backend = new SendInputBackend(transport);
        Assert.False(backend.KeyPressChord(default, []).Success);
        Assert.False(backend.KeyPressChord(PrototypeKeys.A, [PrototypeKeys.Enter]).Success);
        transport.External.Add(PrototypeKeys.A);
        Assert.False(backend.KeyPressChord(PrototypeKeys.A, Modifiers).Success);
        transport.ThrowInspection = true;
        Assert.False(backend.KeyPressChord(PrototypeKeys.Enter, Modifiers).Success);
        Assert.Empty(transport.Calls);
    }
    [Theory]
    [InlineData(0x4B)] [InlineData(0x4D)] [InlineData(0x48)] [InlineData(0x50)] [InlineData(0x53)]
    public void ExtendedSpecialChordUsesGenuineKeyboardFlags(int scan)
    {
        var transport = new Transport(); using var backend = new SendInputBackend(transport);
        Assert.True(backend.KeyPressChord(InputKey.ScanCode((ushort)scan, true), []).Success);
        var events = Assert.Single(transport.Calls);
        Assert.Equal(9u, events[0].Data.Keyboard.Flags); Assert.Equal(11u, events[1].Data.Keyboard.Flags);
        Assert.All(events, e => Assert.Equal((ushort)0, e.Data.Keyboard.VirtualKey));
    }
}
