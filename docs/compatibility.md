# Milestone 2 verification and compatibility

## Automated results — 12 October 2026

Host: Windows ARM64, .NET SDK 10.0.300. x64 execution uses ARM emulation.
The user confirmed successful Milestone 1 build and manual testing, including the
non-activating overlay and SendInput mechanism. No per-application/version results
were supplied, so that confirmation is not expanded into invented compatibility claims.

| Check | Result | Establishes |
|---|---|---|
| Final `dotnet restore FloatKeys.sln` | Pass | Dependencies available |
| Complete Release solution build | Pass, 0 warnings/errors | Compilation |
| ARM64 tests | Pass, 128/128, 0 skipped | Includes all 28 unchanged Milestone 1 tests |
| Self-contained x64 tests | Pass, 128/128, 0 skipped | Same suite under ARM emulation |
| Self-contained `win-arm64` publish | Pass | Native Surface launch folder |
| Self-contained `win-x64` publish | Pass | x64 launch folder |
| Final ARM64 overlay smoke check | Pass | Styles, startup foreground preservation, graceful exit |
| Final x64 overlay smoke check | Pass | Same checks under ARM emulation |
| Offscreen main/symbols renders at 96/144/192 DPI | Pass | Key bounds and image inspection; no injected input |

Both smoke checks observed extended styles `0x08000088`, no APPWINDOW bit and the
same foreground HWND before/after startup. They do not press keys or simulate touch.
x64 emulation is not validation on physical x64 hardware.

The 100 added cases cover QWERTY and all UK punctuation pairs, physical special-key
flags, one-shot Shift, local Caps XOR rules, Ctrl/Alt latching and chords, every partial
prefix of a three-modifier chord, borrowed external modifiers, failed cleanup blocking,
transport failure, repeat delay/interval, stale callbacks, independent concurrent sessions,
layout changes, cancellation, closure, rapid taps and device-neutral duplicate filtering.
WPF routed touch promotion itself still needs physical validation.

The offscreen render probe uses a backend that throws if asked to inject input. Both
layouts contain 37 controls including header keys. Measured main key surfaces are 54 DIP
high, header controls 46 DIP high, and the narrowest symbols keys about 61.8 DIP wide.
Images at 150% and 200% were inspected. These renders do not reproduce a physical
Windows display-scaling change or prove touchscreen usability. The probe and images
are ignored artifacts, not a product dependency.

The restricted sandbox's initial restore/test invocations exited without diagnostics.
Restore, build, tests, rendering, publishes and smoke checks succeeded using approved
execution outside that sandbox. No unresolved build or dependency blocker remains.

## Manual results — updated keyboard pending

| Target | Mode | Milestone 2 result |
|---|---|---|
| Notepad | Local ordinary window | Not physically tested |
| Browser field | Local ordinary window | Not physically tested |
| Moonlight remote editor | Windowed | Not tested |
| Moonlight remote editor | Fullscreen | Not tested |
| WoW Forever focused chat | Borderless windowed | Not tested |
| Surface touchscreen | 150% / 200%, landscape | Not physically tested |

## Manual acceptance checklist

Use one FloatKeys process, run as an ordinary user and select UK English on both local
and remote machines. Turn physical Caps Lock off and release physical modifiers first.
Repeat with mouse and touch at 150% and 200% Windows scaling. Focus the target yourself;
launching through Explorer may require refocusing the target before the first tap.

1. **Notepad typing and focus:** focus a new document and tap `hello world`, Enter,
   then more letters. Each ordinary tap produces one character on release; a long
   letter hold produces one character. The caret remains in Notepad. Test rapid taps
   and two fingers on different letters. Release order determines character order.
2. **Case:** Shift → A gives `A`, then A gives `a`. Caps → A → A gives `AA`.
   With Caps active, Shift → A → A gives `aA`. Disable Caps and verify physical
   keyboard Caps state was never toggled. Active modifier keys must visibly highlight.
