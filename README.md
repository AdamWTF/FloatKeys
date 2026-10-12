# FloatKeys

A portable UK touchscreen keyboard for Windows 11, built with .NET 10 and WPF.
FloatKeys floats above ordinary windows and sends input to the text field you have
focused. It provides Chat and Compact layouts, a Symbols page, draggable overlays,
resizing, and a system tray menu.

## Install and run

From the repository's GitHub Releases page, when available, download
`FloatKeys-v0.1.0-win-arm64.zip` for Surface Pro 11 or
`FloatKeys-v0.1.0-win-x64.zip` for an x64 PC. [Verify the checksum](docs/releases.md#verify-a-download),
extract the complete archive to a permanent folder, and run `FloatKeys.exe`.
No installer, administrator privileges, or separate .NET runtime is required.
Bundled native libraries extract into the per-user temporary cache at startup.

- Tap the floating launcher to show or hide the keyboard; drag it to move it.
- Focus your target text field before typing. FloatKeys does not focus it for you.
- Drag the keyboard header to move it and its bottom-right handle to resize it.
- Use the minus button to collapse the keyboard. Closing an overlay hides it.
- Use the tray menu to restore either overlay, open Settings, or **Exit FloatKeys**.
  Windows may place the tray icon in notification-area overflow.

## Keyboard and settings

**Chat** is the default six-row layout with digits and a dedicated punctuation row.
**Compact** uses five rows with `/` beside Space. **?123** opens Symbols;
**ABC** returns to the selected primary layout. Layout switches cancel pending
touches and repeats while preserving Caps and modifier latches.

- **Shift**, **Ctrl**, and **Alt** latch until the next input. Combine them for
  shortcuts. For Alt+Tab, tap Alt, then Symbols, then Tab.
- **Caps Lock** changes FloatKeys letters only; it does not toggle Windows Caps Lock.
  Shift reverses letter capitalization, and Caps does not add Shift to Ctrl/Alt shortcuts.
- Dedicated punctuation sends the displayed character regardless of latched Shift;
  Ctrl/Alt still participate in shortcuts. Letters and digits use ordinary UK Shift mappings.
- Holding Backspace, Delete, or an arrow repeats after 450 ms at 60 ms intervals.
  Other keys commit on release inside the original key; leaving it cancels the tap.
- Hiding the keyboard cancels pending input, repeats, and one-shot modifier latches.

The gear opens inline settings for launcher size (48–80 DIPs) and background
opacity (65–100%). Positions, keyboard dimensions, layout, and Symbols selection
save automatically to `%LOCALAPPDATA%\FloatKeys\settings.json`. Invalid or missing
settings recover to defaults. Restart shows the launcher with the keyboard hidden.
Small displays use scrolling to keep keys accessible.

**Start with Windows** is initially disabled. Enable it in the tray after placing
the app in a permanent folder. It registers the current executable in the user's
Run registry key. Run a relocated copy once to update an existing entry; disable
startup from the same menu. Windows Startup Apps can separately suppress execution.
A second launch in the same user session exits quietly.

## Compatibility and privacy

Windows 11 ARM64 and x64 packages are supported build targets. Use UK English
locally and remotely; physical modifiers and Windows Caps Lock can affect output.
Synthetic input may be rejected by elevated applications, games, or streaming clients.
Secure desktop, UAC, and protected authentication screens are unsupported.
Exclusive fullscreen and other topmost windows can obscure the overlay.

Physical touch and current Notepad, Moonlight, and WoW Forever input acceptance
remain pending. See the [compatibility checklist](docs/compatibility.md).
There are no application-specific integrations, privileged injection, global input hooks, drivers,
background services, telemetry, typed-key logging, or clipboard collection.
Bounded lifecycle diagnostics are stored in `%LOCALAPPDATA%\FloatKeys\logs\`.

## Build and test

Requires Windows, .NET SDK **10.0.300**, PowerShell **7.4+** for packaging,
and NuGet access. Dependency lock files are committed.

```powershell
dotnet restore FloatKeys.sln --locked-mode
dotnet build FloatKeys.sln -c Release --no-restore
dotnet test FloatKeys.sln -c Release --no-build --no-restore
./scripts/package.ps1
./scripts/verify-packages.ps1 -Directory artifacts/release -Version 0.1.0
```

Packages are written to `artifacts/release`. CI builds, tests, and packages both
architectures; stable version tags trigger GitHub Releases. See
[release instructions](docs/releases.md), [architecture](docs/architecture.md),
and [verification procedures](docs/compatibility.md) for maintenance details.

## License

[MIT](LICENSE). Portable packages include redistributed .NET license notices.
