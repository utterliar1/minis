$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Library\MirrorDirectoryResult.cs'
    Join-Path $root 'Library\MirrorSummaryMessageService.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Core.dll'
)

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

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name did not find pattern: $pattern"
    }
    Write-Host "PASS $name"
}

$result = New-Object BlockBrowser.MirrorDirectoryResult
$result.CopiedNewCount = 1
$result.OverwrittenCount = 2
$result.DeletedCount = 3
$result.ProtectedSkipCount = 4

$dialog = [BlockBrowser.MirrorSummaryMessageService]::FormatDialog($result)
Assert-True 'dialog includes new count' ($dialog.Contains('1'))
Assert-True 'dialog includes overwritten count' ($dialog.Contains('2'))
Assert-True 'dialog includes deleted count' ($dialog.Contains('3'))
Assert-True 'dialog includes protected count' ($dialog.Contains('4'))
Assert-True 'dialog message is multiline' ($dialog.Contains("`n"))

$command = [BlockBrowser.MirrorSummaryMessageService]::FormatCommand($result)
Assert-True 'command includes new count' ($command.Contains('1'))
Assert-True 'command includes overwritten count' ($command.Contains('2'))
Assert-True 'command includes deleted count' ($command.Contains('3'))
Assert-True 'command includes protected count' ($command.Contains('4'))
Assert-False 'command message is single line' ($command.Contains("`n"))

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$pluginSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserPlugin.cs') -Raw
$formSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserForm.cs') -Raw
$mainProject = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.csproj') -Raw
$acadProject = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.AutoCAD.csproj') -Raw
$zwcadProject = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.ZWCAD.csproj') -Raw

Assert-Contains 'BlockLibrary returns mirror result' $pluginSource 'public\s+static\s+MirrorDirectoryResult\s+UpdateLocalMirrorFromNas\(\)'
Assert-Contains 'BBMIRROR writes mirror summary' $pluginSource 'MirrorSummaryMessageService\.FormatCommand\(result\)'
Assert-Contains 'panel shows mirror summary' $formSource 'MirrorSummaryMessageService\.FormatDialog\(result\)'
Assert-Contains 'main project compiles mirror result' $mainProject 'Library\\MirrorDirectoryResult\.cs'
Assert-Contains 'main project compiles mirror summary service' $mainProject 'Library\\MirrorSummaryMessageService\.cs'
Assert-Contains 'AutoCAD project compiles mirror result' $acadProject 'Library\\MirrorDirectoryResult\.cs'
Assert-Contains 'ZWCAD project compiles mirror result' $zwcadProject 'Library\\MirrorDirectoryResult\.cs'

Write-Host 'MirrorFeedback.Tests.ps1 passed'
