$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$syncFiles = @(
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'Sync\ActiveLibraryResolver.cs'
    Join-Path $root 'Sync\ChangeJournal.cs'
    Join-Path $root 'Sync\LocalOnlySyncDiscovery.cs'
    Join-Path $root 'Sync\VersionNameGenerator.cs'
    Join-Path $root 'Sync\SyncPlanner.cs'
    Join-Path $root 'Sync\MetadataMerger.cs'
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
}

function Assert-True($name, $actual) {
    if (-not $actual) {
        throw "$name failed. Expected true."
    }
}

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name failed. Pattern [$pattern] not found in [$text]"
    }
}

$settings = New-Object BlockBrowser.SyncSettings
$settings.LibraryPath = '\\NAS\CADBlocks\BlockBrowser'
$settings.LocalMirrorPath = 'D:\CADBlocks\BlockBrowser'
$settings.PreferLocalWhenNasUnavailable = $true
$settings.CurrentLibraryMode = [BlockBrowser.LibraryMode]::Auto
$settings.AllowNasSync = $true

$autoNas = [BlockBrowser.ActiveLibraryResolver]::Resolve($settings, $true, $true)
Assert-Equal 'auto mode uses NAS when available' ([BlockBrowser.ActiveLibraryKind]::Nas) $autoNas.Kind
Assert-Equal 'auto mode active path is NAS' '\\NAS\CADBlocks\BlockBrowser' $autoNas.ActivePath

$settings.AllowNasSync = $false
$readonlyAutoLocal = [BlockBrowser.ActiveLibraryResolver]::Resolve($settings, $true, $true)
Assert-Equal 'readonly auto mode prefers local mirror when NAS is available' ([BlockBrowser.ActiveLibraryKind]::LocalMirror) $readonlyAutoLocal.Kind
Assert-Equal 'readonly auto path is local mirror' 'D:\CADBlocks\BlockBrowser' $readonlyAutoLocal.ActivePath

$readonlyAutoNas = [BlockBrowser.ActiveLibraryResolver]::Resolve($settings, $true, $false)
Assert-Equal 'readonly auto can browse NAS when local mirror is missing' ([BlockBrowser.ActiveLibraryKind]::Nas) $readonlyAutoNas.Kind
Assert-Equal 'readonly auto browse path is NAS' '\\NAS\CADBlocks\BlockBrowser' $readonlyAutoNas.ActivePath

$settings.AllowNasSync = $true
$autoLocal = [BlockBrowser.ActiveLibraryResolver]::Resolve($settings, $false, $true)
Assert-Equal 'auto mode falls back to local mirror' ([BlockBrowser.ActiveLibraryKind]::LocalMirror) $autoLocal.Kind
Assert-Equal 'auto fallback path is local mirror' 'D:\CADBlocks\BlockBrowser' $autoLocal.ActivePath

$versionName = [BlockBrowser.VersionNameGenerator]::CreateVersionCopyName('Electrical\Socket-5pin.dwg', 'WLUP', [datetime]'2026-06-08T06:20:00Z')
Assert-Equal 'version copy name' 'Electrical\Socket-5pin_WLUP_20260608.dwg' $versionName