3. **UK punctuation:** Symbols shows digits and punctuation. Check every pair:
   `1/!`, `2/"`, `3/£`, `4/$`, `5/%`, `6/^`, `7/&`, `8/*`, `9/(`, `0/)`,
   backtick/`¬`, `-/_`, `=/+`, `[/ {`, `]/}`, `;/:`, apostrophe/`@`, `#/~`,
   backslash/`|`, `,/<`, `./>` and `//?`. Tap Shift before each shifted symbol;
   verify the displayed label and one-shot reset. Caps alone must not shift punctuation.
4. **Shortcuts:** type a harmless line, then Ctrl → A selects all, Ctrl → C copies,
   Right clears selection, Enter adds a line and Ctrl → V pastes. Confirm Ctrl clears
   after each action. With local Caps active, Ctrl+C/V must not gain Shift. Alt →
   Symbols → Tab should perform the application's normal Alt+Tab behaviour and clear Alt.
   This shortcut intentionally changes the active app; refocus the test field afterward.
5. **Special keys:** test Space, Enter, Backspace, Delete, Tab, Escape and all arrows
   in suitable fields. Shift+arrows should extend selection and Shift+Tab move backward
   through focusable fields. Target application behaviour determines the outcome.
6. **Repeat:** hold Backspace/Delete/arrows. Expect one immediate action, a short delay,
   then consistent repetition. Release must stop immediately with no extra release action.
   Hold Shift+Left to extend selection. Later modifier taps must not alter that repeat's
   snapshot. Try two independent repeatable keys and release them separately.
7. **Cancellation:** start an ordinary press, move outside, return and release: no input.
   For a repeating key, move outside: already sent actions remain, but repetition stops.
   Interrupt capture with a system gesture where possible. Switch layouts during a hold;
   no old repeats/taps may continue. Test rapid touch taps for duplicate characters.
8. **Closure:** latch Ctrl/Alt/Shift and press Exit; normal physical typing must work.
   Also close the window via the system/WM_CLOSE while a repeat is active, then verify
   no continuing edits or stuck modifiers. Check the log if cleanup reports failure.
9. **Moonlight:** focus remote Notepad manually and repeat typing, symbols, shortcuts
   and repeat tests. Verify edits on the remote machine. Test windowed and fullscreen
   separately and record overlay visibility, local focus and forwarding. If input fails,
   record client/host versions and capture settings; compare physical keyboard input.
10. **WoW Forever:** manually focus an empty chat field in a harmless private context.
    Test letters, punctuation, deletion and cursor movement. Enter normally submits and
    closes chat; reopen it manually before further tests. FloatKeys never opens chat.
    Record client build, window mode and whether ordinary physical typing succeeds.

For each target record Windows build, app version, client/process architecture, input
layout, physical modifier/Caps state, pointer type, scaling, mode and Pass/Fail. Focus-loss
signs include an unexpectedly inactive target, disappearing caret, premature chat closure
or physical typing going elsewhere. Matching foreground HWNDs alone do not prove child
field focus or input consumption.

For failures collect the exact sequence and `%LOCALAPPDATA%\FloatKeys\prototype.log`
(and `.previous`), noting visibility versus focus loss versus duplicates versus no delivery.
Record native counts/errors and cleanup failures. Native error zero does not exclude UIPI
or application rejection. Do not add application-specific workarounds to disguise failures.

## Limitations and specification interpretation

- Visible keys assume UK layout. Non-UK layouts and physical Shift/Caps can change output;
  remote state is not inspected or synchronized. Local Caps is not a Windows Caps toggle.
- Synthetic input may be rejected by elevated, protected/raw-input or streaming targets.
  Secure desktop/UAC/authentication screens are outside scope. No bypass or elevation is used.
- Exclusive fullscreen or another topmost window may obscure the overlay.
- Physical keyboard state inspection is a snapshot and can race with real input.
  Forced termination, power loss or rejected releases cannot guarantee cleanup.
- Fixed placement and size remain; no display-disconnection recovery, launcher, tray,
  resizing, persistent preferences or packaging system was introduced.
- Repeat timing uses UI dispatcher scheduling: a busy dispatcher can delay a pulse;
  it will not emit a catch-up burst. Existing synchronous logging was retained and
  has not been benchmarked on a physical Surface touchscreen.
- Caps Lock local capitalisation is the approved deviation from a native system Caps
  Lock event. All other visible input keys use real balanced physical events.
