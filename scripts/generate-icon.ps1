# Deterministic, dependency-free ICO generator. Pixels use the existing launcher glyph.
$ErrorActionPreference = 'Stop'
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    $writer.Write([int]40); $writer.Write([int]$size); $writer.Write([int]($size * 2))
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([int]0)
    $writer.Write([int]($size * $size * 4)); $writer.Write([long]0); $writer.Write([long]0)
    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $size; $x++) {
            $gx = ($x + 0.5) * 40 / $size; $gy = ($y + 0.5) * 40 / $size
            $opaque = (($gx - 20) * ($gx - 20) + ($gy - 20) * ($gy - 20)) -le 380
            $white = (($gx -ge 6 -and $gx -le 34 -and ($gy -ge 10 -and $gy -le 12 -or $gy -ge 28 -and $gy -le 30)) -or
                ($gy -ge 10 -and $gy -le 30 -and ($gx -ge 6 -and $gx -le 8 -or $gx -ge 32 -and $gx -le 34)))
            foreach ($row in @(16, 21)) {
                foreach ($col in @(11, 17, 23, 29)) {
                    if ([Math]::Abs($gx - $col) -le 1.5 -and [Math]::Abs($gy - $row) -le 1) { $white = $true }
                }
            }
            if ($gx -ge 12 -and $gx -le 28 -and $gy -ge 25 -and $gy -le 26.5) { $white = $true }
            if ($white) { $writer.Write([byte]250); $writer.Write([byte]247); $writer.Write([byte]245) }
            else { $writer.Write([byte]46); $writer.Write([byte]37); $writer.Write([byte]32) }
            $writer.Write([byte]$(if ($opaque) { 255 } else { 0 }))
        }
    }
    $writer.Write([byte[]]::new(([int][Math]::Ceiling($size / 32) * 4) * $size))
    $images += ,$stream.ToArray(); $writer.Dispose(); $stream.Dispose()
}
$destination = Join-Path $PSScriptRoot '../src/FloatKeys/Resources/FloatKeys.ico'
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($destination))) | Out-Null
$stream = [IO.File]::Create($destination); $writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = [byte]$(if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] })
        $writer.Write($dimension); $writer.Write($dimension); $writer.Write([uint16]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([int]$images[$i].Length); $writer.Write([int]$offset); $offset += $images[$i].Length
    }
    foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
}
finally { $writer.Dispose(); $stream.Dispose() }
