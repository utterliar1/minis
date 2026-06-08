$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Library\BlockInfo.cs'
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'UI\BlockDeletePlanService.cs'
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

function New-Block($path) {
    $block = New-Object BlockBrowser.BlockInfo
    $block.FilePath = $path
    return $block
}

$path = 'D:\Blocks\Door.dwg'
$block = New-Block $path

$local = New-Object BlockBrowser.ActiveLibraryResult
$local.Kind = [BlockBrowser.ActiveLibraryKind]::LocalMirror

$nas = New-Object BlockBrowser.ActiveLibraryResult
$nas.Kind = [BlockBrowser.ActiveLibraryKind]::Nas

$recordPlan = [BlockBrowser.BlockDeletePlanService]::CreatePlan(
    $block,
    $local,
    [Func[string,bool]] { param($p) $true },
    [Func[string,bool]] { param($p) throw 'local mirror should not check file lock' })
Assert-Equal 'local mirror records delete request' ([BlockBrowser.BlockDeleteAction]::RecordLocalDeleteRequest) $recordPlan.Action
Assert-Equal 'local mirror keeps file path' $path $recordPlan.FilePath
Assert-Equal 'local mirror keeps block name' 'Door' $recordPlan.BlockName

$deletePlan = [BlockBrowser.BlockDeletePlanService]::CreatePlan(
    $block,
    $nas,
    [Func[string,bool]] { param($p) $true },
    [Func[string,bool]] { param($p) $true })
Assert-Equal 'nas unlocked deletes file' ([BlockBrowser.BlockDeleteAction]::DeleteFile) $deletePlan.Action

$lockedPlan = [BlockBrowser.BlockDeletePlanService]::CreatePlan(
    $block,
    $nas,
    [Func[string,bool]] { param($p) $true },
    [Func[string,bool]] { param($p) $false })
Assert-Equal 'nas locked warns user' ([BlockBrowser.BlockDeleteAction]::FileLocked) $lockedPlan.Action

$missingPlan = [BlockBrowser.BlockDeletePlanService]::CreatePlan(
    $block,
    $nas,
    [Func[string,bool]] { param($p) $false },
    [Func[string,bool]] { param($p) $true })
Assert-Equal 'missing file warns user' ([BlockBrowser.BlockDeleteAction]::MissingFile) $missingPlan.Action

$nonePlan = [BlockBrowser.BlockDeletePlanService]::CreatePlan(
    $null,
    $nas,
    [Func[string,bool]] { param($p) $true },
    [Func[string,bool]] { param($p) $true })
Assert-Equal 'null block reports no selection' ([BlockBrowser.BlockDeleteAction]::NoSelection) $nonePlan.Action

Write-Host 'BlockDeletePlanService.Tests.ps1 passed'
