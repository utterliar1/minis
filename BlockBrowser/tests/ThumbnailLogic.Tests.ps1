$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Library\BlockInfo.cs'
    Join-Path $root 'Thumbnails\PlaceholderImageFactory.cs'
    Join-Path $root 'Thumbnails\ThumbnailCacheService.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Drawing.dll',
    'System.Core.dll'
)

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        throw "$name failed. Expected: [$expected], Actual: [$actual]"
    }
    Write-Host "PASS $name"
}

function Assert-True($name, $actual) {
    if (-not $actual) {
        throw "$name failed. Expected true."
    }
    Write-Host "PASS $name"
}

function Assert-False($name, $actual) {
    if ($actual) {
        throw "$name failed. Expected false."
    }
    Write-Host "PASS $name"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('BlockBrowserThumbnailTests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

try {
    $dwg = Join-Path $tempRoot 'Socket.dwg'
    Set-Content -Encoding ASCII -Path $dwg -Value 'dwg'
    $block = New-Object BlockBrowser.BlockInfo
    $block.FilePath = $dwg
    $block.Category = 'Electrical'

    $key1 = [BlockBrowser.ThumbnailCacheService]::GetCacheKey($block)
    Assert-Equal 'cache key length' 16 $key1.Length
    Start-Sleep -Milliseconds 30
    Add-Content -Encoding ASCII -Path $dwg -Value 'changed'
    $key2 = [BlockBrowser.ThumbnailCacheService]::GetCacheKey($block)
    Assert-False 'cache key changes with source file' ($key1 -eq $key2)

    $cacheDir = Join-Path $tempRoot '.thumbs'
    $cachePath = [BlockBrowser.ThumbnailCacheService]::GetCachePath($cacheDir, $block)
    Assert-True 'cache path under cache dir' ($cachePath.StartsWith($cacheDir))
    Assert-True 'cache path is png' ($cachePath.EndsWith('.png'))

    $src = New-Object System.Drawing.Bitmap 20, 10
    $scaled = [BlockBrowser.ThumbnailCacheService]::ScaleToSquare($src, 64)
    Assert-Equal 'scaled width' 64 $scaled.Width
    Assert-Equal 'scaled height' 64 $scaled.Height
    $scaled.Dispose()
    $src.Dispose()

    $solid = New-Object System.Drawing.Bitmap 8, 8
    $g = [System.Drawing.Graphics]::FromImage($solid)
    $g.Clear([System.Drawing.Color]::White)
    $g.Dispose()
    Assert-False 'solid bitmap is not useful' ([BlockBrowser.ThumbnailCacheService]::IsBitmapUseful($solid))
    $solid.SetPixel(4, 4, [System.Drawing.Color]::Black)
    Assert-True 'varied bitmap is useful' ([BlockBrowser.ThumbnailCacheService]::IsBitmapUseful($solid))
    $solid.Dispose()

    $placeholder = [BlockBrowser.PlaceholderImageFactory]::Generate('VeryLongBlockNameForLabel', 96)
    Assert-Equal 'placeholder width' 96 $placeholder.Width
    Assert-Equal 'placeholder height' 96 $placeholder.Height
    $placeholder.Dispose()
    [BlockBrowser.PlaceholderImageFactory]::Clear()

    $image = New-Object System.Drawing.Bitmap 32, 32
    $paint = [System.Drawing.Graphics]::FromImage($image)
    $paint.Clear([System.Drawing.Color]::Red)
    $paint.Dispose()
    [BlockBrowser.ThumbnailCacheService]::SaveThumbnailCache($cachePath, $image)
    $image.Dispose()
    Assert-True 'thumbnail cache saved' (Test-Path $cachePath)
    $loaded = [BlockBrowser.ThumbnailCacheService]::TryLoadValidCache($cachePath, $dwg, 48)
    Assert-True 'valid cache loads' ($loaded -ne $null)
    Assert-Equal 'loaded cache scaled width' 48 $loaded.Width
    $loaded.Dispose()

    [BlockBrowser.ThumbnailCacheService]::RefreshThumbnail($cacheDir, $block)
    Assert-False 'refresh thumbnail deletes cache' (Test-Path $cachePath)

    $old = Join-Path $cacheDir 'old.png'
    $large = Join-Path $cacheDir 'large.png'
    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
    Set-Content -Encoding ASCII -Path $old -Value 'old'
    Set-Content -Encoding ASCII -Path $large -Value ('x' * 2048)
    (Get-Item $old).LastWriteTime = (Get-Date).AddDays(-31)
    [BlockBrowser.ThumbnailCacheService]::CleanupDiskCache($cacheDir, 1, 1)
    Assert-False 'cleanup removes old cache file' (Test-Path $old)
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

Write-Host 'ThumbnailLogic.Tests.ps1 passed'
