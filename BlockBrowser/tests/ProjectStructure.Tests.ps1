$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repo 'BlockBrowser'
$manualFileName = -join @([char]0x4f7f, [char]0x7528, [char]0x624b, [char]0x518c, '.html')
$csprojSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.csproj') -Raw
$acadSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.AutoCAD.csproj') -Raw
$zwcadSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.ZWCAD.csproj') -Raw
$readmeSource = Get-Content -Encoding UTF8 (Join-Path $project 'README.md') -Raw

function Assert-True($name, $actual) {
    if (-not $actual) { throw "$name failed. Expected true." }
    Write-Host "PASS $name"
}

function Assert-False($name, $actual) {
    if ($actual) { throw "$name failed. Expected false." }
    Write-Host "PASS $name"
}

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

$formFiles = @(
    'BlockBrowserForm.cs',
    'BlockThumbnailCard.cs',
    'ExportBlocksDialog.cs',
    'InsertSettingsDialog.cs',
    'MirrorPreviewDialog.cs',
    'SettingsDialog.cs',
    'StatusDiagnosticsDialog.cs',
    'SyncCenterDialog.cs',
    'TextPromptDialog.cs'
)

foreach ($file in $formFiles) {
    Assert-True ("forms folder contains " + $file) (Test-Path (Join-Path $project ("Forms\" + $file)))
    Assert-False ("root no longer contains " + $file) (Test-Path (Join-Path $project $file))
    Assert-Contains ("main project references Forms " + $file) $csprojSource ('Compile Include="Forms\\' + [regex]::Escape($file) + '"')
    Assert-Contains ("AutoCAD project references Forms " + $file) $acadSource ('Compile Include="Forms\\' + [regex]::Escape($file) + '"')
    Assert-Contains ("ZWCAD project references Forms " + $file) $zwcadSource ('Compile Include="Forms\\' + [regex]::Escape($file) + '"')
}

Assert-True 'docs folder contains manual checklist' (Test-Path (Join-Path $project 'Docs\MANUAL_TEST_CHECKLIST.md'))
Assert-True 'docs folder contains release notes' (Test-Path (Join-Path $project 'Docs\RELEASE_NOTES.md'))
Assert-False 'root no longer contains manual checklist' (Test-Path (Join-Path $project 'MANUAL_TEST_CHECKLIST.md'))
Assert-False 'root no longer contains release notes' (Test-Path (Join-Path $project 'RELEASE_NOTES.md'))
Assert-True 'user manual remains at root' (Test-Path (Join-Path $project $manualFileName))
Assert-Contains 'README links docs release notes' $readmeSource '\(Docs/RELEASE_NOTES\.md\)'
Assert-Contains 'README links docs manual checklist' $readmeSource '\(Docs/MANUAL_TEST_CHECKLIST\.md\)'

Write-Host 'ProjectStructure.Tests.ps1 passed'
