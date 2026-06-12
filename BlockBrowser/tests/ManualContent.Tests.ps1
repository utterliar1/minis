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
$syncTreePreview = -join ([char[]](0x6811, 0x5F62, 0x660E, 0x7EC6, 0x9884, 0x89C8))
$updateLocalLibrary = [regex]::Escape(-join ([char[]](0x66F4, 0x65B0, 0x672C, 0x5730, 0x56FE, 0x5E93)))
$designatedMaintainer = [regex]::Escape(-join ([char[]](0x6307, 0x5B9A, 0x7EF4, 0x62A4, 0x4EBA)))
$searchBlockNameOnly = -join ([char[]](0x6240, 0x6709, 0x5173, 0x952E, 0x8BCD, 0x90FD, 0x9700, 0x51FA, 0x73B0, 0x5728, 0x5757, 0x540D, 0x4E2D))
$searchIgnoresCategory = -join ([char[]](0x5206, 0x7C7B, 0x540D, 0x4E0D, 0x4F1A, 0x53C2, 0x4E0E, 0x5339, 0x914D))
$searchExample = (-join ([char[]](0x9632, 0x5835))) + ' lc'
$readonlyUpdateSource = -join ([char[]](0x53EA, 0x8BFB, 0x66F4, 0x65B0, 0x6765, 0x6E90))
$preferLocalMirror = -join ([char[]](0x4F18, 0x5148, 0x4F7F, 0x7528, 0x672C, 0x5730, 0x526F, 0x672C))
$nasChanges = -join ([char[]](0x65B0, 0x589E, 0x3001, 0x4FEE, 0x6539, 0x3001, 0x5220, 0x9664))
$ordinaryNoNasWrite = -join ([char[]](0x666E, 0x901A, 0x540C, 0x4E8B, 0x7684, 0x7535, 0x8111, 0x4E0D, 0x4F1A, 0x5199, 0x5165, 0x0020, 0x004E, 0x0041, 0x0053))
$personalBlocksCategory = -join ([char[]](0x4E2A, 0x4EBA, 0x5757))
$protectedMirrorKeepsLocal = -join ([char[]](0x4E0D, 0x4F1A, 0x8986, 0x76D6, 0x6216, 0x5220, 0x9664))
$protectedCategoryNotSyncedToNas = -join ([char[]](0x4E0D, 0x4F1A, 0x81EA, 0x52A8, 0x540C, 0x6B65, 0x5230, 0x0020, 0x004E, 0x0041, 0x0053))
$whitelistSkip = -join ([char[]](0x767D, 0x540D, 0x5355, 0x8DF3, 0x8FC7))
$mirrorProtectedSkip = -join ([char[]](0x4FDD, 0x62A4, 0x8DF3, 0x8FC7))
$mirrorPreview = -join ([char[]](0x66F4, 0x65B0, 0x672C, 0x5730, 0x56FE, 0x5E93, 0x6811, 0x5F62, 0x9884, 0x89C8))
$mirrorTreePreview = -join ([char[]](0x6811, 0x5F62, 0x9884, 0x89C8))
$confirmBeforeUpdate = -join ([char[]](0x786E, 0x8BA4, 0x540E, 0x624D, 0x4F1A, 0x5F00, 0x59CB, 0x6267, 0x884C))
$syncCommandOpensCenter = -join ([char[]](0x6253, 0x5F00, 0x540C, 0x6B65, 0x4E2D, 0x5FC3))

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

Assert-Contains 'manual explains NAS guide section' $manual $nasGuideTitle
Assert-Contains 'manual explains NAS main path' $manual 'NasLibraryPath'
Assert-Contains 'manual explains local mirror path' $manual 'LocalMirrorPath'
Assert-Contains 'manual explains default local mode' $manual 'CurrentLibraryMode=Local'
Assert-Contains 'manual explains auto mode' $manual 'CurrentLibraryMode=Auto'
Assert-Contains 'manual explains mirror command' $manual 'BBMIRROR'
Assert-Contains 'manual explains sync command' $manual 'BBSYNC'
Assert-Contains 'manual explains sync center' $manual $syncCenter
Assert-Contains 'manual explains sync center tree preview' $manual ([regex]::Escape($syncTreePreview))
Assert-Contains 'manual explains sync log' $manual 'sync-log\.txt'
Assert-Contains 'manual explains NAS sync permission flag' $manual 'AllowNasSync'
Assert-Contains 'manual explains update local library wording' $manual $updateLocalLibrary
Assert-Contains 'manual explains ordinary users prefer local mirror' $manual ([regex]::Escape($preferLocalMirror))
Assert-Contains 'manual explains NAS is readonly update source' $manual ([regex]::Escape($readonlyUpdateSource))
Assert-Contains 'manual explains update mirrors NAS changes' $manual ([regex]::Escape($nasChanges))
Assert-Contains 'manual explains ordinary users do not write NAS' $manual ([regex]::Escape($ordinaryNoNasWrite))
Assert-Contains 'manual explains protected local categories config' $manual 'ProtectedLocalCategories'
Assert-Contains 'manual explains default personal blocks category' $manual ([regex]::Escape($personalBlocksCategory))
Assert-Contains 'manual explains mirror keeps protected categories' $manual ([regex]::Escape($protectedMirrorKeepsLocal))
Assert-Contains 'manual explains protected categories are not synced to NAS' $manual ([regex]::Escape($protectedCategoryNotSyncedToNas))
Assert-Contains 'manual explains sync protected categories as whitelist skip' $manual ([regex]::Escape($whitelistSkip))
Assert-Contains 'manual explains mirror result protected skip count' $manual ([regex]::Escape($mirrorProtectedSkip))
Assert-Contains 'manual explains mirror preview' $manual ([regex]::Escape($mirrorPreview))
Assert-Contains 'manual explains mirror tree preview' $manual ([regex]::Escape($mirrorTreePreview))
Assert-Contains 'manual explains mirror waits for confirmation' $manual ([regex]::Escape($confirmBeforeUpdate))
Assert-Contains 'manual explains designated NAS maintainer' $manual $designatedMaintainer
Assert-Contains 'manual explains NAS protection' $manual $nasProtection
Assert-Contains 'manual explains BBSYNC opens sync center' $manual ('BBSYNC[\s\S]*?' + [regex]::Escape($syncCommandOpensCenter))
Assert-NotContains 'manual does not mention removed sync to NAS panel action' $manual '同步到NAS'
Assert-Contains 'manual explains search only matches block names' $manual ([regex]::Escape($searchBlockNameOnly))
Assert-Contains 'manual explains search ignores categories' $manual ([regex]::Escape($searchIgnoresCategory))
Assert-Contains 'manual explains space separated search keywords' $manual ([regex]::Escape($searchExample))

Write-Host 'ManualContent.Tests.ps1 passed'
