$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'UI\CardCacheService.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

$testSource = @'
public class FakeCard : BlockBrowser.IBlockCardState
{
    public string FilePath { get; set; }
    public bool IsDisposed { get; set; }
    public bool Visible { get; set; }
}
'@

$combinedSource = (Get-Content -Encoding UTF8 $sourceFiles[0] -Raw) + "`r`n" + $testSource
Add-Type -TypeDefinition $combinedSource -ReferencedAssemblies @(
    'System.Core.dll'
)

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        throw "$name failed. Expected: [$expected], Actual: [$actual]"
    }
    Write-Host "PASS $name"
}

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

$socket = New-Object FakeCard
$socket.FilePath = 'C:\Blocks\Electrical\Socket.dwg'
$socket.Visible = $true

$chair = New-Object FakeCard
$chair.FilePath = 'C:\Blocks\Furniture\Chair.dwg'
$chair.Visible = $false

$disposed = New-Object FakeCard
$disposed.FilePath = 'C:\Blocks\Electrical\Old.dwg'
$disposed.Visible = $true
$disposed.IsDisposed = $true

$cards = New-Object 'System.Collections.Generic.List[BlockBrowser.IBlockCardState]'
$cards.Add($socket)
$cards.Add($chair)
$cards.Add($disposed)

Assert-Equal 'find card by path' $socket ([BlockBrowser.CardCacheService]::FindByPath($cards, 'C:\Blocks\Electrical\Socket.dwg'))
Assert-Equal 'missing card returns null' $null ([BlockBrowser.CardCacheService]::FindByPath($cards, 'C:\Blocks\Electrical\Missing.dwg'))
Assert-Equal 'visible count ignores hidden and disposed' 1 ([BlockBrowser.CardCacheService]::CountVisible($cards))

$categoryCards = New-Object 'System.Collections.Generic.Dictionary[string,System.Collections.Generic.List[BlockBrowser.IBlockCardState]]'
$categoryCards['Electrical'] = New-Object 'System.Collections.Generic.List[BlockBrowser.IBlockCardState]'
$categoryCards['Electrical'].Add($socket)
$categoryCards['Electrical'].Add($disposed)
$categoryCards['Furniture'] = New-Object 'System.Collections.Generic.List[BlockBrowser.IBlockCardState]'
$categoryCards['Furniture'].Add($chair)

$removed = [BlockBrowser.CardCacheService]::RemoveFirstByPath($categoryCards, 'C:\Blocks\Electrical\Socket.dwg')
Assert-Equal 'removed card returned' $socket $removed
Assert-False 'removed card no longer in category' ($categoryCards['Electrical'].Contains($socket))
Assert-True 'other category preserved' ($categoryCards['Furniture'].Contains($chair))

$removedMissing = [BlockBrowser.CardCacheService]::RemoveFirstByPath($categoryCards, 'C:\Blocks\Electrical\Missing.dwg')
Assert-Equal 'missing remove returns null' $null $removedMissing

Write-Host 'CardCacheService.Tests.ps1 passed'
