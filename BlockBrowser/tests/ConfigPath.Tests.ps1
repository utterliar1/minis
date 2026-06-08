$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    Join-Path $root 'Sync\LibrarySyncModels.cs'
    Join-Path $root 'Sync\ActiveLibraryResolver.cs'
    Join-Path $root 'Library\LibraryNameRules.cs'
    Join-Path $root 'Config\BlockBrowserConfig.cs'
    Join-Path $root 'Config\BlockBrowserConfigStore.cs'
    Join-Path $root 'Paths\LibraryPathService.cs'
)

foreach ($file in $sourceFiles) {
    if (-not (Test-Path $file)) {
        throw "Missing source file: $file"
    }
}

Add-Type -Path $sourceFiles -ReferencedAssemblies @(
    'System.Core.dll',
    'System.Runtime.Serialization.dll',
    'System.Xml.dll'
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

function Assert-DoesNotThrow($name, [scriptblock]$action) {
    try {
        & $action
    }
    catch {
        throw "$name failed. Unexpected exception: $($_.Exception.Message)"
    }
    Write-Host "PASS $name"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('BlockBrowserConfigTests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

try {
    $pluginRoot = Join-Path $tempRoot 'Plugin'
    New-Item -ItemType Directory -Force -Path $pluginRoot | Out-Null
    $defaultIni = @(
        '# default template'
        'LibraryPath=我的常用块'
        'NasLibraryPath=我的常用块'
        'LocalMirrorPath=%USERPROFILE%\Documents\BlockBrowserMirror'
        'PreferLocalWhenNasUnavailable=1'
        'CurrentLibraryMode=Auto'
        'UserName='
        'ThumbSize=128'
        'InsertScale=1'
        'InsertRotation=0'
        'FormWidth=1000'
        'FormHeight=650'
    )
    Set-Content -Encoding UTF8 -Path (Join-Path $pluginRoot 'BlockBrowser.default.ini') -Value $defaultIni

    $store = New-Object BlockBrowser.BlockBrowserConfigStore $pluginRoot
    $defaultConfig = [BlockBrowser.BlockBrowserConfig]::CreateDefault($pluginRoot)
    $loaded = $store.Load($defaultConfig)

    Assert-True 'load creates user config from template' (Test-Path (Join-Path $pluginRoot 'config.ini'))
    Assert-Equal 'relative library path expands under plugin root' (Join-Path $pluginRoot '我的常用块') $loaded.LibraryPath
    Assert-Equal 'missing NAS override follows library path' $loaded.LibraryPath $loaded.NasLibraryPath
    Assert-True 'auto fallback is enabled' $loaded.PreferLocalWhenNasUnavailable
    Assert-Equal 'default thumb size' 128 $loaded.ThumbSize

    $missingNasRoot = Join-Path $tempRoot 'MissingNasPlugin'
    New-Item -ItemType Directory -Force -Path $missingNasRoot | Out-Null
    Set-Content -Encoding UTF8 -Path (Join-Path $missingNasRoot 'config.ini') -Value @(
        'LibraryPath=OnlyLibrary'
    )
    $missingNasStore = New-Object BlockBrowser.BlockBrowserConfigStore $missingNasRoot
    $missingNasDefaults = [BlockBrowser.BlockBrowserConfig]::CreateDefault($missingNasRoot)
    $missingNasLoaded = $missingNasStore.Load($missingNasDefaults)
    Assert-Equal 'missing NAS path follows loaded library path' $missingNasLoaded.LibraryPath $missingNasLoaded.NasLibraryPath

    $customIni = @(
        'LibraryPath=CustomBlocks'
        'NasLibraryPath=\\NAS\CADBlocks\BlockBrowser'
        'LocalMirrorPath=%USERPROFILE%\Documents\BB-Mirror'
        'PreferLocalWhenNasUnavailable=0'
        'CurrentLibraryMode=Local'
        'UserName=WLUP'
        'ThumbSize=160'
        'InsertScale=2.5'
        'InsertRotation=90'
        'FormWidth=1200'
        'FormHeight=700'
        'RecentBlocks=C:\A.dwg|C:\B.dwg|C:\A.dwg'
    )
    Set-Content -Encoding UTF8 -Path (Join-Path $pluginRoot 'config.ini') -Value $customIni

    $loaded = $store.Load($defaultConfig)
    Assert-Equal 'custom library path is relative to plugin root' (Join-Path $pluginRoot 'CustomBlocks') $loaded.LibraryPath
    Assert-Equal 'NAS path is preserved' '\\NAS\CADBlocks\BlockBrowser' $loaded.NasLibraryPath
    Assert-Equal 'local mirror path expands environment variables' ([Environment]::ExpandEnvironmentVariables('%USERPROFILE%\Documents\BB-Mirror')) $loaded.LocalMirrorPath
    Assert-False 'fallback can be disabled' $loaded.PreferLocalWhenNasUnavailable
    Assert-Equal 'library mode parsed' ([BlockBrowser.LibraryMode]::Local) $loaded.CurrentLibraryMode
    Assert-Equal 'sync user parsed' 'WLUP' $loaded.SyncUserName
    Assert-Equal 'thumb size parsed' 160 $loaded.ThumbSize
    Assert-Equal 'insert scale parsed' 2.5 $loaded.InsertScale
    Assert-Equal 'rotation stored as radians' ([math]::PI / 2) $loaded.InsertRotation
    Assert-Equal 'form width parsed' 1200 $loaded.FormWidth
    Assert-Equal 'form height parsed' 700 $loaded.FormHeight
    Assert-Equal 'recent blocks de-duplicated' 2 $loaded.RecentBlocks.Count

    $loaded.LibraryPath = Join-Path $pluginRoot 'CustomBlocks'
    $loaded.NasLibraryPath = '\\NAS\CADBlocks\BlockBrowser'
    $loaded.LocalMirrorPath = Join-Path $pluginRoot 'Mirror'
    $loaded.PreferLocalWhenNasUnavailable = $true
    $loaded.CurrentLibraryMode = [BlockBrowser.LibraryMode]::Auto
    $loaded.SyncUserName = 'Alice'
    $loaded.ThumbSize = 192
    $loaded.InsertScale = 3
    $loaded.InsertRotation = [math]::PI
    $loaded.FormWidth = 1300
    $loaded.FormHeight = 800
    $loaded.RecentBlocks.Clear()
    $loaded.RecentBlocks.Add('C:\C.dwg')
    $store.Save($loaded)
    $savedText = Get-Content -Encoding UTF8 -Path (Join-Path $pluginRoot 'config.ini') -Raw
    Assert-True 'save writes relative library path' ($savedText -match 'LibraryPath=CustomBlocks')
    Assert-True 'save writes relative local mirror path' ($savedText -match 'LocalMirrorPath=Mirror')
    Assert-True 'save writes rotation in degrees' ($savedText -match 'InsertRotation=180')
    Assert-True 'save writes recent blocks' ($savedText -match 'RecentBlocks=C:\\C\.dwg')

    Assert-DoesNotThrow 'save ignores null config' { $store.Save($null) }

    $lockedConfigRoot = Join-Path $tempRoot 'LockedConfigPlugin'
    New-Item -ItemType Directory -Force -Path $lockedConfigRoot | Out-Null
    $lockedConfigPath = Join-Path $lockedConfigRoot 'config.ini'
    Set-Content -Encoding UTF8 -Path $lockedConfigPath -Value @(
        'LibraryPath=LockedBlocks'
    )
    $lockedConfigStream = [System.IO.File]::Open($lockedConfigPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    try {
        $lockedStore = New-Object BlockBrowser.BlockBrowserConfigStore $lockedConfigRoot
        $lockedDefaults = [BlockBrowser.BlockBrowserConfig]::CreateDefault($lockedConfigRoot)
        $lockedLoaded = $null
        Assert-DoesNotThrow 'load ignores unreadable config file' { $script:lockedLoaded = $lockedStore.Load($lockedDefaults) }
        Assert-Equal 'load returns defaults when config read fails' $lockedDefaults.LibraryPath $lockedLoaded.LibraryPath
    }
    finally {
        $lockedConfigStream.Dispose()
    }

    $lockedDefaultRoot = Join-Path $tempRoot 'LockedDefaultPlugin'
    New-Item -ItemType Directory -Force -Path $lockedDefaultRoot | Out-Null
    $lockedDefaultPath = Join-Path $lockedDefaultRoot 'BlockBrowser.default.ini'
    Set-Content -Encoding UTF8 -Path $lockedDefaultPath -Value $defaultIni
    $lockedDefaultStream = [System.IO.File]::Open($lockedDefaultPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    try {
        $lockedDefaultStore = New-Object BlockBrowser.BlockBrowserConfigStore $lockedDefaultRoot
        Assert-DoesNotThrow 'ensure user config ignores template copy failure' { $lockedDefaultStore.EnsureUserConfigExists() }
    }
    finally {
        $lockedDefaultStream.Dispose()
    }

    Assert-True 'safe library name allows normal name' ([BlockBrowser.BlockBrowserConfigStore]::IsSafeLibraryName('电气图库'))
    Assert-False 'safe library name rejects empty name' ([BlockBrowser.BlockBrowserConfigStore]::IsSafeLibraryName(' '))
    Assert-False 'safe library name rejects relative current dir' ([BlockBrowser.BlockBrowserConfigStore]::IsSafeLibraryName('.'))
    Assert-False 'safe library name rejects invalid character' ([BlockBrowser.BlockBrowserConfigStore]::IsSafeLibraryName('A:B'))

    $nas = Join-Path $tempRoot 'NAS'
    $local = Join-Path $tempRoot 'LocalMirror'
    New-Item -ItemType Directory -Force -Path $local | Out-Null
    $pathConfig = [BlockBrowser.BlockBrowserConfig]::CreateDefault($pluginRoot)
    $pathConfig.NasLibraryPath = $nas
    $pathConfig.LocalMirrorPath = $local
    $pathConfig.CurrentLibraryMode = [BlockBrowser.LibraryMode]::Auto
    $pathConfig.PreferLocalWhenNasUnavailable = $true
    $active = [BlockBrowser.LibraryPathService]::RefreshActiveLibrary($pathConfig)
    Assert-Equal 'active library falls back to local mirror' ([BlockBrowser.ActiveLibraryKind]::LocalMirror) $active.Kind
    Assert-Equal 'config library path updated to active local mirror' $local $pathConfig.LibraryPath
    Assert-Equal 'journal path uses local mirror' (Join-Path $local '.blockbrowser\local-changes.json') ([BlockBrowser.LibraryPathService]::GetLocalJournalPath($pathConfig.LocalMirrorPath))

    $blockPath = Join-Path $local 'Electrical\Socket.dwg'
    Assert-Equal 'relative path from active library' 'Electrical\Socket.dwg' ([BlockBrowser.LibraryPathService]::ToLibraryRelativePath($pathConfig.LibraryPath, $blockPath))
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

Write-Host 'ConfigPath.Tests.ps1 passed'
