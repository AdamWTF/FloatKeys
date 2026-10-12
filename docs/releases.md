# Releases

## Prepare a candidate

Use stable `vMAJOR.MINOR.PATCH` tags. Update the project's Version, AssemblyVersion,
FileVersion, manifest identity, release notes, and version examples together.
Assembly/file/manifest versions use `MAJOR.MINOR.PATCH.0`. `verify-version.ps1`
rejects disagreement; packaging derives the version from the project by default.

The SDK is pinned in `global.json`; committed NuGet lock files are enforced.
Intentional dependency updates require regenerating and reviewing lock files.
Run the [build and test commands](../README.md#build-and-test) and
[compatibility checks](compatibility.md), including physical acceptance where possible.

```powershell
./scripts/verify-version.ps1 -Tag v0.1.0
./scripts/package.ps1
./scripts/verify-packages.ps1 -Directory artifacts/release -Version 0.1.0
```

Packaging creates self-contained ARM64 and x64 archives with `FloatKeys.exe`,
`LICENSE.txt`, `README.txt`, and redistributed runtime notices under `licenses/`.
It verifies native architecture, executable version, bundled runtime assets,
archive contents, and SHA-256 checksums. No tests, probes, source, PDBs, settings,
or logs ship. Single-file native extraction requires writable temporary storage.
`-Deployment Directory` is a diagnostic fallback; any change to release format
requires updating both workflows and documenting the reason.

## GitHub Actions

Windows CI runs on pushes and pull requests targeting `main`. It validates versions,
restores locked dependencies, builds/tests Release, packages both architectures,
and uploads portable archives/checksums for 14 days. Test reports upload even after
a failure when available; newer runs cancel obsolete CI for the same branch or PR.
External actions are pinned to verified commit SHAs with version comments; review
and update those pins deliberately.

The tag workflow requires a matching source/tag version and successful build,
tests, and packaging. Validation has read-only repository permissions; only the
publication job has `contents: write`. It verifies downloaded assets, creates a
draft release, uploads assets, then publishes. Upload failures leave any draft
unpublished. Existing releases are not overwritten: inspect an incomplete draft
and deliberately remove it before retrying.

The `windows-2025` runner is x64. ARM64 packaging is cross-compilation and does not
prove native ARM64 execution or physical touch/application compatibility.

This checkout has no Git remote configured. Once the candidate is committed,
configure the intended GitHub repository and push `main`. To publish deliberately:

```powershell
git tag -a v0.1.0 -m "FloatKeys v0.1.0"
git push origin v0.1.0
```

The workflow publishes `FloatKeys-v0.1.0-win-arm64.zip`,
`FloatKeys-v0.1.0-win-x64.zip`, and `SHA256SUMS.txt`. Local builds do not publish.

## Verify a download

Download the architecture-matching archive and `SHA256SUMS.txt` from the same release.
Compare the full hash to the matching checksum line:

```powershell
Get-FileHash ./FloatKeys-v0.1.0-win-arm64.zip -Algorithm SHA256
Expand-Archive ./FloatKeys-v0.1.0-win-arm64.zip -DestinationPath ./FloatKeys
./FloatKeys/FloatKeys.exe
```

Extract the complete archive into a permanent folder. Packages are unsigned;
Windows may show reputation warnings. SHA-256 verifies download integrity,
not publisher authenticity.
