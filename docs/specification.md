# FloatKeys — v0.1 Implementation Specification

**Target:** Windows 11, primarily Microsoft Surface Pro 11 (ARM64)  
**Technology:** C# / .NET 10 / WPF / Win32  
**Application type:** Portable, standalone desktop utility  
**Version:** 0.1.0

## 1. Product Overview

FloatKeys is a universal, touchscreen-friendly virtual keyboard for Windows.

The application provides a small, draggable, always-on-top launcher which opens a custom virtual keyboard. Users can interact with the keyboard without intentionally changing focus away from the underlying application.

The keyboard sends simulated keyboard events through Windows' standard input infrastructure.

FloatKeys must be independent of the application receiving input. It should work with ordinary desktop applications, games and streaming clients wherever Windows permits simulated keyboard input.

### Primary use cases

- Typing into applications on a remote gaming PC through Moonlight running on the Surface.
- Typing into ordinary Windows desktop applications, browsers, launchers and text fields.
- Accessing an on-screen keyboard without depending on automatic Windows touch keyboard activation.

**Core principle:** FloatKeys is a general-purpose input device implemented in software, not an application-specific integration.

## 2. Non-Negotiable Requirements

1. FloatKeys runs locally on the Windows device being touched.
2. The launcher stays above ordinary application windows.
3. The launcher can be freely repositioned using touch.
4. Tapping the launcher opens or closes the keyboard.
5. Opening or interacting with the keyboard must not intentionally activate FloatKeys or steal focus.
6. The keyboard overlays existing content rather than resizing other applications.
7. Every key must generate the appropriate keyboard event.
8. The application must not depend on knowing the target application's identity.
9. Keyboard appearance and location are independent of the underlying application.
10. Closing the keyboard leaves the floating launcher available.
11. The application must run without administrator privileges.
12. No game modification, DLL injection, hooking into games or WoW addons.

The application must not automatically open chat, select text fields, press Enter before typing or implement special behaviours for particular applications.

## 3. Technology and Platform

Use:

- C# with .NET 10
- WPF for the interface
- XAML for reusable UI controls
- Win32 P/Invoke for window styles and input generation
- System.Text.Json for persistence
- xUnit for automated tests
- GitHub Actions for continuous integration

Supported builds:

- `win-arm64` — primary target
- `win-x64` — secondary target

Use native Windows APIs rather than external keyboard automation packages wherever practical.

Avoid Electron, embedded browsers, backend servers, databases and unnecessary dependencies.

The application should be a single foreground-user process with multiple internal windows, not a Windows service.

## 4. Architecture

Implement the following components.

### 4.1 LauncherWindow

A small borderless WPF window containing a keyboard icon.

Responsibilities:

- Always-on-top behaviour.
- Draggable using mouse or touchscreen.
- Single tap to toggle the keyboard.
- Persist launcher position.
- Support adjustable launcher size.
- Support configurable opacity.
- Prevent movement beyond the visible desktop.
- Distinguish tapping from dragging using a movement threshold.
- Remain present when the keyboard is hidden.

Initial defaults:

- Circular launcher
- 56 device-independent pixel diameter
- 85% opacity
- Initial position near the right edge of the primary display

Use a non-activating window and suppress activation on mouse/touch interaction.

### 4.2 KeyboardWindow

A borderless, always-on-top WPF window containing the virtual keyboard.

Responsibilities:

- Open and close on launcher interaction.
- Never intentionally take foreground focus.
- Display a full QWERTY keyboard.
- Support mouse and touchscreen input.
- Support dragging through a dedicated header.
- Support resizing.
- Remember size and position.
- Render correctly at high display scaling.
- Remain within visible display bounds where possible.
- Avoid an unnecessary taskbar or Alt+Tab entry.
- Display clear touch feedback for pressed keys.

The keyboard must overlay the existing application.

Do not use Windows work-area reservation, automatic application resizing or window repositioning.

### 4.3 Input Engine

Create an abstraction for generating keyboard events.

Interface responsibilities:

- KeyDown
- KeyUp
- KeyPress
- Unicode text entry
- Modifier state management
- ReleaseAllKeys
- Error reporting

Implement `SendInputBackend` as the initial backend.

Use the Windows `SendInput` API for keyboard injection, with correct key-down and key-up sequences.

Support both virtual-key/scan-code events and Unicode input.

Use physical-key-style events as the default for ordinary keyboard keys and game/streaming compatibility. Provide Unicode events for characters requiring text-oriented input.

Unicode text injection must not be assumed to work in every game or Moonlight.

The input engine must be independent of WPF controls so input strategies can be changed without rewriting the keyboard UI.

### 4.4 Overlay Service

Owns the launcher and keyboard windows.

Responsibilities:

- Create and initialise both windows.
- Control visibility and ordering.
- Apply native non-activation window styles.
- Coordinate launcher state.
- Handle display configuration changes.
- Recover windows from invalid or disconnected displays.
- Maintain visibility when an underlying application changes.

