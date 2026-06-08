$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Thumbnails\ThumbnailMemoryCacheService.cs'
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

$path = 'C:\Blocks\Electrical\Socket.dwg'
Assert-Equal 'cache key includes size' ($path + '_128') ([BlockBrowser.ThumbnailMemoryCacheService]::GetKey($path, 128))
Assert-Equal 'empty path cache key' '_64' ([BlockBrowser.ThumbnailMemoryCacheService]::GetKey($null, 64))

$cache = New-Object 'System.Collections.Generic.Dictionary[string,string]'
$cache['C:\Blocks\Electrical\Socket.dwg_64'] = 'small'
$cache['C:\Blocks\Electrical\Socket.dwg_128'] = 'large'
$cache['C:\Blocks\Electrical\Switch.dwg_128'] = 'other'

Assert-True 'has cached thumbnail' ([BlockBrowser.ThumbnailMemoryCacheService]::HasValue($cache, 'C:\Blocks\Electrical\Socket.dwg', 64))
Assert-False 'missing cached thumbnail' ([BlockBrowser.ThumbnailMemoryCacheService]::HasValue($cache, 'C:\Blocks\Electrical\Socket.dwg', 256))

$keys = [BlockBrowser.ThumbnailMemoryCacheService]::FindKeysForPath($cache.Keys, 'C:\Blocks\Electrical\Socket.dwg')
Assert-Equal 'finds path key count' 2 $keys.Count
Assert-True 'finds 64 key' ($keys.Contains('C:\Blocks\Electrical\Socket.dwg_64'))
Assert-True 'finds 128 key' ($keys.Contains('C:\Blocks\Electrical\Socket.dwg_128'))

[BlockBrowser.ThumbnailMemoryCacheService]::MovePathEntries($cache, 'C:\Blocks\Electrical\Socket.dwg', 'C:\Blocks\Electrical\Outlet.dwg')
Assert-False 'old key removed after move' ($cache.ContainsKey('C:\Blocks\Electrical\Socket.dwg_64'))
Assert-True 'new key added after move' ($cache.ContainsKey('C:\Blocks\Electrical\Outlet.dwg_64'))
Assert-Equal 'moved value preserved' 'small' $cache['C:\Blocks\Electrical\Outlet.dwg_64']
Assert-True 'other key preserved' ($cache.ContainsKey('C:\Blocks\Electrical\Switch.dwg_128'))

$removeKeys = [BlockBrowser.ThumbnailMemoryCacheService]::FindKeysForPath($cache.Keys, 'C:\Blocks\Electrical\Outlet.dwg')
Assert-Equal 'finds moved path key count' 2 $removeKeys.Count

Write-Host 'ThumbnailMemoryCacheService.Tests.ps1 passed'
