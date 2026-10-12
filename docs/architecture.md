# Milestone 4 architecture

`App` acquires a Local-session named mutex keyed by the current user's SID before
creating any overlays, input or tray resources. Secondary launches exit quietly.
The mutex is held on the UI thread, handles abandoned ownership and is released
only after application cleanup. There is no IPC or process termination policy.

One `TrayService` owns a Windows Forms NotifyIcon, ContextMenuStrip and embedded
icon on the existing WPF dispatcher. .NET's NotifyIcon restores itself when Explorer
broadcasts TaskbarCreated. There are no polling timers or background message loops.
TrayController provides testable routing through IOverlayCommands; StartupService
uses an injectable IStartupStore. These are internal interfaces, not public APIs.

OverlayService still owns one launcher, keyboard and controller for its lifetime.
Keyboard and launcher visibility are independent. Normal native closure is intercepted
before controller disposal and hides only that window. Hidden launchers are excluded
from ordering. Settings opens the existing inline panel. Existing header Exit and tray
Exit share the application lifetime coordinator. All native non-activation styles,
SWP_NOACTIVATE flags, touch routing, geometry and key mappings remain intact.

ShutdownCoordinator runs guarded cleanup stages once. Overlay cleanup cancels gestures,
repeats and latches, saves a final snapshot before destroying HWNDs and detaches hooks;
backend disposal retries owned key releases; tray resources/subscriptions and instance
ownership are then disposed. An error in one stage is logged without skipping later
stages. Session-ending queries cancel input and save while retaining HWNDs/ownership,
since another app can cancel sign-out. Confirmed session termination runs final cleanup.

Startup uses only HKCU's Run key, value FloatKeys, containing the quoted current
FloatKeys.exe path. No arguments, service, task or elevation are used. Missing entries
disable the saved preference; valid existing entries are adopted/repaired after relocation.
Unexpected entries are left intact. Menu opening reconciles Windows registration;
Windows Startup Apps policy can independently suppress that registered entry.
StartWithWindows defaults false in legacy settings and shares OverlayService's existing
settings snapshot, preventing geometry saves from reverting startup changes.

AppLog writes only lifecycle/failure diagnostics to LocalAppData/FloatKeys/logs,
rotating one previous file at approximately 1 MiB. Successful input and touch actions
are not logged. Logs contain no key identities, input text, arbitrary exception messages,
clipboard content or telemetry. Unwritable logging never prevents input or cleanup.

The executable, window and tray icon share a deterministic seven-size ICO generated
by scripts/generate-icon.ps1 from the launcher glyph. The ICO is committed, embedded
for tray use and included as a WPF resource. No graphics package is required.
SDK/version metadata and lock files are committed. The packaging script validates
native runtime-pack architecture and actual single-file manifest assets, then creates
two archives/checksums. Windows CoreCLR/JIT are statically linked into singlefilehost;
the WPF native libraries are bundled for extraction. Trimming stays disabled.

## Milestone 4 file inventory

Added:

- global.json, Directory.Build.props; application/test/probe packages.lock.json files
- Platform/SingleInstance.cs; Services/StartupService.cs, TrayController.cs,
  TrayService.cs, ShutdownCoordinator.cs, AppLog.cs; Resources/FloatKeys.ico
- scripts/generate-icon.ps1, package.ps1, verify-packages.ps1, BundleInspector.cs
- .github/workflows/ci.yml, release.yml
- tests/FloatKeys.Tests/WindowsIntegrationTests.cs
- docs/releases.md, release-notes.md, portable.txt

Modified:

- App.xaml.cs, application/test/probe project files, Models/AppSettings.cs
- Services/OverlayService.cs, SettingsService.cs; Input/SendInputBackend.cs
- KeyboardWindow XAML/code-behind and LauncherWindow XAML (icon only)
- KeyboardTests.cs; development probe and verify-overlay.ps1
- README.md, architecture.md, compatibility.md

PrototypeLog.cs is replaced by AppLog.cs. App.xaml already had explicit shutdown,
so it needs no edit. No layout, pointer-control or input-ABI redesign was performed.

The following Milestone 3/2 sections are historical. Milestone 4 supersedes their
native-close-to-exit behavior, prototype logging and deferred packaging statements.

# Milestone 3 architecture

## Overlay lifecycle and focus

`OverlayService` owns one launcher and one keyboard for the application's lifetime.
Both invisible HWNDs receive styles and activation hooks before the first show. Startup
shows only the launcher; visibility transitions reuse the keyboard/controller. Collapse
calls Hide, never Close. Exit or native closure shuts down both windows. Explicit WPF
shutdown keeps the process running when the keyboard is hidden.

