<#
.SYNOPSIS
    Draws the application icon and writes src/AorusControl.App/Assets/app.ico.

.DESCRIPTION
    The icon is generated rather than drawn by hand so it can be changed by editing numbers
    and re-running, and so every size comes from the same geometry instead of from a chain of
    manual exports.

    The design follows Microsoft's own guidance for Windows app icons:

      - One metaphor, no typography. A fan, because cooling is what this app is for and what
        it is not allowed to get wrong.
      - Built on the 48-unit grid (here 256 = 48 x 5.33) with the outer shape inset, so it
        sits at the same visual weight as the system icons beside it.
      - A distinctive silhouette instead of a filled square. The old icon was a black rounded
        square, which disappears into a dark taskbar - the shape itself should be the thing
        you recognise.
      - Colour in all three ranges (dark, medium, light) so that at least half the icon keeps
        a 3:1 contrast on both light and dark backgrounds. One subtle gradient at 120 degrees,
        nothing else.
      - Legible at 16 px: three blades, wide negative space between them, no inner detail.

    Sizes below 128 are stored as 32-bit BMP frames and the two large ones as PNG, which is
    the combination Windows has always read without argument.

    https://learn.microsoft.com/en-us/windows/apps/design/iconography/app-icon-design
#>
[CmdletBinding()]
param(
    [string] $OutputPath,
    [switch] $PreviewOnly
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputPath) { $OutputPath = Join-Path $root 'src\AorusControl.App\Assets\app.ico' }

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

# Geometry.Parse reads numbers the invariant way; -f writes them the way the machine is set
# up. On a German Windows that means "157,54" going into a parser that reads the comma as a
# separator, and a path that fails with an error about an unexpected token.
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture

# ---- the drawing -------------------------------------------------------------------------
# Everything is expressed on a 256 canvas; 256 / 48 = 5.33 per grid unit.
$canvas = 256.0
$centre = $canvas / 2
$rim = 118.0    # outer radius: leaves 10 px of air, i.e. about 2 grid units
$hub = 33.0
$blades = 3

function Polar([double] $radius, [double] $degrees) {
    $rad = $degrees * [Math]::PI / 180.0
    [Windows.Point]::new($centre + $radius * [Math]::Cos($rad), $centre + $radius * [Math]::Sin($rad))
}

