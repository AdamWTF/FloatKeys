# Compatibility and verification

## Automated evidence

Cleanup validation on 12 October 2026 used Windows ARM64 and SDK 10.0.300.
These checks were rerun for this cleanup; historical milestone reports have been removed.

| Check | Current result |
|---|---|
| Locked solution/probe restore and Release build | Pass; no compiler warnings/errors |
| ARM64 self-contained tests | 227 passed, zero failed/skipped |
| x64 self-contained tests | 227 passed, zero failed/skipped under ARM emulation |
| Both portable packages | Pass: PE versions/architecture, native bundle assets, notices, archive contents, checksums |
| Both smoke/lifecycle/render probes | Pass: secondary instance, hide/exit, 50 visibility cycles, foreground preservation, layouts at 96/144/192 DPI |
| Version validator | Pass: derivation, matching tag/version, seven invalid metadata/tag/version cases rejected |
| Workflow/script syntax and documentation links | Pass: actionlint 1.7.12, PowerShell parser, local links/anchors |
| Public-readiness audit | No common credential signatures in tracked history; NuGet reports no vulnerable direct/transitive packages for app, tests, or probe |

GitHub Actions execution and physical application acceptance have not been performed.

The probe checks real HWND styles, non-activation, visibility cycles, positioning,
tray lifecycle, settings, and layout rendering. Its isolated backend cannot inject
native input. Neither unit tests nor the probe establish target application input
delivery, physical touch routing, or real display rotation. x64 execution on an
ARM64 host is emulation, not testing on a conventional x64 PC.

## Reproduce local checks

Run the [basic build commands](../README.md#build-and-test), then on Windows:

```powershell
dotnet test FloatKeys.sln -c Release --arch arm64 -p:SelfContained=true --no-restore
dotnet test FloatKeys.sln -c Release --arch x64 -p:SelfContained=true --no-restore
dotnet restore scripts/OverlayProbe/OverlayProbe.csproj --locked-mode
dotnet publish scripts/OverlayProbe/OverlayProbe.csproj -c Release -r win-arm64 --self-contained true --no-restore -o artifacts/probe-arm64
dotnet publish scripts/OverlayProbe/OverlayProbe.csproj -c Release -r win-x64 --self-contained true --no-restore -o artifacts/probe-x64
./scripts/verify-overlay.ps1 -Executable ./artifacts/release/win-arm64/FloatKeys.exe -LifecycleProbe ./artifacts/probe-arm64/FloatKeys.Tests.exe
./scripts/verify-overlay.ps1 -Executable ./artifacts/release/win-x64/FloatKeys.exe -LifecycleProbe ./artifacts/probe-x64/FloatKeys.Tests.exe
```

The executable smoke check briefly launches FloatKeys and tests secondary-instance
rejection and session shutdown. Close an existing FloatKeys instance first and avoid
interacting with other windows during foreground checks. The separate lifecycle probe
uses isolated settings and simulated input. Rendered images go to `artifacts/overlay-probe`.

## Manual acceptance — pending

Use the ARM64 package on Surface Pro 11 and repeat on a physical x64 PC before
claiming that hardware is validated. Select UK English locally and remotely,
release physical modifiers, and turn off physical Caps Lock. Record device, Windows
and target-app versions, scaling, orientation, operating mode, and Pass/Fail.

1. **Notepad and browser:** focus a text field manually. Type
   `1234567890 / : . , ? ! ' -` in Chat and `@ " £ #` in Symbols. Verify exact output,
   focus retention, single-tap slash, and no extra characters when switching
   Chat/Compact or returning from Symbols with ABC.
2. **Modifiers and gestures:** test Shift+A, Caps+A, Ctrl+A/C/V, Alt→Symbols→Tab,
   held Backspace/arrows, cancelled taps, and capture loss. Latched Shift followed
   by `/` or `:` must send the label and clear Shift. Layout switches preserve latch
   indicators; hiding stops repeats and clears latches while preserving local Caps.
3. **Geometry and settings:** drag both overlays, resize each page, rotate the device,
   change scaling, and disconnect/reconnect a display. Check touch target size,
   labels, edge recovery, and scrolling on small displays. Restart to verify positions,
   dimensions, primary layout, and Symbols selection; keyboard starts hidden.
4. **Moonlight:** repeat typing and layout switches with remote Notepad in windowed
   and fullscreen modes. Verify forwarding with UK layout on both machines and
   record capture, overlay, or focus failures separately.
5. **WoW Forever:** manually focus chat and test a message, `/say hello`,
   `/party hello`, and `/w Name hello` with supported channels and a valid recipient.
   Check slash, punctuation, channel selection, and focus through layout changes.
6. **Windows lifecycle:** check tray visibility/settings, independent overlay toggles,
   native closure, second launch, and tray Exit. Verify Start with Windows enabled,
   disabled, and after moving the executable; ensure exit leaves no stuck keys.

## Limitations

Elevated/protected targets can reject SendInput. Secure desktop, UAC, and protected
authentication screens are unsupported. Exclusive fullscreen or another topmost
window can obscure FloatKeys. Physical modifiers, Windows Caps Lock, and non-UK
target layouts can change output. Moonlight and games are not special-cased.
Forced termination and rejected key releases cannot guarantee cleanup. Packages
are unsigned; checksums verify integrity, not publisher identity.
