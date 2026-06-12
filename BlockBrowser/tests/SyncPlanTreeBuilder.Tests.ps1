$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'UI\SyncPlanTreeBuilder.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Runtime.Serialization.dll',
    'System.Core.dll',
    'System.Windows.Forms.dll'
)

function Assert-True($name, $actual) {
    if (-not $actual) {
        throw "$name failed. Expected true."
    }
    Write-Host "PASS $name"
}

$plan = New-Object BlockBrowser.SyncPlan

$upload = New-Object BlockBrowser.SyncDecision
$upload.Kind = [BlockBrowser.SyncDecisionKind]::Upload
$upload.Path = 'Electrical\Switch\New.dwg'
$plan.Decisions.Add($upload)

$duplicate = New-Object BlockBrowser.SyncDecision
$duplicate.Kind = [BlockBrowser.SyncDecisionKind]::SkipDuplicate
$duplicate.Path = 'Electrical\Switch\Existing.dwg'
$plan.Decisions.Add($duplicate)

$protected = New-Object BlockBrowser.SyncDecision
$protected.Kind = [BlockBrowser.SyncDecisionKind]::ProtectedCategorySkip
$protected.Path = 'Personal\Draft.dwg'
$plan.Decisions.Add($protected)

$conflict = New-Object BlockBrowser.SyncDecision
$conflict.Kind = [BlockBrowser.SyncDecisionKind]::Conflict
$conflict.Path = 'Process\Valve.dwg'
$plan.Decisions.Add($conflict)

$nodes = [BlockBrowser.SyncPlanTreeBuilder]::Build($plan)
$uploadLabel = -join ([char[]](0x4E0A, 0x4F20))
$duplicateLabel = -join ([char[]](0x91CD, 0x590D, 0x8DF3, 0x8FC7))
$whitelistLabel = -join ([char[]](0x767D, 0x540D, 0x5355, 0x8DF3, 0x8FC7))
$conflictLabel = -join ([char[]](0x51B2, 0x7A81))

Assert-True 'tree has grouped nodes' ($nodes.Count -ge 4)
Assert-True 'tree puts upload group first' ($nodes[0].Text -match $uploadLabel -and $nodes[0].Tag -eq [BlockBrowser.SyncDecisionKind]::Upload)
Assert-True 'tree has duplicate skip group' ($nodes | Where-Object { $_.Text -match $duplicateLabel -and $_.Tag -eq [BlockBrowser.SyncDecisionKind]::SkipDuplicate })
Assert-True 'tree has whitelist skip group' ($nodes | Where-Object { $_.Text -match $whitelistLabel -and $_.Tag -eq [BlockBrowser.SyncDecisionKind]::ProtectedCategorySkip })
Assert-True 'tree has conflict group' ($nodes | Where-Object { $_.Text -match $conflictLabel -and $_.Tag -eq [BlockBrowser.SyncDecisionKind]::Conflict })
Assert-True 'tree groups by first folder' ($nodes[0].Nodes[0].Text -eq 'Electrical')
Assert-True 'tree groups nested folder' ($nodes[0].Nodes[0].Nodes[0].Text -eq 'Switch')
Assert-True 'tree leaf is file name' ($nodes[0].Nodes[0].Nodes[0].Nodes[0].Text -eq 'New.dwg')
Assert-True 'tree leaf tag keeps source decision' ([object]::ReferenceEquals($upload, $nodes[0].Nodes[0].Nodes[0].Nodes[0].Tag))

$emptyNodes = [BlockBrowser.SyncPlanTreeBuilder]::Build($null)
$emptyLabel = -join ([char[]](0x6682, 0x65E0, 0x540C, 0x6B65, 0x9879))
Assert-True 'empty tree shows placeholder' ($emptyNodes.Count -eq 1 -and $emptyNodes[0].Text -match $emptyLabel)

Write-Host 'SyncPlanTreeBuilder.Tests.ps1 passed'
