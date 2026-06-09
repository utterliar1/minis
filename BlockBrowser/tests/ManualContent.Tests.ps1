$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$manualFileName = -join ([char[]](0x4F7F, 0x7528, 0x624B, 0x518C)) + '.html'
$manualPath = Join-Path (Join-Path $repo 'BlockBrowser') $manualFileName
if (-not (Test-Path $manualPath)) {
    throw "Missing manual file: $manualPath"
}

$manual = Get-Content -Encoding UTF8 $manualPath -Raw
$nasGuideTitle = 'NAS\s*' + [regex]::Escape(-join ([char[]](0x4E0E, 0x672C, 0x5730, 0x526F, 0x672C)))
$nasProtection = [regex]::Escape(-join ([char[]](0x4E0D, 0x4F1A, 0x9759, 0x9ED8, 0x8986, 0x76D6))) + '\s*NAS'

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name did not find pattern: $pattern"
    }
    Write-Host "PASS $name"
}

Assert-Contains 'manual explains NAS guide section' $manual $nasGuideTitle
Assert-Contains 'manual explains NAS main path' $manual 'NasLibraryPath'
Assert-Contains 'manual explains local mirror path' $manual 'LocalMirrorPath'
Assert-Contains 'manual explains default local mode' $manual 'CurrentLibraryMode=Local'
Assert-Contains 'manual explains auto mode' $manual 'CurrentLibraryMode=Auto'
Assert-Contains 'manual explains mirror command' $manual 'BBMIRROR'
Assert-Contains 'manual explains sync command' $manual 'BBSYNC'
Assert-Contains 'manual explains NAS protection' $manual $nasProtection

Write-Host 'ManualContent.Tests.ps1 passed'
