$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Library\LibraryNameRules.cs'
    Join-Path $root 'Library\CategoryCreationService.cs'
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

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('BlockBrowserCategoryCreationTests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

try {
    $created = [BlockBrowser.CategoryCreationService]::CreateCategory($tempRoot, ' Electrical ')
    Assert-True 'valid category accepted' $created.IsValid
    Assert-True 'new category marked created' $created.Created
    Assert-Equal 'category trimmed' 'Electrical' $created.Category
    Assert-True 'category directory created' (Test-Path (Join-Path $tempRoot 'Electrical'))

    $existing = [BlockBrowser.CategoryCreationService]::CreateCategory($tempRoot, 'Electrical')
    Assert-True 'existing category accepted' $existing.IsValid
    Assert-False 'existing category not marked created' $existing.Created

    $invalid = [BlockBrowser.CategoryCreationService]::CreateCategory($tempRoot, 'Bad:Category')
    Assert-False 'invalid category rejected' $invalid.IsValid

    $missingLibrary = [BlockBrowser.CategoryCreationService]::CreateCategory('', 'Electrical')
    Assert-False 'empty library rejected' $missingLibrary.IsValid
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

Write-Host 'CategoryCreationService.Tests.ps1 passed'
