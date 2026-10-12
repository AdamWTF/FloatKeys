using System.Runtime.InteropServices;
using FloatKeys.Input;
using FloatKeys.Platform;
using Xunit;

namespace FloatKeys.Tests;

public sealed class InputTests
{
    private sealed class FakeTransport : IInputTransport
    {
        internal readonly List<NativeMethods.Input[]> Calls = [];
        internal readonly Queue<Submission> Results = new();
        internal bool AlreadyDown;
        internal int ThrowCount;
        public bool IsKeyDown(InputKey key) => AlreadyDown;
        public Submission Send(NativeMethods.Input[] events)
        {
            Calls.Add(events);
            if (ThrowCount-- > 0) throw new InvalidOperationException("Test transport failure");
            return Results.TryDequeue(out var result) ? result : new(events.Length, 0);
        }
    }

    [Theory]
    [InlineData(0x1E)]
    [InlineData(0x0E)]
    [InlineData(0x1C)]
    public void ScanCodePressIsOneOrderedPair(int scan)
    {
        var transport = new FakeTransport();
        using var backend = new SendInputBackend(transport);
        Assert.True(backend.KeyPress(InputKey.ScanCode((ushort)scan)).Success);
        var events = Assert.Single(transport.Calls);
        Assert.Equal(2, events.Length);
        Assert.All(events, e =>
        {
            Assert.Equal(1u, e.Type);
            Assert.Equal((ushort)scan, e.Data.Keyboard.Scan);
            Assert.Equal((ushort)0, e.Data.Keyboard.VirtualKey);
            Assert.Equal(0u, e.Data.Keyboard.Time);
            Assert.Equal((nuint)0, e.Data.Keyboard.ExtraInfo);
        });
        Assert.Equal(0x8u, events[0].Data.Keyboard.Flags);
        Assert.Equal(0xAu, events[1].Data.Keyboard.Flags);
        Assert.Equal(0, backend.HeldCount);
    }

    [Fact]
    public void PrototypeMappingsArePhysicalMainKeyboardKeys()
    {
        Assert.Equal(InputKey.ScanCode(0x1E), PrototypeKeys.A);
        Assert.Equal(InputKey.ScanCode(0x0E), PrototypeKeys.Backspace);
        Assert.Equal(InputKey.ScanCode(0x1C), PrototypeKeys.Enter);
        Assert.False(PrototypeKeys.Enter.Extended);
    }

    [Fact]
    public void ExtendedAndVirtualKeyFlagsAreCorrect()
    {
        var scan = InputEventFactory.Create(InputKey.ScanCode(0x1D, true), true);
        Assert.Equal(0xBu, scan.Data.Keyboard.Flags);
        var vk = InputEventFactory.Create(InputKey.VirtualKey(0xA3, true), false);
        Assert.Equal((ushort)0xA3, vk.Data.Keyboard.VirtualKey);
        Assert.Equal((ushort)0, vk.Data.Keyboard.Scan);
        Assert.Equal(1u, vk.Data.Keyboard.Flags);
    }

    [Fact]
    public void UnicodeTextEmitsUtf16PairsIncludingSurrogates()
    {
        var transport = new FakeTransport();
        using var backend = new SendInputBackend(transport);
        const string text = "é\U0001F600";
        Assert.True(backend.SendUnicodeText(text).Success);
        var events = Assert.Single(transport.Calls);
        Assert.Equal(6, events.Length);
        for (int i = 0; i < text.Length; i++)
        {
            Assert.Equal((ushort)text[i], events[i * 2].Data.Keyboard.Scan);
            Assert.Equal((ushort)text[i], events[i * 2 + 1].Data.Keyboard.Scan);
            Assert.Equal(4u, events[i * 2].Data.Keyboard.Flags);
            Assert.Equal(6u, events[i * 2 + 1].Data.Keyboard.Flags);
            Assert.Equal((ushort)0, events[i * 2].Data.Keyboard.VirtualKey);
        }
    }

