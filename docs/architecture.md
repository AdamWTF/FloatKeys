# Milestone 2 architecture

## Preserved Milestone 1 boundary

`App` owns `SendInputBackend` and `OverlayService`; the overlay owns one keyboard window.
The native non-activation implementation is retained unchanged: initialize the invisible
HWND, install the hook and apply styles before Show; use WS_EX_NOACTIVATE/TOOLWINDOW,
HWND_TOPMOST with SWP_NOACTIVATE, and MA_NOACTIVATE/PA_NOACTIVATE without discarding input.
WPF retains ShowActivated=false, ShowInTaskbar=false and non-focusable controls.
There are no foreground-restoration calls, target-app dependencies or activating dialogs.

The asInvoker and PerMonitorV2 manifest, .NET 10 target and architecture-safe native union
are unchanged. INPUT remains 40 bytes with its union at offset 8 on ARM64/x64, KEYBDINPUT
24 bytes, and pointer-sized handles/ULONG_PTR fields. Existing interop ABI tests remain.

## Layout, controller and controls

`KeyboardLayout` defines read-only rows of immutable key records: identity, display label,
logical action, scan-code mapping, alternate shifted label/mapping, letter/repeat flags,
style and relative width. It includes UK ISO backslash (0x56), UK hash (0x2B), apostrophe/@
and 2/double-quote. The symbols page adds Tab, Escape, Delete, Up and Down. Caps and Exit
are header controls. All input keys use physical scan codes; Unicode is not a fallback.

`KeyboardController` is independent of WPF. It owns logical latches, local Caps,
layout selection and pointer-key sessions. Native key ownership remains exclusively in
`KeyboardState`. Modifier taps send no input. The next input snapshots and consumes
Shift/Ctrl/Alt, then asks the backend for a complete chord. Caps XOR Shift affects letters;
Caps does not add Shift to Ctrl/Alt combinations or punctuation. Labels follow that same
rule. Physical and remote keyboard state remains outside controller ownership.

`KeyboardWindow` builds controls from row definitions into weighted Grid columns.
It updates labels/active colours in place. Layout changes first stop all controller
sessions, then cancel captures and rebuild controls. Latches and Caps survive switching.
The opaque 840 x 360 DIP window uses four 60 DIP rows (54 DIP key surfaces), 6 DIP gaps,
rounded keys, frozen brushes and no animations. Header controls have 46 DIP surfaces.
Startup placement remains the existing bottom-right policy. No later-milestone movement,
resizing or screen-recovery system was added.

`KeyboardKey` retains handled touch events, initiating-device capture and promoted mouse
filtering. `PointerSession` centralizes device-neutral ownership and duplicate filtering.
Only an owner can release/cancel its gesture. Ownership clears before capture release to
avoid reentrant lost-capture cancellation. Mouse and independent touch devices share the
controller contract. Non-repeating keys commit on release; leaving cancels permanently.

## Chords, repeats and interruption

`IInputBackend.KeyPressChord` adds one operation to the existing interface. It validates
allowed modifiers, deduplicates them, rejects a held primary and borrows externally held
modifiers without releasing them. One SendInput batch contains modifier downs, primary
down/up, then reverse-order modifier ups. The existing serialized accepted-prefix tracking,
conservative exception ownership, compensating releases and failed-cleanup blocking apply.
The backend still exposes independent KeyDown/KeyUp/KeyPress/SendUnicodeText/ReleaseAllKeys.

Repeatable keys emit one complete chord after successful capture, then repeat after
450 ms every 60 ms. The session snapshots its modifiers once; later modifier taps affect
the next input rather than the ongoing repeat. Every pulse releases all injected keys.
`IRepeatScheduler` allows deterministic tests; `DispatcherRepeatScheduler` uses disposable
one-shot dispatcher timers. Session identity and callback generation reject obsolete ticks.
There are no catch-up bursts, sleeps, background input loops or held native keys between
pulses. Simultaneous sessions are serialized by the UI dispatcher and backend lock.

Leaving bounds, capture loss or cancellation stops that session and clears one-shot
latches. Cleanup failure stops every remaining session. Input failure also cancels all
sessions/captures and preserves the original error with final cleanup status. Closure,
unloading, handled dispatcher exceptions and app exit cancel interactions and retry owned
key releases. Caps persists only for the lifetime of the controller. Rejected releases,
forced termination and physical-input races cannot be guaranteed safe.

The existing bounded `PrototypeLog` remains, including its filename, architecture/error
logging and foreground observations. It contains no target text. Log writes and native
submission remain synchronous and short; their existing I/O cost has not been benchmarked
on a physical touchscreen. No sleeps or waits occur in gesture/repeat handlers.

## Files created and modified

Created:

- `src/FloatKeys/Input/KeyboardLayout.cs`
- `src/FloatKeys/Input/KeyboardController.cs`
- `src/FloatKeys/Input/PointerSession.cs`
- `src/FloatKeys/Services/DispatcherRepeatScheduler.cs`
- `tests/FloatKeys.Tests/KeyboardTests.cs`
- `tests/FloatKeys.Tests/ChordTests.cs`

Modified:

- `src/FloatKeys/Input/IInputBackend.cs`, `SendInputBackend.cs`
- `src/FloatKeys/Controls/KeyboardKey.xaml.cs`
- `src/FloatKeys/Windows/KeyboardWindow.xaml`, `KeyboardWindow.xaml.cs`
- `src/FloatKeys/App.xaml.cs`, `src/FloatKeys/Services/OverlayService.cs` (interruption cleanup)
- `README.md`, `docs/architecture.md`, `docs/compatibility.md`

Platform P/Invokes, window styles, manifests, original tests, specification and project
dependencies remain unchanged. `TapGesture` remains for the original prototype tests;
production controls now use `PointerSession` and the controller session lifecycle.
Generated `bin`, `obj`, publishes and the offscreen UI probe remain ignored under artifacts.
Existing unrelated uncommitted work was retained.

## Review and interpretation

Review followed the code-review skill, covering non-activation, routed-event admission,
capture release reentrancy, timer invalidation, partial chord submissions, external
modifiers, exception shutdown and cleanup blocking. Review fixes align labels with Caps
shortcut rules, reject obsolete ticks and stop all sessions when cleanup fails.
No remaining significant static findings were identified; physical UI behaviour remains
subject to manual testing.

The approved Caps interpretation is local capitalisation, not a genuine native Caps Lock
toggle. This is the deliberate deviation from reading the special-key requirement as a
system Caps Lock event. No other planned functional deviations or later milestones were added.

References: [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput),
[KEYBDINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput),
[WPF input overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/input-overview).
