$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$blockGridSource = Get-Content -Encoding UTF8 (Join-Path $root 'Forms\BlockBrowserForm.BlockGrid.cs') -Raw
$thumbnailSource = Get-Content -Encoding UTF8 (Join-Path $root 'Forms\BlockBrowserForm.Thumbnails.cs') -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name did not find pattern: $pattern"
    }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) {
        throw "$name found forbidden pattern: $pattern"
    }
    Write-Host "PASS $name"
}

Assert-Contains 'block grid clones cached thumbnail before giving it to card' $blockGridSource 'card\.LoadThumbnail\(new\s+Bitmap\(_thumbCache\[ck\]\)\)'
Assert-NotContains 'block grid does not give shared cached image to card' $blockGridSource 'card\.LoadThumbnail\(_thumbCache\[ck\]\)'
Assert-Contains 'thumbnail timer already clones cached thumbnail before giving it to card' $thumbnailSource 'card\.LoadThumbnail\(new\s+Bitmap\(_thumbCache\[ck\]\)\)'
Assert-Contains 'thumbnail timer marks card when generation throws' $thumbnailSource 'catch\s*\{[\s\S]*?card\.SetThumbnailFailed\(true\)'

Write-Host 'ThumbnailCacheClone.Tests.ps1 passed'
