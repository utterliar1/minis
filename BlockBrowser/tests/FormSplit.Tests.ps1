$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repo 'BlockBrowser'
$rootSource = Get-Content -Encoding UTF8 (Join-Path $project 'Forms\BlockBrowserForm.cs') -Raw
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
    'Forms\BlockBrowserForm.Layout.cs',
    'Forms\BlockBrowserForm.CategoryBar.cs',
    'Forms\BlockBrowserForm.BlockGrid.cs',
    'Forms\BlockBrowserForm.Thumbnails.cs',
    'Forms\BlockBrowserForm.Actions.cs',
    'Forms\BlockBrowserForm.Dialogs.cs',
    'Forms\BlockBrowserForm.Diagnostics.cs'
)

foreach ($file in $splitFiles) {
    $path = Join-Path $project $file
    Assert-True ("split file exists " + $file) (Test-Path $path)
    $source = Get-Content -Encoding UTF8 $path -Raw
    Assert-Contains ("split file declares partial form " + $file) $source 'public\s+partial\s+class\s+BlockBrowserForm'
    Assert-Contains ("main project references " + $file) $csprojSource ('Compile Include="' + [regex]::Escape($file) + '"')
    Assert-Contains ("AutoCAD project references " + $file) $acadSource ('Compile Include="' + [regex]::Escape($file) + '"')
    Assert-Contains ("ZWCAD project references " + $file) $zwcadSource ('Compile Include="' + [regex]::Escape($file) + '"')
}

Assert-Contains 'root form stays partial' $rootSource 'public\s+partial\s+class\s+BlockBrowserForm\s*:\s*Form'
Assert-False 'root no longer contains initialize component body' ($rootSource -match 'private\s+void\s+InitializeComponent\(')
Assert-False 'root no longer contains category refresh body' ($rootSource -match 'private\s+void\s+RefreshCategories\(')
Assert-False 'root no longer contains block grid body' ($rootSource -match 'private\s+void\s+ShowBlocks\(')
Assert-False 'root no longer contains delete action body' ($rootSource -match 'private\s+void\s+DoDelete\(')

Write-Host 'FormSplit.Tests.ps1 passed'
