$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'UI\ResourceDisposalService.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

$testSource = @'
public class FakeDisposable : System.IDisposable
{
    public int DisposeCount { get; private set; }
    public bool ThrowOnDispose { get; set; }

    public void Dispose()
    {
        DisposeCount++;
        if (ThrowOnDispose) throw new System.InvalidOperationException("dispose failed");
    }
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

$single = New-Object FakeDisposable
[BlockBrowser.ResourceDisposalService]::DisposeQuietly($single)
Assert-Equal 'single disposable disposed once' 1 $single.DisposeCount

$throwing = New-Object FakeDisposable
$throwing.ThrowOnDispose = $true
[BlockBrowser.ResourceDisposalService]::DisposeQuietly($throwing)
Assert-Equal 'throwing disposable attempted once' 1 $throwing.DisposeCount

$items = New-Object 'System.Collections.Generic.List[FakeDisposable]'
$first = New-Object FakeDisposable
$second = New-Object FakeDisposable
$items.Add($first)
$items.Add($second)
[BlockBrowser.ResourceDisposalService]::DisposeAll($items)
Assert-Equal 'first list item disposed' 1 $first.DisposeCount
Assert-Equal 'second list item disposed' 1 $second.DisposeCount

$map = New-Object 'System.Collections.Generic.Dictionary[string,System.Collections.Generic.List[FakeDisposable]]'
$map['A'] = New-Object 'System.Collections.Generic.List[FakeDisposable]'
$third = New-Object FakeDisposable
$map['A'].Add($third)
[BlockBrowser.ResourceDisposalService]::DisposeDictionaryValuesAndClear($map)
Assert-Equal 'dictionary value disposed' 1 $third.DisposeCount
Assert-Equal 'dictionary cleared' 0 $map.Count

Write-Host 'ResourceDisposalService.Tests.ps1 passed'
