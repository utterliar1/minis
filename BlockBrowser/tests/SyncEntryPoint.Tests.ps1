$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

function Assert-True($name, $actual) {
    if (-not $actual) {
        throw "$name failed. Expected true."
    }
    Write-Host "PASS $name"
}

$formSource = @(
    Get-ChildItem -Path (Join-Path $root 'Forms') -Filter 'BlockBrowserForm*.cs' |
        Sort-Object Name |
        ForEach-Object { Get-Content -Encoding UTF8 $_.FullName -Raw }
) -join "`n"
$pluginSource = @(
    Get-ChildItem -Path (Join-Path $root 'Commands') -Filter 'BlockBrowserCommands*.cs' |
        Sort-Object Name |
        ForEach-Object { Get-Content -Encoding UTF8 $_.FullName -Raw }
) -join "`n"

Assert-True 'panel uses sync center instead of direct sync flow' ($formSource -match 'ShowSyncCenterDialog\(\)[\s\S]*?new\s+SyncCenterDialog\(' -and $formSource -notmatch 'new\s+ToolStripMenuItem\("同步到NAS"\)|FormatPreviewDialog\(preview\)[\s\S]*?SyncSafeUploadsToNas\(\)')
Assert-True 'command sync opens sync center instead of command preview flow' ($pluginSource -match 'SyncLocalChanges\(\)[\s\S]*?OpenSyncCenterDialog\(\)' -and $pluginSource -notmatch 'FormatPreviewCommand\(preview\)[\s\S]*?GetString[\s\S]*?SyncSafeUploadsToNas\(\)')

Write-Host 'SyncEntryPoint.Tests.ps1 passed'
