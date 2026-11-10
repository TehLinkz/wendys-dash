$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets'
New-Item -ItemType Directory -Path $assetDir -Force | Out-Null
$pngs = @()
foreach ($size in @(48, 256)) {
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([Drawing.Color]::FromArgb(198, 43, 57))
    $pen = [Drawing.Pen]::new([Drawing.Color]::White, [single]($size * 0.085))
    $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
    foreach ($x in @(0.28, 0.52)) {
        $points = [Drawing.PointF[]]@([Drawing.PointF]::new([single]($size * $x), [single]($size * 0.29)), [Drawing.PointF]::new([single]($size * ($x + 0.18)), [single]($size * 0.5)), [Drawing.PointF]::new([single]($size * $x), [single]($size * 0.71)))
        $g.DrawLines($pen, $points)
    }
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,$stream.ToArray()
    $stream.Dispose(); $pen.Dispose(); $g.Dispose(); $bitmap.Dispose()
}
$file = [IO.File]::Create((Join-Path $assetDir 'app.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]2)
    $offset = 38
    for ($i = 0; $i -lt 2; $i++) {
        $dimension = if ($i -eq 0) { 48 } else { 0 }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32)
        $writer.Write([UInt32]$pngs[$i].Length); $writer.Write([UInt32]$offset)
        $offset += $pngs[$i].Length
    }
    foreach ($png in $pngs) { $writer.Write([byte[]]$png) }
} finally { $writer.Dispose() }
