$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$dialogPath = Join-Path $repo 'BlockBrowser\ExportBlocksDialog.cs'
$pluginPath = Join-Path $repo 'BlockBrowser\BlockBrowserPlugin.cs'

if (-not (Test-Path $dialogPath)) {
    throw "Missing dialog source file: $dialogPath"
}

$dialogSource = Get-Content -Encoding UTF8 $dialogPath -Raw
$pluginSource = Get-Content -Encoding UTF8 $pluginPath -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'dialog class exists' $dialogSource 'class\s+ExportBlocksDialog\s*:\s*Form'
Assert-Contains 'dialog uses dpi scaling' $dialogSource 'AutoScaleMode\s*=\s*AutoScaleMode\.Dpi'
Assert-Contains 'dialog uses table layout' $dialogSource 'new\s+TableLayoutPanel'
Assert-Contains 'dialog exposes selected blocks' $dialogSource 'IList<string>\s+SelectedBlocks'
Assert-Contains 'dialog exposes selected category' $dialogSource 'string\s+SelectedCategory'
Assert-Contains 'dialog filters blocks from search text' $dialogSource 'ApplyFilter'
Assert-Contains 'plugin opens export dialog' $pluginSource 'new\s+ExportBlocksDialog'
Assert-NotContains 'plugin no inline export form' $pluginSource 'using\s*\(var\s+form\s*=\s*new\s+Form\(\)\)'

Write-Host 'ExportBlocksDialog.Tests.ps1 passed'
