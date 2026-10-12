# FloatKeys

FloatKeys v0.1.0 is a portable UK touchscreen keyboard for Windows 11 / .NET 10 / WPF.
The draggable, non-activating, always-on-top overlay provides QWERTY, digits, UK punctuation,
Shift, local Caps Lock, latched Ctrl/Alt, special keys and hold-to-repeat navigation/deletion.
Focus a target text field before tapping FloatKeys. No application-specific integration is used.

## Run on Surface Pro 11

Windows 11 ARM64 (Surface Pro 11) and x64 are the supported build targets. Physical
touch and application acceptance remain subject to the checklist below; x64 execution
on this ARM64 development host is emulation, not testing on a conventional x64 PC.

On the repository's GitHub **Releases** page, download
`FloatKeys-v0.1.0-win-arm64.zip` for Surface Pro 11, or
`FloatKeys-v0.1.0-win-x64.zip` for an x64 PC, plus `SHA256SUMS.txt`.
[Verify the checksum](docs/releases.md), extract the complete archive to a permanent
folder and run `FloatKeys.exe`. No installer, administrator privileges or separate
.NET runtime are needed. The single-file executable extracts bundled native runtime
libraries into the ordinary per-user temporary cache at startup.

From this repository after packaging:

```powershell
.\artifacts\release\win-arm64\FloatKeys.exe
```

It starts with a floating keyboard icon near the right edge of the primary display.
Tap the icon to show/hide the keyboard; drag the icon to move it. Focus Notepad or your
target application yourself, then tap keys. Drag the keyboard's header grip to move it
and its bottom-right diagonal handle to resize it. The header minus button collapses it;
the launcher remains available above the keyboard. The header Exit or tray
**Exit FloatKeys** closes both windows, releases owned input and terminates the process.

The gear button opens a small launcher settings panel: use +/− to adjust size
(48–80 DIPs) and background opacity (65–100%). Tap the gear again to return to typing.
Positions, dimensions and ABC/Symbols selection are saved automatically in
`%LOCALAPPDATA%\FloatKeys\settings.json`. Missing/corrupt settings recover to defaults.
Restart always begins with the launcher visible and keyboard hidden. A second launch
in the same Windows user session exits quietly without creating additional windows.

## System tray and Windows startup

The FloatKeys tray icon remains available even when both overlays are hidden. Windows
may place it in notification-area overflow. Right-click it for Show/Hide Keyboard,
Show/Hide Launcher, Start with Windows, Settings and Exit FloatKeys. Settings opens
the existing inline keyboard settings panel. Closing a window hides its overlay;
it does not exit the application. Use the tray to restore a hidden launcher.

Start with Windows is initially disabled. Enable it from the tray menu after placing
FloatKeys in a permanent folder. It uses the current user's Run registry value and
does not need elevation. Disable it from the same menu. If you move the executable,
run the new copy once to update an existing startup entry. External removal of the
entry stays disabled. Unexpected entries are left intact and registration failures
are reported without crashing. Windows Startup Apps can separately suppress a Run
entry; the tray checkmark represents registration, not an override of Windows policy.

Diagnostics are bounded local files in `%LOCALAPPDATA%\FloatKeys\logs\`.
There is no telemetry, analytics, typed-key logging or clipboard collection.

## Keyboard behavior

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

Prerequisites: Windows 11, .NET SDK 10.0.300, PowerShell 7.4 or newer for packaging,
and NuGet access for dependencies/runtime packs.

```powershell
dotnet restore FloatKeys.sln --locked-mode
dotnet build FloatKeys.sln -c Release --no-restore
dotnet test FloatKeys.sln -c Release --no-build
dotnet test FloatKeys.sln -c Release --arch x64 -p:SelfContained=true --no-restore
.\scripts\package.ps1 -Version 0.1.0
.\scripts\verify-packages.ps1 -Directory artifacts/release -Version 0.1.0
.\scripts\verify-overlay.ps1 -Executable .\artifacts\release\win-arm64\FloatKeys.exe
.\scripts\verify-overlay.ps1 -Executable .\artifacts\release\win-x64\FloatKeys.exe
dotnet publish scripts/OverlayProbe/OverlayProbe.csproj -c Release -r win-arm64 --self-contained true -o artifacts/probe-arm64
dotnet publish scripts/OverlayProbe/OverlayProbe.csproj -c Release -r win-x64 --self-contained true -o artifacts/probe-x64
.\scripts\verify-overlay.ps1 -Executable .\artifacts\release\win-arm64\FloatKeys.exe -LifecycleProbe .\artifacts\probe-arm64\FloatKeys.Tests.exe
.\scripts\verify-overlay.ps1 -Executable .\artifacts\release\win-x64\FloatKeys.exe -LifecycleProbe .\artifacts\probe-x64\FloatKeys.Tests.exe
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
CI builds/tests/packages both architectures. Tagged releases publish independent
archives and SHA-256 checksums. See [release instructions](docs/releases.md) for the
first release; no release or tag is created by building locally.

## Input compatibility and limitations

Focus the desired text field yourself. Visible keys send balanced physical-style
SendInput events and assume UK layout; local Caps does not change Windows Caps Lock.
Synthetic input may be rejected by elevated, protected or raw-input targets.
Secure desktop, UAC and protected authentication screens are unsupported. Exclusive
fullscreen or another topmost window can obscure FloatKeys.

Moonlight receives local input and may forward it to the remote PC depending on capture
settings and input handling. Test windowed and fullscreen separately. Games, including
WoW Forever, may use different input paths. Neither target is special-cased and current
compatibility is not claimed without physical testing. No hooks, privileged injection,
drivers, services, target-field detection or application-specific APIs are used.
Forced termination or rejected native releases cannot guarantee key cleanup.
