$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'Sync\SyncSummaryMessageService.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Runtime.Serialization.dll',
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

$plan = New-Object BlockBrowser.SyncPlan
$plan.UploadCount = 3
$plan.SkippedDuplicateCount = 4
$plan.ConflictCount = 5
$plan.DeleteReviewCount = 6
$plan.FailedCount = 7
$plan.ProtectedCategorySkipCount = 8
$upload = New-Object BlockBrowser.SyncDecision
$upload.Kind = [BlockBrowser.SyncDecisionKind]::Upload
$upload.Path = 'Electrical\Socket.dwg'
$upload.TargetPath = 'Electrical\Socket.dwg'
$plan.Decisions.Add($upload)

$dialog = [BlockBrowser.SyncSummaryMessageService]::FormatDialog($plan)
Assert-True 'dialog message includes upload count' ($dialog.Contains('3'))
Assert-True 'dialog message includes skipped count' ($dialog.Contains('4'))
Assert-True 'dialog message includes conflict count' ($dialog.Contains('5'))
Assert-True 'dialog message includes delete review count' ($dialog.Contains('6'))
Assert-True 'dialog message includes failed count' ($dialog.Contains('7'))
Assert-True 'dialog message includes protected category skip count' ($dialog.Contains('8'))
Assert-True 'dialog message is multiline' ($dialog.Contains("`n"))

$command = [BlockBrowser.SyncSummaryMessageService]::FormatCommand($plan)
Assert-True 'command message includes upload count' ($command.Contains('3'))
Assert-True 'command message includes failed count' ($command.Contains('7'))
Assert-True 'command message includes protected category skip count' ($command.Contains('8'))
Assert-False 'command message is single line' ($command.Contains("`n"))

$emptyDialog = [BlockBrowser.SyncSummaryMessageService]::FormatDialog($null)
Assert-True 'null plan formats zero count' ($emptyDialog.Contains('0'))

$preview = [BlockBrowser.SyncSummaryMessageService]::FormatPreviewDialog($plan)
$previewTitle = -join ([char[]](0x540C, 0x6B65, 0x9884, 0x89C8))
$previewConfirm = -join ([char[]](0x662F, 0x5426, 0x7EE7, 0x7EED))
Assert-True 'preview dialog names preview' ($preview.Contains($previewTitle))
Assert-True 'preview dialog includes path sample' ($preview.Contains('Electrical\Socket.dwg'))
Assert-True 'preview dialog asks for confirmation' ($preview.Contains($previewConfirm))

$protectedDecision = New-Object BlockBrowser.SyncDecision
$protectedDecision.Kind = [BlockBrowser.SyncDecisionKind]::ProtectedCategorySkip
$protectedDecision.Path = 'Personal\Draft.dwg'
$protectedPlan = New-Object BlockBrowser.SyncPlan
$protectedPlan.ProtectedCategorySkipCount = 1
$protectedPlan.Decisions.Add($protectedDecision)
$protectedReport = [BlockBrowser.SyncSummaryMessageService]::FormatDetailedReport($protectedPlan)
$whitelistSkipLabel = -join ([char[]](0x767D, 0x540D, 0x5355, 0x8DF3, 0x8FC7))
Assert-True 'detailed report labels protected category skip' ($protectedReport.Contains($whitelistSkipLabel))
Assert-True 'detailed report includes protected path' ($protectedReport.Contains('Personal\Draft.dwg'))

Write-Host 'SyncSummaryMessageService.Tests.ps1 passed'
