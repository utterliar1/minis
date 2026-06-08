$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'UI\SettingsUpdateService.cs'
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

$existingPath = 'D:\Blocks'
$same = [BlockBrowser.SettingsUpdateService]::CreatePlan(
    $existingPath,
    '  D:\Blocks  ',
    2.5,
    90,
    [Func[string,bool]] { param($path) $path -eq 'D:\Blocks' })

Assert-True 'existing path is valid' $same.IsValid
Assert-False 'existing path does not require creation' $same.RequiresDirectoryCreation
Assert-False 'same path is not changed' $same.LibraryPathChanged
Assert-Equal 'path is trimmed' 'D:\Blocks' $same.LibraryPath
Assert-Equal 'scale is preserved' 2.5 $same.InsertScale
Assert-Equal 'rotation degrees converted to radians' ([Math]::PI / 2) $same.InsertRotationRadians

$missing = [BlockBrowser.SettingsUpdateService]::CreatePlan(
    $existingPath,
    'D:\NewBlocks',
    1,
    0,
    [Func[string,bool]] { param($path) $false })

Assert-True 'missing path is valid' $missing.IsValid
Assert-True 'missing path requires creation' $missing.RequiresDirectoryCreation
Assert-True 'different path is changed' $missing.LibraryPathChanged

$empty = [BlockBrowser.SettingsUpdateService]::CreatePlan(
    $existingPath,
    '   ',
    1,
    0,
    [Func[string,bool]] { param($path) $true })

Assert-False 'empty path is invalid' $empty.IsValid

Write-Host 'SettingsUpdateService.Tests.ps1 passed'
