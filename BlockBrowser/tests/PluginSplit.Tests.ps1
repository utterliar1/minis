$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repo 'BlockBrowser'
$rootSource = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowserPlugin.cs') -Raw
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
    'Commands\BlockBrowserCommands.cs',
    'Library\BlockLibrary.Configuration.cs',
    'Library\BlockLibrary.Sync.cs',
    'Library\BlockLibrary.Catalog.cs',
    'Library\BlockLibrary.Operations.cs',
    'Thumbnails\BlockLibrary.Thumbnails.cs'
)

foreach ($file in $splitFiles) {
    $path = Join-Path $project $file
    Assert-True ("split file exists " + $file) (Test-Path $path)
    $source = Get-Content -Encoding UTF8 $path -Raw
    if ($file -like 'Library\BlockLibrary.*' -or $file -like 'Thumbnails\BlockLibrary.*') {
        Assert-Contains ("split file declares partial BlockLibrary " + $file) $source 'public\s+static\s+partial\s+class\s+BlockLibrary'
    }
    Assert-Contains ("main project references " + $file) $csprojSource ('Compile Include="' + [regex]::Escape($file) + '"')
    Assert-Contains ("AutoCAD project references " + $file) $acadSource ('Compile Include="' + [regex]::Escape($file) + '"')
    Assert-Contains ("ZWCAD project references " + $file) $zwcadSource ('Compile Include="' + [regex]::Escape($file) + '"')
}

Assert-Contains 'commands class moved to commands folder' (Get-Content -Encoding UTF8 (Join-Path $project 'Commands\BlockBrowserCommands.cs') -Raw) 'public\s+(partial\s+)?class\s+BlockBrowserCommands'
Assert-Contains 'root keeps extension plugin class' $rootSource 'public\s+class\s+BlockBrowserPlugin\s*:\s*IExtensionApplication'
Assert-False 'root no longer contains BlockLibrary implementation' ($rootSource -match 'public\s+static\s+(partial\s+)?class\s+BlockLibrary')
Assert-False 'root no longer contains command implementation' ($rootSource -match 'public\s+class\s+BlockBrowserCommands')

Write-Host 'PluginSplit.Tests.ps1 passed'
