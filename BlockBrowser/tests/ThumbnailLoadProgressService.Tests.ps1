$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Thumbnails\ThumbnailLoadProgressService.cs'
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

Assert-Equal 'default batch size' 5 ([BlockBrowser.ThumbnailLoadProgressService]::DefaultBatchSize)
Assert-True 'completed when index reaches total' ([BlockBrowser.ThumbnailLoadProgressService]::IsComplete(10, 10))
Assert-True 'completed when index passes total' ([BlockBrowser.ThumbnailLoadProgressService]::IsComplete(11, 10))
Assert-False 'not completed before total' ([BlockBrowser.ThumbnailLoadProgressService]::IsComplete(9, 10))

$loading = [BlockBrowser.ThumbnailLoadProgressService]::FormatLoadingStatus(3, 10)
Assert-True 'loading status includes current count' ($loading.Contains('3'))
Assert-True 'loading status includes total count' ($loading.Contains('10'))

$failed = [BlockBrowser.ThumbnailLoadProgressService]::FormatFailedReadyStatus(2)
Assert-True 'failed status includes fail count' ($failed.Contains('2'))

Write-Host 'ThumbnailLoadProgressService.Tests.ps1 passed'
