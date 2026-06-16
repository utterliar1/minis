$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$dialogs = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.UI\Dialogs.cs') -Raw
$textCommands = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit\TextCommands.cs') -Raw
$drawCommands = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit\DrawCommands.cs') -Raw

function Assert-Match($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotMatch($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Match 'align dialog has auto base option' $dialogs 'rbFirst\.Text\s*=\s*"\\u81EA\\u52A8"'
Assert-Match 'align dialog has manual base option' $dialogs 'rbPick\.Text\s*=\s*"\\u624B\\u52A8"'
Assert-NotMatch 'align dialog no longer says first selected text' $dialogs '\\u7B2C\\u4E00\\u4E2A\\u9009\\u4E2D\\u6587\\u5B57'

Assert-Match 'align command sorts text by Y' $textCommands 'texts\.Sort\(delegate\(DBText a, DBText b\) \{ return b\.Position\.Y\.CompareTo\(a\.Position\.Y\); \}\)'
Assert-Match 'align command auto base uses top text' $textCommands 'if\s*\(useAutoBase\)[\s\S]*var t0 = texts\[0\];'
Assert-Match 'align command manual base converts point to world' $textCommands 'GetPointInWorld\(ppr\.Value\)'
Assert-Match 'align command manual base uses world point' $textCommands 'Point3d\s+worldPoint\s*=\s*GetPointInWorld\(ppr\.Value\)'

Assert-Match 'inc copy stores selection anchor' $drawCommands 'anchor\s*=\s*\(\(DBText\)ent\)\.Position'
Assert-Match 'inc copy converts point to world' $drawCommands 'GetPointInWorld\(ppr\.Value\)'
Assert-Match 'inc copy computes displacement from converted point X' $drawCommands 'double dx = worldPoint\.X - anchor\.X;'
Assert-Match 'inc copy computes displacement from converted point Y' $drawCommands 'double dy = worldPoint\.Y - anchor\.Y;'

$projectFiles = @(
    'CadToolkit\src\CadToolkit\CadToolkit.AutoCAD.csproj',
    'CadToolkit\src\CadToolkit\CadToolkit.GstarCAD.csproj',
    'CadToolkit\src\CadToolkit\CadToolkit.ZWCAD.csproj'
)
foreach ($projectFile in $projectFiles) {
    $project = Get-Content -Encoding UTF8 (Join-Path $repo $projectFile) -Raw
    Assert-Match "$projectFile includes shared coordinate helpers" $project '<Compile Include="CoordinateHelpers\.cs" />'
}
