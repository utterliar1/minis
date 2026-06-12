$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$formSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserForm.cs') -Raw
$dialogSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\StatusDiagnosticsDialog.cs') -Raw
$serviceSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\UI\StatusDiagnosticsService.cs') -Raw
$csprojSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.csproj') -Raw
$acadSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.AutoCAD.csproj') -Raw
$zwcadSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.ZWCAD.csproj') -Raw
$statusDiagnosticsText = -join ([char[]](0x72B6, 0x6001, 0x8BCA, 0x65AD))

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'form declares status diagnostics menu item' $formSource ('var\s+btnStatusDiagnostics\s*=\s*new\s+ToolStripMenuItem\("' + [regex]::Escape($statusDiagnosticsText) + '"\)')
Assert-Contains 'form opens status diagnostics dialog from menu' $formSource 'btnStatusDiagnostics\.Click\s*\+=\s*\(s,\s*e\)\s*=>\s*ShowStatusDiagnosticsDialog\(\)'
Assert-Contains 'form builds diagnostics report' $formSource 'StatusDiagnosticsService\.FormatReport\('
Assert-Contains 'form passes active library' $formSource 'BlockLibrary\.ActiveLibrary'
Assert-Contains 'form passes current library mode' $formSource 'BlockLibrary\.CurrentLibraryMode'
Assert-Contains 'form counts local changes' $formSource 'ChangeJournal\.Load\(BlockLibrary\.LocalJournalPath\)\.Count'
Assert-Contains 'form counts thumbnail cache png files' $formSource 'Directory\.GetFiles\(cachePath,\s*"\*\.png",\s*SearchOption\.AllDirectories\)\.Length'
Assert-Contains 'dialog is read only text report' $dialogSource 'ReadOnly\s*=\s*true'
Assert-Contains 'dialog supports copying report' $dialogSource 'Clipboard\.SetText\(_txtReport\.Text\)'
Assert-Contains 'service exposes format report' $serviceSource 'public\s+static\s+string\s+FormatReport\('
Assert-Contains 'main project compiles status dialog' $csprojSource 'Compile Include="StatusDiagnosticsDialog\.cs"'
Assert-Contains 'main project compiles status service' $csprojSource 'Compile Include="UI\\StatusDiagnosticsService\.cs"'
Assert-Contains 'AutoCAD project compiles status dialog' $acadSource 'Compile Include="StatusDiagnosticsDialog\.cs"'
Assert-Contains 'AutoCAD project compiles status service' $acadSource 'Compile Include="UI\\StatusDiagnosticsService\.cs"'
Assert-Contains 'ZWCAD project compiles status dialog' $zwcadSource 'Compile Include="StatusDiagnosticsDialog\.cs"'
Assert-Contains 'ZWCAD project compiles status service' $zwcadSource 'Compile Include="UI\\StatusDiagnosticsService\.cs"'

Write-Host 'StatusDiagnostics.Tests.ps1 passed'
