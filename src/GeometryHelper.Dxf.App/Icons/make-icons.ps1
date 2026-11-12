# Writes the icons of GeometryHelper DXF from the vector drawing below: app.ico, the app's (ApplicationIcon and the window's),
# and drawing-file.ico, the icon of a .dxf or .dwg file associated with the app (--register). Each holds 16, 24, 32, 48 and
# 256 pixels: 16 and 24 are drawn on their own pixel grid, the others from one drawing 256 units square. The 256 frame is a
# PNG, the smaller ones 32-bit bitmaps. Run it with Windows PowerShell from any folder after changing a shape:
#   powershell -NoProfile -ExecutionPolicy Bypass -File src\GeometryHelper.Dxf.App\Icons\make-icons.ps1
# The drawing is our own: a set square and a point on a blue tile, nothing taken from another program's icon.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

$blue = '#1F6FD6'
$blueEdge = '#154C96'
$orange = '#FF9F1C'
$paper = '#FFFFFF'
$paperEdge = '#5B6573'
$fold = '#DCE1E8'

# The shapes of each size: [units across, tile, tile radius, set square (even-odd: the hole a second figure), point x, y, r].
$app = @{
    16  = @(16, '0.5,0.5,15,15', 3, 'M3,13 L13,13 L3,3 Z', 11.5, 4.5, 2)
    24  = @(24, '0.5,0.5,23,23', 4.5, 'M5,19 L19,19 L5,5 Z M8,16 L11.76,16 L8,12.24 Z', 17.5, 6.5, 2.5)
    256 = @(256, '16,16,224,224', 40, 'M60,200 L200,200 L60,60 Z M88,172 L132.4,172 L88,127.6 Z', 184, 76, 24)
}

# The file: a page with its corner folded, the app's tile on it; [units across, page, fold, then the tile as for the app].
$doc = @{
    16  = @(16, 'M2.5,0.5 L9.5,0.5 L13.5,4.5 L13.5,15.5 L2.5,15.5 Z', 'M9.5,0.5 L9.5,4.5 L13.5,4.5 Z', '4,7,8,8', 1.5, 'M5.5,13.5 L10.5,13.5 L5.5,8.5 Z', 0, 0, 0)
    24  = @(24, 'M3.5,0.5 L14.5,0.5 L20.5,6.5 L20.5,23.5 L3.5,23.5 Z', 'M14.5,0.5 L14.5,6.5 L20.5,6.5 Z', '6.5,10.5,11,11', 2, 'M8.5,19.5 L15.5,19.5 L8.5,12.5 Z', 15, 13, 1.5)
    256 = @(256, 'M48,8 L168,8 L224,64 L224,248 L48,248 Z', 'M168,8 L168,64 L224,64 Z', '80,120,112,112', 20, 'M102,212 L172,212 L102,142 Z M116,198 L138.2,198 L116,175.8 Z', 164, 150, 12)
}

function Get-Brush($hex) { [System.Windows.Media.BrushConverter]::new().ConvertFromString($hex) }

function Get-Shape($size, $table) {
    if ($table.ContainsKey($size)) { return $table[$size] }
    return $table[256]
}

function Write-Tile($dc, $rect, $radius, $square, $px, $py, $pr, $line) {
    $r = [System.Windows.Rect]::Parse($rect)
    $dc.DrawRoundedRectangle((Get-Brush $blue), [System.Windows.Media.Pen]::new((Get-Brush $blueEdge), $line), $r, $radius, $radius)
    $dc.DrawGeometry((Get-Brush $paper), $null, [System.Windows.Media.Geometry]::Parse($square))
    if ($pr -gt 0) {
        $dc.DrawEllipse((Get-Brush $orange), $null, [System.Windows.Point]::new($px, $py), $pr, $pr)
    }
}

function Get-Rendering($size, $isDocument) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $dc = $visual.RenderOpen()
    $s = if ($isDocument) { Get-Shape $size $doc } else { Get-Shape $size $app }
    $units = $s[0]
    $dc.PushTransform([System.Windows.Media.ScaleTransform]::new($size / $units, $size / $units))
    $line = $units / $size

    if ($isDocument) {
        $pen = [System.Windows.Media.Pen]::new((Get-Brush $paperEdge), $line)
        $dc.DrawGeometry((Get-Brush $paper), $pen, [System.Windows.Media.Geometry]::Parse($s[1]))
        $dc.DrawGeometry((Get-Brush $fold), $pen, [System.Windows.Media.Geometry]::Parse($s[2]))
        Write-Tile $dc $s[3] $s[4] $s[5] $s[6] $s[7] $s[8] $line
    }
    else {
        Write-Tile $dc $s[1] $s[2] $s[3] $s[4] $s[5] $s[6] $line
    }

    $dc.Pop()
    $dc.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    return [System.Windows.Media.Imaging.FormatConvertedBitmap]::new($bitmap, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
}

function Get-FrameBytes($bitmap, $size) {
    if ($size -ge 256) {
        $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
        $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $stream = [System.IO.MemoryStream]::new()
        $encoder.Save($stream)
        return , $stream.ToArray()
    }

    # A bitmap of the icon format: its header with the height doubled, the rows bottom up, then an AND mask left clear.
    $stride = $size * 4
    $pixels = New-Object byte[] ($stride * $size)
    $bitmap.CopyPixels($pixels, $stride, 0)
    $maskStride = [int][Math]::Ceiling($size / 32) * 4
    $stream = [System.IO.MemoryStream]::new()
    $w = [System.IO.BinaryWriter]::new($stream)
    $w.Write([int]40); $w.Write([int]$size); $w.Write([int]($size * 2)); $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]0); $w.Write([int]($stride * $size + $maskStride * $size)); $w.Write([int]0); $w.Write([int]0)
    $w.Write([int]0); $w.Write([int]0)
    for ($y = $size - 1; $y -ge 0; $y--) { $w.Write($pixels, $y * $stride, $stride) }
    $w.Write((New-Object byte[] ($maskStride * $size)))
    $w.Flush()
    return , $stream.ToArray()
}

function Write-Icon($path, $isDocument) {
    $sizes = 16, 24, 32, 48, 256
    $frames = foreach ($size in $sizes) { , (Get-FrameBytes (Get-Rendering $size $isDocument) $size) }
    $stream = [System.IO.File]::Create($path)
    $w = [System.IO.BinaryWriter]::new($stream)
    $w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $across = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $w.Write([byte]$across); $w.Write([byte]$across); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$frames[$i].Length); $w.Write([int]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $w.Write($frame) }
    $w.Close()
    Write-Output ("Wrote " + $path + " (" + (Get-Item $path).Length + " bytes)")
}

Write-Icon (Join-Path $PSScriptRoot 'app.ico') $false
Write-Icon (Join-Path $PSScriptRoot 'drawing-file.ico') $true
