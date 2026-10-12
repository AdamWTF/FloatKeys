# Milestone 4 verification — 12 October 2026

Host: Windows ARM64 with .NET SDK 10.0.300. x64 execution uses Windows ARM
emulation and does not establish compatibility on physical x64 hardware.

| Check | Result | What it establishes |
|---|---|---|
| Locked solution restore | Pass | Committed dependency graph restores |
| Release solution build | Pass, 0 warnings/errors | Compilation |
| ARM64 automated suite | Pass, 183/183, 0 skipped | Existing keyboard tests plus Windows integration tests |
| Self-contained x64 automated suite | Pass, 183/183, 0 skipped | Same checks under ARM emulation |
| win-arm64 portable publish | Pass | Self-contained single executable |
| win-x64 portable publish | Pass | Self-contained single executable |
| Native runtime packs and bundle inspection | Pass, both architectures | Correct PE architecture; required WPF native libraries bundled |
| Archive names/content/SHA-256 | Pass | Two independent v0.1.0 archives, checksums verified |
| Final archive extraction/startup | Pass, both architectures | Extracted release executables launch, reject duplicates and terminate cleanly |
| ARM64 published executable smoke | Pass | Non-activation, second-instance rejection, close-to-hide, session shutdown/process termination |
| x64 published executable smoke | Pass under emulation | Same checks |
| ARM64/x64 isolated STA probes | Pass | 50 visibility cycles, retained HWNDs, tray object lifetime, Settings/Exit routing, native closure/restoration |
| Simulated TaskbarCreated | Pass, both probes | Framework tray re-registration callback; no actual Explorer restart |
| Workflow syntax | Pass, actionlint 1.7.12 | CI and release YAML/action inputs checked locally |
| PowerShell syntax and icon generation | Pass | Scripts parse; ICO regeneration has identical SHA-256 |

The smoke script sends session messages only to HWNDs belonging to its test process;
it does not sign out Windows or send messages to other applications. The isolated
probe's input backend cannot inject input, and its startup adapter cannot modify the
registry. Startup tests use fake storage. Actual reboot startup registration and tray
interaction with touch remain manual checks. Automated exit uses session notifications;
Tray Exit routing and keyboard minimise cleanup are tested separately through the command/controller paths.

Verification fixed session-query cleanup ordering, addressed session messages to WPF's
hidden application HWND, and qualified icon resources with their owning assembly.
An added shutdown test initially reused an active pointer ID; the test setup was corrected
and both suites rerun successfully. Bundle validation accounts for the Windows runtime's
statically linked CoreCLR/JIT rather than requiring separate embedded coreclr/clrjit files.
Initial failed smoke processes were cleaned up; final checks leave no FloatKeys process.

Build/test/publish/native checks used approved execution outside the restricted sandbox,
whose SDK invocations can fail without diagnostics. No test input was submitted to a target
application. The workflows have not run on GitHub, and no release/tag has been created.
This checkout currently has no Git remote; publication requires configuring its repository.

## Surface Pro 11 manual acceptance — pending

Extract the complete FloatKeys-v0.1.0-win-arm64.zip archive into a permanent folder
and run FloatKeys.exe as an ordinary user. Record Windows build, architecture,
orientation, scaling, target application/client version, input layout and Pass/Fail.
Start with physical modifiers released and UK English selected locally/remotely.

1. Confirm the launcher appears, the keyboard starts hidden and the FloatKeys tray
   icon exists, including notification-area overflow if Windows hides it there.
2. From the tray, show/hide the keyboard repeatedly. Restore it after native close.
   Focus Notepad before tapping the launcher; confirm the target caret stays usable.
3. Hide/restore the launcher from the tray with the keyboard open and hidden. Hide
   both overlays and verify the tray remains available and can restore either.
4. Open Settings from the tray. Confirm the existing inline panel opens, covered keys
   cannot type, and size/opacity changes still persist without activating the keyboard.
5. Enable Start with Windows. Restart Windows; confirm exactly one launcher/tray icon
   appears, the keyboard is hidden and FloatKeys does not automatically take focus.
