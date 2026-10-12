# FloatKeys

Milestone 3 floating UK touchscreen keyboard for Windows 11 / .NET 10 / WPF.
The draggable, non-activating, always-on-top overlay provides QWERTY, digits, UK punctuation,
Shift, local Caps Lock, latched Ctrl/Alt, special keys and hold-to-repeat navigation/deletion.
Focus a target text field before tapping FloatKeys. No application-specific integration is used.

## Run on Surface Pro 11

Launch [the ARM64 executable](artifacts/win-arm64/FloatKeys.exe) from the complete
`artifacts/win-arm64` folder. Copy the **entire folder** when moving it to another device.
The self-contained build needs no separate .NET installation. Run as an ordinary user:

```powershell
.\artifacts\win-arm64\FloatKeys.exe
```

It starts with a floating keyboard icon near the right edge of the primary display.
Tap the icon to show/hide the keyboard; drag the icon to move it. Focus Notepad or your
target application yourself, then tap keys. Drag the keyboard's header grip to move it
and its bottom-right diagonal handle to resize it. The header minus button collapses it;
the launcher remains available above the keyboard. Exit closes both windows.

The gear button opens a small launcher settings panel: use +/− to adjust size
(48–80 DIPs) and background opacity (65–100%). Tap the gear again to return to typing.
Positions, dimensions and ABC/Symbols selection are saved automatically in
`%LOCALAPPDATA%\FloatKeys\settings.json`. Missing/corrupt settings recover to defaults.
Restart always begins with the keyboard hidden. There is no tray or startup integration.
Do not run multiple copies while testing.

- **Shift:** tap to latch/unlatch; consumed by the next input, including arrows/Tab.
- **Caps Lock:** toggles capitalisation for FloatKeys letters only; Shift temporarily
  reverses it. It does not toggle Windows or remote Caps Lock.
- **Ctrl/Alt:** tap to latch, then tap the desired key. Combine modifiers if needed.
  Caps does not add Shift to Ctrl/Alt shortcuts. For Alt+Tab, tap Alt → Symbols → Tab.
- **Symbols/ABC:** switches layouts and preserves latches and Caps. Pending touches
  and repeats are cancelled before switching.
- **Backspace/Delete/arrows:** press sends one event pair immediately; holding repeats
  after 450 ms at 60 ms intervals. Release adds no extra input.
- Ordinary keys commit on release inside the original key. Leaving a key or losing
  capture cancels its gesture. Already emitted repeat pulses cannot be undone.

Choose UK English on both local and remote systems, with physical Caps Lock off and
physical modifiers released for baseline tests. Physical keyboard state can affect output.
Unicode entry remains a separate backend capability; visible keys send physical events.

## Build and verify

Prerequisites: Windows 11, .NET 10 SDK and NuGet access for dependencies/runtime packs.

```powershell
dotnet restore FloatKeys.sln
dotnet build FloatKeys.sln -c Release --no-restore
dotnet test FloatKeys.sln -c Release --no-build
dotnet test FloatKeys.sln -c Release --arch x64 -p:SelfContained=true
dotnet publish src/FloatKeys/FloatKeys.csproj -c Release -r win-arm64 --self-contained true -o artifacts/win-arm64
dotnet publish src/FloatKeys/FloatKeys.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
.\scripts\verify-overlay.ps1 -Executable .\artifacts\win-arm64\FloatKeys.exe
.\scripts\verify-overlay.ps1 -Executable .\artifacts\win-x64\FloatKeys.exe
dotnet publish scripts/OverlayProbe/OverlayProbe.csproj -c Release -r win-arm64 --self-contained true -o artifacts/probe-arm64
dotnet publish scripts/OverlayProbe/OverlayProbe.csproj -c Release -r win-x64 --self-contained true -o artifacts/probe-x64
.\scripts\verify-overlay.ps1 -Executable .\artifacts\win-arm64\FloatKeys.exe -LifecycleProbe .\artifacts\probe-arm64\FloatKeys.Tests.exe
.\scripts\verify-overlay.ps1 -Executable .\artifacts\win-x64\FloatKeys.exe -LifecycleProbe .\artifacts\probe-x64\FloatKeys.Tests.exe
git diff --check
```

See [architecture and file inventory](docs/architecture.md),
[verification results, manual checklist and limitations](docs/compatibility.md),
and the unchanged [v0.1 specification](docs/specification.md).

Milestone 1 was built and manually tested successfully, as confirmed by the user.
The updated overlay still needs physical touch and application compatibility validation.
The development probe uses a backend that cannot inject input and isolated settings;
it checks real HWND lifecycle, native positioning, foreground preservation and offscreen
layout rendering. It does not simulate physical touch or prove target input delivery.
Milestone 4 remains deferred: tray, startup, single-instance support, installer, CI release
automation and release archives. No selectable themes or game integration were added.
