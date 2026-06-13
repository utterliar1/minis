$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$pluginSource = @(
    Get-Content -Encoding UTF8 (Join-Path $root 'BlockBrowserPlugin.cs') -Raw
    Get-Content -Encoding UTF8 (Join-Path $root 'Library\BlockLibrary.Configuration.cs') -Raw
) -join "`n"
$assemblySource = Get-Content -Encoding UTF8 (Join-Path $root 'Properties\AssemblyInfo.cs') -Raw
$autoloadSource = Get-Content -Encoding UTF8 (Join-Path $root 'autoload.lsp') -Raw
$manualSource = Get-Content -Encoding UTF8 (Join-Path $root 'Docs\MANUAL_TEST_CHECKLIST.md') -Raw
$releaseSource = Get-Content -Encoding UTF8 (Join-Path $root 'Docs\RELEASE_NOTES.md') -Raw

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

Assert-Contains 'plugin app version is 1.3.2' $pluginSource 'public const string AppVersion = "1\.3\.2";'
Assert-Contains 'assembly version is 1.3.2.0' $assemblySource 'AssemblyVersion\("1\.3\.2\.0"\)'
Assert-Contains 'assembly file version is 1.3.2.0' $assemblySource 'AssemblyFileVersion\("1\.3\.2\.0"\)'
Assert-Contains 'autoload message is 1.3.2' $autoloadSource 'BlockBrowser v1\.3\.2 ready'
Assert-Contains 'manual checklist names 1.3.2' $manualSource 'BlockBrowser 1\.3\.2'
Assert-Contains 'manual checklist status label is 1.3.2' $manualSource 'v1\.3\.2 \| WLUP'
Assert-Contains 'release notes names 1.3.2' $releaseSource '1\.3\.2'

Assert-NotContains 'plugin no old 1.25.2 version' $pluginSource '1\.25\.2'
Assert-NotContains 'assembly no old 1.25.2 version' $assemblySource '1\.25\.2'
Assert-NotContains 'autoload no old 1.25.2 version' $autoloadSource '1\.25\.2'

Write-Host 'VersionConsistency.Tests.ps1 passed'