6. Disable Start with Windows and restart again. Confirm FloatKeys does not launch.
   Enable again only if desired. If Windows Startup Apps has independently disabled
   the entry, restore its policy deliberately; the tray does not override that policy.
7. With startup enabled, move the portable folder and run the new copy once. Restart
   and confirm the new path works. Removing the Run entry externally should stay disabled.
8. Launch a second copy (also try another extracted folder). Confirm the first stays
   running with one launcher, one keyboard and one tray icon.
9. Type in Notepad with mouse and touch: letters, UK symbols, Shift/local Caps,
   Ctrl/Alt combinations, arrows and held Backspace. Verify the focused field receives
   input and there is no focus-stealing or duplicate touch regression.
10. In Moonlight, manually focus remote Notepad and test typing/shortcuts/repeats.
    Record windowed and fullscreen results separately, including remote delivery and
    local focus/overlay visibility. No compatibility claim is made until tested.
11. In WoW Forever, manually focus chat in a harmless context and test typing,
    punctuation, deletion/navigation and Enter. Record mode and client version.
12. Repeat keyboard close/reopen at least 50 times. Cancel touch/drag captures,
    hide during repeat and test display rotation/scaling. Existing movement, sizing,
    recovery and touch behavior must continue working.
13. Restart Windows Explorer through Task Manager while FloatKeys runs. Confirm
    one usable tray icon returns, including when both overlays were hidden.
14. Latch modifiers and start a repeat, then use **Exit FloatKeys** in the tray.
    Confirm both windows disappear and Task Manager shows no FloatKeys process.
    Relaunch, latch modifiers and start a repeat, then minimise using the keyboard minus
    button. Confirm the launcher and tray remain available and can restore the keyboard.
    Normal physical typing must work afterward, with no continuing repeat or logically
    held simulated modifiers/keys. Neither keyboard layout should expose an Exit button.
15. Relaunch and confirm positions, dimensions, opacity, Symbols selection and startup
    configuration persist; launcher is visible and keyboard hidden. Review logs for
    cleanup/settings failures. Repeat final exit with both overlays hidden.

## Remaining limitations and deviations

Physical Surface touch, real Explorer restart, reboot startup, mixed-DPI/display changes,
Moonlight and WoW Forever acceptance remain pending. x64 hardware testing and hosted
GitHub workflow execution are also pending. Synthetic input can be rejected by protected,
elevated or raw-input targets; exclusive fullscreen can hide overlays. Secure desktop,
UAC and protected authentication screens remain unsupported.

Windows Startup Apps policy can independently disable a registered Run entry; FloatKeys
does not bypass it. Portable relocation requires running the new copy once; an executable
cannot repair its old path while it is not running. Mutex ownership does not guarantee
input cleanup after a forced kill, power loss or rejected native releases.

Single-file packages are unsigned and require writable temporary storage for WPF native
extraction. There is no installer or signing service. No new Milestone 4 functional
deviation is known. The pre-existing approved local Caps interpretation remains.

The following Milestone 3/2 records are historical; their prototype logging, native-close
exit semantics, old publish locations and deferred Milestone 4 statements are superseded.

# Milestone 3 verification and Surface acceptance

## Automated verification — 12 October 2026

Host: Windows 11 ARM64, .NET SDK 10.0.300. x64 execution uses ARM emulation.
Milestone 1 manual success was previously confirmed by the user. Updated Milestone 3
physical touch and application compatibility have not been tested and are not claimed.

| Check | Result | What it establishes |
|---|---|---|
| Dependency restore | Pass | Solution dependencies available |
| Complete Release solution build | Pass, zero warnings/errors | Compilation |
| ARM64 xUnit suite | Pass, 161/161, zero skipped | Original 128 cases plus 33 overlay/settings cases |
| Self-contained x64 xUnit suite | Pass, 161/161, zero skipped | Same suite under ARM emulation |
| win-arm64 and win-x64 builds/publishes | Pass | Both architecture launch folders |
| ARM64 native startup/shutdown smoke | Pass | Both HWND styles, launcher-only startup, same foreground, graceful exit |
| x64 native startup/shutdown smoke | Pass | Same checks under emulation |
| ARM64 and x64 lifecycle probes | Pass | 50 show/hide cycles per architecture, same HWND, retained bounds, unchanged foreground |
| Geometry and settings integration probe | Pass | Service-driven launcher tap/drag, header drag/resize, native minimum, collapse, settings panel and persisted values |
| Settings write checks | Pass | No writes for unchanged snapshots or ordinary show/hide; atomic replacement and failure retry |
| Main/symbols renders | Pass | 840x360, 720x360, 1000x480 DIPs; 96/144/192-DPI output |
| Settings, launcher and small-display renders | Pass | Visual inspection, usable targets, scroll fallback |
| git diff --check | Pass | No whitespace errors |

