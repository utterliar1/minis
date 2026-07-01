$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$formSource = @(
    Get-ChildItem -Path (Join-Path $repo 'BlockBrowser\Forms') -Filter 'BlockBrowserForm*.cs' |
        Sort-Object Name |
        ForEach-Object { Get-Content -Encoding UTF8 $_.FullName -Raw }
) -join "`n"

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'form has reusable visible thumbnail queue helper' $formSource 'private\s+void\s+QueueVisibleMissingThumbnails\(\)'
Assert-Contains 'form has viewport thumbnail helper' $formSource 'private\s+bool\s+IsCardNearThumbnailViewport\(BlockThumbnailCard\s+card\)'
Assert-Contains 'filter refreshes visible thumbnail queue' $formSource 'private\s+void\s+DoFilter\(\)[\s\S]*?QueueVisibleMissingThumbnails\(\)'
Assert-Contains 'thumbnail queue only loads viewport-near visible matching cards' $formSource '_cards\.Where\(c\s*=>\s*c\.Visible\s*&&\s*IsCardNearThumbnailViewport\(c\)\s*&&\s*BlockFilterService\.Matches\(c\.Block,\s*_txtSearch\.Text\)\s*&&\s*!HasThumbnail\(c\)\)'
Assert-Contains 'thumbnail viewport helper uses screen coordinates after scrolling' $formSource 'card\.RectangleToScreen\(card\.ClientRectangle\)'
Assert-Contains 'thumbnail viewport helper compares against flow screen viewport' $formSource '_flowBlocks\.RectangleToScreen\(_flowBlocks\.ClientRectangle\)'
Assert-Contains 'thumbnail viewport helper preloads around client area' $formSource 'viewBounds\.Inflate\(0,\s*Math\.Max\(_thumbSize,\s*120\)\)'
Assert-Contains 'stable flow panel exposes viewport changed event' $formSource 'public\s+event\s+EventHandler\s+ViewportChanged'
Assert-Contains 'stable flow panel detects mouse wheel viewport changes' $formSource 'protected\s+override\s+void\s+OnMouseWheel\(MouseEventArgs\s+e\)[\s\S]*?OnViewportChanged\(\)'
Assert-Contains 'stable flow panel detects scrollbar messages' $formSource 'WM_VSCROLL[\s\S]*?WM_MOUSEWHEEL[\s\S]*?OnViewportChanged\(\)'
Assert-Contains 'form declares viewport thumbnail debounce timer' $formSource 'private\s+System\.Windows\.Forms\.Timer\s+_viewportThumbTimer;'
Assert-Contains 'viewport changes request delayed thumbnail queue' $formSource '_flowBlocks\.ViewportChanged\s*\+=\s*\(s,\s*e\)\s*=>\s*RequestVisibleThumbnailQueue\(\)'
Assert-Contains 'delayed thumbnail queue helper restarts timer' $formSource 'private\s+void\s+RequestVisibleThumbnailQueue\(\)[\s\S]*?_viewportThumbTimer\.Stop\(\)[\s\S]*?_viewportThumbTimer\.Start\(\)'
Assert-Contains 'viewport queue timer triggers visible thumbnail queue' $formSource '_viewportThumbTimer\.Tick\s*\+=\s*\(s,\s*e\)\s*=>\s*\{[\s\S]*?_viewportThumbTimer\.Stop\(\);[\s\S]*?QueueVisibleMissingThumbnails\(\);[\s\S]*?\}'
Assert-Contains 'card batch build requests visible thumbnail queue after adding new controls' $formSource 'CardTimerTick\(object\s+sender,\s+EventArgs\s+e\)[\s\S]*?RefreshBlockGridPaint\(\)[\s\S]*?RequestVisibleThumbnailQueue\(\)'
Assert-Contains 'visible-only prebuild still uses visible cards after form is shown' $formSource 'PrebuildVisibleThumbnails\(\)[\s\S]*?_cards\.Where\(c\s*=>\s*c\.Visible\s*&&\s*!HasThumbnail\(c\)\)'
Assert-Contains 'show blocks uses visible queue helper' $formSource 'ShowBlocks\(List<BlockInfo>\s+blocks\)[\s\S]*?QueueVisibleMissingThumbnails\(\)'
Assert-Contains 'thumbnail queue stops previous timer before recalculating' $formSource 'QueueVisibleMissingThumbnails\(\)[\s\S]*?_thumbTimer\.Stop\(\)'

Write-Host 'ThumbnailTriggerOptimization.Tests.ps1 passed'
