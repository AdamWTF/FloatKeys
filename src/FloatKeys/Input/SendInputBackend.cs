namespace FloatKeys.Input;

public sealed class SendInputBackend : IInputBackend
{
    private readonly object gate = new();
    private readonly IInputTransport transport;
    private readonly Action<string>? log;
    private readonly KeyboardState state = new();
    private bool cleanupPending;
    private bool disposed;

    public SendInputBackend() : this(new NativeInputTransport()) { }
    internal SendInputBackend(IInputTransport transport, Action<string>? log = null)
    {
        this.transport = transport;
        this.log = log;
    }

    internal int HeldCount { get { lock (gate) return state.Count; } }

    public InputResult KeyDown(InputKey key) => Press(key, false);
    public InputResult KeyPress(InputKey key) => Press(key, true);

    public InputResult KeyPressChord(InputKey key, IReadOnlyList<InputKey> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        lock (gate)
        {
            if (disposed || cleanupPending) return InputResult.Rejected("Input unavailable until cleanup completes.");
            if (!key.IsValid || key.Kind == InputKeyKind.Unicode || state.Contains(key))
                return InputResult.Rejected("Invalid or already owned primary key.");
            var requested = modifiers.Distinct().ToArray();
            if (requested.Any(m => m != KeyboardLayout.ShiftKey && m != KeyboardLayout.CtrlKey && m != KeyboardLayout.AltKey)
                || requested.Contains(key) || requested.Any(state.Contains))
                return InputResult.Rejected("Invalid or already owned modifier.");
            try
            {
                if (transport.IsKeyDown(key)) return InputResult.Rejected("This key is already held. Release it before tapping.");
                // Borrow external modifiers, never take ownership or release them.
                var owned = requested.Where(m => !transport.IsKeyDown(m)).ToArray();
                return Submit(owned.Select(m => (m, false))
                    .Concat(new[] { (key, false), (key, true) })
                    .Concat(owned.Reverse().Select(m => (m, true))).ToArray());
            }
            catch (Exception exception)
            {
                WriteLog($"state inspection failed={exception.GetType().Name}");
                return InputResult.Rejected("Unable to inspect keyboard state.");
            }
        }
    }

    private InputResult Press(InputKey key, bool pair)
    {
        lock (gate)
        {
            if (disposed) return InputResult.Rejected("Input backend has closed.");
            if (!key.IsValid) return InputResult.Rejected("Invalid key.");
            if (cleanupPending) return InputResult.Rejected("Key release failed. Cancel or exit to retry cleanup.");
            if (state.Contains(key)) return InputResult.Rejected("Key is already owned by FloatKeys.");
            try
            {
                if (transport.IsKeyDown(key)) return InputResult.Rejected("This key is already held. Release it before tapping.");
            }
            catch (Exception exception)
            {
                WriteLog($"state inspection failed={exception.GetType().Name}");
                return InputResult.Rejected("Unable to inspect keyboard state.");
            }
            return Submit(pair ? [(key, false), (key, true)] : [(key, false)]);
        }
    }

    public InputResult KeyUp(InputKey key)
    {
        lock (gate)
        {
            if (!state.Contains(key)) return new(0, 0, 0, false, null);
            return Submit([(key, true)]);
        }
    }

    public InputResult SendUnicodeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (gate)
        {
            if (disposed || cleanupPending)
                return InputResult.Rejected("Input is unavailable until cleanup completes.");
            // One UTF-16 unit per pair, including both halves of supplementary characters.
            // Cap one submission to avoid unbounded allocation; not used by the prototype UI.
            if (text.Length > 4096) return InputResult.Rejected("Text exceeds 4096 UTF-16 units.");
            if (text.Any(c => state.Contains(InputKey.Unicode(c))))
                return InputResult.Rejected("A Unicode key is already owned by FloatKeys.");
            return Submit(text.SelectMany(c => new[] { (InputKey.Unicode(c), false),
                (InputKey.Unicode(c), true) }).ToArray());
        }
    }

    // Caller holds gate. SendInput inserts events serially; apply only the accepted prefix.
    private InputResult Submit((InputKey Key, bool Up)[] changes)
    {
        InputResult result = Send(changes);
        if (!result.Success)
        {
            InputResult cleanup = ReleaseAllKeys();
            result = result with { CleanupFailed = !cleanup.Success };
        }
        return result;
    }

    private InputResult Send((InputKey Key, bool Up)[] changes)
    {
        if (changes.Length == 0) return new(0, 0, 0, false, null);
        var events = changes.Select(c => InputEventFactory.Create(c.Key, c.Up)).ToArray();
        Submission submission;
        try { submission = transport.Send(events); }
        catch (Exception exception)
        {
            // Delivery is unknown if a transport throws. Retain potential downs for cleanup;
            // never assume that a matching up was accepted.
            foreach (var change in changes.Where(c => !c.Up)) state.Apply(change.Key, false);
            WriteLog($"submission exception={exception.GetType().Name} owned={state.Count}");
            return new(changes.Length, 0, 0, state.Count != 0, "Input submission threw; delivery is unknown.");
        }
        int sent = Math.Clamp(submission.Count, 0, changes.Length);
        for (int i = 0; i < sent; i++) state.Apply(changes[i].Key, changes[i].Up);
        if (state.Count == 0) cleanupPending = false;
        bool success = submission.Count == changes.Length;
        if (!success) WriteLog($"submission requested={changes.Length} submitted={submission.Count} nativeError={submission.Error} owned={state.Count}");
        return new(changes.Length, sent, submission.Error, false,
            success ? null : "Windows accepted fewer input events than requested; delivery is unverified.");
    }

    public InputResult ReleaseAllKeys()
    {
        lock (gate)
        {
            var changes = state.Snapshot().Select(k => (k, true)).ToArray();
            var result = Send(changes);
            cleanupPending = state.Count != 0;
            if (cleanupPending) WriteLog($"cleanup failed owned={state.Count}");
            return result with { CleanupFailed = cleanupPending };
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            // Retry outstanding releases even on a repeated Dispose call.
            ReleaseAllKeys();
        }
    }

    private void WriteLog(string message)
    {
        try { log?.Invoke(message); }
        catch { /* Diagnostics must never interrupt a release. */ }
    }
}
