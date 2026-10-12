namespace FloatKeys.Input;

internal interface IRepeatScheduler
{
    IDisposable Schedule(TimeSpan delay, Action callback);
}

internal sealed record RepeatTiming(TimeSpan Delay, TimeSpan Interval)
{
    internal static readonly RepeatTiming Default = new(TimeSpan.FromMilliseconds(450), TimeSpan.FromMilliseconds(60));
}

// Called on the UI dispatcher. No WPF dependencies; native ownership stays in the backend.
internal sealed class KeyboardController : IDisposable
{
    private sealed class Session(KeyDefinition key)
    {
        internal readonly KeyDefinition Key = key;
        internal InputKey[] Modifiers = [];
        internal InputKey Mapping;
        internal IDisposable? Timer;
        internal long Generation;
    }
    private readonly IInputBackend input;
    private readonly IRepeatScheduler scheduler;
    private readonly RepeatTiming timing;
    private readonly Dictionary<long, Session> sessions = [];
    private bool closed;
    internal bool Shift { get; private set; }
    internal bool Ctrl { get; private set; }
    internal bool Alt { get; private set; }
    internal bool Caps { get; private set; }
    internal bool Symbols { get; private set; }
    internal PrimaryKeyboardLayout SelectedLayout { get; private set; }
    internal int InteractionCount => sessions.Count;
    internal event Action? Changed;
    internal event Action<InputResult>? Result;
    internal event Action? InputFailed;

    internal KeyboardController(IInputBackend input, IRepeatScheduler scheduler, RepeatTiming? timing = null, bool symbols = false,
        PrimaryKeyboardLayout layout = PrimaryKeyboardLayout.Chat)
    {
        this.input = input;
        this.scheduler = scheduler;
        Symbols = symbols;
        SelectedLayout = layout;
        this.timing = timing ?? RepeatTiming.Default;
        if (this.timing.Delay <= TimeSpan.Zero || this.timing.Interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timing));
    }

    internal string Label(KeyDefinition key)
    {
        if (key.FixedCharacter) return key.Label;
        bool shifted = key.Letter && !Ctrl && !Alt ? Caps ^ Shift : Shift;
        return shifted && key.ShiftedLabel is not null ? key.ShiftedLabel : key.Label;
    }
    internal bool Active(KeyDefinition key) => key.Action switch
    {
        KeyAction.Shift => Shift, KeyAction.Ctrl => Ctrl, KeyAction.Alt => Alt,
        KeyAction.Caps => Caps, _ => false
    };

    internal void SelectLayout(PrimaryKeyboardLayout layout)
    {
        if (closed || SelectedLayout == layout) return;
        StopSessions();
        ReportCleanup();
        SelectedLayout = layout;
        Changed?.Invoke();
    }

    internal bool Begin(long pointer, KeyDefinition key)
    {
        if (closed || sessions.ContainsKey(pointer) || sessions.Values.Any(s => s.Key.Id == key.Id)) return false;
        var session = new Session(key);
        sessions.Add(pointer, session);
        if (key.Repeat)
        {
            Snapshot(session);
            if (Emit(session)) Arm(pointer, session, timing.Delay);
        }
        return sessions.ContainsKey(pointer);
    }

    internal void End(long pointer, bool inside)
    {
        if (!inside) { Cancel(pointer); return; }
        if (!sessions.Remove(pointer, out var session)) return;
        session.Timer?.Dispose();
        if (session.Key.Repeat) return; // Initial press and timer own all repeat input.
        switch (session.Key.Action)
        {
            case KeyAction.Shift: Shift = !Shift; break;
            case KeyAction.Ctrl: Ctrl = !Ctrl; break;
            case KeyAction.Alt: Alt = !Alt; break;
            case KeyAction.Caps: Caps = !Caps; break;
            case KeyAction.SwitchLayout:
                StopSessions();
                ReportCleanup();
                Symbols = !Symbols;
                break;
            case KeyAction.Input: Snapshot(session); Emit(session); break;
        }
        Changed?.Invoke();
    }

    private void Snapshot(Session session)
    {
        bool shift = session.Key.FixedCharacter ? session.Key.RequiredShift :
            session.Key.Letter && !Ctrl && !Alt ? Caps ^ Shift : Shift;
        session.Mapping = (shift ? session.Key.ShiftedMapping : null) ?? session.Key.Mapping
            ?? throw new InvalidOperationException("Input key has no mapping.");
        var modifiers = new List<InputKey>(3);
        if (Ctrl) modifiers.Add(KeyboardLayout.CtrlKey);
        if (Alt) modifiers.Add(KeyboardLayout.AltKey);
        if (shift) modifiers.Add(KeyboardLayout.ShiftKey);
        session.Modifiers = modifiers.ToArray();
        ClearLatches();
        Changed?.Invoke();
    }

    private bool Emit(Session session)
    {
        InputResult result = input.KeyPressChord(session.Mapping, session.Modifiers);
        if (!result.Success)
        {
            StopSessions();
            ClearLatches();
            var cleanup = input.ReleaseAllKeys();
            result = result with { CleanupFailed = !cleanup.Success };
            // Preserve the original error and report the final cleanup status.
            Result?.Invoke(result);
            InputFailed?.Invoke();
            Changed?.Invoke();
            return false;
        }
        Result?.Invoke(result);
        return true;
    }

    private void Arm(long pointer, Session session, TimeSpan delay)
    {
        if (closed || !sessions.TryGetValue(pointer, out var current) || !ReferenceEquals(current, session)) return;
        long generation = ++session.Generation;
        session.Timer = scheduler.Schedule(delay, () =>
        {
            if (closed || !sessions.TryGetValue(pointer, out var active) || !ReferenceEquals(active, session)
                || session.Generation != generation) return;
            session.Generation++;
            session.Timer?.Dispose();
            session.Timer = null;
            if (Emit(session)) Arm(pointer, session, timing.Interval);
        });
    }

    internal void Cancel(long pointer)
    {
        if (!sessions.Remove(pointer, out var session)) return;
        session.Timer?.Dispose();
        ClearLatches();
        ReportCleanup();
        Changed?.Invoke();
    }

    private void ClearLatches() { Shift = Ctrl = Alt = false; }
    private void StopSessions()
    {
        var old = sessions.Values.ToArray();
        sessions.Clear(); // Invalidate callbacks before disposing timers.
        foreach (var session in old) session.Timer?.Dispose();
    }
    private void ReportCleanup()
    {
        InputResult result = input.ReleaseAllKeys();
        if (!result.Success)
        {
            StopSessions();
            ClearLatches();
            Result?.Invoke(result);
            InputFailed?.Invoke();
        }
    }
    internal void CancelAll()
    {
        StopSessions();
        ClearLatches();
        ReportCleanup();
        Changed?.Invoke();
    }
    public void Dispose()
    {
        closed = true;
        CancelAll();
    }
}
