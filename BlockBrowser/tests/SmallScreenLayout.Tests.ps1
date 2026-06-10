$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$formSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserForm.cs') -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'form declares search host field for adaptive width' $formSource 'private\s+ToolStripControlHost\s+_txtSearchHost;'
Assert-Contains 'form clamps initial size to working area' $formSource 'private\s+Size\s+GetInitialFormSize\(\)[\s\S]*?Screen\.FromPoint\(Cursor\.Position\)\.WorkingArea'
Assert-Contains 'initial size reserves horizontal screen margin' $formSource 'work\.Width\s*-\s*40'
Assert-Contains 'initial size reserves vertical screen margin' $formSource 'work\.Height\s*-\s*60'
Assert-Contains 'form uses clamped initial size' $formSource 'Size\s*=\s*GetInitialFormSize\(\)'
Assert-Contains 'form keeps explicit minimum size constants' $formSource 'MinimumSize\s*=\s*new\s+Size\(MinFormWidth,\s*MinFormHeight\)'
Assert-Contains 'search host uses adaptive initial width' $formSource '_txtSearchHost\s*=\s*new\s+ToolStripControlHost\(_txtSearch\)\s*\{[\s\S]*?Width\s*=\s*GetSearchBoxWidth\(\)'
Assert-Contains 'search width shrinks at narrow form widths' $formSource 'if\s*\(width\s*<=\s*760\)\s*return\s*100'
Assert-Contains 'search width uses middle width for compact screens' $formSource 'if\s*\(width\s*<=\s*900\)\s*return\s*120'
Assert-Contains 'search width returns normal desktop width' $formSource 'return\s*140'
Assert-Contains 'form updates search box on resize' $formSource 'Resize\s*\+=\s*\(s,\s*e\)\s*=>\s*UpdateSearchBoxWidth\(\)'

Write-Host 'SmallScreenLayout.Tests.ps1 passed'
