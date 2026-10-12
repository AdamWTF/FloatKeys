# Release preparation

Use stable `vMAJOR.MINOR.PATCH` tags. The source project Version and tag must agree;
FloatKeys v0.1.0 has assembly/file version 0.1.0.0. The manifest identity must also
be updated for any future version. The current SDK is pinned in global.json;
committed NuGet lock files are enforced by CI. Intentional dependency updates
require regenerating and reviewing those lock files.

## Local validation

On Windows with .NET SDK 10.0.300, PowerShell 7.4 or newer and NuGet access:

```powershell
dotnet restore FloatKeys.sln --locked-mode
dotnet build FloatKeys.sln -c Release --no-restore
dotnet test FloatKeys.sln -c Release --no-build
./scripts/package.ps1 -Version 0.1.0
./scripts/verify-packages.ps1 -Directory artifacts/release -Version 0.1.0
```

The packaging script is shared by local builds, CI and releases. It produces
self-contained single-file executables for win-arm64 and win-x64, including native
runtime/WPF libraries for extraction under the runtime's ordinary per-user temp
cache. Trimming is disabled. No runtime installation, administrator privileges,
service or installer is required. Run from a user-writable permanent folder.
Single-file native extraction requires writable temporary storage.

`-Deployment Directory` is an explicit fallback for investigating a demonstrated
single-file incompatibility. It is not the default release format. If needed,
validate both architectures, change both workflows to use it and document the
reason before publishing. Do not distribute an incomplete folder.

Each archive has FloatKeys.exe, LICENSE.txt and README.txt at its root, plus
redistributed .NET runtime licenses/notices under licenses/. No test/probe/source/PDB/settings/log files
are distributed. The script validates executable architecture/version, required
native runtime-pack assets and bundled WPF libraries, archive contents, names and SHA-256 checksums. Copy
the complete extracted contents; existing settings stay under LocalAppData.

## Workflows and first release

Windows CI runs on pushes and pull requests targeting main. It restores locked
dependencies, builds/tests Release, publishes both architectures, and uploads
the archives/checksums as the FloatKeys-portable Actions artifact for 14 days.
Windows-2025 is an x64 runner: ARM64 packaging is cross-compilation, not ARM64
execution. Physical acceptance and ARM64 execution are separate release checks.

The tag workflow validates the version, builds/tests/packages with read-only
permissions, then passes verified artifacts to a job with contents:write. GitHub
CLI creates a draft release, uploads assets and only then publishes it. A failed
build/test/package blocks this job; a failed upload leaves any draft unpublished.
An existing release is not overwritten automatically on a rerun: inspect/delete
an incomplete draft deliberately before retrying.

Before the first release, complete and record the Surface checklist in
[compatibility.md](compatibility.md), review release-notes.md and commit the final
candidate. This checkout currently has no configured Git remote; configure the
intended GitHub repository and push main before tagging. Once approved:

```powershell
git tag -a v0.1.0 -m "FloatKeys v0.1.0"
git push origin v0.1.0
```

Those commands trigger publication. They are instructions, not commands run by
the implementation. Assets appear on that repository's GitHub Releases page:

- FloatKeys-v0.1.0-win-arm64.zip
- FloatKeys-v0.1.0-win-x64.zip
- SHA256SUMS.txt

Packages are unsigned; Windows may show reputation warnings. SHA-256 verifies
download integrity, not publisher authenticity. No signing credentials are used.

## Verify a downloaded archive

Download SHA256SUMS.txt from the same release. In PowerShell:

```powershell
Get-FileHash ./FloatKeys-v0.1.0-win-arm64.zip -Algorithm SHA256
```

Compare the complete hash with that archive's line in SHA256SUMS.txt. With both
archives and the repository verification script available:

```powershell
./scripts/verify-packages.ps1 -Directory C:/Downloads/FloatKeys -Version 0.1.0
Expand-Archive ./FloatKeys-v0.1.0-win-arm64.zip -DestinationPath ./FloatKeys
./FloatKeys/FloatKeys.exe
```

Check the extracted file list and version properties, then perform the manual
acceptance checklist. Never infer touch/input/game compatibility from packaging
or a build passing.
