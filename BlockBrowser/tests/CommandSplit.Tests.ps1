$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repo 'BlockBrowser'
$rootCommandsPath = Join-Path $project 'Commands\BlockBrowserCommands.cs'
$rootCommandsSource = Get-Content -Encoding UTF8 $rootCommandsPath -Raw
$csprojSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.csproj') -Raw
$acadSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.AutoCAD.csproj') -Raw
$zwcadSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.ZWCAD.csproj') -Raw

function Assert-True($name, $actual) {
    if (-not $actual) { throw "$name failed. Expected true." }
    Write-Host "PASS $name"
}

function Assert-False($name, $actual) {
    if ($actual) { throw "$name failed. Expected false." }
    Write-Host "PASS $name"
}

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

$splitFiles = @(
    'Commands\BlockBrowserCommands.BlockActions.cs',
    'Commands\BlockBrowserCommands.Sync.cs',
    'Commands\BlockBrowserCommands.Utilities.cs'
)

foreach ($file in $splitFiles) {
    $path = Join-Path $project $file
    Assert-True ("command split file exists " + $file) (Test-Path $path)
    $source = Get-Content -Encoding UTF8 $path -Raw
    Assert-Contains ("command split file declares partial class " + $file) $source 'public\s+partial\s+class\s+BlockBrowserCommands'
    Assert-Contains ("main project references " + $file) $csprojSource ('Compile Include="' + [regex]::Escape($file) + '"')
    Assert-Contains ("AutoCAD project references " + $file) $acadSource ('Compile Include="' + [regex]::Escape($file) + '"')
    Assert-Contains ("ZWCAD project references " + $file) $zwcadSource ('Compile Include="' + [regex]::Escape($file) + '"')
}

$blockActionsSource = Get-Content -Encoding UTF8 (Join-Path $project 'Commands\BlockBrowserCommands.BlockActions.cs') -Raw
$syncSource = Get-Content -Encoding UTF8 (Join-Path $project 'Commands\BlockBrowserCommands.Sync.cs') -Raw
$utilitiesSource = Get-Content -Encoding UTF8 (Join-Path $project 'Commands\BlockBrowserCommands.Utilities.cs') -Raw

Assert-Contains 'root command class is partial' $rootCommandsSource 'public\s+partial\s+class\s+BlockBrowserCommands'
Assert-Contains 'root keeps panel command' $rootCommandsSource 'CommandMethod\("BB"'
Assert-Contains 'root keeps panel alias command' $rootCommandsSource 'CommandMethod\("KLLQ"'
Assert-False 'root no direct add command implementation' ($rootCommandsSource -match 'CommandMethod\("BBADD"|private\s+void\s+DoAddToLibrary|public\s+void\s+AddToLibrary')
Assert-False 'root no direct export command implementation' ($rootCommandsSource -match 'CommandMethod\("BBEXPORT"|private\s+void\s+DoExportBlock|public\s+void\s+ExportBlockToLibrary')
Assert-False 'root no direct sync command implementation' ($rootCommandsSource -match 'CommandMethod\("BBSYNC"|CommandMethod\("BBMIRROR"|public\s+void\s+SyncLocalChanges|public\s+void\s+UpdateLocalMirror')
Assert-False 'root no direct utility command implementation' ($rootCommandsSource -match 'CommandMethod\("BBTHUMB"|CommandMethod\("BBINFO"|public\s+void\s+RefreshThumbnails|public\s+void\s+ShowInfo')

Assert-Contains 'block actions owns add command' $blockActionsSource 'CommandMethod\("BBADD"'
Assert-Contains 'block actions owns export command' $blockActionsSource 'CommandMethod\("BBEXPORT"'
Assert-Contains 'block actions opens export dialog' $blockActionsSource 'new\s+ExportBlocksDialog'
Assert-Contains 'sync owns sync command' $syncSource 'CommandMethod\("BBSYNC"'
Assert-Contains 'sync owns mirror command' $syncSource 'CommandMethod\("BBMIRROR"'
Assert-Contains 'sync opens sync center dialog' $syncSource 'new\s+SyncCenterDialog'
Assert-Contains 'utilities owns thumbnail command' $utilitiesSource 'CommandMethod\("BBTHUMB"'
Assert-Contains 'utilities owns info command' $utilitiesSource 'CommandMethod\("BBINFO"'

Write-Host 'CommandSplit.Tests.ps1 passed'