# One blade, as a comma that starts narrow at the hub and opens towards the rim. Two
# quadratic curves and one arc - as few corners as the guidance asks for.
function BladeGeometry([double] $turn) {
    # 62 degrees of rim per blade rather than 48: at 16 px the thinner version came out
    # spindly, and a blade you cannot see is not a blade.
    $start = Polar $hub ($turn - 16)
    $lead = Polar $rim ($turn + 22)
    $trail = Polar $rim ($turn + 84)
    $leadControl = Polar ($rim * 0.64) ($turn - 4)
    $trailControl = Polar ($rim * 0.50) ($turn + 42)

    $data = "M {0:F2},{1:F2} Q {2:F2},{3:F2} {4:F2},{5:F2} A {6:F2},{6:F2} 0 0 1 {7:F2},{8:F2} Q {9:F2},{10:F2} {0:F2},{1:F2} Z" -f `
        $start.X, $start.Y, $leadControl.X, $leadControl.Y, $lead.X, $lead.Y,
        $rim, $trail.X, $trail.Y, $trailControl.X, $trailControl.Y
    [Windows.Media.Geometry]::Parse($data)
}

function BuildDrawing {
    $group = [Windows.Media.DrawingGroup]::new()

    # The app's own accent, extended into a dark and a light step of the same hue so the
    # icon carries all three ranges rather than one flat cyan that fails on white.
    $deep = [Windows.Media.Color]::FromRgb(0x0B, 0x5C, 0x74)
    $mid = [Windows.Media.Color]::FromRgb(0x35, 0xC7, 0xE6)
    $light = [Windows.Media.Color]::FromRgb(0x9B, 0xEA, 0xF7)

    # 120 degrees is the default gradient angle in the guidance; light at the top left.
    $sweep = [Windows.Media.LinearGradientBrush]::new()
    $sweep.StartPoint = [Windows.Point]::new(0.15, 0.0)
    $sweep.EndPoint = [Windows.Point]::new(0.85, 1.0)
    $sweep.GradientStops.Add([Windows.Media.GradientStop]::new($light, 0.0))
    $sweep.GradientStops.Add([Windows.Media.GradientStop]::new($mid, 0.45))
    $sweep.GradientStops.Add([Windows.Media.GradientStop]::new($deep, 1.0))
    $sweep.Freeze()

    $all = [Windows.Media.GeometryGroup]::new()
    for ($i = 0; $i -lt $blades; $i++) {
        $all.Children.Add((BladeGeometry ($i * (360.0 / $blades))))
    }
    $group.Children.Add([Windows.Media.GeometryDrawing]::new($sweep, $null, $all))

    # The hub, a shade darker so the three blades read as attached to something rather than
    # floating - and so the centre still has a shape at 16 px.
    $hubBrush = [Windows.Media.SolidColorBrush]::new($deep)
    $hubBrush.Freeze()
    $ring = [Windows.Media.EllipseGeometry]::new([Windows.Point]::new($centre, $centre), $hub, $hub)
    $group.Children.Add([Windows.Media.GeometryDrawing]::new($hubBrush, $null, $ring))

    $eye = [Windows.Media.SolidColorBrush]::new($light)
    $eye.Freeze()
    $inner = [Windows.Media.EllipseGeometry]::new([Windows.Point]::new($centre, $centre), $hub * 0.42, $hub * 0.42)
    $group.Children.Add([Windows.Media.GeometryDrawing]::new($eye, $null, $inner))

    $group.Freeze()
    $group
}

function Render([int] $size, $drawing) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.PushTransform([Windows.Media.ScaleTransform]::new($size / $canvas, $size / $canvas))
    $context.DrawDrawing($drawing)
    $context.Pop()
    $context.Close()

    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, 'Pbgra32')
    $bitmap.Render($visual)
    $bitmap.Freeze()
    $bitmap
}

function SavePng($bitmap, [string] $path) {
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}

$drawing = BuildDrawing

if ($PreviewOnly) {
    $previewDir = Join-Path $root 'research\runs\icon'
    New-Item -ItemType Directory -Force -Path $previewDir | Out-Null
    foreach ($size in 16, 24, 32, 48, 256) {
        SavePng (Render $size $drawing) (Join-Path $previewDir "app-$size.png")
    }
    # A sheet that shows the small sizes as they really are, next to each other on both
    # backgrounds - the only honest way to judge whether 16 px still reads.
    $sheetWidth = 520; $sheetHeight = 150
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.DrawRectangle([Windows.Media.Brushes]::White, $null, [Windows.Rect]::new(0, 0, $sheetWidth, $sheetHeight / 2))
    $dark = [Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromRgb(0x20, 0x20, 0x20))
    $context.DrawRectangle($dark, $null, [Windows.Rect]::new(0, $sheetHeight / 2, $sheetWidth, $sheetHeight / 2))
    $x = 16.0
    foreach ($size in 16, 24, 32, 48, 128) {
        foreach ($row in 0, 1) {
            $y = $row * ($sheetHeight / 2) + (($sheetHeight / 2) - $size) / 2
            $context.PushTransform([Windows.Media.TranslateTransform]::new($x, $y))
            $context.PushTransform([Windows.Media.ScaleTransform]::new($size / $canvas, $size / $canvas))
            $context.DrawDrawing($drawing)
            $context.Pop(); $context.Pop()
        }
        $x += $size + 20
    }
    $context.Close()
    $sheet = [Windows.Media.Imaging.RenderTargetBitmap]::new($sheetWidth, $sheetHeight, 96, 96, 'Pbgra32')
    $sheet.Render($visual)
    SavePng $sheet (Join-Path $previewDir 'app-sheet.png')
    Write-Host "Vorschau: $previewDir" -ForegroundColor Green
    return
}

# ---- the .ico container ------------------------------------------------------------------
# No encoder in .NET writes one, and the format is small enough to write out: a header, one
# 16-byte directory entry per size, then the images back to back.
function BmpFrame($bitmap) {
    [int] $w = $bitmap.PixelWidth
    [int] $h = $bitmap.PixelHeight
    $stride = $w * 4
    $pixels = [byte[]]::new($stride * $h)
    $bitmap.CopyPixels($pixels, $stride, 0)

    $out = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($out)
    # BITMAPINFOHEADER. The height is doubled because the mask counts as part of the image.
    $writer.Write([int] 40); $writer.Write([int] $w); $writer.Write([int] ($h * 2))
    $writer.Write([int16] 1); $writer.Write([int16] 32); $writer.Write([int] 0)
    $writer.Write([int] ($stride * $h)); $writer.Write([int] 0); $writer.Write([int] 0)
    $writer.Write([int] 0); $writer.Write([int] 0)
    # Bottom-up, as BMP has always been.
    for ($y = $h - 1; $y -ge 0; $y--) { $writer.Write($pixels, $y * $stride, $stride) }
    # The AND mask is not used for 32-bit frames, but it has to be there and 4-byte aligned.
    $maskStride = [Math]::Floor(($w + 31) / 32) * 4
    $writer.Write([byte[]]::new($maskStride * $h))
    $writer.Flush()
    $out.ToArray()
}

function PngFrame($bitmap) {
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $out = [IO.MemoryStream]::new()
    $encoder.Save($out)
    $out.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = foreach ($size in $sizes) {
    $bitmap = Render $size $drawing
    # The cast is not decoration: PowerShell unrolls an array on its way out of a function,
    # so without it Data arrives as Object[] and BinaryWriter has no overload for that.
    [pscustomobject]@{
        Size = $size
        # PNG only for the two large ones; small frames stay BMP, which every shell reads.
        Data = [byte[]] $(if ($size -ge 128) { PngFrame $bitmap } else { BmpFrame $bitmap })
    }
}

$stream = [IO.File]::Create($OutputPath)
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([int16] 0); $writer.Write([int16] 1); $writer.Write([int16] $frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        # 256 is written as 0: the field is one byte wide.
        $writer.Write([byte] ($frame.Size % 256)); $writer.Write([byte] ($frame.Size % 256))
        $writer.Write([byte] 0); $writer.Write([byte] 0)
        $writer.Write([int16] 1); $writer.Write([int16] 32)
        $writer.Write([int] $frame.Data.Length); $writer.Write([int] $offset)
        $offset += $frame.Data.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]] $frame.Data, 0, $frame.Data.Length) }
}
finally { $writer.Dispose(); $stream.Dispose() }

# Read it back the way Windows will. A container that is merely written is not a container
# that works, and a silently broken icon shows up as a blank square weeks later.
$check = [IO.File]::OpenRead($OutputPath)
try {
    $decoded = [Windows.Media.Imaging.IconBitmapDecoder]::new(
        $check, 'None', [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    $found = ($decoded.Frames | ForEach-Object { $_.PixelWidth } | Sort-Object)
    if ($found.Count -ne $sizes.Count) { throw "Nur $($found.Count) von $($sizes.Count) Groessen lesbar." }
}
finally { $check.Dispose() }

Write-Host ("Geschrieben: {0} ({1:N1} KB)" -f $OutputPath, ((Get-Item $OutputPath).Length / 1KB)) -ForegroundColor Green
Write-Host ("Zurueckgelesen: {0}" -f ($found -join ', ')) -ForegroundColor Green
