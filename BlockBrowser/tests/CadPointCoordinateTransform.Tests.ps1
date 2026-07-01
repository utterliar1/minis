$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$insertSource = Get-Content -Encoding UTF8 (Join-Path $root 'Library\BlockLibrary.Insert.cs') -Raw
$commandSource = Get-Content -Encoding UTF8 (Join-Path $root 'Commands\BlockBrowserCommands.BlockActions.cs') -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'insert point is converted from UCS to WCS before creating BlockReference' $insertSource 'Point3d\s+pt\s*=\s*pr\.Value\.TransformBy\(ed\.CurrentUserCoordinateSystem\)'
Assert-Contains 'add-to-library dialog base point is converted from UCS to WCS' $commandSource 'basePt\s*=\s*pr\.Value\.TransformBy\(ed\.CurrentUserCoordinateSystem\)'
Assert-Contains 'BBADD command base point is converted from UCS to WCS' $commandSource 'if\s*\(pr\.Status\s*==\s*PromptStatus\.OK\)\s*basePt\s*=\s*pr\.Value\.TransformBy\(ed\.CurrentUserCoordinateSystem\)'
Assert-Contains 'base point default remains world origin' $commandSource 'basePt\s*=\s*new\s+Point3d\(0,\s*0,\s*0\)'

Write-Host 'CadPointCoordinateTransform.Tests.ps1 passed'
