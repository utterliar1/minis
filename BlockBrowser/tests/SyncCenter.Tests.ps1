$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$syncSummarySource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\Sync\SyncSummaryMessageService.cs') -Raw
$syncTreeSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\UI\SyncPlanTreeBuilder.cs') -Raw
$syncCenterSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\Forms\SyncCenterDialog.cs') -Raw
$formSource = @(
    Get-ChildItem -Path (Join-Path $repo 'BlockBrowser\Forms') -Filter 'BlockBrowserForm*.cs' |
        Sort-Object Name |
        ForEach-Object { Get-Content -Encoding UTF8 $_.FullName -Raw }
) -join "`n"
$pluginSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\Commands\BlockBrowserCommands.cs') -Raw
$csprojSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.csproj') -Raw
$acadSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.AutoCAD.csproj') -Raw
$zwcadSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.ZWCAD.csproj') -Raw
$simpleSyncConfirm = -join ([char[]](0x786E, 0x5B9A, 0x6309, 0x5F53, 0x524D, 0x540C, 0x6B65, 0x4E2D, 0x5FC3, 0x9884, 0x89C8, 0x6267, 0x884C, 0x540C, 0x6B65, 0x5230, 0x0020, 0x004E, 0x0041, 0x0053))

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'sync summary exposes detailed report formatter' $syncSummarySource 'public\s+static\s+string\s+FormatDetailedReport\(SyncPlan\s+plan'
Assert-Contains 'sync detailed report includes decision details' $syncSummarySource 'FormatDetailedReport\(SyncPlan\s+plan[\s\S]*?foreach\s*\(var\s+decision\s+in\s+plan\.Decisions'
Assert-Contains 'sync summary exposes log append helper' $syncSummarySource 'public\s+static\s+void\s+AppendLog\(string\s+logPath,\s*SyncPlan\s+plan'
Assert-Contains 'sync tree builder exists' $syncTreeSource 'public\s+static\s+class\s+SyncPlanTreeBuilder'
Assert-Contains 'sync center uses a tree view' $syncCenterSource 'private\s+readonly\s+TreeView\s+_treeDetails'
Assert-Contains 'sync center populates tree preview' $syncCenterSource 'SyncPlanTreeBuilder\.Populate\(_treeDetails,\s*plan\)'
Assert-Contains 'sync center keeps copy text report' $syncCenterSource 'SyncSummaryMessageService\.FormatDetailedReport\(_lastPlan\)'
Assert-Contains 'sync center appends log after execution' $syncCenterSource '_syncRunner\(\)[\s\S]*?SyncSummaryMessageService\.AppendLog'
Assert-Contains 'sync center refreshes tree before simple execution confirmation' $syncCenterSource ('_previewProvider\(\)[\s\S]*?SyncPlanTreeBuilder\.Populate\(_treeDetails,\s*preview\)[\s\S]*?' + [regex]::Escape($simpleSyncConfirm))
Assert-NotContains 'sync center execution confirm does not repeat full text preview' $syncCenterSource 'RunSync\(\)[\s\S]*?SyncSummaryMessageService\.FormatPreviewDialog'
Assert-Contains 'sync center dialog exists' $formSource 'new\s+SyncCenterDialog\('
Assert-Contains 'library menu contains sync center when allowed' $formSource 'if\s*\(BlockLibrary\.AllowNasSync\)[\s\S]*?DropDownItems\.Add\(btnSyncCenter\);'
Assert-NotContains 'library menu does not contain separate sync action' $formSource 'new\s+ToolStripMenuItem\("同步到NAS"\)|DropDownItems\.Add\(btnSync\)'
Assert-Contains 'BBSYNC opens sync center dialog' $pluginSource 'SyncLocalChanges\(\)[\s\S]*?OpenSyncCenterDialog\(\)'
Assert-Contains 'command helper creates sync center dialog' $pluginSource 'OpenSyncCenterDialog\(\)[\s\S]*?new\s+SyncCenterDialog\('
Assert-Contains 'main project compiles sync center dialog' $csprojSource 'Compile Include="Forms\\SyncCenterDialog\.cs"'
Assert-Contains 'main project compiles sync plan tree builder' $csprojSource 'Compile Include="UI\\SyncPlanTreeBuilder\.cs"'
Assert-Contains 'AutoCAD project compiles sync center dialog' $acadSource 'Compile Include="Forms\\SyncCenterDialog\.cs"'
Assert-Contains 'AutoCAD project compiles sync plan tree builder' $acadSource 'Compile Include="UI\\SyncPlanTreeBuilder\.cs"'
Assert-Contains 'ZWCAD project compiles sync center dialog' $zwcadSource 'Compile Include="Forms\\SyncCenterDialog\.cs"'
Assert-Contains 'ZWCAD project compiles sync plan tree builder' $zwcadSource 'Compile Include="UI\\SyncPlanTreeBuilder\.cs"'

Write-Host 'SyncCenter.Tests.ps1 passed'
