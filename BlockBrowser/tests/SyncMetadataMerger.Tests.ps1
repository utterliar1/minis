$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$syncFiles = @(
    Join-Path $root 'Sync\MetadataMerger.cs'
)

foreach ($file in $syncFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing sync source file: $file"
    }
}

Add-Type -Path $syncFiles -ReferencedAssemblies @(
    'System.Core.dll'
)

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        throw "$name failed. Expected: [$expected], Actual: [$actual]"
    }
    Write-Host "PASS $name"
}

function Assert-True($name, $actual) {
    if (-not $actual) {
        throw "$name failed. Expected true."
    }
    Write-Host "PASS $name"
}

$nasTags = New-Object 'System.Collections.Generic.List[string]'
$nasTags.Add('electrical')
$localTags = New-Object 'System.Collections.Generic.List[string]'
$localTags.Add('common')
$localTags.Add('electrical')
$merged = [BlockBrowser.MetadataMerger]::Merge($nasTags, 'NAS note', $localTags, 'Local note')
Assert-Equal 'metadata tag merge count' 2 $merged.Tags.Count
Assert-True 'metadata note conflict' $merged.HasNoteConflict

Write-Host 'SyncMetadataMerger.Tests.ps1 passed'
