$ErrorActionPreference = 'Stop'

$testsRoot = $PSScriptRoot

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

$focusedTests = @(
    'SyncActiveLibraryResolver.Tests.ps1',
    'SyncChangeJournal.Tests.ps1',
    'SyncPlanner.Tests.ps1',
    'LocalOnlySyncDiscovery.Tests.ps1',
    'SyncMetadataMerger.Tests.ps1',
    'SyncEntryPoint.Tests.ps1'
)

foreach ($file in $focusedTests) {
    $path = Join-Path $testsRoot $file
    Assert-True ("focused sync test exists " + $file) (Test-Path $path)
    $source = Get-Content -Encoding UTF8 $path -Raw
    Assert-Contains ("focused sync test has pass footer " + $file) $source ([regex]::Escape($file + ' passed'))
}

Assert-False 'old combined SyncLogic test removed' (Test-Path (Join-Path $testsRoot 'SyncLogic.Tests.ps1'))

$resolverSource = Get-Content -Encoding UTF8 (Join-Path $testsRoot 'SyncActiveLibraryResolver.Tests.ps1') -Raw
$journalSource = Get-Content -Encoding UTF8 (Join-Path $testsRoot 'SyncChangeJournal.Tests.ps1') -Raw
$plannerSource = Get-Content -Encoding UTF8 (Join-Path $testsRoot 'SyncPlanner.Tests.ps1') -Raw
$discoverySource = Get-Content -Encoding UTF8 (Join-Path $testsRoot 'LocalOnlySyncDiscovery.Tests.ps1') -Raw
$metadataSource = Get-Content -Encoding UTF8 (Join-Path $testsRoot 'SyncMetadataMerger.Tests.ps1') -Raw
$entryPointSource = Get-Content -Encoding UTF8 (Join-Path $testsRoot 'SyncEntryPoint.Tests.ps1') -Raw

Assert-Contains 'resolver test focuses resolver' $resolverSource 'ActiveLibraryResolver'
Assert-False 'resolver test does not cover journal' ($resolverSource -match 'ChangeJournal')
Assert-Contains 'journal test focuses journal' $journalSource 'ChangeJournal'
Assert-False 'journal test does not cover planner' ($journalSource -match 'SyncPlanner')
Assert-Contains 'planner test focuses planner' $plannerSource 'SyncPlanner'
Assert-False 'planner test does not cover UI entry point' ($plannerSource -match 'ShowSyncCenterDialog|OpenSyncCenterDialog')
Assert-Contains 'local-only discovery test focuses discovery' $discoverySource 'LocalOnlySyncDiscovery'
Assert-Contains 'metadata test focuses merger' $metadataSource 'MetadataMerger'
Assert-Contains 'entry point test focuses sync center wiring' $entryPointSource 'OpenSyncCenterDialog|ShowSyncCenterDialog'

Write-Host 'SyncTestStructure.Tests.ps1 passed'
