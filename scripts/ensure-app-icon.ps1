Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$pngPath = Join-Path $projectRoot "kikisenapp.png"
$icoPath = Join-Path $projectRoot "assets\kikisenapp.ico"

if (-not (Test-Path $pngPath)) {
    throw "アイコン画像が見つかりません: $pngPath"
}

Add-Type -AssemblyName System.Drawing

function New-ResizedPngBytes {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.Image]$SourceImage,

        [Parameter(Mandatory = $true)]
        [int]$Size
    )

    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.DrawImage($SourceImage, 0, 0, $Size, $Size)

        $memoryStream = New-Object System.IO.MemoryStream
        try {
            $bitmap.Save($memoryStream, [System.Drawing.Imaging.ImageFormat]::Png)
            return ,$memoryStream.ToArray()
        }
        finally {
            $memoryStream.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$sourceImage = [System.Drawing.Image]::FromFile($pngPath)
try {
    $frames = foreach ($size in 16, 32, 48, 256) {
        [pscustomobject]@{
            Size = $size
            Bytes = [byte[]](New-ResizedPngBytes -SourceImage $sourceImage -Size $size)
        }
    }
}
finally {
    $sourceImage.Dispose()
}

New-Item -ItemType Directory -Force (Split-Path -Parent $icoPath) | Out-Null

$fileStream = [System.IO.File]::Create($icoPath)
$writer = New-Object System.IO.BinaryWriter($fileStream)

try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $dimensionByte = if ($frame.Size -ge 256) { [byte]0 } else { [byte]$frame.Size }

        $writer.Write($dimensionByte)
        $writer.Write($dimensionByte)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frame.Bytes.Length)
        $writer.Write([UInt32]$offset)

        $offset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) {
        $writer.Write([byte[]]$frame.Bytes)
    }
}
finally {
    $writer.Dispose()
    $fileStream.Dispose()
}
