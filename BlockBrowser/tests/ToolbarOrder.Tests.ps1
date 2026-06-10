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
    'btnInsertSettings',
    'btnAddToLib',
    'btnExportBlock',
    'btnRefresh',
    'btnManage',
    'btnLibrary'
)

Assert-False 'toolbar excludes create category button' ($items.Contains('btnCreateCategory'))
Assert-False 'toolbar excludes direct rename button' ($items.Contains('btnRename'))
Assert-False 'toolbar excludes direct delete button' ($items.Contains('btnDelete'))
Assert-False 'toolbar excludes direct open folder button' ($items.Contains('btnOpenFolder'))
Assert-False 'toolbar excludes direct mirror update button' ($items.Contains('btnUpdateMirror'))
Assert-False 'toolbar excludes direct sync button' ($items.Contains('btnSync'))
Assert-False 'toolbar excludes direct settings button' ($items.Contains('btnSettings'))

$lastIndex = -1
foreach ($item in $expectedOrder) {
    $index = $items.IndexOf($item)
    Assert-True ("toolbar contains " + $item) ($index -ge 0)
    Assert-True ("toolbar order after previous for " + $item) ($index -gt $lastIndex)
    $lastIndex = $index
}

$manageMatch = [regex]::Match($formSource, 'btnManage\.DropDownItems\.AddRange\(new\s+ToolStripItem\[\]\s*\{(?<items>[\s\S]*?)\}\);')
Assert-True 'manage menu add range block found' $manageMatch.Success
$manageItems = $manageMatch.Groups['items'].Value
Assert-False 'manage menu excludes export block' ($manageItems.Contains('btnExportBlock'))
$expectedManageOrder = @('btnRename', 'btnDelete', 'btnOpenFolder')
$lastIndex = -1
foreach ($item in $expectedManageOrder) {
    $index = $manageItems.IndexOf($item)
    Assert-True ("manage menu contains " + $item) ($index -ge 0)
    Assert-True ("manage menu order after previous for " + $item) ($index -gt $lastIndex)
    $lastIndex = $index
}

$libraryMatch = [regex]::Match($formSource, 'btnLibrary\.DropDownItems\.AddRange\(new\s+ToolStripItem\[\]\s*\{(?<items>[\s\S]*?)\}\);')
Assert-True 'library menu add range block found' $libraryMatch.Success
$libraryItems = $libraryMatch.Groups['items'].Value
$expectedLibraryOrder = @('btnUpdateMirror', 'btnSync', 'btnPrebuildThumbnails', 'btnRebuildThumbnails', 'btnSettings')
$lastIndex = -1
foreach ($item in $expectedLibraryOrder) {
    $index = $libraryItems.IndexOf($item)
    Assert-True ("library menu contains " + $item) ($index -ge 0)
    Assert-True ("library menu order after previous for " + $item) ($index -gt $lastIndex)
    $lastIndex = $index
}

Write-Host 'ToolbarOrder.Tests.ps1 passed'
