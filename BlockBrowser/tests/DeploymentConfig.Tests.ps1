$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repo 'BlockBrowser'
$buildAll = Get-Content -Encoding UTF8 (Join-Path $project 'build-all.bat') -Raw
$buildAllBytes = [System.IO.File]::ReadAllBytes((Join-Path $project 'build-all.bat'))
$gstarProject = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.csproj') -Raw
$acadProject = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.AutoCAD.csproj') -Raw
$zwcadProject = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.ZWCAD.csproj') -Raw
$plugin = @(
    Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowserPlugin.cs') -Raw
    Get-Content -Encoding UTF8 (Join-Path $project 'Library\BlockLibrary.Configuration.cs') -Raw
) -join "`n"
$defaultConfig = Get-Content -Encoding UTF8 (Join-Path $project 'BlockBrowser.default.ini') -Raw
$defaultLibraryFolder = -join ([char[]](0x6211, 0x7684, 0x5E38, 0x7528, 0x5757))
$manualFileName = -join ([char[]](0x4F7F, 0x7528, 0x624B, 0x518C)) + '.html'
$orderWarning = -join ([char[]](0x4E0D, 0x8981, 0x968F, 0x610F, 0x6539, 0x53D8, 0x914D, 0x7F6E, 0x9879, 0x987A, 0x5E8F))
$readOnlyFlag = -join ([char[]](0x0030, 0x003D, 0x53EA, 0x8BFB))

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NoBareLf($name, [byte[]]$bytes) {
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 10 -and ($i -eq 0 -or $bytes[$i - 1] -ne 13)) {
            throw "$name found bare LF at byte $i"
        }
    }
    Write-Host "PASS $name"
}

if (-not (Test-Path (Join-Path $project 'BlockBrowser.default.ini'))) {
    throw 'default config template is missing: BlockBrowser.default.ini'
}

if (Test-Path (Join-Path $project 'BlockBrowser.AutoCAD8.csproj')) {
    throw 'AutoCAD 2026 project should be removed: BlockBrowser.AutoCAD8.csproj'
}

Assert-Contains 'local deploy publishes default config template' $buildAll 'BlockBrowser\.default\.ini'
Assert-Contains 'local deploy publishes user manual' $buildAll ([regex]::Escape($manualFileName))
Assert-NoBareLf 'local deploy batch uses CRLF line endings' $buildAllBytes
Assert-Contains 'local deploy target is C BlockBrowser' $buildAll 'set "OUTPUT=C:\\BlockBrowser"'
Assert-Contains 'local deploy uses installed ZWCAD 2020' $buildAll 'set "ZWCAD_DIR=C:\\Program Files\\ZWSOFT\\ZWCAD 2020"'
Assert-Contains 'local deploy uses powershell for unicode GstarCAD path' $buildAll 'powershell -NoProfile -ExecutionPolicy Bypass -Command'
Assert-Contains 'local deploy discovers GstarCAD SDK in powershell' $buildAll 'Get-ChildItem \$env:ProgramFiles -Recurse -Filter gmdb\.dll'
Assert-NotContains 'local deploy does not pass cmd-expanded GstarCAD path' $buildAll '/p:GstarCADDir=%GCAD_DIR%'
Assert-Contains 'local deploy tracks build failures' $buildAll 'set "BUILD_FAILED=1"'
Assert-Contains 'local deploy exits nonzero when any build fails' $buildAll 'exit /b 1'
Assert-Contains 'local deploy pauses only when requested' $buildAll 'if "%BLOCKBROWSER_PAUSE%"=="1" pause'
Assert-Contains 'local deploy only creates user config when missing' $buildAll 'if not exist "%OUTPUT%\\config\.ini"'
Assert-NotContains 'local deploy does not overwrite user config' $buildAll 'copy /Y "%BASE%config\.ini" "%OUTPUT%\\?"'
Assert-Contains 'local deploy hashes existing user config before publish' $buildAll 'Get-FileHash.*%OUTPUT%\\config\.ini'
Assert-Contains 'local deploy uses delayed expansion for config hash' $buildAll 'setlocal EnableDelayedExpansion'
Assert-Contains 'local deploy aborts if user config changes during publish' $buildAll 'config\.ini changed during deployment'
Assert-Contains 'plugin can create user config from default template' $plugin 'BlockBrowser\.default\.ini'
Assert-Contains 'default local mirror uses plugin library folder' $defaultConfig ('LocalMirrorPath=' + [regex]::Escape($defaultLibraryFolder))
Assert-Contains 'default library mode is local' $defaultConfig 'CurrentLibraryMode=Local'
Assert-Contains 'default config warns to keep key order' $defaultConfig ([regex]::Escape($orderWarning))
Assert-Contains 'default config explains NAS permission' $defaultConfig ('AllowNasSync[\s\S]*?' + [regex]::Escape($readOnlyFlag))
Assert-Contains 'default config includes AllowNasSync default' $defaultConfig 'AllowNasSync=0'
Assert-Contains 'default config includes thumbnail size default' $defaultConfig 'ThumbSize=128'
Assert-Contains 'default config includes insert scale default' $defaultConfig 'InsertScale=1'
Assert-Contains 'default config includes window size defaults' $defaultConfig 'FormWidth=1000[\s\S]*FormHeight=650'
Assert-Contains 'local deploy builds AutoCAD 2020' $buildAll 'Building for AutoCAD 2020'
Assert-Contains 'local deploy uses AutoCAD 2020 project' $buildAll 'BlockBrowser\.AutoCAD\.csproj'
Assert-Contains 'local deploy uses installed AutoCAD 2020 SDK' $buildAll 'set "ACAD_DIR=C:\\Program Files\\Autodesk\\AutoCAD 2020"'
Assert-NotContains 'AutoCAD 2026 build is not used' $buildAll 'AutoCAD 2026'
Assert-NotContains 'MSBuild output paths do not end with escaped quote backslash' $buildAll '/p:OutputPath="%OUTPUT%\\[^"]+\\"'
Assert-Contains 'GstarCAD project uses isolated release intermediate output' $gstarProject '<IntermediateOutputPath>obj\\x64\\Release-GstarCAD\\</IntermediateOutputPath>'
Assert-Contains 'AutoCAD project uses isolated release intermediate output' $acadProject '<IntermediateOutputPath>obj\\x64\\Release-AutoCAD\\</IntermediateOutputPath>'
Assert-Contains 'ZWCAD project uses isolated release intermediate output' $zwcadProject '<IntermediateOutputPath>obj\\x64\\Release-ZWCAD\\</IntermediateOutputPath>'
