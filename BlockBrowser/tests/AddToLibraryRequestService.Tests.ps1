$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'UI\AddToLibraryRequestService.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Core.dll'
)

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        throw "$name failed. Expected: [$expected], Actual: [$actual]"
    }
    Write-Host "PASS $name"
}

$safe = [Func[string,bool]] { param($name) -not [string]::IsNullOrWhiteSpace($name) -and $name -notlike '*\*' }

$valid = [BlockBrowser.AddToLibraryRequestService]::CreatePlan('  常用  ', '  Door  ', $safe)
Assert-Equal 'valid request action' ([BlockBrowser.AddToLibraryRequestAction]::StartCommand) $valid.Action
Assert-Equal 'valid request trims category' '常用' $valid.Category
Assert-Equal 'valid request trims block name' 'Door' $valid.BlockName
Assert-Equal 'valid request command' 'BBADD' $valid.PendingCommand

$cancelCategory = [BlockBrowser.AddToLibraryRequestService]::CreatePlan($null, 'Door', $safe)
Assert-Equal 'null category cancels' ([BlockBrowser.AddToLibraryRequestAction]::Cancel) $cancelCategory.Action

$cancelName = [BlockBrowser.AddToLibraryRequestService]::CreatePlan('常用', '', $safe)
Assert-Equal 'empty block name cancels' ([BlockBrowser.AddToLibraryRequestAction]::Cancel) $cancelName.Action

$invalidCategory = [BlockBrowser.AddToLibraryRequestService]::CreatePlan('Bad\Name', 'Door', $safe)
Assert-Equal 'invalid category rejected' ([BlockBrowser.AddToLibraryRequestAction]::InvalidName) $invalidCategory.Action

$invalidName = [BlockBrowser.AddToLibraryRequestService]::CreatePlan('常用', 'Bad\Name', $safe)
Assert-Equal 'invalid block name rejected' ([BlockBrowser.AddToLibraryRequestAction]::InvalidName) $invalidName.Action

Write-Host 'AddToLibraryRequestService.Tests.ps1 passed'
