$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$formSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserForm.cs') -Raw
$dialogPath = Join-Path $repo 'BlockBrowser\SettingsDialog.cs'
if (-not (Test-Path $dialogPath)) {
    throw "Missing source file: $dialogPath"
}
$dialogSource = Get-Content -Encoding UTF8 $dialogPath -Raw
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
Assert-Contains 'settings dialog groups insert options' $dialogSource ('new GroupBox\s*\{[^}]*Text = "' + [regex]::Escape($insertGroupTitle) + '"')
Assert-Contains 'settings dialog uses wider path text boxes' $dialogSource 'textBox\.Width = 520'
Assert-Contains 'settings dialog uses three-column path rows' $dialogSource 'ColumnCount = 3'
Assert-Contains 'settings dialog keeps path labels unwrapped at high DPI' $dialogSource 'ColumnStyle\(SizeType\.Absolute,\s*150\)'
Assert-Contains 'settings dialog aligns browse buttons with path inputs' $dialogSource 'pathPanel\.Controls\.Add\(btnBrowse,\s*2,\s*row\)'
Assert-Contains 'settings dialog keeps mode dropdown readable at high DPI' $dialogSource '_cmbLibraryMode[\s\S]*?Width = 128'
Assert-Contains 'settings dialog reserves mode dropdown column width' $dialogSource 'ColumnStyle\(SizeType\.Absolute,\s*128\)'
Assert-Contains 'settings dialog exposes NAS library path' $dialogSource 'NasLibraryPathValue'
Assert-Contains 'settings dialog exposes local mirror path' $dialogSource 'LocalMirrorPathValue'
Assert-Contains 'settings dialog exposes current library mode' $dialogSource 'CurrentLibraryModeValue'
Assert-Contains 'settings dialog exposes insert scale' $dialogSource 'InsertScaleValue'
Assert-Contains 'settings dialog exposes rotation degrees' $dialogSource 'InsertRotationDegreesValue'
Assert-Contains 'form opens settings dialog' $formSource 'new SettingsDialog'
Assert-Contains 'form refreshes active library after settings change' $formSource 'BlockLibrary\.RefreshActiveLibrary\(\)'
Assert-NotContains 'form does not overwrite NAS path with local path' $formSource 'BlockLibrary\.NasLibraryPath\s*=\s*plan\.LibraryPath'
Assert-NotContains 'form no inline settings form' $formSource 'using\s*\(var\s+form\s*=\s*new\s+Form\s*\(\s*\)\)'
Assert-NotContains 'settings dialog no fixed 450x260 form size' $dialogSource 'Size = new Size\(450,\s*260\)'
