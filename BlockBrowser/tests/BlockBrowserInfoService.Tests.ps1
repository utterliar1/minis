$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'UI\BlockBrowserInfoService.cs'
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

function Assert-Contains($name, $actual, $expectedPart) {
    if ($actual -notlike "*$expectedPart*") {
        throw "$name failed. Expected [$actual] to contain [$expectedPart]"
    }
    Write-Host "PASS $name"
}

$lines = [BlockBrowser.BlockBrowserInfoService]::FormatLines('1.2.3', 'GstarCAD', 'D:\Blocks')
$commandPrefix = "$([char]0x547D)$([char]0x4EE4)"

Assert-Equal 'info line count' 3 $lines.Count
Assert-Contains 'header includes version' $lines[0] 'v1.2.3'
Assert-Contains 'header includes platform' $lines[0] 'GstarCAD'
Assert-Contains 'library line includes path' $lines[1] 'D:\Blocks'
Assert-Equal 'commands line preserves existing commands' ($commandPrefix + ': BB KLLQ BBADD BBEXPORT BBTHUMB') $lines[2]

Write-Host 'BlockBrowserInfoService.Tests.ps1 passed'
