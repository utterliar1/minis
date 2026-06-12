$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repo 'BlockBrowser'
$operationsPath = Join-Path $project 'Library\BlockLibrary.Operations.cs'
$operationsSource = Get-Content -Encoding UTF8 $operationsPath -Raw
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
    'Library\BlockLibrary.Insert.cs',
    'Library\BlockLibrary.BlockWrite.cs',
    'Library\BlockLibrary.Export.cs'
)

foreach ($file in $splitFiles) {
    $path = Join-Path $project $file
    Assert-True ("operation split file exists " + $file) (Test-Path $path)
    $source = Get-Content -Encoding UTF8 $path -Raw
    Assert-Contains ("operation split file declares partial BlockLibrary " + $file) $source 'public\s+static\s+partial\s+class\s+BlockLibrary'
    Assert-Contains ("main project references " + $file) $csprojSource ('Compile Include="' + [regex]::Escape($file) + '"')
    Assert-Contains ("AutoCAD project references " + $file) $acadSource ('Compile Include="' + [regex]::Escape($file) + '"')
    Assert-Contains ("ZWCAD project references " + $file) $zwcadSource ('Compile Include="' + [regex]::Escape($file) + '"')
}

$insertSource = Get-Content -Encoding UTF8 (Join-Path $project 'Library\BlockLibrary.Insert.cs') -Raw
$blockWriteSource = Get-Content -Encoding UTF8 (Join-Path $project 'Library\BlockLibrary.BlockWrite.cs') -Raw
$exportSource = Get-Content -Encoding UTF8 (Join-Path $project 'Library\BlockLibrary.Export.cs') -Raw

Assert-Contains 'insert file owns InsertBlock' $insertSource 'public\s+static\s+void\s+InsertBlock\('
Assert-Contains 'insert file owns external drawing import' $insertSource 'ReadDwgFile\('
Assert-Contains 'block write file owns SaveSelectionAsBlockWithSelection' $blockWriteSource 'public\s+static\s+bool\s+SaveSelectionAsBlockWithSelection\('
Assert-Contains 'block write file owns Wblock selection write' $blockWriteSource 'Wblock\(ids,\s*basePt\)'
Assert-Contains 'export file owns ExportBlockFromCurrentDrawing' $exportSource 'public\s+static\s+bool\s+ExportBlockFromCurrentDrawing\('
Assert-Contains 'export file owns single block Wblock write' $exportSource 'Wblock\(blockId\)'

Assert-Contains 'operations root keeps RenameBlock' $operationsSource 'public\s+static\s+bool\s+RenameBlock\('
Assert-False 'operations root no direct InsertBlock body' ($operationsSource -match 'public\s+static\s+void\s+InsertBlock\(')
Assert-False 'operations root no direct SaveSelectionAsBlockWithSelection body' ($operationsSource -match 'public\s+static\s+bool\s+SaveSelectionAsBlockWithSelection\(')
Assert-False 'operations root no direct ExportBlockFromCurrentDrawing body' ($operationsSource -match 'public\s+static\s+bool\s+ExportBlockFromCurrentDrawing\(')
Assert-False 'operations root no direct CAD database import' ($operationsSource -match 'ReadDwgFile\(|Wblock\(')

Write-Host 'OperationsSplit.Tests.ps1 passed'