    [Fact]
    public void NativeLayoutMatches64BitWindowsAbi()
    {
        Assert.Equal(8, IntPtr.Size);
        Assert.Equal(40, Marshal.SizeOf<NativeMethods.Input>());
        Assert.Equal(32, Marshal.SizeOf<NativeMethods.InputUnion>());
        Assert.Equal(24, Marshal.SizeOf<NativeMethods.KeyboardInputData>());
        Assert.Equal(32, Marshal.SizeOf<NativeMethods.MouseInput>());
        Assert.Equal(8, Marshal.SizeOf<NativeMethods.HardwareInput>());
        Assert.Equal((nint)8, Marshal.OffsetOf<NativeMethods.Input>(nameof(NativeMethods.Input.Data)));
        Assert.Equal((nint)16, Marshal.OffsetOf<NativeMethods.KeyboardInputData>(nameof(NativeMethods.KeyboardInputData.ExtraInfo)));
        Assert.Equal((nint)24, Marshal.OffsetOf<NativeMethods.MouseInput>(nameof(NativeMethods.MouseInput.ExtraInfo)));
    }

    [Fact]
    public void StateTracksOwnershipAndDuplicateTransitionsAreHarmless()
    {
        var transport = new FakeTransport();
        using var backend = new SendInputBackend(transport);
        Assert.True(backend.KeyDown(PrototypeKeys.A).Success);
        Assert.Equal(1, backend.HeldCount);
        Assert.False(backend.KeyDown(PrototypeKeys.A).Success);
        Assert.False(backend.KeyPress(PrototypeKeys.A).Success);
        Assert.Single(transport.Calls);
        Assert.True(backend.KeyUp(PrototypeKeys.A).Success);
        Assert.True(backend.KeyUp(PrototypeKeys.A).Success);
        Assert.Equal(2, transport.Calls.Count);
        Assert.Equal(0, backend.HeldCount);
    }

    [Fact]
    public void DoesNotReleaseOrPressAnAlreadyHeldExternalKey()
    {
        var transport = new FakeTransport { AlreadyDown = true };
        using var backend = new SendInputBackend(transport);
        Assert.False(backend.KeyPress(PrototypeKeys.A).Success);
        Assert.True(backend.KeyUp(PrototypeKeys.A).Success);
        Assert.True(backend.ReleaseAllKeys().Success);
        Assert.Empty(transport.Calls);
    }

    [Fact]
    public void ZeroSubmissionReportsErrorWithoutInventingHeldKey()
    {
        var transport = new FakeTransport();
        transport.Results.Enqueue(new(0, 5));
        using var backend = new SendInputBackend(transport);
        var result = backend.KeyPress(PrototypeKeys.A);
        Assert.False(result.Success);
        Assert.Equal(2, result.Requested);
        Assert.Equal(0, result.Submitted);
        Assert.Equal(5, result.NativeError);
        Assert.False(result.CleanupFailed);
        Assert.Equal(0, backend.HeldCount);
        Assert.Single(transport.Calls);
    }

    [Fact]
    public void PartialPairAttemptsCompensatingRelease()
    {
        var transport = new FakeTransport();
        transport.Results.Enqueue(new(1, 0));
        using var backend = new SendInputBackend(transport);
        var result = backend.KeyPress(PrototypeKeys.A);
        Assert.False(result.Success);
        Assert.Equal(1, result.Submitted);
        Assert.False(result.CleanupFailed);
        Assert.Equal(2, transport.Calls.Count);
        Assert.Equal(0xAu, Assert.Single(transport.Calls[1]).Data.Keyboard.Flags);
        Assert.Equal(0, backend.HeldCount);
    }

    [Fact]
    public void FailedCleanupBlocksPressesAndCanBeRetried()
    {
        var transport = new FakeTransport();
        transport.Results.Enqueue(new(1, 0));
        transport.Results.Enqueue(new(0, 5));
        using var backend = new SendInputBackend(transport);
        Assert.True(backend.KeyPress(PrototypeKeys.A).CleanupFailed);
        Assert.Equal(1, backend.HeldCount);
        Assert.False(backend.KeyPress(PrototypeKeys.Enter).Success);
        Assert.Equal(2, transport.Calls.Count);
        Assert.True(backend.ReleaseAllKeys().Success);
        Assert.Equal(0, backend.HeldCount);
        Assert.True(backend.KeyPress(PrototypeKeys.Enter).Success);
    }

