<#
    Generates the application and tray icons for Move Mouse as multi-resolution
    .ico files (16/32/48/256 px, PNG-compressed entries). Run once during
    development; the resulting .ico files are committed to the repository under
    src/MoveMouse/Assets so end users and CI do not need to regenerate them.
#>
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\..\src\MoveMouse\Assets")
)

Add-Type -AssemblyName System.Drawing

$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

function New-CursorPath {
    param([float]$Scale, [float]$OffsetX, [float]$OffsetY)
    # Classic mouse-pointer arrow silhouette, defined on a 0..100 design grid.
    $pts = @(
        @(28, 18), @(28, 82), @(44, 67), @(53, 86),
        @(62, 82), @(53, 63), @(74, 63)
    )
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $points = $pts | ForEach-Object { [System.Drawing.PointF]::new([float]($_[0] * $Scale + $OffsetX), [float]($_[1] * $Scale + $OffsetY)) }
    $path.AddPolygon($points)
    return $path
}

function New-RoundedRect {
    param([float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $Radius * 2
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $Width - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $Width - $d, $Y + $Height - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $Height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap {
    param(
        [int]$Size,
        [bool]$WithMotionLines,
        [bool]$ActiveBadge,
        [bool]$Transparent
    )

    $bmp = [System.Drawing.Bitmap]::new($Size, $Size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $margin = $Size * 0.06
    $rectSize = $Size - ($margin * 2)
    $radius = $rectSize * 0.26

    if (-not $Transparent) {
        $bgPath = New-RoundedRect -X $margin -Y $margin -Width $rectSize -Height $rectSize -Radius $radius
        $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.PointF]::new([float]$margin, [float]$margin),
            [System.Drawing.PointF]::new([float]($margin + $rectSize), [float]($margin + $rectSize)),
            [System.Drawing.Color]::FromArgb(255, 124, 92, 239),
            [System.Drawing.Color]::FromArgb(255, 58, 123, 233)
        )
        $g.FillPath($brush, $bgPath)
        $brush.Dispose()
        $bgPath.Dispose()
    }

    # Motion swoosh arcs (only on larger / detailed renders)
    if ($WithMotionLines) {
        $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(140, 255, 255, 255), [Math]::Max(1.5, $Size * 0.025))
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        for ($i = 0; $i -lt 3; $i++) {
            $r = $Size * (0.62 + $i * 0.09)
            $rect = [System.Drawing.RectangleF]::new([float]($Size * 0.50 - $r/2), [float]($Size * 0.18 - $r/2), [float]$r, [float]$r)
            $g.DrawArc($pen, $rect, -55, 50)
        }
        $pen.Dispose()
    }

    # Cursor glyph (white) with a soft drop shadow for depth
    $cursorScale = $Size * 0.0062
    $shadow = New-CursorPath -Scale $cursorScale -OffsetX ($Size * 0.025) -OffsetY ($Size * 0.03)
    $g.FillPath([System.Drawing.Brushes]::Black, $shadow)
    $shadow.Dispose()

    $cursor = New-CursorPath -Scale $cursorScale -OffsetX 0 -OffsetY 0
    $g.FillPath([System.Drawing.Brushes]::White, $cursor)
    $outline = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 58, 123, 233), [Math]::Max(1.0, $Size * 0.012))
    $g.DrawPath($outline, $cursor)
    $outline.Dispose()
    $cursor.Dispose()

    if ($ActiveBadge) {
        $badgeR = $Size * 0.26
        $badgeX = $Size - $badgeR - ($Size * 0.04)
        $badgeY = $Size - $badgeR - ($Size * 0.04)
        $g.FillEllipse([System.Drawing.Brushes]::White, $badgeX - 2, $badgeY - 2, $badgeR + 4, $badgeR + 4)
        $g.FillEllipse([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 48, 191, 110)), $badgeX, $badgeY, $badgeR, $badgeR)
    }

    $g.Dispose()
    return $bmp
}

function Save-Ico {
    param([string]$Path, [int[]]$Sizes, [bool]$WithMotionLines, [bool]$ActiveBadge, [bool]$Transparent)

    $images = @()
    foreach ($s in $Sizes) {
        $detailed = $WithMotionLines -and ($s -ge 48)
        $bmp = New-IconBitmap -Size $s -WithMotionLines $detailed -ActiveBadge $ActiveBadge -Transparent $Transparent
        $ms = [System.IO.MemoryStream]::new()
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $images += , @{ Size = $s; Bytes = $ms.ToArray() }
        $bmp.Dispose()
        $ms.Dispose()
    }

    $fs = [System.IO.FileStream]::new($Path, [System.IO.FileMode]::Create)
    $bw = [System.IO.BinaryWriter]::new($fs)

    # ICONDIR
    $bw.Write([UInt16]0)      # reserved
    $bw.Write([UInt16]1)      # type = icon
    $bw.Write([UInt16]$images.Count)

    $headerSize = 6 + (16 * $images.Count)
    $offset = $headerSize
    foreach ($img in $images) {
        $w = if ($img.Size -ge 256) { 0 } else { $img.Size }
        $h = $w
        $bw.Write([byte]$w)
        $bw.Write([byte]$h)
        $bw.Write([byte]0)    # color count
        $bw.Write([byte]0)    # reserved
        $bw.Write([UInt16]1)  # planes
        $bw.Write([UInt16]32) # bit count
        $bw.Write([UInt32]$img.Bytes.Length)
        $bw.Write([UInt32]$offset)
        $offset += $img.Bytes.Length
    }
    foreach ($img in $images) {
        $bw.Write($img.Bytes)
    }

    $bw.Flush()
    $bw.Dispose()
    $fs.Dispose()
}

$sizes = @(16, 32, 48, 256)

Save-Ico -Path (Join-Path $OutputDir "AppIcon.ico") -Sizes $sizes -WithMotionLines $true -ActiveBadge $false -Transparent $false
Save-Ico -Path (Join-Path $OutputDir "TrayIdle.ico") -Sizes @(16, 32) -WithMotionLines $false -ActiveBadge $false -Transparent $false
Save-Ico -Path (Join-Path $OutputDir "TrayActive.ico") -Sizes @(16, 32) -WithMotionLines $false -ActiveBadge $true -Transparent $false

Write-Output "Icons written to $OutputDir"
Get-ChildItem $OutputDir -Filter "*.ico" | Select-Object Name, Length