Use `WS_EX_NOACTIVATE`, `WS_EX_TOPMOST` and `WS_EX_TOOLWINDOW` where appropriate.

Handle `WM_MOUSEACTIVATE` to prevent unintended activation.

Do not set foreground focus to a remembered window after keypresses. The overlay should preserve the current target's focus rather than repeatedly restoring it.

### 4.5 Settings Service

Persist settings in:

`%LOCALAPPDATA%\FloatKeys\settings.json`

Save:

- Launcher position and size
- Launcher opacity
- Keyboard position and dimensions
- Preferred keyboard layout
- Theme
- Startup preference
- Selected input mode, if additional modes are implemented

Use safe defaults when settings are missing or invalid.

## 5. Keyboard Layout and Behaviour

### 5.1 Standard layout

Implement an English QWERTY layout.

Rows:

**Row 1:** Q W E R T Y U I O P  
**Row 2:** A S D F G H J K L  
**Row 3:** Shift Z X C V B N M Backspace  
**Row 4:** Symbols Ctrl Alt Space Left Right Enter

Add a secondary symbols layout with digits, punctuation and common special characters.

Include support for the UK keyboard layout and its punctuation mappings. Do not assume US keyboard key positions.

### 5.2 Shift and Caps Lock

Shift changes the next character entered and returns to its previous state.

Caps Lock remains enabled until explicitly disabled.

Modifier state must be visibly indicated.

Use correct combinations of key-down and key-up events.

### 5.3 Special keys

Support:

- Backspace
- Delete
- Enter
- Tab
- Escape
- Space
- Left/Right/Up/Down
- Ctrl
- Alt
- Shift
- Caps Lock

Modifier keys should support touch-friendly toggle or latch behaviour, allowing combinations such as Ctrl+A without requiring the user to hold two touchscreen keys simultaneously.

Release modifier keys when appropriate and on application shutdown.

### 5.4 Key repeating

Support repeating for Backspace and navigation keys through press-and-hold.

Repeated keys must stop immediately when touch ends, is cancelled, or the application loses a pointer interaction.

### 5.5 Touch behaviour

- Large touch targets.
- Visual feedback on press.
- No hover dependency.
- No requirement to hold physical modifier keys.
- Tapping a key must not move the keyboard.
- Dedicated drag handles for both the launcher and keyboard.
- Touch cancellation must not leave a key logically pressed.

The keyboard must be usable in landscape and portrait orientations.

## 6. Input and Focus Management

This is the highest-risk technical requirement.

### 6.1 Focus preservation

The launcher and keyboard must use non-activating native window behaviour.

A user should be able to:

1. Focus an input field in another application.
2. Tap the floating launcher.
3. Tap a virtual key.
4. Have the target application receive input without FloatKeys becoming active.

Avoid WPF controls or interaction patterns that require keyboard focus within FloatKeys.

### 6.2 Event injection

Use Win32 `SendInput`.

Ensure:

- Events are correctly sequenced.
- Key-down events are paired with key-up events.
- Correct structure sizes and architecture-safe P/Invoke definitions.
- Partial or failed submissions are detected.
- Errors are captured without crashing the UI.
- No stuck modifier keys after cancellation or window closure.

The backend should support keyboard state inspection where useful to avoid interfering with physically pressed modifiers.

### 6.3 Known limitations

Document that synthetic input is not guaranteed to work with all games or applications.

In particular:

- Protected or exclusive input systems may reject events.
- Some applications use input processing paths that behave differently from ordinary text fields.
- Elevated target applications may reject events from an unelevated FloatKeys process.
- Certain fullscreen rendering modes may prevent the overlay from being visible.
- Windows secure desktop, UAC prompts and protected authentication screens are outside the supported scope.
- A text field must already have focus; FloatKeys does not identify or focus arbitrary text fields.

Do not use privilege escalation, invasive hooks or injection techniques to bypass these limitations.

## 7. Moonlight Compatibility

Moonlight is a primary acceptance target, not an application-specific dependency.

The expected workflow is:

1. Moonlight runs on the Surface Pro.
2. A remote gaming PC is being streamed.
3. The user focuses a remote text input field.
4. The floating FloatKeys icon remains visible above Moonlight.
5. The user opens FloatKeys.
6. FloatKeys generates local keyboard input.
7. Moonlight receives and forwards that input to the remote PC.
8. The text appears in the remote application's input field.

FloatKeys must not implement Moonlight APIs or special-case Moonlight window titles.

Test both Moonlight windowed mode and fullscreen mode.

Investigate Moonlight keyboard capture behaviour if input forwarding fails.

Do not introduce alternative drivers or invasive input mechanisms unless there is concrete evidence that the initial backend is insufficient.

A compatibility failure must be documented explicitly rather than disguised through UI workarounds.

## 8. Visual Design

Use a clean, modern, dark aesthetic appropriate for gaming.

### Launcher

- Circular or rounded-square appearance.
- Keyboard glyph.
- Semi-transparent background.
- Subtle border or shadow.
- Visible touch press state.

