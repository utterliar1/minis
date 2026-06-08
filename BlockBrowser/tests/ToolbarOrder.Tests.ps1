$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$formSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserForm.cs') -Raw

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

$match = [regex]::Match($formSource, '_toolbar\.Items\.AddRange\(new ToolStripItem\[\]\s*\{(?<items>[\s\S]*?)\}\);')
Assert-True 'toolbar add range block found' $match.Success

$items = $match.Groups['items'].Value
$expectedOrder = @(
    'lblSearch',
    'txtSearchHost',
    'lblSize',
    'cmbHost',
    'btnInsert',
    'btnAddToLib',
    'btnExportBlock',
    'btnRename',
    'btnDelete',
    'btnRefresh',
    'btnOpenFolder',
    'btnUpdateMirror',
    'btnSync',
    'btnSettings'
)

Assert-False 'toolbar excludes create category button' ($items.Contains('btnCreateCategory'))

$lastIndex = -1
foreach ($item in $expectedOrder) {
    $index = $items.IndexOf($item)
    Assert-True ("toolbar contains " + $item) ($index -ge 0)
    Assert-True ("toolbar order after previous for " + $item) ($index -gt $lastIndex)
    $lastIndex = $index
}

Write-Host 'ToolbarOrder.Tests.ps1 passed'
