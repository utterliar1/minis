$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'UI\StatusDiagnosticsService.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Runtime.Serialization.dll',
    'System.Core.dll'
)

function Assert-Contains($name, $actual, $expectedPart) {
    if ($actual -notlike "*$expectedPart*") {
        throw "$name failed. Expected [$actual] to contain [$expectedPart]"
    }
    Write-Host "PASS $name"
}

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        throw "$name failed. Expected: [$expected], Actual: [$actual]"
    }
    Write-Host "PASS $name"
}

function U([int[]]$codes) {
    return -join ([char[]]$codes)
}

$title = U @(0x5757, 0x6D4F, 0x89C8, 0x5668, 0x72B6, 0x6001, 0x8BCA, 0x65AD)
$versionLabel = U @(0x7248, 0x672C)
$platformLabel = U @(0x5E73, 0x53F0)
$modeLabel = U @(0x5F53, 0x524D, 0x6A21, 0x5F0F)
$activeLabel = U @(0x5F53, 0x524D, 0x56FE, 0x5E93)
$actualPathLabel = U @(0x5B9E, 0x9645, 0x8DEF, 0x5F84)
$actualAvailableLabel = U @(0x5B9E, 0x9645, 0x8DEF, 0x5F84, 0x53EF, 0x7528)
$yes = U @(0x662F)
$no = U @(0x5426)
$nasAvailableLabel = "NAS " + (U @(0x53EF, 0x8BBF, 0x95EE))
$localMirrorAvailableLabel = U @(0x672C, 0x5730, 0x526F, 0x672C, 0x53EF, 0x8BBF, 0x95EE)
$allowNasSyncLabel = (U @(0x5141, 0x8BB8, 0x540C, 0x6B65, 0x5230)) + " NAS"
$localChangesLabel = U @(0x672C, 0x5730, 0x53D8, 0x66F4, 0x8BB0, 0x5F55)
$thumbnailCacheLabel = U @(0x7F29, 0x7565, 0x56FE, 0x7F13, 0x5B58)
$syncUserLabel = U @(0x540C, 0x6B65, 0x7528, 0x6237)
$journalPathLabel = U @(0x53D8, 0x66F4, 0x8BB0, 0x5F55, 0x6587, 0x4EF6)
$thumbnailPathLabel = U @(0x7F29, 0x7565, 0x56FE, 0x76EE, 0x5F55)

$active = New-Object BlockBrowser.ActiveLibraryResult
$active.Kind = [BlockBrowser.ActiveLibraryKind]::LocalMirror
$active.ActivePath = 'D:\Blocks\Mirror'
$active.Message = 'Using local mirror.'
$active.IsAvailable = $true

$report = [BlockBrowser.StatusDiagnosticsService]::FormatReport(
    '1.3.2',
    'AutoCAD',
    [BlockBrowser.LibraryMode]::Auto,
    $active,
    'D:\Blocks\Mirror',
    '\\NAS\CADBlocks',
    'D:\Blocks\Mirror',
    $true,
    $true,
    $true,
    3,
    42,
    'WLUP',
    'D:\Blocks\Mirror\.blockbrowser\local-changes.json',
    'D:\Blocks\Mirror\.thumbs')

Assert-Contains 'report includes title' $report $title
Assert-Contains 'report includes version' $report ($versionLabel + ': 1.3.2')
Assert-Contains 'report includes platform' $report ($platformLabel + ': AutoCAD')
Assert-Contains 'report includes current mode' $report ($modeLabel + ': Auto')
Assert-Contains 'report includes active kind' $report ($activeLabel + ': LocalMirror')
Assert-Contains 'report includes active path' $report ($actualPathLabel + ': D:\Blocks\Mirror')
Assert-Contains 'report includes active availability' $report ($actualAvailableLabel + ': ' + $yes)
Assert-Contains 'report includes library path' $report 'LibraryPath: D:\Blocks\Mirror'
Assert-Contains 'report includes NAS path' $report 'NasLibraryPath: \\NAS\CADBlocks'
Assert-Contains 'report includes local mirror path' $report 'LocalMirrorPath: D:\Blocks\Mirror'
Assert-Contains 'report includes NAS availability' $report ($nasAvailableLabel + ': ' + $yes)
Assert-Contains 'report includes local availability' $report ($localMirrorAvailableLabel + ': ' + $yes)
Assert-Contains 'report includes sync permission' $report ($allowNasSyncLabel + ': ' + $yes)
Assert-Contains 'report includes local change count' $report ($localChangesLabel + ': 3')
Assert-Contains 'report includes thumbnail count' $report ($thumbnailCacheLabel + ': 42')
Assert-Contains 'report includes user name' $report ($syncUserLabel + ': WLUP')
Assert-Contains 'report includes journal path' $report ($journalPathLabel + ': D:\Blocks\Mirror\.blockbrowser\local-changes.json')
Assert-Contains 'report includes thumbnail path' $report ($thumbnailPathLabel + ': D:\Blocks\Mirror\.thumbs')

$emptyActiveReport = [BlockBrowser.StatusDiagnosticsService]::FormatReport(
    '1.3.2',
    'GstarCAD',
    [BlockBrowser.LibraryMode]::Local,
    $null,
    '',
    '',
    '',
    $false,
    $false,
    $false,
    0,
    0,
    '',
    '',
    '')

Assert-Contains 'null active report has fallback' $emptyActiveReport ($activeLabel + ': None')
Assert-Contains 'false values are localized' $emptyActiveReport ($allowNasSyncLabel + ': ' + $no)
Assert-Equal 'report uses CRLF friendly lines' $true ($report.Contains([Environment]::NewLine))

Write-Host 'StatusDiagnosticsService.Tests.ps1 passed'
