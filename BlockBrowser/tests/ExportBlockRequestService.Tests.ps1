$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'UI\ExportBlockRequestService.cs'
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

$safe = [Func[string,bool]] { param($name) -not [string]::IsNullOrWhiteSpace($name) -and $name -notlike '*\*' }

$valid = [BlockBrowser.ExportBlockRequestService]::CreatePlan(
    [string[]]@('Door', 'Window'),
    '  常用  ',
    $safe)
Assert-Equal 'valid export action' ([BlockBrowser.ExportBlockRequestAction]::Export) $valid.Action
Assert-Equal 'valid export category trimmed' '常用' $valid.Category
Assert-Equal 'valid export selected count' 2 $valid.SelectedBlocks.Count

$noBlocks = [BlockBrowser.ExportBlockRequestService]::CreatePlan(
    [string[]]@(),
    '常用',
    $safe)
Assert-Equal 'empty selection cancels' ([BlockBrowser.ExportBlockRequestAction]::Cancel) $noBlocks.Action

$noCategory = [BlockBrowser.ExportBlockRequestService]::CreatePlan(
    [string[]]@('Door'),
    '',
    $safe)
Assert-Equal 'empty category cancels' ([BlockBrowser.ExportBlockRequestAction]::Cancel) $noCategory.Action

$invalid = [BlockBrowser.ExportBlockRequestService]::CreatePlan(
    [string[]]@('Door'),
    'Bad\Name',
    $safe)
Assert-Equal 'invalid category rejected' ([BlockBrowser.ExportBlockRequestAction]::InvalidCategory) $invalid.Action

$message = [BlockBrowser.ExportBlockRequestService]::FormatCompletion(2, 1)
Assert-Contains 'completion includes success count' $message '2'
Assert-Contains 'completion includes fail count' $message '1'

Write-Host 'ExportBlockRequestService.Tests.ps1 passed'
