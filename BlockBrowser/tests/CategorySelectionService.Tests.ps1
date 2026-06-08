$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Library\CategorySelectionService.cs'
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

$allCategory = ([string][char]0x5168) + ([string][char]0x90E8)
$recentCategory = ([string][char]0x6700) + ([string][char]0x8FD1)

$categories = New-Object 'System.Collections.Generic.List[string]'
$categories.Add($allCategory)
$categories.Add($recentCategory)
$categories.Add('Electrical')
$categories.Add('Furniture')

$userCategories = [BlockBrowser.CategorySelectionService]::GetUserCategories($categories)
Assert-Equal 'user category count' 2 $userCategories.Count
Assert-True 'keeps first user category' ($userCategories.Contains('Electrical'))
Assert-True 'keeps second user category' ($userCategories.Contains('Furniture'))
Assert-False 'excludes all category' ($userCategories.Contains($allCategory))
Assert-False 'excludes recent category' ($userCategories.Contains($recentCategory))

$empty = [BlockBrowser.CategorySelectionService]::GetUserCategories($null)
Assert-Equal 'null categories return empty list' 0 $empty.Count

Write-Host 'CategorySelectionService.Tests.ps1 passed'
