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
Assert-Contains 'filter refreshes visible thumbnail queue' $formSource 'private\s+void\s+DoFilter\(\)[\s\S]*?QueueVisibleMissingThumbnails\(\)'
Assert-Contains 'thumbnail queue uses search matching instead of control visibility' $formSource '_cards\.Where\(c\s*=>\s*BlockFilterService\.Matches\(c\.Block,\s*_txtSearch\.Text\)\s*&&\s*!HasThumbnail\(c\)\)'
Assert-Contains 'visible-only prebuild still uses visible cards after form is shown' $formSource 'PrebuildVisibleThumbnails\(\)[\s\S]*?_cards\.Where\(c\s*=>\s*c\.Visible\s*&&\s*!HasThumbnail\(c\)\)'
Assert-Contains 'show blocks uses visible queue helper' $formSource 'ShowBlocks\(List<BlockInfo>\s+blocks\)[\s\S]*?QueueVisibleMissingThumbnails\(\)'
Assert-Contains 'thumbnail queue stops previous timer before recalculating' $formSource 'QueueVisibleMissingThumbnails\(\)[\s\S]*?_thumbTimer\.Stop\(\)'

Write-Host 'ThumbnailTriggerOptimization.Tests.ps1 passed'
