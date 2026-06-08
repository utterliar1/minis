$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$formSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserForm.cs') -Raw
$serviceSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\UI\BlockRenamePlanService.cs') -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'form rename delegates planning' $formSource 'BlockRenamePlanService\.CreatePlan'
Assert-Contains 'rename plan delegates validation' $serviceSource 'BlockFileOperations\.CanRenameBlock'
Assert-Contains 'rename plan delegates target path' $serviceSource 'BlockFileOperations\.GetRenameTargetPath'
Assert-NotContains 'form rename no inline target combine' $formSource 'Path\.Combine\(dir,\s*newName\s*\+\s*"\\.dwg"\)'

Write-Host 'FormRenameDelegation.Tests.ps1 passed'
