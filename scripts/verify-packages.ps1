param([Parameter(Mandatory)] [string] $Directory, [Parameter(Mandatory)] [string] $Version)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'Invalid version.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$expected = @("FloatKeys-v$Version-win-arm64.zip", "FloatKeys-v$Version-win-x64.zip")
$lines = @(Get-Content -LiteralPath (Join-Path $Directory 'SHA256SUMS.txt'))
if ($lines.Count -ne 2) { throw 'Expected exactly two checksums.' }
$seen = @{}
foreach ($line in $lines) {
    if ($line -notmatch '^([a-fA-F0-9]{64})  (FloatKeys-v\d+\.\d+\.\d+-win-(?:arm64|x64)\.zip)$') { throw 'Malformed checksum entry.' }
    $digest = $Matches[1]; $name = $Matches[2]
    if ($name -notin $expected -or $seen.ContainsKey($name)) { throw 'Unexpected or duplicate archive.' }
    $seen[$name] = $true; $path = Join-Path $Directory $name
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $digest) { throw "Checksum mismatch: $name" }
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entries = @($zip.Entries.FullName)
        foreach ($required in @('FloatKeys.exe', 'LICENSE.txt', 'README.txt', 'licenses/NETCore/LICENSE.txt', 'licenses/NETCore/THIRD-PARTY-NOTICES.txt', 'licenses/WindowsDesktop/LICENSE.txt')) {
            if ($required -notin $entries) { throw "Missing archive entry: $required" }
        }
        foreach ($entry in $entries) {
            if ($entry -match '(^|[/\\])\.\.|^[/\\]|:|\.(pdb|cs|csproj|ps1|log)$|settings\.json|(^|[/\\])(tests|obj|bin)[/\\]') { throw "Unexpected archive content: $entry" }
        }
    } finally { $zip.Dispose() }
    Write-Host "PASS archive=$name SHA256=$digest"
}
