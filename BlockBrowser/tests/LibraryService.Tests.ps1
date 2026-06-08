$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Config\BlockBrowserConfig.cs'
    Join-Path $root 'Config\BlockBrowserConfigStore.cs'
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'Library\BlockInfo.cs'
    Join-Path $root 'Library\LibraryNameRules.cs'
    Join-Path $root 'Library\BlockLibraryService.cs'
    Join-Path $root 'Library\BlockFileOperations.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Core.dll',
    'System.Runtime.Serialization.dll',
    'Microsoft.VisualBasic.dll'
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

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('BlockBrowserLibraryTests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

try {
    $library = Join-Path $tempRoot 'Library'
    New-Item -ItemType Directory -Force -Path $library | Out-Null
    Set-Content -Encoding ASCII -Path (Join-Path $library 'RootBlock.dwg') -Value 'root'

    $electrical = Join-Path $library 'Electrical'
    $empty = Join-Path $library 'Empty'
    $hidden = Join-Path $library '.thumbs'
    New-Item -ItemType Directory -Force -Path $electrical, $empty, $hidden | Out-Null
    Set-Content -Encoding ASCII -Path (Join-Path $electrical 'Socket.dwg') -Value 'socket'
    Set-Content -Encoding ASCII -Path (Join-Path $electrical 'Switch.dwg') -Value 'switch'
    Set-Content -Encoding ASCII -Path (Join-Path $hidden 'Ignored.dwg') -Value 'ignored'

    $categories = [BlockBrowser.BlockLibraryService]::GetCategories($library)
    Assert-Equal 'category count' 4 $categories.Count
    Assert-Equal 'category all first' '全部' $categories[0]
    Assert-Equal 'category recent second' '最近' $categories[1]
    Assert-Equal 'category with dwg included' 'Electrical' $categories[2]
    Assert-True 'empty category included' ($categories -contains 'Empty')
    Assert-False 'hidden category excluded' ($categories -contains '.thumbs')

    $browsableCategories = [BlockBrowser.BlockLibraryService]::GetBrowsableCategories($library)
    Assert-Equal 'browsable category count' 3 $browsableCategories.Count
    Assert-Equal 'browsable category all first' '全部' $browsableCategories[0]
    Assert-Equal 'browsable category recent second' '最近' $browsableCategories[1]
    Assert-True 'browsable includes category with dwg' ($browsableCategories -contains 'Electrical')
    Assert-False 'browsable excludes empty category' ($browsableCategories -contains 'Empty')
    Assert-False 'browsable excludes hidden category' ($browsableCategories -contains '.thumbs')

    $allBlocks = [BlockBrowser.BlockLibraryService]::GetBlocks($library, '全部', @())
    Assert-Equal 'all blocks count' 3 $allBlocks.Count
    Assert-Equal 'all blocks first name' 'Socket' $allBlocks[0].Name
    Assert-Equal 'all blocks first category' 'Electrical' $allBlocks[0].Category
    Assert-Equal 'all blocks second name' 'Switch' $allBlocks[1].Name
    Assert-Equal 'all blocks second category' 'Electrical' $allBlocks[1].Category
    Assert-Equal 'all blocks third name' 'RootBlock' $allBlocks[2].Name
    Assert-Equal 'all blocks third category' '未分类' $allBlocks[2].Category

    $categoryBlocks = [BlockBrowser.BlockLibraryService]::GetBlocks($library, 'Electrical', @())
    Assert-Equal 'category blocks count' 2 $categoryBlocks.Count
    Assert-Equal 'category blocks sorted' 'Socket' $categoryBlocks[0].Name

    $recentPath = Join-Path $electrical 'Switch.dwg'
    $recentBlocks = [BlockBrowser.BlockLibraryService]::GetBlocks($library, '最近', @($recentPath, (Join-Path $library 'Missing.dwg')))
    Assert-Equal 'recent only existing count' 1 $recentBlocks.Count
    Assert-Equal 'recent category label' '最近' $recentBlocks[0].Category
    Assert-Equal 'recent path preserved' $recentPath $recentBlocks[0].FilePath

    $info = New-Object BlockBrowser.BlockInfo
    $info.FilePath = Join-Path $electrical 'Socket.dwg'
    $info.Category = 'Electrical'
    Assert-Equal 'block info derived name' 'Socket' $info.Name
    $info.FilePath = Join-Path $electrical 'SocketRenamed.dwg'
    Assert-Equal 'block info name updates with path' 'SocketRenamed' $info.Name

    Assert-True 'safe rename accepts normal name' ([BlockBrowser.BlockFileOperations]::CanRenameBlock($info, 'Socket2', $true))
    Assert-False 'safe rename rejects empty name' ([BlockBrowser.BlockFileOperations]::CanRenameBlock($info, ' ', $false))
    Assert-False 'safe rename rejects invalid name' ([BlockBrowser.BlockFileOperations]::CanRenameBlock($info, 'A:B', $false))

    $lockFile = Join-Path $library 'LockCheck.dwg'
    Set-Content -Encoding ASCII -Path $lockFile -Value 'lock'
    Assert-True 'unlocked file can open for write' ([BlockBrowser.BlockFileOperations]::CanOpenForExclusiveWrite($lockFile))
    $lockStream = [System.IO.File]::Open($lockFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    try {
        Assert-False 'locked file cannot open for write' ([BlockBrowser.BlockFileOperations]::CanOpenForExclusiveWrite($lockFile))
    }
    finally {
        $lockStream.Dispose()
    }
    Assert-False 'missing file cannot open for write' ([BlockBrowser.BlockFileOperations]::CanOpenForExclusiveWrite((Join-Path $library 'MissingLockCheck.dwg')))

    $copySource = Join-Path $tempRoot 'CopySource'
    $copyTarget = Join-Path $tempRoot 'CopyTarget'
    New-Item -ItemType Directory -Force -Path (Join-Path $copySource 'A'), (Join-Path $copySource 'A\.thumbs'), (Join-Path $copySource '.blockbrowser'), (Join-Path $copySource '.thumbs'), (Join-Path $copySource '.thumbs-backup') | Out-Null
    Set-Content -Encoding ASCII -Path (Join-Path $copySource 'A\Keep.dwg') -Value 'keep'
    Set-Content -Encoding ASCII -Path (Join-Path $copySource 'A\.thumbs\NestedSkip.png') -Value 'skip'
    Set-Content -Encoding ASCII -Path (Join-Path $copySource '.blockbrowser\Skip.json') -Value 'skip'
    Set-Content -Encoding ASCII -Path (Join-Path $copySource '.thumbs\Skip.png') -Value 'skip'
    Set-Content -Encoding ASCII -Path (Join-Path $copySource '.thumbs-backup\Keep.txt') -Value 'keep'
    [BlockBrowser.BlockFileOperations]::CopyDirectoryContents($copySource, $copyTarget)
    Assert-True 'copy keeps normal file' (Test-Path (Join-Path $copyTarget 'A\Keep.dwg'))
    Assert-False 'copy skips journal dir' (Test-Path (Join-Path $copyTarget '.blockbrowser\Skip.json'))
    Assert-False 'copy skips thumb dir' (Test-Path (Join-Path $copyTarget '.thumbs\Skip.png'))
    Assert-False 'copy skips nested thumb dir' (Test-Path (Join-Path $copyTarget 'A\.thumbs\NestedSkip.png'))
    Assert-True 'copy keeps thumb backup dir' (Test-Path (Join-Path $copyTarget '.thumbs-backup\Keep.txt'))
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

Write-Host 'LibraryService.Tests.ps1 passed'
