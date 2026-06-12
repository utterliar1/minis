$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$formSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserForm.cs') -Raw
$updateLocalLibraryText = -join ([char[]](0x66F4, 0x65B0, 0x672C, 0x5730, 0x56FE, 0x5E93))
$updateLocalMirrorText = -join ([char[]](0x66F4, 0x65B0, 0x672C, 0x5730, 0x526F, 0x672C))
$completeThumbnailsText = -join ([char[]](0x8865, 0x5168, 0x7F29, 0x7565, 0x56FE))
$prebuildThumbnailsText = -join ([char[]](0x9884, 0x751F, 0x6210, 0x7F29, 0x7565, 0x56FE))

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

function Find-TokenIndex($text, $token) {
    $match = [regex]::Match($text, '(?<![A-Za-z0-9_])' + [regex]::Escape($token) + '(?![A-Za-z0-9_])')
    if ($match.Success) { return $match.Index }
    return -1
}

$match = [regex]::Match($formSource, '_toolbar\.Items\.AddRange\(new ToolStripItem\[\]\s*\{(?<items>[\s\S]*?)\}\);')
Assert-True 'toolbar add range block found' $match.Success

$items = $match.Groups['items'].Value
$expectedOrder = @(
    'lblSearch',
    '_txtSearchHost',
    'lblSize',
    'cmbHost',
    'btnInsert',
    'btnInsertSettings',
    'btnAddToLib',
    'btnExportBlock',
    'btnRefresh',
    'btnUpdateLocalLibrary',
    'btnManage',
    'btnLibrary'
)

Assert-False 'toolbar excludes create category button' ($items.Contains('btnCreateCategory'))
Assert-False 'toolbar excludes direct rename button' ($items.Contains('btnRename'))
Assert-False 'toolbar excludes direct delete button' ($items.Contains('btnDelete'))
Assert-False 'toolbar excludes direct open folder button' ($items.Contains('btnOpenFolder'))
Assert-False 'toolbar excludes direct sync button' ($items.Contains('btnSync'))
Assert-False 'toolbar excludes direct settings button' ($items.Contains('btnSettings'))
Assert-True 'toolbar uses user friendly local library update text' ($formSource.Contains('new ToolStripButton("' + $updateLocalLibraryText + '")'))
Assert-False 'toolbar does not use local mirror wording' ($formSource.Contains('new ToolStripButton("' + $updateLocalMirrorText + '")'))
Assert-True 'library menu uses complete thumbnails wording' ($formSource.Contains('new ToolStripMenuItem("' + $completeThumbnailsText + '")'))
Assert-False 'library menu no prebuild thumbnails wording' ($formSource.Contains('new ToolStripMenuItem("' + $prebuildThumbnailsText + '")'))
Assert-True 'manage actions use menu items for dropdown layout' ($formSource -match 'var\s+btnRename\s*=\s*new\s+ToolStripMenuItem' -and $formSource -match 'var\s+btnDelete\s*=\s*new\s+ToolStripMenuItem' -and $formSource -match 'var\s+btnOpenFolder\s*=\s*new\s+ToolStripMenuItem')
Assert-True 'library actions use menu items for dropdown layout' ($formSource -notmatch 'var\s+btnSync\s*=' -and $formSource -match 'var\s+btnSyncCenter\s*=\s*new\s+ToolStripMenuItem' -and $formSource -match 'var\s+btnPrebuildThumbnails\s*=\s*new\s+ToolStripMenuItem' -and $formSource -match 'var\s+btnRebuildThumbnails\s*=\s*new\s+ToolStripMenuItem' -and $formSource -match 'var\s+btnStatusDiagnostics\s*=\s*new\s+ToolStripMenuItem' -and $formSource -match 'var\s+btnSettings\s*=\s*new\s+ToolStripMenuItem')

$lastIndex = -1
foreach ($item in $expectedOrder) {
    $index = Find-TokenIndex $items $item
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
    $index = Find-TokenIndex $manageItems $item
    Assert-True ("manage menu contains " + $item) ($index -ge 0)
    Assert-True ("manage menu order after previous for " + $item) ($index -gt $lastIndex)
    $lastIndex = $index
}

$libraryMatch = [regex]::Match($formSource, 'var\s+btnLibrary\s*=\s*new\s+ToolStripDropDownButton\("[^"]+"\);(?<items>[\s\S]*?)// Search box')
Assert-True 'library menu block found' $libraryMatch.Success
$libraryItems = $libraryMatch.Groups['items'].Value
Assert-False 'library menu excludes local library update action' ($libraryItems.Contains('btnUpdateLocalLibrary') -or $libraryItems.Contains('btnUpdateMirror'))
Assert-True 'library menu guards sync actions by permission' ($libraryItems -match 'if\s*\(BlockLibrary\.AllowNasSync\)')
$expectedLibraryOrder = @('btnSyncCenter', 'btnPrebuildThumbnails', 'btnRebuildThumbnails', 'btnStatusDiagnostics', 'btnSettings')
$lastIndex = -1
foreach ($item in $expectedLibraryOrder) {
    $index = Find-TokenIndex $libraryItems $item
    Assert-True ("library menu contains " + $item) ($index -ge 0)
    Assert-True ("library menu order after previous for " + $item) ($index -gt $lastIndex)
    $lastIndex = $index
}

Write-Host 'ToolbarOrder.Tests.ps1 passed'