The original NOACTIVATE/TOOLWINDOW/TOPMOST policy and MA_NOACTIVATE/PA_NOACTIVATE
responses remain. Every native positioning operation uses SWP_NOACTIVATE. The hook
also adds that flag to WM_WINDOWPOSCHANGING, protecting WPF visibility/DPI operations.
No Activate, Focus, DragMove, native resizing loop, foreground restoration, application
identification or work-area reservation is used. Native ordering keeps the launcher above
the keyboard without moving either. Ordering is checked before raising it to prevent
recursive z-order notifications. Other topmost/exclusive applications remain a limitation.

Hide cancels geometry/key captures, controller sessions, repeat callbacks and Shift/Ctrl/Alt
latches before hiding. It retries release of owned input, retaining existing failure blocking.
Local Caps and ABC/Symbols selection survive hiding. Only ABC/Symbols survives restarts.
WPF may destroy HWNDs before OnExit; final persistence falls back to the last valid snapshot.
Window disposal and hook removal tolerate that lifecycle and precede backend disposal.

## Gestures, display geometry and appearance

`PointerHandle` admits one initiating mouse/touch device, rejects promoted mouse input,
uses physical screen coordinates and clears ownership before releasing capture. `MoveGesture`
is independently testable: 8 DIPs of total movement permanently commits a drag; only an
uncancelled release inside below that threshold is a tap. Pointer movement changes native
bounds; one completion/cancellation saves the final position. Key gestures stay separate.

`DisplayService` enumerates native monitor/work rectangles and effective DPI. Desktop X/Y
remain physical pixels, including negative origins; visual sizes remain DIPs. `OverlayGeometry`
selects displays by intersection/distance, preserves valid bounds and recovers invalid bounds
into the nearest working area. Defaults use the primary working area. Intentional movement
uses full monitor bounds, including ordinary fullscreen content. Rotation, disconnection,
work-area and DPI notifications coalesce on the dispatcher without timers/polling. WPF owns
DPI layout updates; recovery runs afterward. Desktop changes cancel interactions; crossing
a DPI boundary retains a valid geometry gesture. No normal foreground-switch handler exists.

The keyboard uses stretching rows, weighted columns, rounded charcoal surfaces, modifier
highlights and a dedicated header/resize target. Normal minimum dimensions are 720 x 360
DIPs; content retains that minimum inside a scrollable viewport if the display is smaller.
Scrollbars have 44-DIP minimum touch targets. The circular launcher defaults to 56 DIPs
with 85% background opacity; its vector icon stays opaque. The keyboard's gear opens an
inline panel with size and opacity step buttons, disables covered keys and cancels input.
No third settings window, appearance editor, animation or rendering dependency was added.

## Settings and test boundaries

`AppSettings` contains explicitly named physical positions, DIP dimensions, opacity,
UK layout identity, ABC/Symbols state and the fixed dark theme. SettingsService uses
System.Text.Json at `%LOCALAPPDATA%\FloatKeys\settings.json`, validates fields independently,
and defaults missing/corrupt documents. Temporary files are flushed then replaced/renamed
on the same filesystem. I/O failure is logged without blocking the utility; failed saves
can be retried on the next save boundary. Equal validated snapshots cause no file writes.
No input text, clipboard, history, Caps, modifier latches or visibility is persisted.

xUnit covers pure geometry, gesture, persistence, visibility and controller cleanup.
The optional development probe uses the existing test-only internals grant, an isolated
settings file and a backend that throws on input injection. It checks real HWNDs, 50
visibility cycles, native movement/resizing, ordering, unchanged settings write counts,
foreground preservation and renders. Callback driving tests service integration; it does
not reproduce WPF physical touch routing, real display rotation or target-app consumption.

## Milestone 3 file inventory

Created:
- `src/FloatKeys/Windows/LauncherWindow.xaml`, `LauncherWindow.xaml.cs`
- `src/FloatKeys/Controls/PointerHandle.cs`, `Input/MoveGesture.cs`
- `src/FloatKeys/Models/AppSettings.cs`
- `src/FloatKeys/Platform/DisplayService.cs`, `OverlayGeometry.cs`
- `src/FloatKeys/Services/SettingsService.cs`, `OverlayVisibility.cs`
- `tests/FloatKeys.Tests/PolishedOverlayTests.cs`, `SettingsTests.cs`
- `scripts/OverlayProbe/OverlayProbe.csproj`, `Program.cs`

Modified:
- App XAML/lifecycle, OverlayService, KeyboardWindow XAML/code-behind, NativeMethods
- KeyboardController (optional initial symbols selection only), KeyboardTests
- `scripts/verify-overlay.ps1`, README and architecture/compatibility documentation

The input backend, native input ABI, key mappings, key control and repeat scheduler remain
unchanged. No Milestone 4 feature was introduced. The small-display scroll fallback and
local Caps interpretation are explicit; the latter is the pre-existing approved deviation.

The following Milestone 2 record is historical; the overlay changes above supersede its
fixed-window and lack-of-persistence descriptions.

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
