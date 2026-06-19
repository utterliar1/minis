$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$pluginSource = @(
    Get-ChildItem -Path (Join-Path $repo 'BlockBrowser\Library') -Filter 'BlockLibrary*.cs' |
        Sort-Object Name |
        ForEach-Object { Get-Content -Encoding UTF8 $_.FullName -Raw }
    Get-ChildItem -Path (Join-Path $repo 'BlockBrowser\Commands') -Filter 'BlockBrowserCommands*.cs' |
        Sort-Object Name |
        ForEach-Object { Get-Content -Encoding UTF8 $_.FullName -Raw }
) -join "`n"
$formSource = @(
    Get-ChildItem -Path (Join-Path $repo 'BlockBrowser\Forms') -Filter 'BlockBrowserForm*.cs' |
        Sort-Object Name |
        ForEach-Object { Get-Content -Encoding UTF8 $_.FullName -Raw }
) -join "`n"
$configSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\Config\BlockBrowserConfig.cs') -Raw
$storeSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\Config\BlockBrowserConfigStore.cs') -Raw
$defaultConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.default.ini') -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'config exposes AllowNasSync' $configSource 'public\s+bool\s+AllowNasSync\s*\{\s*get;\s*set;\s*\}'
Assert-Contains 'config exposes protected local categories' $configSource 'ProtectedLocalCategories'
Assert-Contains 'default config disables NAS sync' $configSource 'AllowNasSync\s*=\s*false'
Assert-Contains 'default ini disables NAS sync' $defaultConfig 'AllowNasSync=0'
Assert-Contains 'config load parses AllowNasSync' $storeSource 'key\.Equals\("AllowNasSync"[\s\S]*?config\.AllowNasSync'
Assert-Contains 'config save writes AllowNasSync' $storeSource 'AddConfigEntry\(lines,\s*"AllowNasSync",\s*config\.AllowNasSync\s*\?'
Assert-Contains 'config upgrade appends AllowNasSync' $storeSource 'AddMissingConfigEntry\(missingLines,\s*loadedKeys,\s*"AllowNasSync"'
Assert-Contains 'BlockLibrary exposes AllowNasSync' $pluginSource 'public\s+static\s+bool\s+AllowNasSync\s*\{\s*get;\s*set;\s*\}'
Assert-Contains 'BlockLibrary exposes protected local categories' $pluginSource 'ProtectedLocalCategories'
Assert-Contains 'mirror update passes protected categories' $pluginSource 'MirrorDirectoryContents\(NasLibraryPath,\s*LocalMirrorPath,\s*GetProtectedLocalPaths\(pending\),\s*ProtectedLocalCategories'
Assert-Contains 'sync discovery passes protected categories' $pluginSource 'LocalOnlySyncDiscovery\.Discover\([\s\S]*?ProtectedLocalCategories'
Assert-Contains 'sync function guards NAS sync permission' $pluginSource 'SyncSafeUploadsToNas\(\)[\s\S]*?EnsureNasSyncAllowed\(\)'
Assert-Contains 'preview function guards NAS sync permission' $pluginSource 'PreviewLocalSync\(\)[\s\S]*?EnsureNasSyncAllowed\(\)'
Assert-Contains 'BBSYNC command checks permission before opening sync center' $pluginSource 'SyncLocalChanges\(\)[\s\S]*?if\s*\(!BlockLibrary\.AllowNasSync\)[\s\S]*?return;[\s\S]*?OpenSyncCenterDialog\(\)'
Assert-Contains 'write helper blocks readonly NAS writes' $pluginSource 'EnsureActiveLibraryWritable\(\)[\s\S]*?ActiveLibrary\.Kind\s*==\s*ActiveLibraryKind\.Nas[\s\S]*?!AllowNasSync'
Assert-Contains 'create category checks active library write permission' $pluginSource 'CreateCategory\(string\s+category\)[\s\S]*?EnsureActiveLibraryWritable\(\)'
Assert-Contains 'rename checks active library write permission' $pluginSource 'RenameBlock\(BlockInfo\s+block,\s*string\s+newName\)[\s\S]*?EnsureActiveLibraryWritable\(\)'
Assert-Contains 'save selection checks active library write permission' $pluginSource 'SaveSelectionAsBlockWithSelection[\s\S]*?EnsureActiveLibraryWritable\(\)'
Assert-Contains 'export block checks active library write permission' $pluginSource 'ExportBlockFromCurrentDrawing[\s\S]*?EnsureActiveLibraryWritable\(\)'
Assert-Contains 'form hides sync center unless allowed' $formSource 'if\s*\(BlockLibrary\.AllowNasSync\)[\s\S]*?btnLibrary\.DropDownItems\.Add\(btnSyncCenter\)'
Assert-NotContains 'form has no separate sync to NAS menu item' $formSource 'new\s+ToolStripMenuItem\("同步到NAS"\)|btnLibrary\.DropDownItems\.Add\(btnSync\)'
Assert-NotContains 'form no unconditional sync center menu array' $formSource 'btnLibrary\.DropDownItems\.AddRange\(new\s+ToolStripItem\[\]\s*\{[\s\S]*?btnSyncCenter'

Write-Host 'NasSyncPermission.Tests.ps1 passed'