Both windows retain NOACTIVATE, TOOLWINDOW and TOPMOST (mask 0x08000088), without
APPWINDOW. WPF transparency also adds the LAYERED bit, giving observed styles 0x08080088.
The lifecycle probe uses a backend that throws on any input-injection request and an isolated
settings path. It drives service callbacks and header actions rather than physical pointer
messages. Genuine WM_POINTERACTIVATE delivery needs real touch; its response policy is
covered by xUnit. Posted mouse messages were unsuitable for exercising WPF's real pointer
coordinate path and are not used as evidence of touchscreen behavior.

Key/control targets measure at least 50x46 DIPs at tested dimensions. The narrowest symbols
row keys at minimum width are about 51.8 DIPs wide. Rendering at higher output DPI is an
image/layout check, not a physical Windows scaling change. The ordinary minimum is 720x360;
a smaller display uses a scrollable viewport with retained content dimensions and 44-DIP
scrollbars. Renderer/probe output is ignored under artifacts and is not shipped in the app.

Initial sandbox .NET commands could fail without diagnostics; approved execution outside
the sandbox was used. Review fixed shutdown snapshots after HWND destruction and guarded
z-order reentrancy. No unresolved automated failure remains. Physical x64 hardware, actual
mixed-DPI monitor dragging, touchscreen capture, rotation and target input still need testing.

## Run and verification commands

Run as an ordinary user from the repository root:

```powershell
.\artifacts\win-arm64\FloatKeys.exe
# Secondary architecture:
.\artifacts\win-x64\FloatKeys.exe
```

Copy the entire architecture folder to another device; the existing self-contained publish
workflow needs no separately installed runtime. These are local validation outputs, not a
new installer or release-archive pipeline. Source execution is also available with
`dotnet run --project src/FloatKeys/FloatKeys.csproj` on a Windows machine with .NET 10 SDK.

The launcher is always present; tap it to show/hide the keyboard. Header minus collapses,
gear toggles launcher settings, and tray Exit shuts down both windows. Saved positions and sizes
are reused. Focus the target field yourself. Settings live at
`%LOCALAPPDATA%\FloatKeys\settings.json`; errors go to the existing `prototype.log`.

Exact restore/build/test/publish and optional architecture-probe commands are in README.
Keep the desktop idle while the foreground smoke check runs. The optional probe opens
non-activating test windows briefly, writes isolated settings under artifacts, and creates
rendered PNGs. It neither types into applications nor moves the real mouse pointer.

## Manual Surface Pro 11 checklist — pending

Use one process, normal privileges, UK English on local/remote systems, physical Caps off,
and released physical modifiers. Repeat pointer checks with mouse and touch at 150% and
200% scaling. Record Windows build, app/game/client version, architecture, screen orientation,
monitor arrangement, input layout and Pass/Fail. Match both target foreground and its caret;
a stable top-level HWND alone does not prove child-field focus or input consumption.

1. **Launcher:** start with only the icon visible. Drag with touch/mouse in all directions,
   including display edges. Small movement below 8 DIPs toggles only on release; a drag
   never toggles, even after returning to its origin. Test cancelled captures and rapid taps.
2. **Expand/collapse:** focus Notepad, tap launcher, type, tap launcher again, then reopen.
   Try the header minus button. Launcher remains visible, clickable and above an overlapping
   keyboard; neither window unexpectedly moves. The target stays active throughout.
