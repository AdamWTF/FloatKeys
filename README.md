# FloatKeys

Milestone 2 functional UK touchscreen keyboard for Windows 11 / .NET 10 / WPF.
The fixed, non-activating, always-on-top overlay provides QWERTY, digits, UK punctuation,
Shift, local Caps Lock, latched Ctrl/Alt, special keys and hold-to-repeat navigation/deletion.
Focus a target text field before tapping FloatKeys. No application-specific integration is used.

## Run on Surface Pro 11

Launch [the ARM64 executable](artifacts/win-arm64/FloatKeys.exe) from the complete
`artifacts/win-arm64` folder. Copy the **entire folder** when moving it to another device.
The self-contained build needs no separate .NET installation. Run as an ordinary user:

```powershell
.\artifacts\win-arm64\FloatKeys.exe
```

It opens near the bottom-right of the primary display. Focus Notepad or your target
application, then tap keys. Exit closes the application. There is no launcher or tray.
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
git diff --check
```

See [architecture and file inventory](docs/architecture.md),
[verification results, manual checklist and limitations](docs/compatibility.md),
and the unchanged [v0.1 specification](docs/specification.md).

Milestone 1 was built and manually tested successfully, as confirmed by the user.
The updated Milestone 2 keyboard still needs physical touch and application compatibility
validation. Milestone 3/4 features remain deferred: launcher, dragging/resizing, advanced
positioning, persistence, settings, themes, tray, startup, installer and release packaging.
