<#
.SYNOPSIS
    Draws the QuietFan application icon (a white fan on a teal tile) and saves it as a multi-size .ico.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 128
$tileTop = [System.Drawing.Color]::FromArgb(20, 184, 166)
$tileBottom = [System.Drawing.Color]::FromArgb(15, 118, 110)

function New-FanBitmap([int]$Size) {
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $diameter = $Size * 0.44
    $tile = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $tile.AddArc(0, 0, $diameter, $diameter, 180, 90)
    $tile.AddArc($Size - $diameter - 1, 0, $diameter, $diameter, 270, 90)
    $tile.AddArc($Size - $diameter - 1, $Size - $diameter - 1, $diameter, $diameter, 0, 90)
    $tile.AddArc(0, $Size - $diameter - 1, $diameter, $diameter, 90, 90)
    $tile.CloseFigure()
    $gradient = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(0, 0), [System.Drawing.PointF]::new(0, $Size), $tileTop, $tileBottom)
    $graphics.FillPath($gradient, $tile)

    $center = $Size / 2
    for ($blade = 0; $blade -lt 4; $blade++) {
        $state = $graphics.Save()
        $graphics.TranslateTransform($center, $center)
        $graphics.RotateTransform(90 * $blade + 25)
        $graphics.FillEllipse([System.Drawing.Brushes]::White, -$Size * 0.12, -$Size * 0.40, $Size * 0.24, $Size * 0.36)
        $graphics.Restore($state)
    }
    $hub = $Size * 0.18
    $hubBrush = [System.Drawing.SolidBrush]::new($tileBottom)
    $graphics.FillEllipse($hubBrush, $center - $hub / 2, $center - $hub / 2, $hub, $hub)

    $hubBrush.Dispose()
    $gradient.Dispose()
    $tile.Dispose()
    $graphics.Dispose()
    return $bitmap
}

# One icon image in the classic DIB layout: header, bottom-up BGRA pixels, then the 1-bit transparency mask.
function ConvertTo-IconImage([System.Drawing.Bitmap]$Bitmap) {
    $width = $Bitmap.Width
    $height = $Bitmap.Height
    $data = $Bitmap.LockBits([System.Drawing.Rectangle]::new(0, 0, $width, $height),
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $pixels = [byte[]]::new($stride * $height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $Bitmap.UnlockBits($data)

    $maskStride = [int][Math]::Ceiling($width / 32.0) * 4
    $mask = [byte[]]::new($maskStride * $height)
    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    $writer.Write([int]40)
    $writer.Write([int]$width)
    $writer.Write([int]($height * 2))
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int]0)
    $writer.Write([int]($width * $height * 4 + $mask.Length))
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)
    for ($y = $height - 1; $y -ge 0; $y--) {
        $writer.Write($pixels, $y * $stride, $width * 4)
        $maskRow = ($height - 1 - $y) * $maskStride
        for ($x = 0; $x -lt $width; $x++) {
            if ($pixels[$y * $stride + $x * 4 + 3] -eq 0) {
                $mask[$maskRow + ($x -shr 3)] = $mask[$maskRow + ($x -shr 3)] -bor (0x80 -shr ($x -band 7))
            }
        }
    }
    $writer.Write($mask)
    $writer.Flush()
    return , $stream.ToArray()
}

$images = [System.Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) {
    $bitmap = New-FanBitmap $size
    $images.Add((ConvertTo-IconImage $bitmap))
    $bitmap.Dispose()
}

$file = [System.IO.File]::Create($Path)
$writer = [System.IO.BinaryWriter]::new($file)
$writer.Write([int16]0)
$writer.Write([int16]1)
$writer.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $writer.Write([byte]$sizes[$i])
    $writer.Write([byte]$sizes[$i])
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int]$images[$i].Length)
    $writer.Write([int]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write($image) }
$writer.Dispose()
