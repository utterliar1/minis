$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Library\MirrorDirectoryAction.cs'
    Join-Path $root 'Library\MirrorDirectoryEntry.cs'
    Join-Path $root 'Library\MirrorDirectoryResult.cs'
    Join-Path $root 'Library\MirrorSummaryMessageService.cs'
    Join-Path $root 'UI\MirrorPreviewTreeBuilder.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Core.dll',
    'System.Windows.Forms.dll'
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

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) {
        throw "$name found forbidden pattern: $pattern"
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

$entry = New-Object BlockBrowser.MirrorDirectoryEntry
$entry.Action = [BlockBrowser.MirrorDirectoryAction]::CopyNew
$entry.RelativePath = 'A\New.dwg'
$result.Entries.Add($entry)
$previewTitle = -join ([char[]](0x9884, 0x89C8))
$previewConfirm = -join ([char[]](0x662F, 0x5426, 0x7EE7, 0x7EED))
$previewDialog = [BlockBrowser.MirrorSummaryMessageService]::FormatPreviewDialog($result)
Assert-True 'preview dialog includes preview title' ($previewDialog.Contains($previewTitle))
Assert-True 'preview dialog includes path sample' ($previewDialog.Contains('A\New.dwg'))
Assert-True 'preview dialog asks for confirmation' ($previewDialog.Contains($previewConfirm))
$previewCommand = [BlockBrowser.MirrorSummaryMessageService]::FormatPreviewCommand($result)
Assert-True 'preview command includes preview title' ($previewCommand.Contains($previewTitle))
Assert-True 'preview command includes path sample' ($previewCommand.Contains('A\New.dwg'))

$deleteEntry = New-Object BlockBrowser.MirrorDirectoryEntry
$deleteEntry.Action = [BlockBrowser.MirrorDirectoryAction]::Delete
$deleteEntry.RelativePath = 'Electrical\Switch\Old.dwg'
$overwriteEntry = New-Object BlockBrowser.MirrorDirectoryEntry
$overwriteEntry.Action = [BlockBrowser.MirrorDirectoryAction]::Overwrite
$overwriteEntry.RelativePath = 'Electrical\Switch\Door.dwg'
$protectedEntry = New-Object BlockBrowser.MirrorDirectoryEntry
$protectedEntry.Action = [BlockBrowser.MirrorDirectoryAction]::ProtectedCategorySkip
$protectedEntry.RelativePath = '个人块\Draft.dwg'
$treeResult = New-Object BlockBrowser.MirrorDirectoryResult
$treeResult.Entries.Add($deleteEntry)
$treeResult.Entries.Add($overwriteEntry)
$treeResult.Entries.Add($protectedEntry)
$localChangeEntry = New-Object BlockBrowser.MirrorDirectoryEntry
$localChangeEntry.Action = [BlockBrowser.MirrorDirectoryAction]::ProtectedLocalChangeSkip
$localChangeEntry.RelativePath = 'LocalChange\Draft.dwg'
$treeResult.Entries.Add($localChangeEntry)
$treeNodes = [BlockBrowser.MirrorPreviewTreeBuilder]::Build($treeResult)
Assert-True 'tree has action groups' ($treeNodes.Count -ge 3)
Assert-True 'tree puts delete group first' ($treeNodes[0].Tag -eq [BlockBrowser.MirrorDirectoryAction]::Delete)
Assert-True 'tree keeps overwrite group second' ($treeNodes[1].Tag -eq [BlockBrowser.MirrorDirectoryAction]::Overwrite)
$categorySkipLabel = -join ([char[]](0x767D, 0x540D, 0x5355, 0x8DF3, 0x8FC7))
$localChangeSkipLabel = -join ([char[]](0x672C, 0x5730, 0x53D8, 0x66F4, 0x8DF3, 0x8FC7))
Assert-True 'tree includes category skip group' ($treeNodes | Where-Object { $_.Text -match $categorySkipLabel -and $_.Tag -eq [BlockBrowser.MirrorDirectoryAction]::ProtectedCategorySkip })
Assert-True 'tree includes local change skip group' ($treeNodes | Where-Object { $_.Text -match $localChangeSkipLabel -and $_.Tag -eq [BlockBrowser.MirrorDirectoryAction]::ProtectedLocalChangeSkip })
Assert-True 'tree groups delete by first folder' ($treeNodes[0].Nodes[0].Text -eq 'Electrical')
Assert-True 'tree groups nested folder' ($treeNodes[0].Nodes[0].Nodes[0].Text -eq 'Switch')
Assert-True 'tree leaf is file name' ($treeNodes[0].Nodes[0].Nodes[0].Nodes[0].Text -eq 'Old.dwg')
Assert-True 'tree leaf tag keeps source entry' ([object]::ReferenceEquals($deleteEntry, $treeNodes[0].Nodes[0].Nodes[0].Nodes[0].Tag))

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$pluginSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserPlugin.cs') -Raw
$formSource = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowserForm.cs') -Raw
$mainProject = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.csproj') -Raw
$acadProject = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.AutoCAD.csproj') -Raw
$zwcadProject = Get-Content -Encoding UTF8 (Join-Path $repo 'BlockBrowser\BlockBrowser.ZWCAD.csproj') -Raw

Assert-Contains 'BlockLibrary exposes mirror preview' $pluginSource 'public\s+static\s+MirrorDirectoryResult\s+PreviewLocalMirrorFromNas\(\)'
Assert-Contains 'BlockLibrary returns mirror result' $pluginSource 'public\s+static\s+MirrorDirectoryResult\s+UpdateLocalMirrorFromNas\(\)'
Assert-Contains 'BlockLibrary uses mirror preview for update' $pluginSource 'PreviewLocalMirrorFromNas\(\)[\s\S]*?BlockFileOperations\.ApplyMirrorDirectoryResult'
Assert-Contains 'BBMIRROR writes mirror summary' $pluginSource 'MirrorSummaryMessageService\.FormatCommand\(result\)'
Assert-Contains 'BBMIRROR writes mirror preview' $pluginSource 'MirrorSummaryMessageService\.FormatPreviewCommand\(preview\)'
Assert-Contains 'BBMIRROR asks for confirmation' $pluginSource 'GetString\([\s\S]*?\[Y/N\]'
Assert-Contains 'panel shows tree mirror preview before update' $formSource 'new\s+MirrorPreviewDialog\(preview\)'
Assert-Contains 'panel confirms through tree preview dialog' $formSource 'previewDialog\.ShowDialog\(this\)\s*!=\s*DialogResult\.OK'
Assert-NotContains 'panel no longer uses text message box preview' $formSource 'MirrorSummaryMessageService\.FormatPreviewDialog\(preview\)'
Assert-Contains 'panel shows mirror summary' $formSource 'MirrorSummaryMessageService\.FormatDialog\(result\)'
Assert-Contains 'main project compiles mirror action' $mainProject 'Library\\MirrorDirectoryAction\.cs'
Assert-Contains 'main project compiles mirror entry' $mainProject 'Library\\MirrorDirectoryEntry\.cs'
Assert-Contains 'main project compiles mirror result' $mainProject 'Library\\MirrorDirectoryResult\.cs'
Assert-Contains 'main project compiles mirror summary service' $mainProject 'Library\\MirrorSummaryMessageService\.cs'
Assert-Contains 'main project compiles mirror preview tree builder' $mainProject 'UI\\MirrorPreviewTreeBuilder\.cs'
Assert-Contains 'main project compiles mirror preview dialog' $mainProject 'MirrorPreviewDialog\.cs'
Assert-Contains 'AutoCAD project compiles mirror action' $acadProject 'Library\\MirrorDirectoryAction\.cs'
Assert-Contains 'AutoCAD project compiles mirror entry' $acadProject 'Library\\MirrorDirectoryEntry\.cs'
Assert-Contains 'AutoCAD project compiles mirror result' $acadProject 'Library\\MirrorDirectoryResult\.cs'
Assert-Contains 'AutoCAD project compiles mirror preview dialog' $acadProject 'MirrorPreviewDialog\.cs'
Assert-Contains 'ZWCAD project compiles mirror action' $zwcadProject 'Library\\MirrorDirectoryAction\.cs'
Assert-Contains 'ZWCAD project compiles mirror entry' $zwcadProject 'Library\\MirrorDirectoryEntry\.cs'
Assert-Contains 'ZWCAD project compiles mirror result' $zwcadProject 'Library\\MirrorDirectoryResult\.cs'
Assert-Contains 'ZWCAD project compiles mirror preview dialog' $zwcadProject 'MirrorPreviewDialog\.cs'

Write-Host 'MirrorFeedback.Tests.ps1 passed'
