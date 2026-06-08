$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$workflowPath = Join-Path $repo '.github\workflows\release.yml'
$workflow = Get-Content -Encoding UTF8 $workflowPath -Raw
$manualFile = -join ([char[]](0x4F7F, 0x7528, 0x624B, 0x518C)) + '.html'
$manualFilePattern = [regex]::Escape($manualFile)

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name did not find pattern: $pattern"
    }
    Write-Host "PASS $name"
}

Assert-Contains 'release workflow handles BlockBrowser release tags' $workflow "'blockbrowser-v\*'"
Assert-Contains 'release workflow strips bb-v and blockbrowser-v prefixes' $workflow "\^\(bb-v\|blockbrowser-v\)"
Assert-Contains 'release package includes user manual' $workflow $manualFilePattern
Assert-Contains 'release package requires manual to exist' $workflow "$manualFilePattern`".*-ErrorAction Stop"
Assert-Contains 'release notes list user manual' $workflow "``$manualFilePattern``"

Write-Host 'ReleaseWorkflow.Tests.ps1 passed'
