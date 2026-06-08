$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Library\BlockInfo.cs'
    Join-Path $root 'UI\BlockInfoStatusService.cs'
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

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('bb-status-' + [Guid]::NewGuid().ToString('N') + '.dwg')
try {
    [System.IO.File]::WriteAllBytes($tmp, [byte[]](1, 2, 3))
    $lastWrite = [DateTime]::Parse('2026-06-08 19:30:00')
    [System.IO.File]::SetLastWriteTime($tmp, $lastWrite)

    $block = New-Object BlockBrowser.BlockInfo
    $block.FilePath = $tmp
    $status = [BlockBrowser.BlockInfoStatusService]::Format($block)

    Assert-Contains 'status includes block name' $status ([System.IO.Path]::GetFileNameWithoutExtension($tmp))
    Assert-Contains 'status includes byte size' $status '3 B'
    Assert-Contains 'status includes modified time' $status '2026-06-08 19:30'
}
finally {
    if (Test-Path $tmp) {
        Remove-Item -LiteralPath $tmp -Force
    }
}

$ready = "$([char]0x5C31)$([char]0x7EEA)"

$missing = New-Object BlockBrowser.BlockInfo
$missing.FilePath = Join-Path ([System.IO.Path]::GetTempPath()) 'missing-block-info-status.dwg'
Assert-Equal 'missing file returns ready' $ready ([BlockBrowser.BlockInfoStatusService]::Format($missing))
Assert-Equal 'null block returns ready' $ready ([BlockBrowser.BlockInfoStatusService]::Format($null))

Write-Host 'BlockInfoStatusService.Tests.ps1 passed'
