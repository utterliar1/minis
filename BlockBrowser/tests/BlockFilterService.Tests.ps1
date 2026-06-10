$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Library\BlockInfo.cs'
    Join-Path $root 'Library\BlockFilterService.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Core.dll'
)

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        throw "$name failed. Expected: [$expected], Actual: [$actual]"
    }
    Write-Host "PASS $name"
}

function Assert-True($name, $actual) {
    if (-not $actual) {
        throw "$name failed. Expected true."
    }
    Write-Host "PASS $name"
}

function Assert-False($name, $actual) {
    if ($actual) {
        throw "$name failed. Expected false."
    }
    Write-Host "PASS $name"
}

$socket = New-Object BlockBrowser.BlockInfo
$socket.FilePath = 'C:\Blocks\Electrical\Socket.dwg'
$socket.Category = 'Electrical'

$chair = New-Object BlockBrowser.BlockInfo
$chair.FilePath = 'C:\Blocks\Furniture\Chair.dwg'
$chair.Category = 'Furniture'

$antiClog = New-Object BlockBrowser.BlockInfo
$antiClog.FilePath = 'C:\Blocks\Test\防堵-lc.dwg'
$antiClog.Category = 'TEST'

Assert-True 'empty keyword matches block' ([BlockBrowser.BlockFilterService]::Matches($socket, ''))
Assert-True 'whitespace keyword matches block' ([BlockBrowser.BlockFilterService]::Matches($socket, '   '))
Assert-True 'name match is case insensitive' ([BlockBrowser.BlockFilterService]::Matches($socket, 'sock'))
Assert-False 'category name does not match search' ([BlockBrowser.BlockFilterService]::Matches($socket, 'ELECT'))
Assert-True 'space separated keywords all match block name' ([BlockBrowser.BlockFilterService]::Matches($antiClog, '防堵 lc'))
Assert-True 'space separated keywords match in any order' ([BlockBrowser.BlockFilterService]::Matches($antiClog, 'lc 防堵'))
Assert-False 'all keywords must match block name' ([BlockBrowser.BlockFilterService]::Matches($antiClog, '防堵 pump'))
Assert-False 'nonmatching keyword hides block' ([BlockBrowser.BlockFilterService]::Matches($chair, 'socket'))
Assert-False 'null block does not match search' ([BlockBrowser.BlockFilterService]::Matches($null, 'socket'))
Assert-True 'null block matches empty search' ([BlockBrowser.BlockFilterService]::Matches($null, ''))

$blocks = New-Object 'System.Collections.Generic.List[BlockBrowser.BlockInfo]'
$blocks.Add($socket)
$blocks.Add($chair)
$visible = [BlockBrowser.BlockFilterService]::CountMatches($blocks, 'fur')
Assert-Equal 'count ignores category' 0 $visible

$countText = [BlockBrowser.BlockFilterService]::FormatCount(2)
Assert-True 'count label starts with number' ($countText.StartsWith('2 '))

Write-Host 'BlockFilterService.Tests.ps1 passed'
