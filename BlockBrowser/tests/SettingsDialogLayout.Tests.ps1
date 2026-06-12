$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$formSource = @(
    Get-ChildItem -Path (Join-Path $repo 'BlockBrowser\Forms') -Filter 'BlockBrowserForm*.cs' |
        Sort-Object Name |
        ForEach-Object { Get-Content -Encoding UTF8 $_.FullName -Raw }
) -join "`n"
$dialogPath = Join-Path $repo 'BlockBrowser\Forms\SettingsDialog.cs'
if (-not (Test-Path $dialogPath)) {
    throw "Missing source file: $dialogPath"
}
$dialogSource = Get-Content -Encoding UTF8 $dialogPath -Raw
$settingsMethod = [regex]::Match($formSource, 'private\s+void\s+ShowSettingsDialog\(\)[\s\S]*?private\s+void\s+ShowInsertSettingsDialog').Value
$insertDialogPath = Join-Path $repo 'BlockBrowser\Forms\InsertSettingsDialog.cs'
if (-not (Test-Path $insertDialogPath)) {
    throw "Missing source file: $insertDialogPath"
}
$insertDialogSource = Get-Content -Encoding UTF8 $insertDialogPath -Raw
$libraryGroupTitle = -join ([char[]](0x56FE, 0x5E93, 0x4F4D, 0x7F6E))
$insertGroupTitle = -join ([char[]](0x63D2, 0x5165, 0x8BBE, 0x7F6E))

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'settings dialog class exists' $dialogSource 'class\s+SettingsDialog\s*:\s*Form'
Assert-Contains 'settings dialog uses table layout for DPI scaling' $dialogSource 'new TableLayoutPanel'
Assert-Contains 'settings dialog grows to content' $dialogSource 'AutoSizeMode = AutoSizeMode\.GrowAndShrink'
Assert-Contains 'settings dialog sets AutoScaleMode' $dialogSource 'AutoScaleMode = AutoScaleMode\.Dpi'
Assert-Contains 'settings dialog groups library paths' $dialogSource ('new GroupBox\s*\{[^}]*Text = "' + [regex]::Escape($libraryGroupTitle) + '"')
Assert-NotContains 'settings dialog does not duplicate insert options' $dialogSource ('new GroupBox\s*\{[^}]*Text = "' + [regex]::Escape($insertGroupTitle) + '"')
Assert-Contains 'settings dialog uses wider path text boxes' $dialogSource 'textBox\.Width = 520'
Assert-Contains 'settings dialog uses three-column path rows' $dialogSource 'ColumnCount = 3'
Assert-Contains 'settings dialog keeps path labels unwrapped at high DPI' $dialogSource 'ColumnStyle\(SizeType\.Absolute,\s*150\)'
Assert-Contains 'settings dialog aligns browse buttons with path inputs' $dialogSource 'pathPanel\.Controls\.Add\(btnBrowse,\s*2,\s*row\)'
Assert-Contains 'settings dialog keeps mode dropdown readable at high DPI' $dialogSource '_cmbLibraryMode[\s\S]*?Width = 128'
Assert-Contains 'settings dialog places protected categories before mode' $dialogSource 'AddTextRow\(pathPanel,\s*2,\s*"'
Assert-Contains 'settings dialog places mode in library group' $dialogSource 'AddModeRow\(pathPanel,\s*3,\s*"'
Assert-Contains 'settings dialog exposes NAS library path' $dialogSource 'NasLibraryPathValue'
Assert-Contains 'settings dialog exposes local mirror path' $dialogSource 'LocalMirrorPathValue'
Assert-Contains 'settings dialog exposes protected local categories' $dialogSource 'ProtectedLocalCategoriesValue'
Assert-Contains 'settings dialog exposes current library mode' $dialogSource 'CurrentLibraryModeValue'
Assert-NotContains 'settings dialog no insert scale value' $dialogSource 'InsertScaleValue'
Assert-NotContains 'settings dialog no rotation degrees value' $dialogSource 'InsertRotationDegreesValue'
Assert-Contains 'form opens settings dialog' $formSource 'new SettingsDialog'
Assert-Contains 'form passes protected categories to settings dialog' $formSource 'BlockLibrary\.GetProtectedLocalCategoriesText\(\)'
Assert-NotContains 'form does not read insert values from settings dialog' $settingsMethod 'form\.InsertScaleValue|form\.InsertRotationDegreesValue'
Assert-Contains 'form refreshes active library after settings change' $formSource 'BlockLibrary\.RefreshActiveLibrary\(\)'
Assert-Contains 'form saves protected categories config' $formSource 'BlockLibrary\.SetProtectedLocalCategoriesFromText\(plan\.ProtectedLocalCategories\)'
Assert-NotContains 'form does not overwrite NAS path with local path' $formSource 'BlockLibrary\.NasLibraryPath\s*=\s*plan\.LibraryPath'
Assert-NotContains 'form no inline settings form' $formSource 'using\s*\(var\s+form\s*=\s*new\s+Form\s*\(\s*\)\)'
Assert-NotContains 'settings dialog no fixed 450x260 form size' $dialogSource 'Size = new Size\(450,\s*260\)'
Assert-Contains 'insert settings dialog class exists' $insertDialogSource 'class\s+InsertSettingsDialog\s*:\s*Form'
Assert-Contains 'insert settings dialog uses dpi scaling' $insertDialogSource 'AutoScaleMode = AutoScaleMode\.Dpi'
Assert-Contains 'insert settings dialog exposes insert scale' $insertDialogSource 'InsertScaleValue'
Assert-Contains 'insert settings dialog exposes rotation degrees' $insertDialogSource 'InsertRotationDegreesValue'
Assert-Contains 'form opens insert settings dialog' $formSource 'new InsertSettingsDialog'
Assert-Contains 'form saves insert settings config' $formSource 'ShowInsertSettingsDialog[\s\S]*?BlockLibrary\.SaveConfig\(\)'
