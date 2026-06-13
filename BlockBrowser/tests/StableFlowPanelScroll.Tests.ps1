$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Encoding UTF8 (Join-Path $root 'Forms\BlockBrowserForm.cs') -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name did not find pattern: $pattern"
    }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) {
        throw "$name found forbidden pattern: $pattern"
    }
    Write-Host "PASS $name"
}

Assert-Contains 'stable flow panel overrides scroll target calculation' $source 'protected\s+override\s+Point\s+ScrollToControl\s*\(\s*Control\s+activeControl\s*\)'
Assert-Contains 'stable flow panel keeps current display rectangle position' $source 'return\s+DisplayRectangle\.Location\s*;'
Assert-NotContains 'stable flow panel does not rely on hidden public scroll method' $source 'new\s+void\s+ScrollControlIntoView'
Assert-NotContains 'stable flow panel has no unreachable comment after return' $source 'return\s+DisplayRectangle\.Location\s*;\s*//'

Write-Host 'StableFlowPanelScroll.Tests.ps1 passed'
