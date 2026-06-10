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
$syncCenter = [regex]::Escape(-join ([char[]](0x540C, 0x6B65, 0x4E2D, 0x5FC3)))
$searchBlockNameOnly = -join ([char[]](0x6240, 0x6709, 0x5173, 0x952E, 0x8BCD, 0x90FD, 0x9700, 0x51FA, 0x73B0, 0x5728, 0x5757, 0x540D, 0x4E2D))
$searchIgnoresCategory = -join ([char[]](0x5206, 0x7C7B, 0x540D, 0x4E0D, 0x4F1A, 0x53C2, 0x4E0E, 0x5339, 0x914D))
$searchExample = (-join ([char[]](0x9632, 0x5835))) + ' lc'

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
Assert-Contains 'manual explains sync center' $manual $syncCenter
Assert-Contains 'manual explains sync log' $manual 'sync-log\.txt'
Assert-Contains 'manual explains NAS protection' $manual $nasProtection
Assert-Contains 'manual explains search only matches block names' $manual ([regex]::Escape($searchBlockNameOnly))
Assert-Contains 'manual explains search ignores categories' $manual ([regex]::Escape($searchIgnoresCategory))
Assert-Contains 'manual explains space separated search keywords' $manual ([regex]::Escape($searchExample))

Write-Host 'ManualContent.Tests.ps1 passed'
