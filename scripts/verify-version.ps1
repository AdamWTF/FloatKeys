param([string] $Version, [string] $Tag)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'src/FloatKeys/FloatKeys.csproj') -Raw
$sourceVersion = [string]$project.Project.PropertyGroup.Version
if ($sourceVersion -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'Expected source MAJOR.MINOR.PATCH version.' }
if ($Version -and $Version -ne $sourceVersion) { throw 'Package version must match the project version.' }
if ($Tag -and $Tag -cne "v$sourceVersion") { throw 'Expected vMAJOR.MINOR.PATCH tag matching the project version.' }
foreach ($name in @('AssemblyVersion', 'FileVersion')) {
    if ([string]$project.Project.PropertyGroup.$name -ne "$sourceVersion.0") { throw "$name must match the project version." }
}
[xml]$manifest = Get-Content -LiteralPath (Join-Path $root 'src/FloatKeys/app.manifest') -Raw
if ([string]$manifest.assembly.assemblyIdentity.version -ne "$sourceVersion.0") { throw 'Manifest version must match the project version.' }
$sourceVersion