### Keyboard

- Dark charcoal background.
- Rounded keys.
- High-contrast labels.
- Clear distinction between normal and special keys.
- Slightly different styling for modifier keys.
- Distinctive Enter key.
- Compact header with drag handle, collapse and close controls.

Use smooth but minimal animations.

Provide sufficient spacing to avoid accidental touch input.

Do not implement predictive text, suggestions, dictionaries, autocorrect or swipe typing in v0.1.

## 9. System Integration

Implement:

- System tray icon.
- Show/hide keyboard menu action.
- Settings menu action.
- Exit menu action.
- Single-instance enforcement.
- Optional startup with Windows.
- Graceful shutdown and input state cleanup.

Exiting the application must close both windows and release any simulated held keys.

Do not require background services.

## 10. Repository Structure

```text
FloatKeys/
├── src/
│   └── FloatKeys/
│       ├── FloatKeys.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── Windows/
│       │   ├── LauncherWindow.xaml
│       │   └── KeyboardWindow.xaml
│       ├── Controls/
│       │   └── KeyboardKey.xaml
│       ├── Input/
│       │   ├── IInputBackend.cs
│       │   ├── SendInputBackend.cs
│       │   ├── KeyboardState.cs
│       │   └── KeyboardLayout.cs
│       ├── Platform/
│       │   ├── NativeMethods.cs
│       │   └── WindowStyles.cs
│       ├── Services/
│       │   ├── OverlayService.cs
│       │   └── SettingsService.cs
│       └── Models/
│           └── AppSettings.cs
├── tests/
│   └── FloatKeys.Tests/
├── docs/
│   ├── architecture.md
│   └── compatibility.md
├── .github/
│   └── workflows/
│       └── ci.yml
├── README.md
└── FloatKeys.sln
```

Separate platform integration, UI and keyboard state logic.

Avoid excessive abstraction or premature plugin systems.

## 11. Implementation Milestones

### Milestone 1 — Compatibility Prototype

Implement a tiny floating window with three keys:

- A
- Backspace
- Enter

Implement native non-activating window behaviour and the `SendInput` backend.

Test against:

1. Windows Notepad.
2. A browser text field.
3. Moonlight on the Surface, streaming a remote desktop text editor.
4. WoW Forever running locally with its chat field already focused.

Verify that touch events do not steal focus, key events arrive and the overlay is visible.

**Stop after this milestone and report findings.**

Do not continue to the full keyboard until the developer has had the opportunity to test the prototype on the physical Surface.

### Milestone 2 — Functional Keyboard

After confirmation, implement the complete QWERTY keyboard with:

- Letters, digits and punctuation
- Shift/Caps Lock
- Modifier keys
- Cursor controls
- Key repeating
- Keyboard state management

### Milestone 3 — Polished Overlay

Implement:

- Full launcher
- Keyboard dragging and resizing
- Opacity
- Position persistence
- Settings
- Screen bounds recovery
- Display scaling
- Appearance improvements

### Milestone 4 — Packaging

Implement:

- System tray integration
- Startup option
- Single-instance support
- Portable packaging
- CI builds
- ARM64 and x64 release archives

Produce self-contained builds.

## 12. Testing Requirements

Automate tests for:

- Keyboard layout mappings
- Modifier state transitions
- Key-up/key-down generation
- Key repeat state
- Launcher tap-versus-drag detection
- Settings persistence and invalid settings recovery
- Display position clamping
- Input cancellation cleanup

Manual testing is required for touch, non-activation and application compatibility.

Document results in `docs/compatibility.md`, with application, operating mode, architecture, test result and observed limitations.

Do not claim Moonlight or WoW compatibility without physical testing.

## 13. Acceptance Criteria

v0.1 is complete when:

- FloatKeys runs natively on the Surface Pro 11.
- A floating launcher remains available over ordinary windows.
- The launcher can be repositioned with touch.
- The keyboard can be opened and closed without focusing itself.
- The keyboard sends valid input to focused Windows applications.
- The normal QWERTY layout is functional.
- Shift, Caps Lock, modifiers and essential special keys function correctly.
- The keyboard can be repositioned and resized.
- Positions and preferences persist across launches.
- The app operates without administrator privileges.
- Closing the keyboard preserves the launcher.
- The app can exit cleanly without stuck keys.
- Windows ARM64 and x64 builds are available.
- Moonlight and WoW Forever compatibility have been tested and explicitly documented.

**Moonlight input forwarding is a priority product goal.** If it cannot be achieved with the initial permitted input backend, document the failure and proposed next steps before declaring the project successful.

## 14. Out of Scope

Do not implement:

- Application-specific integrations
- WoW addon interaction
- Automatic chat activation
- Automatic input-field detection
- Clipboard monitoring
- Predictive text or autocorrect
- Voice dictation
- Keyboard macros
- Cloud synchronisation
- Account management
- Background network services
- Device drivers or privileged input interception
- Game injection or process memory manipulation
