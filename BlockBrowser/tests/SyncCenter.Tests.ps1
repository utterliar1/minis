$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$syncSummarySource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\Sync\SyncSummaryMessageService.cs') -Raw
$syncTreeSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\UI\SyncPlanTreeBuilder.cs') -Raw
$syncCenterSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\SyncCenterDialog.cs') -Raw
$formSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserForm.cs') -Raw
$pluginSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserPlugin.cs') -Raw
$csprojSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.csproj') -Raw
$acadSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.AutoCAD.csproj') -Raw
$zwcadSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.ZWCAD.csproj') -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'sync summary exposes detailed report formatter' $syncSummarySource 'public\s+static\s+string\s+FormatDetailedReport\(SyncPlan\s+plan'
Assert-Contains 'sync detailed report includes decision details' $syncSummarySource 'FormatDetailedReport\(SyncPlan\s+plan[\s\S]*?foreach\s*\(var\s+decision\s+in\s+plan\.Decisions'
Assert-Contains 'sync summary exposes log append helper' $syncSummarySource 'public\s+static\s+void\s+AppendLog\(string\s+logPath,\s*SyncPlan\s+plan'
Assert-Contains 'sync tree builder exists' $syncTreeSource 'public\s+static\s+class\s+SyncPlanTreeBuilder'
Assert-Contains 'sync center uses a tree view' $syncCenterSource 'private\s+readonly\s+TreeView\s+_treeDetails'
Assert-Contains 'sync center populates tree preview' $syncCenterSource 'SyncPlanTreeBuilder\.Populate\(_treeDetails,\s*plan\)'
Assert-Contains 'sync center keeps copy text report' $syncCenterSource 'SyncSummaryMessageService\.FormatDetailedReport\(_lastPlan\)'
Assert-Contains 'sync center dialog exists' $formSource 'new\s+SyncCenterDialog\('
Assert-Contains 'library menu contains sync center before sync action' $formSource 'if\s*\(BlockLibrary\.AllowNasSync\)[\s\S]*?DropDownItems\.Add\(btnSyncCenter\);[\s\S]*?DropDownItems\.Add\(btnSync\);'
Assert-Contains 'panel sync appends log after execution' $formSource 'SyncSafeUploadsToNas\(\)[\s\S]*?SyncSummaryMessageService\.AppendLog'
Assert-Contains 'command sync appends log after execution' $pluginSource 'SyncSafeUploadsToNas\(\)[\s\S]*?SyncSummaryMessageService\.AppendLog'
Assert-Contains 'main project compiles sync center dialog' $csprojSource 'Compile Include="SyncCenterDialog\.cs"'
Assert-Contains 'main project compiles sync plan tree builder' $csprojSource 'Compile Include="UI\\SyncPlanTreeBuilder\.cs"'
Assert-Contains 'AutoCAD project compiles sync center dialog' $acadSource 'Compile Include="SyncCenterDialog\.cs"'
Assert-Contains 'AutoCAD project compiles sync plan tree builder' $acadSource 'Compile Include="UI\\SyncPlanTreeBuilder\.cs"'
Assert-Contains 'ZWCAD project compiles sync center dialog' $zwcadSource 'Compile Include="SyncCenterDialog\.cs"'
Assert-Contains 'ZWCAD project compiles sync plan tree builder' $zwcadSource 'Compile Include="UI\\SyncPlanTreeBuilder\.cs"'

Write-Host 'SyncCenter.Tests.ps1 passed'
