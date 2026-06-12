$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$syncFiles = @(
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'Sync\ChangeJournal.cs'
    Join-Path $root 'Sync\LocalOnlySyncDiscovery.cs'
    Join-Path $root 'Sync\SyncPlanner.cs'
)

foreach ($file in $syncFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing sync source file: $file"
    }
}

Add-Type -Path $syncFiles -ReferencedAssemblies @(
    'System.Runtime.Serialization.dll',
    'System.Xml.dll',
    'System.Core.dll'
)

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        throw "$name failed. Expected: [$expected], Actual: [$actual]"
    }
    Write-Host "PASS $name"
}

$scanTemp = Join-Path ([System.IO.Path]::GetTempPath()) ('BlockBrowserLocalScanTests-' + [guid]::NewGuid().ToString('N'))
$scanLocal = Join-Path $scanTemp 'Local'
$scanNas = Join-Path $scanTemp 'NAS'
try {
    New-Item -ItemType Directory -Force -Path (Join-Path $scanLocal 'Electrical') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $scanLocal '.blockbrowser') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $scanLocal '.thumbs') | Out-Null
    New-Item -ItemType Directory -Force -Path $scanNas | Out-Null
    Set-Content -Path (Join-Path $scanLocal 'Electrical\LocalOnly.dwg') -Value 'local dwg'
    $protectedCategoryName = -join ([char[]](0x4E2A, 0x4EBA, 0x5757))
    New-Item -ItemType Directory -Force -Path (Join-Path $scanLocal $protectedCategoryName) | Out-Null
    Set-Content -Path (Join-Path $scanLocal (Join-Path $protectedCategoryName 'PersonalOnly.dwg')) -Value 'personal dwg'
    Set-Content -Path (Join-Path $scanLocal '.blockbrowser\ignore.dwg') -Value 'journal internal'
    Set-Content -Path (Join-Path $scanLocal '.thumbs\ignore.dwg') -Value 'thumb internal'

    $emptyJournal = New-Object 'System.Collections.Generic.List[BlockBrowser.ChangeJournalEntry]'
    $protectedCategories = New-Object 'System.Collections.Generic.List[string]'
    $protectedCategories.Add($protectedCategoryName)
    $discovered = [BlockBrowser.LocalOnlySyncDiscovery]::Discover($scanLocal, $scanNas, $emptyJournal, 'WLUP', [datetime]'2026-06-09T08:00:00Z', $protectedCategories)
    Assert-Equal 'local-only scan count' 2 $discovered.Count
    Assert-Equal 'local-only scan action' ([BlockBrowser.LocalChangeAction]::Add) $discovered[0].Action
    Assert-Equal 'local-only scan path' 'Electrical\LocalOnly.dwg' $discovered[0].Path
    Assert-Equal 'protected local-only scan action' ([BlockBrowser.LocalChangeAction]::ProtectedCategorySkip) $discovered[1].Action
    Assert-Equal 'protected local-only scan path' ($protectedCategoryName + '\PersonalOnly.dwg') $discovered[1].Path

    $scanSnapshots = New-Object 'System.Collections.Generic.List[BlockBrowser.SyncFileSnapshot]'
    foreach ($change in $discovered) {
        $scanSnapshot = New-Object BlockBrowser.SyncFileSnapshot
        $scanSnapshot.Path = $change.Path
        $scanSnapshot.LocalExists = $true
        $scanSnapshot.NasExists = $false
        $scanSnapshots.Add($scanSnapshot)
    }
    $scanPlan = [BlockBrowser.SyncPlanner]::CreatePlan($discovered, $scanSnapshots)
    Assert-Equal 'protected local-only scan becomes whitelist skip' ([BlockBrowser.SyncDecisionKind]::ProtectedCategorySkip) $scanPlan.Decisions[1].Kind
    Assert-Equal 'whitelist skip count' 1 $scanPlan.ProtectedCategorySkipCount
}
finally {
    if (Test-Path $scanTemp) {
        Remove-Item -LiteralPath $scanTemp -Recurse -Force
    }
}

Write-Host 'LocalOnlySyncDiscovery.Tests.ps1 passed'
