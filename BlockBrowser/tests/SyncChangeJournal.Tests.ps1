$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$syncFiles = @(
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'Sync\ChangeJournal.cs'
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

$entry = New-Object BlockBrowser.ChangeJournalEntry
$entry.Id = '20260608-142000-WLUP-001'
$entry.Action = [BlockBrowser.LocalChangeAction]::Add
$entry.Path = 'Electrical\Socket-5pin.dwg'
$entry.User = 'WLUP'
$entry.CreatedUtc = [datetime]'2026-06-08T06:20:00Z'
$entry.LocalLastWriteUtc = [datetime]'2026-06-08T06:20:00Z'

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('BlockBrowserSyncJournalTests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $temp | Out-Null
try {
    $journalPath = Join-Path $temp '.blockbrowser\local-changes.json'
    $entryList = New-Object 'System.Collections.Generic.List[BlockBrowser.ChangeJournalEntry]'
    $entryList.Add($entry)
    [BlockBrowser.ChangeJournal]::Save($journalPath, $entryList)
    $loaded = [BlockBrowser.ChangeJournal]::Load($journalPath)
    Assert-Equal 'journal count' 1 $loaded.Count
    Assert-Equal 'journal action roundtrip' ([BlockBrowser.LocalChangeAction]::Add) $loaded[0].Action
}
finally {
    if (Test-Path $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force
    }
}

Write-Host 'SyncChangeJournal.Tests.ps1 passed'
