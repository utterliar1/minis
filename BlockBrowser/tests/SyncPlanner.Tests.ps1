$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$syncFiles = @(
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'Sync\VersionNameGenerator.cs'
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

$versionName = [BlockBrowser.VersionNameGenerator]::CreateVersionCopyName('Electrical\Socket-5pin.dwg', 'WLUP', [datetime]'2026-06-08T06:20:00Z')
Assert-Equal 'version copy name' 'Electrical\Socket-5pin_WLUP_20260608.dwg' $versionName

$entry = New-Object BlockBrowser.ChangeJournalEntry
$entry.Action = [BlockBrowser.LocalChangeAction]::Add
$entry.Path = 'Electrical\Socket-5pin.dwg'
$snapshot = New-Object BlockBrowser.SyncFileSnapshot
$snapshot.Path = 'Electrical\Socket-5pin.dwg'
$snapshot.LocalExists = $true
$snapshot.NasExists = $false
$snapshots = New-Object 'System.Collections.Generic.List[BlockBrowser.SyncFileSnapshot]'
$snapshots.Add($snapshot)
$entries = New-Object 'System.Collections.Generic.List[BlockBrowser.ChangeJournalEntry]'
$entries.Add($entry)
$plan = [BlockBrowser.SyncPlanner]::CreatePlan($entries, $snapshots)
Assert-Equal 'new local file uploads' ([BlockBrowser.SyncDecisionKind]::Upload) $plan.Decisions[0].Kind
Assert-Equal 'upload count' 1 $plan.UploadCount

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

Write-Host 'SyncPlanner.Tests.ps1 passed'
