#requires -Version 7.4
param(
    [string] $Version = '0.1.0',
    [ValidateSet('SingleFile', 'Directory')] [string] $Deployment = 'SingleFile',
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/release')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'Expected MAJOR.MINOR.PATCH version.' }
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'src/FloatKeys/FloatKeys.csproj') -Raw
if ($project.Project.PropertyGroup.Version -ne $Version) { throw 'Package version must match the project version.' }
# Only clean known, generated subdirectories beneath the requested output directory.
[IO.Directory]::CreateDirectory($output) | Out-Null
function Invoke-Dotnet([string[]] $Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE)." }
}
function Get-PeMachine([string] $Path, [long] $Offset = 0) {
    $reader = [IO.BinaryReader]::new([IO.File]::OpenRead($Path))
    try {
        $reader.BaseStream.Position = $Offset
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw "Invalid PE: $Path" }
        $reader.BaseStream.Position = $Offset + 0x3c; $peOffset = $reader.ReadInt32()
        $reader.BaseStream.Position = $Offset + $peOffset
        if ($reader.ReadUInt32() -ne 0x4550) { throw "Invalid PE signature: $Path" }
        return $reader.ReadUInt16()
    } finally { $reader.Dispose() }
}
function Test-Bundle([string] $Path, [int] $Machine, [string[]] $Required) {
    # Inspect the runtime's bundle marker and version-6 manifest using only the
    # PowerShell host BCL; loading SDK 10 assemblies would require PowerShell on .NET 10.
    # Format: dotnet/runtime Microsoft.NET.HostModel/Bundle/{Manifest,FileEntry}.cs.
    if (-not ('FloatKeysBundleInspector' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'BundleInspector.cs') }
    $header = [FloatKeysBundleInspector]::HeaderOffset($Path)
    $reader = [IO.BinaryReader]::new([IO.File]::OpenRead($Path))
    try {
        $reader.BaseStream.Position = $header
        $major = $reader.ReadUInt32(); $minor = $reader.ReadUInt32(); $count = $reader.ReadInt32()
        if ($major -ne 6 -or $minor -ne 0 -or $count -lt 1 -or $count -gt 10000) { throw 'Unexpected bundle format.' }
        $null = $reader.ReadString(); $reader.BaseStream.Position += 40
        $bundled = @{}
        for ($index = 0; $index -lt $count; $index++) {
            $offset = $reader.ReadInt64(); $size = $reader.ReadInt64(); $compressed = $reader.ReadInt64()
            $type = $reader.ReadByte(); $name = $reader.ReadString()
            if ($offset -lt 0 -or $size -le 0 -or $offset + $size -gt $reader.BaseStream.Length -or $compressed -ne 0) { throw 'Invalid or compressed bundle entry.' }
            $bundled[$name] = $true
            if ($name -in $Required -and (Get-PeMachine $Path $offset) -ne $Machine) { throw "Incorrect bundled native architecture: $name" }
        }
        foreach ($name in $Required + @('FloatKeys.dll', 'FloatKeys.runtimeconfig.json', 'FloatKeys.deps.json', 'PresentationFramework.dll', 'System.Windows.Forms.dll')) {
            if (-not $bundled.ContainsKey($name)) { throw "Missing bundled asset: $name" }
        }
        Write-Host "Verified $count bundled files, including required native libraries."
    } finally { $reader.Dispose() }
}
Push-Location $root
try {
    Invoke-Dotnet @('restore', 'src/FloatKeys/FloatKeys.csproj', '--locked-mode')
    $checksums = @()
    foreach ($rid in @('win-arm64', 'win-x64')) {
        $publish = Join-Path $output $rid
        $resolvedPublish = [IO.Path]::GetFullPath($publish)
        if (-not $resolvedPublish.StartsWith($output.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe publish directory.' }
        if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
        $single = ($Deployment -eq 'SingleFile').ToString().ToLowerInvariant()
        Invoke-Dotnet @('publish', 'src/FloatKeys/FloatKeys.csproj', '-c', 'Release', '-r', $rid,
            '--self-contained', 'true', '--no-restore', '-o', $publish,
            "-p:Version=$Version", "-p:FileVersion=$Version.0", "-p:AssemblyVersion=$Version.0",
            "-p:PublishSingleFile=$single", '-p:IncludeNativeLibrariesForSelfExtract=true',
            '-p:PublishTrimmed=false', '-p:EnableCompressionInSingleFile=false', '-p:DebugType=None', '-p:DebugSymbols=false')
        $exe = Join-Path $publish 'FloatKeys.exe'
        $machine = if ($rid -eq 'win-arm64') { 0xAA64 } else { 0x8664 }
        if ((Get-PeMachine $exe) -ne $machine) { throw "Incorrect executable architecture: $rid" }
        if ([Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion -ne "$Version.0") { throw 'Executable version mismatch.' }
        # Inspect resolved runtime-pack native files which feed the single-file bundle.
        $assets = Get-Content -LiteralPath 'src/FloatKeys/obj/project.assets.json' -Raw | ConvertFrom-Json -AsHashtable
        $deps = Get-Content -LiteralPath "src/FloatKeys/bin/Release/net10.0-windows/$rid/FloatKeys.deps.json" -Raw | ConvertFrom-Json -AsHashtable
        $target = $deps.targets[$deps.runtimeTarget.name]
        if (-not $target) { throw "Missing runtime target: $rid" }
        $required = @('coreclr.dll', 'clrjit.dll', 'PresentationNative_cor3.dll', 'wpfgfx_cor3.dll', 'PenImc_cor3.dll', 'vcruntime140_cor3.dll')
        $found = @{}
        foreach ($library in $target.Keys) {
            if (-not $target[$library].ContainsKey('native')) { continue }
            foreach ($asset in $target[$library].native.Keys) {
                $name = [IO.Path]::GetFileName($asset)
                if ($name -notin $required) { continue }
                $parts = ($library -replace '^runtimepack\.', '') -split '/'
                $relative = "$($parts[0].ToLowerInvariant())/$($parts[1])/runtimes/$rid/native"
                foreach ($folder in $assets.packageFolders.Keys) {
                    $nativePath = Join-Path $folder "$relative/$asset"
                    if (Test-Path -LiteralPath $nativePath) {
                        if ((Get-PeMachine $nativePath) -ne $machine) { throw "Incorrect native architecture: $name" }
                        $found[$name] = $true
                    }
                }
            }
        }
        foreach ($name in $required) { if (-not $found.ContainsKey($name)) { throw "Missing required native asset: $rid/$name" } }
        if ($Deployment -eq 'SingleFile') {
            # CoreCLR/JIT are statically linked into the SDK singlefilehost on Windows;
            # WPF's separate native libraries must be actual bundle entries.
            Test-Bundle $exe $machine @($required | Where-Object { $_ -notin @('coreclr.dll', 'clrjit.dll') })
            $files = @(Get-ChildItem -LiteralPath $publish -File)
            if ($files.Count -ne 1 -or $files[0].Name -ne 'FloatKeys.exe') { throw 'Single-file publish contains unexpected companion files.' }
        } else {
            foreach ($name in $required + @('FloatKeys.dll', 'FloatKeys.runtimeconfig.json', 'System.Windows.Forms.dll', 'PresentationFramework.dll')) {
                if (-not (Test-Path -LiteralPath (Join-Path $publish $name))) { throw "Incomplete directory publish: $name" }
            }
        }
        Copy-Item -LiteralPath 'LICENSE' -Destination (Join-Path $publish 'LICENSE.txt')
        Copy-Item -LiteralPath 'docs/portable.txt' -Destination (Join-Path $publish 'README.txt')
        foreach ($framework in @('NETCore', 'WindowsDesktop')) {
            $library = @($target.Keys | Where-Object { $_ -like "runtimepack.Microsoft.$framework.App.Runtime.$rid/*" })
            if ($library.Count -ne 1) { throw "Missing runtime license source: $framework" }
            $parts = ($library[0] -replace '^runtimepack\.', '') -split '/'
            $packagePath = $null
            foreach ($folder in $assets.packageFolders.Keys) {
                $candidate = Join-Path $folder "$($parts[0].ToLowerInvariant())/$($parts[1])"
                if (Test-Path -LiteralPath $candidate) { $packagePath = $candidate; break }
            }
            if (-not $packagePath) { throw "Runtime package unavailable: $framework" }
            $license = Join-Path $packagePath $(if ($framework -eq 'NETCore') { 'LICENSE.TXT' } else { 'LICENSE' })
            $noticeDirectory = Join-Path $publish "licenses/$framework"
            [IO.Directory]::CreateDirectory($noticeDirectory) | Out-Null
            Copy-Item -LiteralPath $license -Destination (Join-Path $noticeDirectory 'LICENSE.txt')
            $notices = Join-Path $packagePath 'THIRD-PARTY-NOTICES.TXT'
            if (Test-Path -LiteralPath $notices) { Copy-Item -LiteralPath $notices -Destination (Join-Path $noticeDirectory 'THIRD-PARTY-NOTICES.txt') }
        }
        $archive = Join-Path $output "FloatKeys-v$Version-$rid.zip"
        Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive -Force
        $checksums += '{0}  {1}' -f (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($archive)
        Write-Host "Verified $rid $Deployment publish and archive."
    }
    [IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'), ($checksums -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
    & (Join-Path $PSScriptRoot 'verify-packages.ps1') -Directory $output -Version $Version
} finally { Pop-Location }
