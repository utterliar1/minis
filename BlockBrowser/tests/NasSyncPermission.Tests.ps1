$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$pluginSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserPlugin.cs') -Raw
$formSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserForm.cs') -Raw
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
Assert-Contains 'default config disables NAS sync' $configSource 'AllowNasSync\s*=\s*false'
Assert-Contains 'default ini disables NAS sync' $defaultConfig 'AllowNasSync=0'
Assert-Contains 'config load parses AllowNasSync' $storeSource 'key\.Equals\("AllowNasSync"[\s\S]*?config\.AllowNasSync'
Assert-Contains 'config save writes AllowNasSync' $storeSource 'AllowNasSync="\s*\+\s*\(config\.AllowNasSync\s*\?'
Assert-Contains 'config upgrade appends AllowNasSync' $storeSource 'AddMissingConfigLine\(missingLines,\s*loadedKeys,\s*"AllowNasSync"'
Assert-Contains 'BlockLibrary exposes AllowNasSync' $pluginSource 'public\s+static\s+bool\s+AllowNasSync\s*\{\s*get;\s*set;\s*\}'
Assert-Contains 'sync function guards NAS sync permission' $pluginSource 'SyncSafeUploadsToNas\(\)[\s\S]*?EnsureNasSyncAllowed\(\)'
Assert-Contains 'preview function guards NAS sync permission' $pluginSource 'PreviewLocalSync\(\)[\s\S]*?EnsureNasSyncAllowed\(\)'
Assert-Contains 'BBSYNC command checks permission before preview' $pluginSource 'SyncLocalChanges\(\)[\s\S]*?if\s*\(!BlockLibrary\.AllowNasSync\)[\s\S]*?return;[\s\S]*?PreviewLocalSync\(\)'
Assert-Contains 'write helper blocks readonly NAS writes' $pluginSource 'EnsureActiveLibraryWritable\(\)[\s\S]*?ActiveLibrary\.Kind\s*==\s*ActiveLibraryKind\.Nas[\s\S]*?!AllowNasSync'
Assert-Contains 'create category checks active library write permission' $pluginSource 'CreateCategory\(string\s+category\)[\s\S]*?EnsureActiveLibraryWritable\(\)'
Assert-Contains 'rename checks active library write permission' $pluginSource 'RenameBlock\(BlockInfo\s+block,\s*string\s+newName\)[\s\S]*?EnsureActiveLibraryWritable\(\)'
Assert-Contains 'save selection checks active library write permission' $pluginSource 'SaveSelectionAsBlockWithSelection[\s\S]*?EnsureActiveLibraryWritable\(\)'
Assert-Contains 'export block checks active library write permission' $pluginSource 'ExportBlockFromCurrentDrawing[\s\S]*?EnsureActiveLibraryWritable\(\)'
Assert-Contains 'form hides sync entries unless allowed' $formSource 'if\s*\(BlockLibrary\.AllowNasSync\)[\s\S]*?btnLibrary\.DropDownItems\.Add\(btnSyncCenter\)[\s\S]*?btnLibrary\.DropDownItems\.Add\(btnSync\)'
Assert-NotContains 'form no unconditional sync center menu array' $formSource 'btnLibrary\.DropDownItems\.AddRange\(new\s+ToolStripItem\[\]\s*\{[\s\S]*?btnSyncCenter[\s\S]*?btnSync'

Write-Host 'NasSyncPermission.Tests.ps1 passed'
