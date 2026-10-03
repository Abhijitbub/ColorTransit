$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'StoreAssets/Upload'
New-Item -ItemType Directory -Force $out | Out-Null
function Export-Size([string]$source, [string]$target, [int]$width, [int]$height) {
    $inputImage = [System.Drawing.Image]::FromFile((Join-Path $root $source))
    $bitmap = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($inputImage, 0, 0, $width, $height)
        $bitmap.Save((Join-Path $out $target), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose(); $inputImage.Dispose() }
}
Export-Size 'Assets/ColorTransit/Resources/Art/TransitIcon.png' 'Icon-512.png' 512 512
Export-Size 'StoreAssets/Source/Feature.png' 'Feature-1024x500.png' 1024 500
Write-Output 'Exported opaque 512px icon and 1024x500 feature graphic.'
