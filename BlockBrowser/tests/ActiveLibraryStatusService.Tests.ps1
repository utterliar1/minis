$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'UI\ActiveLibraryStatusService.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Runtime.Serialization.dll',
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

$nas = New-Object BlockBrowser.ActiveLibraryResult
$nas.Kind = [BlockBrowser.ActiveLibraryKind]::Nas
$nas.ActivePath = '\\NAS\CADBlocks'
$nas.IsAvailable = $true
Assert-Equal 'formats nas active path' 'NAS: \\NAS\CADBlocks' ([BlockBrowser.ActiveLibraryStatusService]::Format($nas))

$local = New-Object BlockBrowser.ActiveLibraryResult
$local.Kind = [BlockBrowser.ActiveLibraryKind]::LocalMirror
$local.ActivePath = 'D:\CADBlocks'
$local.IsAvailable = $true
Assert-Equal 'formats local active path' 'Local mirror: D:\CADBlocks' ([BlockBrowser.ActiveLibraryStatusService]::Format($local))

$unavailable = New-Object BlockBrowser.ActiveLibraryResult
$unavailable.IsAvailable = $false
$unavailable.Message = 'NAS unavailable, using local mirror.'
Assert-Equal 'uses unavailable message' 'NAS unavailable, using local mirror.' ([BlockBrowser.ActiveLibraryStatusService]::Format($unavailable))

Assert-True 'null result returns fallback text' ([BlockBrowser.ActiveLibraryStatusService]::Format($null).Length -gt 0)

Write-Host 'ActiveLibraryStatusService.Tests.ps1 passed'
