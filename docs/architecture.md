# Architecture

FloatKeys is a Windows WPF application with a Windows Forms tray icon. UI,
keyboard state, native input, and platform services have separate responsibilities.
Production code has no third-party package dependencies; xUnit and the test SDK
are development dependencies.

## Lifecycle and overlays

`App` acquires a user/session mutex before creating resources. Secondary launches
exit quietly. `OverlayService` owns one launcher, keyboard, and controller for the
application's lifetime; visibility changes reuse their HWNDs. Startup shows only
the launcher. Native closure hides the affected overlay; tray Exit shuts down the app.

Both windows use NOACTIVATE, TOOLWINDOW, and TOPMOST styles, reject mouse/pointer
activation, and apply `SWP_NOACTIVATE` to native positioning. Ordering keeps the
visible launcher above the keyboard without taking foreground focus. There is no
foreground restoration, target application detection, or work-area reservation.

`ShutdownCoordinator` runs cleanup stages once, continuing after individual failures.
Overlay cleanup cancels gestures and repeats, clears modifier latches, saves settings,
and detaches hooks. Input cleanup releases owned keys; tray and mutex resources
are disposed afterward. Session-ending queries prepare input/settings while retaining
windows because another application may cancel sign-out.

## Keyboard and input

`KeyboardLayout` defines UK scan-code mappings for Chat, Compact, and Symbols.
`KeyboardController` owns layout selection, pointer sessions, local Caps, modifier
latches, and repeat scheduling. Selecting a layout cancels pending sessions and
releases owned input while preserving Caps and latches. Hide clears one-shot latches.
Dedicated punctuation records its required UK Shift independently of the Shift latch.

`KeyboardKey` captures one initiating pointer and rejects promoted mouse input.
Ordinary taps commit on release inside the original key; repeatable keys send
immediately and repeat through the dispatcher scheduler. Leaving a key, losing capture,
or cancelling a session stops further input.

`SendInputBackend` submits balanced chords through injectable `IInputTransport`.
It tracks input it owns, borrows already-held physical modifiers, and attempts cleanup
after partial submissions. Cleanup failures block further unsafe input until recovery.
Forced termination or rejected native releases cannot guarantee key cleanup.
Visible keys use physical events; Unicode entry is a separate backend capability.

## Geometry and persistence

`PointerHandle` and `MoveGesture` distinguish launcher taps from drags after an
8-DIP threshold. Desktop positions use physical pixels; visual sizes use DIPs.
`DisplayService` enumerates monitor/work rectangles and effective DPI, including
negative desktop origins. `OverlayGeometry` preserves valid bounds and recovers
offscreen windows after display changes. Recovery notifications coalesce on the dispatcher.

`KeyboardSizing` supplies shared size limits: Chat defaults to 840 × 480 DIPs
(minimum 720 × 432); Compact defaults to 840 × 420 (minimum 720 × 382).
Symbols uses the five-row minimum. Weighted grids retain 44 × 44 DIP minimum
key targets, with scrolling when a display cannot fit the content.

`SettingsService` validates fields independently and writes flushed temporary files
before atomic replacement. Equal validated snapshots avoid writes. It persists
positions, dimensions, appearance, `KeyboardLayout` (`chat`/`compact`), Symbols,
and startup preference. Missing/invalid layouts default to Chat. Caps, modifier
latches, input history, and visibility are not persisted.

## Windows integration and maintenance

`TrayService` owns the NotifyIcon on the WPF dispatcher; .NET restores it after
Explorer restarts. `TrayController` routes commands through testable interfaces.
`StartupService` reconciles a quoted executable path in HKCU's Run key, respecting
external removal and leaving unexpected entries intact. `AppLog` records bounded
lifecycle/failure diagnostics with one rotated file, without key identities or text.

The committed icon is reproducible using `scripts/generate-icon.ps1`.
Packaging validates versions, PE architectures, bundled WPF native libraries,
runtime notices, archives, and checksums. Trimming is disabled.
The isolated overlay probe uses fake input/startup stores and temporary settings;
it tests real windows and rendering without proving physical input delivery.
See [compatibility](compatibility.md) and [releases](releases.md).
