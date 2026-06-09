$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'UI\SettingsUpdateService.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Core.dll',
    'System.Runtime.Serialization.dll'
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

$currentNasPath = '\\NAS\CADBlocks\BlockBrowser'
$currentLocalPath = 'D:\Blocks'
$same = [BlockBrowser.SettingsUpdateService]::CreatePlan(
    $currentNasPath,
    '  \\NAS\CADBlocks\BlockBrowser  ',
    $currentLocalPath,
    '  D:\Blocks  ',
    [BlockBrowser.LibraryMode]::Auto,
    [BlockBrowser.LibraryMode]::Auto,
    2.5,
    90,
    [Func[string,bool]] { param($path) $path -eq 'D:\Blocks' })

Assert-True 'existing path is valid' $same.IsValid
Assert-False 'existing local path does not require creation' $same.RequiresLocalMirrorDirectoryCreation
Assert-False 'same NAS path is not changed' $same.NasLibraryPathChanged
Assert-False 'same local mirror path is not changed' $same.LocalMirrorPathChanged
Assert-False 'same mode is not changed' $same.CurrentLibraryModeChanged
Assert-Equal 'NAS path is trimmed' '\\NAS\CADBlocks\BlockBrowser' $same.NasLibraryPath
Assert-Equal 'local path is trimmed' 'D:\Blocks' $same.LocalMirrorPath
Assert-Equal 'mode is preserved' ([BlockBrowser.LibraryMode]::Auto) $same.CurrentLibraryMode
Assert-Equal 'scale is preserved' 2.5 $same.InsertScale
Assert-Equal 'rotation degrees converted to radians' ([Math]::PI / 2) $same.InsertRotationRadians

$missing = [BlockBrowser.SettingsUpdateService]::CreatePlan(
    $currentNasPath,
    $currentNasPath,
    $currentLocalPath,
    'D:\NewBlocks',
    [BlockBrowser.LibraryMode]::Auto,
    [BlockBrowser.LibraryMode]::Local,
    1,
    0,
    [Func[string,bool]] { param($path) $false })

Assert-True 'missing path is valid' $missing.IsValid
Assert-True 'missing local path requires creation' $missing.RequiresLocalMirrorDirectoryCreation
Assert-False 'unchanged NAS path remains unchanged' $missing.NasLibraryPathChanged
Assert-True 'different local path is changed' $missing.LocalMirrorPathChanged
Assert-True 'different mode is changed' $missing.CurrentLibraryModeChanged
Assert-Equal 'NAS path is not overwritten by local path' $currentNasPath $missing.NasLibraryPath
Assert-Equal 'changed local path is captured' 'D:\NewBlocks' $missing.LocalMirrorPath
Assert-Equal 'changed mode is captured' ([BlockBrowser.LibraryMode]::Local) $missing.CurrentLibraryMode

$changedNas = [BlockBrowser.SettingsUpdateService]::CreatePlan(
    $currentNasPath,
    '\\NAS2\CADBlocks\BlockBrowser',
    $currentLocalPath,
    $currentLocalPath,
    [BlockBrowser.LibraryMode]::Auto,
    [BlockBrowser.LibraryMode]::Auto,
    1,
    0,
    [Func[string,bool]] { param($path) $true })

Assert-True 'explicit NAS path change is tracked' $changedNas.NasLibraryPathChanged
Assert-False 'explicit NAS path change does not change local mirror' $changedNas.LocalMirrorPathChanged
Assert-Equal 'changed NAS path is captured' '\\NAS2\CADBlocks\BlockBrowser' $changedNas.NasLibraryPath
Assert-Equal 'local mirror path is preserved during NAS change' $currentLocalPath $changedNas.LocalMirrorPath

$empty = [BlockBrowser.SettingsUpdateService]::CreatePlan(
    $currentNasPath,
    '   ',
    $currentLocalPath,
    '   ',
    [BlockBrowser.LibraryMode]::Auto,
    [BlockBrowser.LibraryMode]::Auto,
    1,
    0,
    [Func[string,bool]] { param($path) $true })

Assert-False 'empty path is invalid' $empty.IsValid

Write-Host 'SettingsUpdateService.Tests.ps1 passed'
