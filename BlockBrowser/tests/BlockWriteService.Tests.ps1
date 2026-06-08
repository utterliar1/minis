$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Library\BlockInfo.cs'
    Join-Path $root 'Library\LibraryNameRules.cs'
    Join-Path $root 'Library\BlockWriteService.cs'
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

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('BlockBrowserWriteTests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

try {
    $plan = [BlockBrowser.BlockWriteService]::PrepareSaveTarget($tempRoot, '  Socket  ', '  Electrical  ')
    Assert-True 'valid plan accepted' $plan.IsValid
    Assert-Equal 'block name trimmed' 'Socket' $plan.BlockName
    Assert-Equal 'category trimmed' 'Electrical' $plan.Category
    Assert-Equal 'category directory' (Join-Path $tempRoot 'Electrical') $plan.CategoryDirectory
    Assert-Equal 'output path' (Join-Path (Join-Path $tempRoot 'Electrical') 'Socket.dwg') $plan.OutputPath
    Assert-False 'new target is not overwrite' $plan.Exists
    Assert-True 'category directory created' (Test-Path $plan.CategoryDirectory)

    Set-Content -Encoding ASCII -Path $plan.OutputPath -Value 'dwg'
    $existing = [BlockBrowser.BlockWriteService]::PrepareSaveTarget($tempRoot, 'Socket', 'Electrical')
    Assert-True 'existing plan accepted' $existing.IsValid
    Assert-True 'existing target detected' $existing.Exists

    $invalidName = [BlockBrowser.BlockWriteService]::PrepareSaveTarget($tempRoot, '..', 'Electrical')
    Assert-False 'invalid block name rejected' $invalidName.IsValid
    Assert-Equal 'invalid block name message' 'Name or category contains invalid characters.' $invalidName.Message

    $invalidCategory = [BlockBrowser.BlockWriteService]::PrepareSaveTarget($tempRoot, 'Socket', 'Bad:Category')
    Assert-False 'invalid category rejected' $invalidCategory.IsValid

    $missingLibrary = [BlockBrowser.BlockWriteService]::PrepareSaveTarget('', 'Socket', 'Electrical')
    Assert-False 'missing library rejected' $missingLibrary.IsValid
    Assert-Equal 'missing library message' 'Library path is empty.' $missingLibrary.Message

    $info = [BlockBrowser.BlockWriteService]::CreateSavedBlockInfo($plan.OutputPath, $plan.Category)
    Assert-Equal 'saved block path' $plan.OutputPath $info.FilePath
    Assert-Equal 'saved block category' 'Electrical' $info.Category
    Assert-Equal 'saved block name' 'Socket' $info.Name
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

Write-Host 'BlockWriteService.Tests.ps1 passed'