    [Fact]
    public void ExplicitKeyUpRetryAlsoUnblocksPresses()
    {
        var transport = new FakeTransport();
        transport.Results.Enqueue(new(1, 0));
        transport.Results.Enqueue(new(0, 5));
        using var backend = new SendInputBackend(transport);
        backend.KeyPress(PrototypeKeys.A);
        Assert.True(backend.KeyUp(PrototypeKeys.A).Success);
        Assert.True(backend.KeyPress(PrototypeKeys.Enter).Success);
    }

    [Fact]
    public void PartialMultiKeyCleanupRetainsOnlyUnreleasedOwnedKeys()
    {
        var transport = new FakeTransport();
        using var backend = new SendInputBackend(transport);
        backend.KeyDown(PrototypeKeys.A);
        backend.KeyDown(PrototypeKeys.Enter);
        transport.Results.Enqueue(new(1, 0));
        Assert.True(backend.ReleaseAllKeys().CleanupFailed);
        Assert.Equal(1, backend.HeldCount);
        Assert.True(backend.ReleaseAllKeys().Success);
        var retried = Assert.Single(transport.Calls[3]);
        Assert.NotEqual(transport.Calls[2][0].Data.Keyboard.Scan, retried.Data.Keyboard.Scan);
    }

    [Fact]
    public void FailureCleansUpPreviouslyOwnedModifierOnly()
    {
        var transport = new FakeTransport();
        using var backend = new SendInputBackend(transport);
        backend.KeyDown(InputKey.ScanCode(0x2A)); // Owned Shift, no modifier UI.
        transport.Results.Enqueue(new(0, 0));
        Assert.False(backend.KeyPress(PrototypeKeys.A).Success);
        var cleanup = Assert.Single(transport.Calls[2]);
        Assert.Equal((ushort)0x2A, cleanup.Data.Keyboard.Scan);
        Assert.Equal(0xAu, cleanup.Data.Keyboard.Flags);
        Assert.Equal(0, backend.HeldCount);
    }

    [Fact]
    public void DisposeIsIdempotentAndRejectsNewInput()
    {
        var transport = new FakeTransport();
        var backend = new SendInputBackend(transport);
        backend.KeyDown(PrototypeKeys.A);
        backend.Dispose();
        backend.Dispose();
        Assert.Equal(2, transport.Calls.Count);
        Assert.Equal(0, backend.HeldCount);
        Assert.False(backend.KeyPress(PrototypeKeys.A).Success);
    }

    [Fact]
    public void RepeatedDisposeRetriesFailedRelease()
    {
        var transport = new FakeTransport();
        var backend = new SendInputBackend(transport);
        backend.KeyDown(PrototypeKeys.A);
        transport.Results.Enqueue(new(0, 0));
        backend.Dispose();
        Assert.Equal(1, backend.HeldCount);
        backend.Dispose();
        Assert.Equal(0, backend.HeldCount);
        Assert.Equal(3, transport.Calls.Count);
    }

    [Fact]
    public void TransportExceptionAttemptsReleaseAndDoesNotEscape()
    {
        var transport = new FakeTransport { ThrowCount = 2 };
        using var backend = new SendInputBackend(transport);
        var result = backend.KeyPress(PrototypeKeys.A);
        Assert.False(result.Success);
        Assert.True(result.CleanupFailed);
        Assert.Equal(1, backend.HeldCount);
        Assert.True(backend.ReleaseAllKeys().Success);
    }

    [Fact]
    public void LogFailureCannotPreventInputOrCleanup()
    {
        var transport = new FakeTransport();
        using var backend = new SendInputBackend(transport, _ => throw new IOException());
        Assert.True(backend.KeyDown(PrototypeKeys.A).Success);
        Assert.True(backend.ReleaseAllKeys().Success);
        Assert.Equal(0, backend.HeldCount);
    }

    [Fact]
    public void EmptyTextAndInvalidInputNeverSubmitEvents()
    {
        var transport = new FakeTransport();
        using var backend = new SendInputBackend(transport);
        Assert.True(backend.SendUnicodeText("").Success);
        Assert.False(backend.KeyPress(default).Success);
        Assert.False(backend.SendUnicodeText(new string('a', 4097)).Success);
        Assert.Throws<ArgumentOutOfRangeException>(() => InputKey.VirtualKey(255));
        Assert.Throws<ArgumentOutOfRangeException>(() => InputKey.ScanCode(0));
        Assert.Empty(transport.Calls);
    }
}