3. **Move/resize:** drag only the header grip; resize with the diagonal bottom-right handle.
   Keys never move the window. Verify the minimum size and all essential keys remain usable.
   Move between external displays with different scaling; the pointer should continue dragging
   across DPI boundaries. Test release/capture interruption and check persistence afterward.
4. **Settings:** gear opens the inline panel and covered keys cannot type. Step launcher size
   and opacity to both limits; the icon remains clear. Close settings, type again and restart
   to confirm values. Settings changes must not take foreground focus.
5. **Notepad:** type letters, UK symbols, spaces, Enter and navigation. Test Shift, local Caps,
   Ctrl+A/C/V and Alt shortcuts in harmless contexts. Caps must not toggle system Caps state.
   Hold Backspace/arrows, then collapse while held: repeats stop and normal physical typing
   works afterward. Latches clear on hiding; local Caps and ABC/Symbols remain on reopening.
6. **Moonlight:** manually focus remote Notepad and repeat typing, punctuation, shortcuts and
   repeats. Check remote results in windowed and fullscreen modes separately. Record overlay
   visibility, local focus and forwarding. Compare physical keyboard input if delivery fails;
   record capture settings and client/host versions without adding app-specific workarounds.
7. **WoW Forever:** manually focus an empty chat field in a harmless context. Test letters,
   symbols, deletion and navigation, then Enter. Reopen chat yourself when needed. Check
   borderless and fullscreen modes, input delivery and whether chat focus changes after
   opening, dragging, resizing or collapsing FloatKeys.
8. **Rotation/resolution:** rotate landscape to portrait with keyboard open and hidden.
   Change resolution. Both windows remain reachable; invalid bounds recover into an available
   working area. The keyboard retains large keys. No application is resized or work area reserved.
9. **Scaling/monitors:** change 150% to 200% and back. Connect/disconnect/reconnect external
   displays, including one left of the primary monitor. Hidden keyboard bounds also recover.
   Ordinary app switching must not move overlays. Intentional full-screen placement is retained
   while valid. On an unusually small display, verify touch scrolling reaches every key/control.
10. **Persistence:** move both windows, resize keyboard, change settings and switch to Symbols.
    Use tray Exit and relaunch. Positions, dimensions and preferences return; keyboard starts hidden.
    With the app stopped, back up settings and test missing/corrupted JSON; defaults recover.
11. **Stress/exit:** show/hide 50 times while the target is focused. Cancel touches and hide
    during repeat/modifier interactions. There must be no continuing edits, stuck modifiers,
    duplicate taps or focus loss. Minimise through the header and close each native window;
    the process and tray stay available. Restore through the tray, then use tray Exit;
    both windows and the process disappear without changing target focus.

## Remaining risks and deviations

- Physical Surface, Moonlight and WoW Forever acceptance is pending. Synthetic input can be
  rejected by elevated/protected/raw-input targets. Secure desktop and UAC are unsupported.
- Exclusive fullscreen and other topmost applications may obscure overlays. There is no
  invasive hook, elevation or timer-based topmost enforcement.
- Real mixed-DPI transitions and monitor hot-plug recovery need physical validation; pure
  geometry and current-monitor native positioning are tested. Floating-point/physical rounding
  can differ by a pixel. A recovered location is retained after reconnection to avoid surprise.
- Settings save failures keep the utility usable; persistence retries at a later save boundary.
  Forced termination/power loss or rejected native key releases cannot guarantee cleanup.
- Exceptionally small displays use scrolling rather than shrinking touch targets; this is the
  planned fallback, not a claim that every key fits simultaneously on every resolution.
- Local Caps capitalization remains the approved Milestone 2 deviation from system Caps Lock.
  No other functional specification deviation is known. Milestone 4 remains entirely deferred.

The following Milestone 2 record is historical. Its fixed-window limitations are superseded
by the Milestone 3 implementation and verification above.

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
8. **Closure:** latch Ctrl/Alt/Shift and minimise; normal physical typing must work.
   Also close the window via the system/WM_CLOSE while a repeat is active, then verify
   no continuing edits or stuck modifiers. Restore from the tray, then use tray Exit
   to close the app. Check the log if cleanup reports failure.
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