$entry = New-Object BlockBrowser.ChangeJournalEntry
$entry.Id = '20260608-142000-WLUP-001'
$entry.Action = [BlockBrowser.LocalChangeAction]::Add
$entry.Path = 'Electrical\Socket-5pin.dwg'
$entry.User = 'WLUP'
$entry.CreatedUtc = [datetime]'2026-06-08T06:20:00Z'
$entry.LocalLastWriteUtc = [datetime]'2026-06-08T06:20:00Z'

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('BlockBrowserSyncTests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $temp | Out-Null
try {
    $journalPath = Join-Path $temp '.blockbrowser\local-changes.json'
    $entryList = New-Object 'System.Collections.Generic.List[BlockBrowser.ChangeJournalEntry]'
    $entryList.Add($entry)
    [BlockBrowser.ChangeJournal]::Save($journalPath, $entryList)
    $loaded = [BlockBrowser.ChangeJournal]::Load($journalPath)
    Assert-Equal 'journal count' 1 $loaded.Count
    Assert-Equal 'journal action roundtrip' ([BlockBrowser.LocalChangeAction]::Add) $loaded[0].Action

    $snapshots = New-Object 'System.Collections.Generic.List[BlockBrowser.SyncFileSnapshot]'
    $snapshot = New-Object BlockBrowser.SyncFileSnapshot
    $snapshot.Path = 'Electrical\Socket-5pin.dwg'
    $snapshot.LocalExists = $true
    $snapshot.NasExists = $false
    $snapshot.LocalLastWriteUtc = [datetime]'2026-06-08T06:20:00Z'
    $snapshots.Add($snapshot)

    $plan = [BlockBrowser.SyncPlanner]::CreatePlan($loaded, $snapshots)
    Assert-Equal 'new local file uploads' ([BlockBrowser.SyncDecisionKind]::Upload) $plan.Decisions[0].Kind
    Assert-Equal 'upload count' 1 $plan.UploadCount
}
finally {
    if (Test-Path $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force
    }
}

$dupEntry = New-Object BlockBrowser.ChangeJournalEntry
$dupEntry.Action = [BlockBrowser.LocalChangeAction]::Add
$dupEntry.Path = 'Electrical\Socket-5pin.dwg'
$dupSnapshot = New-Object BlockBrowser.SyncFileSnapshot
$dupSnapshot.Path = 'Electrical\Socket-5pin.dwg'
$dupSnapshot.LocalExists = $true
$dupSnapshot.NasExists = $true
$dupEntries = New-Object 'System.Collections.Generic.List[BlockBrowser.ChangeJournalEntry]'
$dupEntries.Add($dupEntry)
$dupSnapshots = New-Object 'System.Collections.Generic.List[BlockBrowser.SyncFileSnapshot]'
$dupSnapshots.Add($dupSnapshot)
$dupPlan = [BlockBrowser.SyncPlanner]::CreatePlan($dupEntries, $dupSnapshots)
Assert-Equal 'duplicate add is skipped' ([BlockBrowser.SyncDecisionKind]::SkipDuplicate) $dupPlan.Decisions[0].Kind

$editEntry = New-Object BlockBrowser.ChangeJournalEntry
$editEntry.Action = [BlockBrowser.LocalChangeAction]::Edit
$editEntry.Path = 'Electrical\Socket-5pin.dwg'
$editEntry.BaseNasLastWriteUtc = [datetime]'2026-06-08T06:00:00Z'
$editSnapshot = New-Object BlockBrowser.SyncFileSnapshot
$editSnapshot.Path = 'Electrical\Socket-5pin.dwg'
$editSnapshot.LocalExists = $true
$editSnapshot.NasExists = $true
$editSnapshot.NasLastWriteUtc = [datetime]'2026-06-08T06:30:00Z'
$editEntries = New-Object 'System.Collections.Generic.List[BlockBrowser.ChangeJournalEntry]'
$editEntries.Add($editEntry)
$editSnapshots = New-Object 'System.Collections.Generic.List[BlockBrowser.SyncFileSnapshot]'
$editSnapshots.Add($editSnapshot)
$editPlan = [BlockBrowser.SyncPlanner]::CreatePlan($editEntries, $editSnapshots)
Assert-Equal 'local and NAS edit becomes conflict' ([BlockBrowser.SyncDecisionKind]::Conflict) $editPlan.Decisions[0].Kind

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

$nasTags = New-Object 'System.Collections.Generic.List[string]'
$nasTags.Add('electrical')
$localTags = New-Object 'System.Collections.Generic.List[string]'
$localTags.Add('common')
$localTags.Add('electrical')
$merged = [BlockBrowser.MetadataMerger]::Merge($nasTags, 'NAS note', $localTags, 'Local note')
Assert-Equal 'metadata tag merge count' 2 $merged.Tags.Count
Assert-True 'metadata note conflict' $merged.HasNoteConflict

$formSource = Get-Content -Encoding UTF8 (Join-Path $root 'Forms\BlockBrowserForm.cs') -Raw
$pluginSource = Get-Content -Encoding UTF8 (Join-Path $root 'BlockBrowserPlugin.cs') -Raw
Assert-True 'panel uses sync center instead of direct sync flow' ($formSource -match 'ShowSyncCenterDialog\(\)[\s\S]*?new\s+SyncCenterDialog\(' -and $formSource -notmatch 'new\s+ToolStripMenuItem\("同步到NAS"\)|FormatPreviewDialog\(preview\)[\s\S]*?SyncSafeUploadsToNas\(\)')
Assert-True 'command sync opens sync center instead of command preview flow' ($pluginSource -match 'SyncLocalChanges\(\)[\s\S]*?OpenSyncCenterDialog\(\)' -and $pluginSource -notmatch 'FormatPreviewCommand\(preview\)[\s\S]*?GetString[\s\S]*?SyncSafeUploadsToNas\(\)')

Write-Host 'SyncLogic.Tests.ps1 passed'
