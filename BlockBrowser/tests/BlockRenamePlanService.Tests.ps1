$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'Config\BlockBrowserConfig.cs'
    Join-Path $root 'Config\BlockBrowserConfigStore.cs'
    Join-Path $root 'Library\BlockInfo.cs'
    Join-Path $root 'Library\LibraryNameRules.cs'
    Join-Path $root 'Library\MirrorDirectoryAction.cs'
    Join-Path $root 'Library\MirrorDirectoryEntry.cs'
    Join-Path $root 'Library\MirrorDirectoryResult.cs'
    Join-Path $root 'Library\BlockFileOperations.cs'
    Join-Path $root 'UI\BlockRenamePlanService.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Core.dll',
    'System.Runtime.Serialization.dll'
)

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        throw "$name failed. Expected: [$expected], Actual: [$actual]"
    }
    Write-Host "PASS $name"
}

$dir = Join-Path ([System.IO.Path]::GetTempPath()) ('bb-rename-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dir | Out-Null
try {
    $oldPath = Join-Path $dir 'Door.dwg'
    $existingPath = Join-Path $dir 'Window.dwg'
    Set-Content -LiteralPath $oldPath -Value '' -Encoding ASCII
    Set-Content -LiteralPath $existingPath -Value '' -Encoding ASCII

    $block = New-Object BlockBrowser.BlockInfo
    $block.FilePath = $oldPath

    $rename = [BlockBrowser.BlockRenamePlanService]::CreatePlan($block, ' Cabinet ', [Func[string,bool]] { param($path) [System.IO.File]::Exists($path) })
    Assert-Equal 'valid rename action' ([BlockBrowser.BlockRenameAction]::Rename) $rename.Action
    Assert-Equal 'valid rename trims name' 'Cabinet' $rename.NewName
    Assert-Equal 'valid rename keeps old name' 'Door' $rename.OldName
    Assert-Equal 'valid rename target path' (Join-Path $dir 'Cabinet.dwg') $rename.NewPath

    $same = [BlockBrowser.BlockRenamePlanService]::CreatePlan($block, 'Door', [Func[string,bool]] { param($path) $false })
    Assert-Equal 'same name cancels' ([BlockBrowser.BlockRenameAction]::Cancel) $same.Action

    $empty = [BlockBrowser.BlockRenamePlanService]::CreatePlan($block, '   ', [Func[string,bool]] { param($path) $false })
    Assert-Equal 'empty name cancels' ([BlockBrowser.BlockRenameAction]::Cancel) $empty.Action

    $invalid = [BlockBrowser.BlockRenamePlanService]::CreatePlan($block, '..', [Func[string,bool]] { param($path) $false })
    Assert-Equal 'invalid name rejected' ([BlockBrowser.BlockRenameAction]::InvalidName) $invalid.Action

    $exists = [BlockBrowser.BlockRenamePlanService]::CreatePlan($block, 'Window', [Func[string,bool]] { param($path) [System.IO.File]::Exists($path) })
    Assert-Equal 'existing target rejected' ([BlockBrowser.BlockRenameAction]::TargetExists) $exists.Action

    $none = [BlockBrowser.BlockRenamePlanService]::CreatePlan($null, 'Any', [Func[string,bool]] { param($path) $false })
    Assert-Equal 'null block reports no selection' ([BlockBrowser.BlockRenameAction]::NoSelection) $none.Action
}
finally {
    if (Test-Path $dir) {
        Remove-Item -LiteralPath $dir -Recurse -Force
    }
}

Write-Host 'BlockRenamePlanService.Tests.ps1 passed'
