using FloatKeys.Input;
using Xunit;

namespace FloatKeys.Tests;

public sealed class KeyboardTests
{
    [Fact]
    public void ApplicationShutdownCancelsRepeatsLatchesAndPendingTouchesBeforeRelease()
    {
        var backend = new Backend(); var scheduler = new Scheduler();
        var controller = new KeyboardController(backend, scheduler);
        Tap(controller, "Ctrl"); controller.Begin(1, Find("backspace"));
        Tap(controller, Find("Alt"), 3); controller.Begin(2, Find("a"));
        var lifetime = new FloatKeys.Services.ShutdownCoordinator(_ => { });
        lifetime.Add("controller", controller.Dispose);
        lifetime.Add("input", () => backend.ReleaseAllKeys());
        lifetime.Stop(); lifetime.Stop();
        int submitted = backend.Calls.Count;
        foreach (var tick in scheduler.Ticks) tick.Fire();
        controller.End(2, true);
        Assert.Equal(submitted, backend.Calls.Count);
        Assert.Equal(0, controller.InteractionCount);
        Assert.False(controller.Shift || controller.Ctrl || controller.Alt);
        Assert.False(controller.Begin(3, Find("a")));
        Assert.True(backend.Cleanups >= 2);
    }
    [Fact]
    public void HideStopsRepeatsAndLatchesButRetainsCapsLayoutAndController()
    {
        var backend = new Backend(); var scheduler = new Scheduler();
        using var controller = new KeyboardController(backend, scheduler, symbols: true);
        Tap(controller, KeyboardLayout.Caps);
        for (int i = 0; i < 50; i++)
        {
            var state = new FloatKeys.Services.OverlayVisibility(() => { }, controller.CancelAll, () => { });
            state.Show();
            Tap(controller, "Ctrl"); Tap(controller, "Alt"); Tap(controller, "Shift");
            Assert.True(controller.Begin(5, Find("backspace")));
            var stale = scheduler.Ticks.Last();
            state.Hide();
            int count = backend.Calls.Count;
            stale.Fire(); controller.End(5, true);
            Assert.Equal(count, backend.Calls.Count);
            Assert.Equal(0, controller.InteractionCount);
            Assert.False(controller.Shift || controller.Ctrl || controller.Alt);
            Assert.True(controller.Caps); Assert.True(controller.Symbols);
            state.Show(); Tap(controller, "a");
            Assert.Equal(count + 1, backend.Calls.Count);
            Assert.Equal(new[] { KeyboardLayout.ShiftKey }, backend.Calls.Last().Modifiers);
        }
        Assert.True(backend.Cleanups >= 50);
    }
    private sealed class Backend : IInputBackend
    {
        internal readonly List<(InputKey Key, InputKey[] Modifiers)> Calls = [];
        internal int Cleanups;
        internal bool Fail;
        internal bool CleanupFail;
        public InputResult KeyPressChord(InputKey key, IReadOnlyList<InputKey> modifiers)
        {
            Calls.Add((key, modifiers.ToArray()));
            return Fail ? new(4, 1, 5, false, "test failure") : new(2, 2, 0, false, null);
        }
        public InputResult KeyDown(InputKey key) => throw new NotSupportedException();
        public InputResult KeyUp(InputKey key) => throw new NotSupportedException();
        public InputResult KeyPress(InputKey key) => KeyPressChord(key, []);
        public InputResult SendUnicodeText(string text) => throw new NotSupportedException();
        public InputResult ReleaseAllKeys()
        {
            Cleanups++;
            return CleanupFail ? new(1, 0, 5, true, "cleanup failure") : new(0, 0, 0, false, null);
        }
        public void Dispose() { }
    }
    private sealed class Scheduler : IRepeatScheduler
    {
        internal sealed class Tick(TimeSpan delay, Action callback) : IDisposable
        {
            internal TimeSpan Delay = delay;
            internal bool Disposed;
            internal void Fire() => callback(); // Deliberately permit stale callbacks.
            public void Dispose() => Disposed = true;
        }
        internal readonly List<Tick> Ticks = [];
        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            var tick = new Tick(delay, callback);
            Ticks.Add(tick);
            return tick;
        }
    }
    private static KeyDefinition Find(string id) => KeyboardLayout.Main.Concat(KeyboardLayout.Symbols)
        .SelectMany(r => r).First(k => k.Id == id);
    private static void Tap(KeyboardController controller, KeyDefinition key, long pointer = 1)
    {
        Assert.True(controller.Begin(pointer, key));
        controller.End(pointer, true);
    }
    private static void Tap(KeyboardController controller, string id) => Tap(controller, Find(id));

    public static IEnumerable<object[]> Letters()
    {
        const string keys = "qwertyuiopasdfghjklzxcvbnm";
        ushort[] scans = [0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x18,0x19,
            0x1E,0x1F,0x20,0x21,0x22,0x23,0x24,0x25,0x26,0x2C,0x2D,0x2E,0x2F,0x30,0x31,0x32];
        return keys.Select((c,i) => new object[] { c.ToString(), (int)scans[i] });
    }
    [Theory, MemberData(nameof(Letters))]
    public void QwertyMappingAndLetterEvents(string id, int scan)
    {
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler());
        var key = Find(id);
        Assert.True(key.Letter);
        Assert.Equal(id.ToUpperInvariant(), key.ShiftedLabel);
        Tap(controller, key);
        var call = Assert.Single(backend.Calls);
        Assert.Equal(InputKey.ScanCode((ushort)scan), call.Key);
        Assert.Empty(call.Modifiers);
    }

    [Theory]
    [InlineData("1","!",0x02)] [InlineData("2","\"",0x03)] [InlineData("3","£",0x04)]
    [InlineData("4","$",0x05)] [InlineData("5","%",0x06)] [InlineData("6","^",0x07)]
    [InlineData("7","&",0x08)] [InlineData("8","*",0x09)] [InlineData("9","(",0x0A)] [InlineData("0",")",0x0B)]
    public void UkNumberRowMapsBothShiftStates(string label, string shifted, int scan)
    {
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler());
        var key = Find(label);
        Assert.Equal(shifted, key.ShiftedLabel);
        Tap(controller, key);
        Tap(controller, "Shift");
        Assert.Equal(shifted, controller.Label(key));
        Tap(controller, key);
        Assert.All(backend.Calls, c => Assert.Equal(InputKey.ScanCode((ushort)scan), c.Key));
        Assert.Empty(backend.Calls[0].Modifiers);
        Assert.Equal(new[] { KeyboardLayout.ShiftKey }, backend.Calls[1].Modifiers);
        Assert.False(controller.Shift);
    }

    [Theory]
    [InlineData("/",0x35,false)] [InlineData(":",0x27,true)] [InlineData(".",0x34,false)]
    [InlineData(",",0x33,false)] [InlineData("?",0x35,true)] [InlineData("!",0x02,true)]
    [InlineData("'",0x28,false)] [InlineData("-",0x0C,false)] [InlineData("@",0x28,true)]
    [InlineData("\"",0x03,true)] [InlineData("£",0x04,true)] [InlineData("#",0x2B,false)]
    [InlineData("$",0x05,true)] [InlineData("%",0x06,true)] [InlineData("&",0x08,true)]
    [InlineData("*",0x09,true)] [InlineData("(",0x0A,true)] [InlineData(")",0x0B,true)]
    [InlineData("[",0x1A,false)] [InlineData("]",0x1B,false)] [InlineData("{",0x1A,true)]
    [InlineData("}",0x1B,true)] [InlineData("\\",0x56,false)] [InlineData("|",0x56,true)]
    [InlineData("_",0x0C,true)] [InlineData("=",0x0D,false)] [InlineData("+",0x0D,true)]
    [InlineData(";",0x27,false)] [InlineData("`",0x29,false)] [InlineData("¬",0x29,true)]
    [InlineData("^",0x07,true)] [InlineData("~",0x2B,true)] [InlineData("<",0x33,true)] [InlineData(">",0x34,true)]
    public void DedicatedCharactersUseExactUkChordsRegardlessOfShiftOrCaps(string label, int scan, bool shift)
    {
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler());
        var key = Find(label);
        Assert.True(key.FixedCharacter);
        Tap(controller, key);
        Tap(controller, KeyboardLayout.Caps);
        Tap(controller, "Shift");
        Assert.Equal(label, controller.Label(key));
        Tap(controller, key);
        Assert.All(backend.Calls, c => {
            Assert.Equal(InputKey.ScanCode((ushort)scan), c.Key);
            Assert.Equal(shift ? new[] { KeyboardLayout.ShiftKey } : [], c.Modifiers);
        });
        Assert.False(controller.Shift);
        Assert.True(controller.Caps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BothPrimaryLayoutsProvideDirectSlashAndNumbers(bool compact)
    {
        var layout = KeyboardLayout.Primary(compact ? PrimaryKeyboardLayout.Compact : PrimaryKeyboardLayout.Chat);
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler(), layout: compact ? PrimaryKeyboardLayout.Compact : PrimaryKeyboardLayout.Chat);
        foreach (char label in "1234567890/")
            Tap(controller, Assert.Single(layout.SelectMany(r => r), k => k.Id == label.ToString()));
        Assert.Equal(11, backend.Calls.Count);
        Assert.All(backend.Calls, call => Assert.Empty(call.Modifiers));
        Assert.Equal(InputKey.ScanCode(0x35), backend.Calls.Last().Key);
        Assert.False(controller.Symbols);
        Assert.Equal(compact ? 5 : 6, layout.Count);
    }

    [Fact]
    public void ChatRowsAndCompactControlsMatchTheRequestedArrangement()
    {
        string[][] expected = [
            "1234567890".Select(c => c.ToString()).ToArray(),
            "qwertyuiop".Select(c => c.ToString()).ToArray(),
            "asdfghjkl".Select(c => c.ToString()).ToArray(),
            ["Shift", "z", "x", "c", "v", "b", "n", "m", "backspace"],
            ["/", ":", ".", ",", "?", "!", "'", "-"],
            ["layout", "Ctrl", "Alt", "space", "left", "right", "enter"] ];
        for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], KeyboardLayout.Chat[i].Select(k => k.Id));
        for (int i = 0; i < 4; i++) Assert.Equal(expected[i], KeyboardLayout.Compact[i].Select(k => k.Id));
        Assert.Equal(new[] { "layout", "Ctrl", "Alt", "/", "space", "left", "right", "enter" }, KeyboardLayout.Compact[4].Select(k => k.Id));
        Assert.All(KeyboardLayout.Chat[4], key => Assert.Equal(1, key.Width));
        foreach (var page in new[] { KeyboardLayout.Chat, KeyboardLayout.Compact, KeyboardLayout.Symbols })
        {
            Assert.Equal(page.Sum(r => r.Count), page.SelectMany(r => r).Select(k => k.Id).Distinct().Count());
            Assert.True(page.Last().Single(k => k.Id == "space").Width > 3);
        }
        Assert.Equal("ABC", KeyboardLayout.Symbols.Last()[0].Label);
    }

    [Theory]
    [InlineData("/", false)] [InlineData(":", true)]
    public void PunctuationRetainsShortcutModifiersAndConsumesLatches(string label, bool requiredShift)
    {
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler());
        Tap(controller, "Ctrl"); Tap(controller, "Alt"); Tap(controller, "Shift");
        Tap(controller, label);
        var modifiers = new List<InputKey> { KeyboardLayout.CtrlKey, KeyboardLayout.AltKey };
        if (requiredShift) modifiers.Add(KeyboardLayout.ShiftKey);
        Assert.Equal(modifiers, Assert.Single(backend.Calls).Modifiers);
        Assert.False(controller.Ctrl || controller.Alt || controller.Shift);
        Tap(controller, "a"); Assert.Empty(backend.Calls.Last().Modifiers);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SelectingPrimaryLayoutPreservesStateAndCancelsSessionsWithoutInput(bool symbols)
    {
        var backend = new Backend(); var scheduler = new Scheduler();
        using var controller = new KeyboardController(backend, scheduler, symbols: symbols);
        Tap(controller, KeyboardLayout.Caps);
        controller.Begin(10, Find("backspace"));
        controller.Begin(11, Find("a"));
        Tap(controller, Find("Shift"), 12); Tap(controller, Find("Ctrl"), 12); Tap(controller, Find("Alt"), 12);
        int calls = backend.Calls.Count;
        controller.SelectLayout(PrimaryKeyboardLayout.Compact);
        scheduler.Ticks[0].Fire(); controller.End(11, true);
        Assert.Equal(calls, backend.Calls.Count);
        Assert.Equal(0, controller.InteractionCount);
        Assert.True(controller.Caps && controller.Shift && controller.Ctrl && controller.Alt);
        Assert.Equal(symbols, controller.Symbols);
        Assert.Equal(PrimaryKeyboardLayout.Compact, controller.SelectedLayout);
        Tap(controller, "layout");
        Assert.Equal(PrimaryKeyboardLayout.Compact, controller.SelectedLayout);
        Assert.Equal(!symbols, controller.Symbols);
        Assert.Equal(calls, backend.Calls.Count);
    }

    [Fact]
    public void LayoutCleanupFailureClearsLatchesAndReportsFailure()
    {
        var backend = new Backend { CleanupFail = true };
        using var controller = new KeyboardController(backend, new Scheduler());
        Tap(controller, "Shift");
        int failures = 0;
        controller.InputFailed += () => failures++;
        controller.SelectLayout(PrimaryKeyboardLayout.Compact);
        Assert.False(controller.Shift); Assert.Equal(1, failures); Assert.Empty(backend.Calls);
    }

    [Theory]
    [InlineData("backspace",0x0E,false)] [InlineData("delete",0x53,true)]
    [InlineData("enter",0x1C,false)] [InlineData("tab",0x0F,false)]
    [InlineData("escape",0x01,false)] [InlineData("space",0x39,false)]
    [InlineData("left",0x4B,true)] [InlineData("right",0x4D,true)]
    [InlineData("up",0x48,true)] [InlineData("down",0x50,true)]
    public void SpecialKeysArePhysicalEvents(string id, int scan, bool extended)
    {
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler());
        Tap(controller, id);
        Assert.Equal(InputKey.ScanCode((ushort)scan, extended), Assert.Single(backend.Calls).Key);
    }

    [Fact]
    public void ShiftIsOneShotAndCanToggleOff()
    {
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler());
        Tap(controller, "Shift");
        Assert.True(controller.Active(Find("Shift")));
        Assert.Empty(backend.Calls);
        Tap(controller, "Shift");
        Assert.False(controller.Shift);
        Tap(controller, "Shift");
        Tap(controller, "a");
        Tap(controller, "a");
        Assert.Equal(new[] { KeyboardLayout.ShiftKey }, backend.Calls[0].Modifiers);
        Assert.Empty(backend.Calls[1].Modifiers);
    }
    [Fact]
    public void CapsPersistsAndXorsShiftOnlyForLetters()
    {
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler());
        Tap(controller, KeyboardLayout.Caps);
        Assert.True(controller.Caps);
        Assert.Equal("A", controller.Label(Find("a")));
        Tap(controller, "a");
        Tap(controller, "Shift");
        Assert.Equal("a", controller.Label(Find("a")));
        Tap(controller, "a");
        Tap(controller, "2");
        Assert.Equal(new[] { KeyboardLayout.ShiftKey }, backend.Calls[0].Modifiers);
        Assert.Empty(backend.Calls[1].Modifiers);
        Assert.Empty(backend.Calls[2].Modifiers);
        Assert.True(controller.Caps);
        Tap(controller, KeyboardLayout.Caps);
        Assert.False(controller.Caps);
        Assert.Equal("a", controller.Label(Find("a")));
    }
    [Theory]
    [InlineData("a")] [InlineData("c")] [InlineData("v")]
    public void CtrlShortcutsIgnoreCapsAndReleaseLatch(string letter)
    {
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler());
        Tap(controller, KeyboardLayout.Caps);
        Tap(controller, "Ctrl");
        Assert.Equal(letter, controller.Label(Find(letter)));
        Tap(controller, letter);
        Assert.Equal(new[] { KeyboardLayout.CtrlKey }, Assert.Single(backend.Calls).Modifiers);
        Assert.False(controller.Ctrl);
    }
    [Fact]
    public void LayoutSwitchPreservesLatchesForAltTabAndShiftSelection()
    {
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler());
        Tap(controller, "Alt");
        Tap(controller, "layout");
        Assert.True(controller.Symbols);
        Assert.True(controller.Alt);
        Tap(controller, "tab");
        Assert.Equal(new[] { KeyboardLayout.AltKey }, backend.Calls[0].Modifiers);
        Assert.False(controller.Alt);
        Tap(controller, "Shift");
        Tap(controller, "left");
        Assert.Equal(new[] { KeyboardLayout.ShiftKey }, backend.Calls[1].Modifiers);
        Assert.False(controller.Shift);
        Tap(controller, "layout");
        Assert.False(controller.Symbols);
    }
    [Fact]
    public void CombinedModifiersAndToggleOff()
    {
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler());
        Tap(controller, "Ctrl"); Tap(controller, "Ctrl");
        Tap(controller, "Alt"); Tap(controller, "Alt");
        Assert.False(controller.Ctrl); Assert.False(controller.Alt);
        Tap(controller, "Ctrl"); Tap(controller, "Alt"); Tap(controller, "Shift"); Tap(controller, "tab");
        Assert.Equal(new[] { KeyboardLayout.CtrlKey, KeyboardLayout.AltKey, KeyboardLayout.ShiftKey }, Assert.Single(backend.Calls).Modifiers);
        Assert.False(controller.Ctrl || controller.Alt || controller.Shift);
    }

    [Theory]
    [InlineData("backspace")] [InlineData("delete")] [InlineData("left")]
    [InlineData("right")] [InlineData("up")] [InlineData("down")]
    public void RepeatStartsAfterDelayAndEndsWithoutReleasePress(string id)
    {
        var backend = new Backend(); var scheduler = new Scheduler();
        using var controller = new KeyboardController(backend, scheduler);
        Tap(controller, "Shift");
        Assert.True(controller.Begin(5, Find(id)));
        Assert.Single(backend.Calls);
        Assert.False(controller.Shift);
        Assert.Equal(TimeSpan.FromMilliseconds(450), scheduler.Ticks[0].Delay);
        scheduler.Ticks[0].Fire();
        Assert.Equal(2, backend.Calls.Count);
        scheduler.Ticks[0].Fire(); // An obsolete tick must not emit again within the same session.
        Assert.Equal(2, backend.Calls.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(60), scheduler.Ticks[1].Delay);
        scheduler.Ticks[1].Fire();
        controller.End(5, true);
        Assert.All(scheduler.Ticks, t => Assert.True(t.Disposed));
        scheduler.Ticks[^1].Fire();
        Assert.Equal(3, backend.Calls.Count);
        Assert.All(backend.Calls, c => Assert.Equal(new[] { KeyboardLayout.ShiftKey }, c.Modifiers));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CancellationOrClosureInvalidatesStaleRepeats(bool close)
    {
        var backend = new Backend(); var scheduler = new Scheduler();
        using var controller = new KeyboardController(backend, scheduler);
        controller.Begin(1, Find("backspace"));
        if (close) controller.Dispose(); else controller.Cancel(1);
        scheduler.Ticks[0].Fire();
        controller.End(1, true);
        Assert.Single(backend.Calls);
        Assert.Equal(0, controller.InteractionCount);
        Assert.True(backend.Cleanups > 0);
        if (close) Assert.False(controller.Begin(2, Find("a")));
    }
    [Fact]
    public void LayoutSwitchCancelsHeldKeysAndRepeats()
    {
        var backend = new Backend(); var scheduler = new Scheduler();
        using var controller = new KeyboardController(backend, scheduler);
        controller.Begin(1, Find("backspace"));
        controller.Begin(2, Find("a"));
        Tap(controller, Find("layout"), 3);
        scheduler.Ticks[0].Fire(); controller.End(2, true);
        Assert.Single(backend.Calls);
        Assert.Equal(0, controller.InteractionCount);
    }
    [Fact]
    public void SequentialAndConcurrentTouchesCommitExactlyOnce()
    {
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler());
        Assert.True(controller.Begin(1, Find("a")));
        Assert.False(controller.Begin(1, Find("b")));
        Assert.False(controller.Begin(2, Find("a")));
        Assert.True(controller.Begin(2, Find("b")));
        controller.End(2, true); controller.End(1, true); controller.End(1, true);
        for (int i = 0; i < 20; i++) Tap(controller, "c");
        Assert.Equal(22, backend.Calls.Count);
        Assert.Equal(Find("b").Mapping, backend.Calls[0].Key);
        Assert.Equal(Find("a").Mapping, backend.Calls[1].Key);
    }
    [Fact]
    public void LeavingOrCaptureLossCancelsOrdinaryKeyAndLatches()
    {
        var backend = new Backend();
        using var controller = new KeyboardController(backend, new Scheduler());
        Tap(controller, "Ctrl");
        controller.Begin(1, Find("a")); controller.End(1, false); controller.End(1, true);
        Assert.Empty(backend.Calls); Assert.False(controller.Ctrl);
        controller.Begin(1, Find("b")); controller.Cancel(1); controller.End(1, true);
        Assert.Empty(backend.Calls);
        Tap(controller, "a"); Assert.Single(backend.Calls);
    }
    [Fact]
    public void InputFailureStopsEveryInteractionAndReportsOriginalError()
    {
        var backend = new Backend(); var scheduler = new Scheduler();
        using var controller = new KeyboardController(backend, scheduler);
        InputResult? result = null; int failures = 0;
        controller.Result += r => result = r; controller.InputFailed += () => failures++;
        controller.Begin(1, Find("backspace")); controller.Begin(2, Find("a"));
        Tap(controller, Find("Alt"), 3);
        backend.Fail = true; scheduler.Ticks[0].Fire();
        controller.End(2, true); scheduler.Ticks[0].Fire();
        Assert.Equal(2, backend.Calls.Count); Assert.Equal(1, failures);
        Assert.Equal("test failure", result?.Error);
        Assert.Equal(0, controller.InteractionCount); Assert.False(controller.Alt);
        Assert.True(backend.Cleanups > 0);
    }
    [Fact]
    public void PointerAdmissionSuppressesPromotedMouseAndDuplicates()
    {
        var pointer = new PointerSession();
        Assert.True(pointer.Begin(10));
        Assert.False(pointer.Begin(-1, true)); Assert.False(pointer.Owns(-1, true));
        Assert.False(pointer.Begin(10)); Assert.False(pointer.Begin(11));
        Assert.True(pointer.Owns(10)); Assert.False(pointer.Owns(11));
        Assert.Equal(10L, pointer.Clear()); Assert.Null(pointer.Clear());
        Assert.False(pointer.Begin(-1, true)); Assert.True(pointer.Begin(-1));
        Assert.True(pointer.Owns(-1)); Assert.False(pointer.Owns(-1, true));
    }
    [Fact]
    public void RepeatSettingsAreInternalAndValidated()
    {
        var backend = new Backend(); var scheduler = new Scheduler();
        using var controller = new KeyboardController(backend, scheduler, new(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(80)));
        controller.Begin(1, Find("left")); scheduler.Ticks[0].Fire();
        Assert.Equal(TimeSpan.FromMilliseconds(300), scheduler.Ticks[0].Delay);
        Assert.Equal(TimeSpan.FromMilliseconds(80), scheduler.Ticks[1].Delay);
        Assert.Throws<ArgumentOutOfRangeException>(() => new KeyboardController(backend, scheduler, new(TimeSpan.Zero, TimeSpan.Zero)));
    }

    [Fact]
    public void CleanupFailureStopsOtherSessionsWithoutDependingOnTheWindow()
    {
        var backend = new Backend(); var scheduler = new Scheduler();
        using var controller = new KeyboardController(backend, scheduler);
        controller.Begin(1, Find("backspace")); controller.Begin(2, Find("left"));
        backend.CleanupFail = true;
        InputResult? reported = null;
        controller.Result += r => reported = r;
        controller.Cancel(1);
        Assert.Equal(0, controller.InteractionCount);
        Assert.True(reported?.CleanupFailed);
        scheduler.Ticks[1].Fire();
        Assert.Equal(2, backend.Calls.Count);
    }

    [Fact]
    public void ConcurrentRepeatsStopIndependentlyAndSnapshotsDoNotChange()
    {
        var backend = new Backend(); var scheduler = new Scheduler();
        using var controller = new KeyboardController(backend, scheduler);
        Tap(controller, "Shift"); controller.Begin(1, Find("left"));
        controller.Begin(2, Find("backspace"));
        Tap(controller, Find("Ctrl"), 3);
        scheduler.Ticks[0].Fire();
        Assert.Equal(new[] { KeyboardLayout.ShiftKey }, backend.Calls[2].Modifiers);
        controller.End(1, true);
        scheduler.Ticks[1].Fire();
        Assert.Empty(backend.Calls[3].Modifiers);
        Assert.True(controller.Ctrl);
        controller.End(2, true);
        Assert.Equal(0, controller.InteractionCount);
    }

    [Fact]
    public void OldPointerCallbackCannotAffectANewSessionAndCloseClearsLatches()
    {
        var backend = new Backend(); var scheduler = new Scheduler();
        using var controller = new KeyboardController(backend, scheduler);
        controller.Begin(1, Find("backspace")); controller.End(1, true);
        controller.Begin(1, Find("left")); scheduler.Ticks[0].Fire();
        Assert.Equal(2, backend.Calls.Count);
        Tap(controller, Find("Shift"), 2); Tap(controller, Find("Ctrl"), 2); Tap(controller, Find("Alt"), 2);
        controller.Dispose(); scheduler.Ticks[1].Fire();
        Assert.False(controller.Shift || controller.Ctrl || controller.Alt);
        Assert.Equal(2, backend.Calls.Count);
    }
}
