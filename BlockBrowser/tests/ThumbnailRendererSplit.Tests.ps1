$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repo 'BlockBrowser'
$thumbnailSource = Get-Content -Encoding UTF8 (Join-Path $project 'Thumbnails\BlockLibrary.Thumbnails.cs') -Raw
$rendererPath = Join-Path $project 'Thumbnails\CadThumbnailRenderer.cs'
$csprojSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.csproj') -Raw
$acadSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.AutoCAD.csproj') -Raw
$zwcadSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.ZWCAD.csproj') -Raw

function Assert-True($name, $actual) {
    if (-not $actual) { throw "$name failed. Expected true." }
    Write-Host "PASS $name"
}

function Assert-False($name, $actual) {
    if ($actual) { throw "$name failed. Expected false." }
    Write-Host "PASS $name"
}

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-True 'CAD thumbnail renderer file exists' (Test-Path $rendererPath)
$rendererSource = Get-Content -Encoding UTF8 $rendererPath -Raw

Assert-Contains 'renderer declares renderer class' $rendererSource 'static\s+class\s+CadThumbnailRenderer'
Assert-Contains 'renderer exposes TryRender entry point' $rendererSource 'static\s+Bitmap\s+TryRender\(string\s+filePath,\s*int\s+size\)'
Assert-Contains 'renderer owns CAD database loading' $rendererSource 'new\s+Database\(false,\s*true\)'
Assert-Contains 'renderer owns block content rendering' $rendererSource 'RenderBlockContents\('
Assert-Contains 'renderer owns entity drawing' $rendererSource 'DrawEntXf\('

Assert-Contains 'thumbnail library delegates CAD rendering' $thumbnailSource 'CadThumbnailRenderer\.TryRender\(block\.FilePath,\s*size\)'
Assert-False 'thumbnail library no direct CAD conditional usings' ($thumbnailSource -match '#if\s+AUTOCAD|#elif\s+GSTARCAD|#elif\s+ZWCAD')
Assert-False 'thumbnail library no direct CAD database loading' ($thumbnailSource -match 'new\s+Database\(false,\s*true\)')
Assert-False 'thumbnail library no direct block renderer helper' ($thumbnailSource -match 'RenderBlockContents\(')
Assert-False 'thumbnail library no direct entity drawing helper' ($thumbnailSource -match 'DrawEntXf\(')

Assert-Contains 'main project compiles CAD renderer' $csprojSource 'Compile Include="Thumbnails\\CadThumbnailRenderer\.cs"'
Assert-Contains 'AutoCAD project compiles CAD renderer' $acadSource 'Compile Include="Thumbnails\\CadThumbnailRenderer\.cs"'
Assert-Contains 'ZWCAD project compiles CAD renderer' $zwcadSource 'Compile Include="Thumbnails\\CadThumbnailRenderer\.cs"'

Write-Host 'ThumbnailRendererSplit.Tests.ps1 passed'
