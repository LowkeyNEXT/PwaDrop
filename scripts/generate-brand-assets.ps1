[CmdletBinding()]
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\assets\brand\pwadrop-logo-master.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourcePath = [System.IO.Path]::GetFullPath($Source)
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Brand master was not found: $sourcePath"
}

function Export-LogoPng {
    param(
        [System.Drawing.Image]$Image,
        [int]$Size,
        [string]$Path
    )

    $targetPath = [System.IO.Path]::GetFullPath($Path)
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($targetPath)) | Out-Null
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($Image, [System.Drawing.Rectangle]::new(0, 0, $Size, $Size))
        }
        finally {
            $graphics.Dispose()
        }

        $bitmap.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

function Export-MultiSizeIcon {
    param(
        [System.Drawing.Image]$Image,
        [int[]]$Sizes,
        [string]$Path
    )

    $pngStreams = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($size in $Sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.DrawImage($Image, [System.Drawing.Rectangle]::new(0, 0, $size, $size))
            }
            finally {
                $graphics.Dispose()
            }

            $stream = [System.IO.MemoryStream]::new()
            try {
                $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $pngStreams.Add($stream.ToArray())
            }
            finally {
                $stream.Dispose()
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }

    $targetPath = [System.IO.Path]::GetFullPath($Path)
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($targetPath)) | Out-Null
    $file = [System.IO.File]::Open($targetPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    $writer = [System.IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$Sizes.Count)
        $offset = 6 + (16 * $Sizes.Count)
        for ($index = 0; $index -lt $Sizes.Count; $index++) {
            $size = $Sizes[$index]
            $bytes = $pngStreams[$index]
            $writer.Write([byte]$(if ($size -ge 256) { 0 } else { $size }))
            $writer.Write([byte]$(if ($size -ge 256) { 0 } else { $size }))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$bytes.Length)
            $writer.Write([uint32]$offset)
            $offset += $bytes.Length
        }

        foreach ($bytes in $pngStreams) {
            $writer.Write($bytes)
        }
    }
    finally {
        $writer.Dispose()
        $file.Dispose()
    }
}

$master = [System.Drawing.Image]::FromFile($sourcePath)
try {
    Export-LogoPng $master 512 (Join-Path $repositoryRoot 'src\PwaDrop.App\Assets\PwaDropLogo.png')
    Export-LogoPng $master 512 (Join-Path $repositoryRoot 'packaging\Assets\Logo512.png')
    Export-LogoPng $master 150 (Join-Path $repositoryRoot 'packaging\Assets\Square150x150Logo.png')
    Export-LogoPng $master 44 (Join-Path $repositoryRoot 'packaging\Assets\Square44x44Logo.png')
    Export-LogoPng $master 50 (Join-Path $repositoryRoot 'packaging\Assets\StoreLogo.png')
    Export-LogoPng $master 512 (Join-Path $repositoryRoot 'website\public\pwadrop-logo.png')
    Export-MultiSizeIcon $master @(16, 20, 24, 32, 40, 48, 64, 128, 256) (Join-Path $repositoryRoot 'src\PwaDrop.App\Assets\PwaDrop.ico')
}
finally {
    $master.Dispose()
}

Write-Host 'PWADrop brand assets regenerated from:' $sourcePath
