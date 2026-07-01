$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$formSource = Get-Content -Encoding UTF8 (Join-Path $root 'Forms\BlockBrowserForm.BlockGrid.cs') -Raw
$allFormSource = @(
    Get-ChildItem -Path (Join-Path $root 'Forms') -Filter 'BlockBrowserForm*.cs' |
        Sort-Object Name |
        ForEach-Object { Get-Content -Encoding UTF8 $_.FullName -Raw }
) -join "`n"
$loadingText = -join ([char[]](0x52A0, 0x8F7D, 0x4E2D, 0x002E, 0x002E, 0x002E))
$progressPrefix = -join ([char[]](0x52A0, 0x8F7D, 0x4E2D, 0x002E, 0x002E, 0x002E, 0x0020))

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name unexpectedly found pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-True($name, $actual) {
    if (-not $actual) { throw "$name failed. Expected true." }
    Write-Host "PASS $name"
}

Assert-Contains 'load data uses async task path' $formSource 'private\s+async\s+Task\s+LoadDataAsync\(\)'
Assert-Contains 'load data shows loading state' $formSource ('_lblStatus\.Text\s*=\s*"' + [regex]::Escape($loadingText) + '"')
Assert-Contains 'show blocks uses card batch builder' $formSource 'BeginCardBuild\(catKey,\s*blocks\s*\?\?\s*new\s+List<BlockInfo>\(\)\)'
Assert-Contains 'card batch builder exists' $formSource 'private\s+void\s+BeginCardBuild\(string\s+catKey,\s+List<BlockInfo>\s+blocks\)'
Assert-Contains 'card batch size constant exists' $formSource 'private\s+const\s+int\s+CardBuildBatchSize\s*=\s*24'
Assert-Contains 'initial card batch size constant exists' $formSource 'private\s+const\s+int\s+InitialCardBuildBatchSize\s*=\s*18'
Assert-Contains 'card timer interval stays responsive' $formSource '_cardTimer\s*=\s*new\s+System\.Windows\.Forms\.Timer\s*\{\s*Interval\s*=\s*15\s*\}'
Assert-Contains 'card timer tick exists' $formSource 'private\s+void\s+CardTimerTick\(object\s+sender,\s+EventArgs\s+e\)'
Assert-Contains 'card build completion stores cache' $formSource '_categoryCards\[_pendingCategoryKey\]\s*=\s*_pendingBuiltCards'
Assert-Contains 'card batch build updates loading progress' $formSource ('_lblStatus\.Text\s*=\s*"' + [regex]::Escape($progressPrefix) + '".*?_pendingCardIndex.*?"/".*?_pendingBlocks\.Count')
Assert-Contains 'form has background task stop helper' $allFormSource 'private\s+void\s+StopPanelBackgroundWork\(\)'
Assert-Contains 'insert stops panel background work before closing' $allFormSource 'DoInsert\(\)[\s\S]*?StopPanelBackgroundWork\(\)[\s\S]*?this\.Close\(\)'
Assert-NotContains 'insert does not hide panel before modal close' $allFormSource 'DoInsert\(\)[\s\S]*?this\.Hide\(\)[\s\S]*?this\.Close\(\)'

Write-Host 'BlockBrowserLaunchOptimization.Tests.ps1 passed'
