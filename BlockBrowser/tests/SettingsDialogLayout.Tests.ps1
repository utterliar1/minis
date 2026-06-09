$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$formSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserForm.cs') -Raw
$dialogPath = Join-Path $repo 'BlockBrowser\SettingsDialog.cs'
if (-not (Test-Path $dialogPath)) {
    throw "Missing source file: $dialogPath"
}
$dialogSource = Get-Content -Encoding UTF8 $dialogPath -Raw

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
