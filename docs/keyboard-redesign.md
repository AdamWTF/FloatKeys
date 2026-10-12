# Keyboard redesign — 12 October 2026

Chat is the default six-row keyboard: numbers, three familiar QWERTY rows, common
punctuation and controls. Compact uses five rows: numbers, three QWERTY rows and controls
with `/` beside Space. Both expose slash and numbers without layout switching.
The header selects and persists Chat/Compact; `?123` opens shared Symbols and `ABC`
returns to that preference. Caps Lock stays in the header; Tab/Escape/Delete/Up/Down
remain on Symbols. All original UK punctuation remains accessible.

## Implementation decisions

- Reuse the existing controller, scan-code chord backend, touch controls and weighted WPF grids.
- Dedicated punctuation always types its displayed label and consumes one-shot latches.
  UK Shift is supplied internally for `: ? ! @ " £` and other shifted characters.
  Ctrl/Alt still participate in shortcuts; letters and digits retain ordinary Shift behavior.
- Selection cancels pending touches/repeats and releases owned input while preserving
  Caps and modifier latches. Input cleanup failures retain the existing safety/error behavior.
- Persist `KeyboardLayout` as `chat`/`compact`; retain `Layout: uk` and Symbols separately.
  Missing or invalid preferences default to Chat. Atomic settings writes are unchanged.
- Centralize DIP sizing: Chat defaults to 840 × 480, minimum 720 × 432; Compact defaults
  to 840 × 420, minimum 720 × 382. Symbols uses the five-row minimum. Switching grows
  only insufficient dimensions. Position moves only when display bounds require recovery.
- Keep 6-DIP key gaps, 44 × 44 DIP minimum key surfaces, a centered home row and wide
  Space/Shift/Backspace/Enter. Dark styling gains subdued slash and Enter accents.

## Validation

Validation results are recorded in [compatibility.md](compatibility.md). The probe uses
isolated settings, fake startup storage and a backend that cannot inject native input.
It verifies real HWND lifecycle, non-activation and renderer geometry, rather than proving
delivery to Notepad, Moonlight or WoW. Tests assert UK scan-code/Shift mappings and event
behavior; physical target output remains part of acceptance below.

## Modified files

- `src/FloatKeys/Input/KeyboardLayout.cs`, `KeyboardController.cs`; added `KeyboardSizing.cs`.
- `src/FloatKeys/Windows/KeyboardWindow.xaml`, `KeyboardWindow.xaml.cs`.
- `src/FloatKeys/Controls/KeyboardKey.xaml`, `KeyboardKey.xaml.cs`.
- `src/FloatKeys/Models/AppSettings.cs`.
- `src/FloatKeys/Services/SettingsService.cs`, `OverlayService.cs`.
- `src/FloatKeys/Platform/OverlayGeometry.cs`.
- `tests/FloatKeys.Tests/KeyboardTests.cs`, `SettingsTests.cs`, `PolishedOverlayTests.cs`.
- `scripts/OverlayProbe/Program.cs`.
- `README.md`, `docs/specification.md`, `docs/architecture.md`, `docs/compatibility.md`;
  added this report.

## Surface Pro 11 manual checklist — pending

Use the local redesigned ARM64 package in `artifacts/keyboard-redesign`. Published v0.1.0
downloads are not updated by this work. Select UK English locally and remotely, release
physical modifiers, and turn off physical Caps Lock. Record orientation, scaling,
application version and Pass/Fail for each step.

1. **Notepad:** focus its text field manually, show Chat and type
   `1234567890 / : . , ? ! ' -`. Open Symbols and type `@ " £ #`. Confirm exact output.
   Switch Chat/Compact and verify the caret retains focus, no text appears from selection,
   slash needs one tap and numbers remain visible. Return from Symbols with ABC.
2. **Modifiers:** test Shift+A, Caps+A, Ctrl+A/C/V, Alt→Symbols→Tab and held Backspace/arrows.
   With Shift latched, tap `/` and `:`: each must produce its label and clear Shift.
   Switch primary layouts with modifiers latched and confirm their indicators survive.
   Hide/reopen and confirm repeat/latches cancel, while Caps and layout remain.
3. **Geometry/persistence:** drag launcher/header, resize each page to its minimum, rotate
   portrait/landscape and test normal Windows scaling. Confirm labels fit and controls do
   not overlap. Switch layouts at a screen edge. Restart and confirm selected layout,
   Symbols page, positions and sufficient dimensions restore; keyboard starts hidden.
4. **Moonlight:** in windowed and fullscreen modes, manually focus remote Notepad and repeat
   the text/selection checks. Confirm punctuation and slash forwarding with UK selected
   on both machines. Record any overlay/capture failure; no special integration is used.
5. **WoW Forever:** manually focus chat, then type a message and `/say hello`, `/party hello`
   and `/w Name hello` using the target's supported channels and recipient. Confirm single-tap
   slash, common punctuation, channel changes and focus retention through layout changes.
6. **Existing behavior:** check visibility toggling, launcher dragging, tray Settings/Exit,
   single-instance rejection and the existing Start with Windows preference.

## Limitations

Physical touchscreen acceptance and Notepad/Moonlight/WoW output checks remain pending.
Synthetic input restrictions, elevated targets and exclusive fullscreen limitations remain.
Physical modifiers/Windows Caps Lock can affect output; FloatKeys does not override them.
The implementation targets the standard UK keyboard mapping; different target layouts can
produce different characters. Very small displays use scrolling instead of undersized keys.
x64 execution on the ARM64 host is emulation, not physical x64 hardware validation.
No application detection, macros, new input drivers, version change, tag or GitHub release
is introduced. Existing CI/release workflows remain intact.
