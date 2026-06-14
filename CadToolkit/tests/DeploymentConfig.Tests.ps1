$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$buildAll = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\build-all.bat') -Raw
$deployLocalPath = Join-Path $repo 'CadToolkit\deploy-local.ps1'
if (-not (Test-Path $deployLocalPath)) {
    throw 'deploy-local.ps1 is missing'
}
$deployLocal = Get-Content -Encoding UTF8 $deployLocalPath -Raw
$workflow = Get-Content -Encoding UTF8 (Join-Path $repo '.github\workflows\cadtoolkit.yml') -Raw
$projectConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.ini') -Raw
$defaultConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.default.ini') -Raw
$configSource = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.Core\Config.cs') -Raw
$assemblyInfo = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.Core\Properties\AssemblyInfo.cs') -Raw
$autoload = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\autoload.lsp') -Raw
$manualFileName = 'CadToolkit' + (-join ([char[]](0x4F7F, 0x7528, 0x624B, 0x518C))) + '.html'
$manual = Get-Content -Encoding UTF8 (Join-Path (Join-Path $repo 'CadToolkit') $manualFileName) -Raw
$parseErrors = $null
[System.Management.Automation.Language.Parser]::ParseFile($deployLocalPath, [ref]$null, [ref]$parseErrors) | Out-Null
$preserveOrderNote = -join ([char[]](0x4E0D, 0x8981, 0x968F, 0x610F, 0x8C03, 0x6574, 0x914D, 0x7F6E, 0x9879, 0x548C, 0x5206, 0x7EC4, 0x987A, 0x5E8F))
$layerStandardFormatNote = (-join ([char[]](0x6807, 0x51C6, 0x56FE, 0x5C42))) + '=' + (-join ([char[]](0x989C, 0x8272))) + '|' + (-join ([char[]](0x7EBF, 0x578B))) + '|' + (-join ([char[]](0x7EBF, 0x5BBD))) + '|' + (-join ([char[]](0x662F, 0x5426, 0x6253, 0x5370)))
$textStyleStandardFormatNote = (-join ([char[]](0x6807, 0x51C6, 0x6837, 0x5F0F))) + '=' + (-join ([char[]](0x5B57, 0x4F53, 0x6587, 0x4EF6))) + '|' + (-join ([char[]](0x5927, 0x5B57, 0x4F53, 0x6587, 0x4EF6))) + '|' + (-join ([char[]](0x56FA, 0x5B9A, 0x5B57, 0x9AD8))) + '|' + (-join ([char[]](0x5BBD, 0x5EA6, 0x56E0, 0x5B50))) + '|' + (-join ([char[]](0x503E, 0x659C, 0x89D2)))

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-ContainsLiteral($name, $text, $literal) {
    if (-not $text.Contains($literal)) { throw "$name did not find literal: $literal" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-CommandsSectionHasNoEqualsComments($name, $text) {
    $match = [regex]::Match($text, '(?s)\[Commands\](.*?)\[LayerStandard\]')
    if (-not $match.Success) { throw "$name could not find Commands to LayerStandard block" }
    $lines = $match.Groups[1].Value -split "`r?`n"
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if ($trimmed.StartsWith('#') -and $trimmed.Contains('=')) {
            throw "$name found equals comment inside Commands section: $trimmed"
        }
    }
    Write-Host "PASS $name"
}

Assert-Contains 'build-all delegates to local deploy script' $buildAll 'deploy-local\.ps1'
if ($parseErrors.Count -gt 0) {
    throw "deploy-local.ps1 has parse errors: $($parseErrors[0].Message)"
}
Write-Host 'PASS local deploy parses in Windows PowerShell'
$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    $probeOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $deployLocalPath -MSBuildPath (Join-Path $repo 'missing-msbuild.exe') -DeployRoot (Join-Path $env:TEMP 'CadToolkitDeployProbe') 2>&1
    $probeExitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorActionPreference
}
$probeText = $probeOutput | Out-String
if ($probeExitCode -eq 0 -or $probeText -notmatch 'MSBuild not found') {
    throw "deploy-local.ps1 did not start cleanly before MSBuild validation: $probeText"
}
Write-Host 'PASS local deploy starts before MSBuild validation'
Assert-ContainsLiteral 'local deploy uses real AutoCAD SDK path' $deployLocal 'C:\Program Files\Autodesk\AutoCAD 2020'
Assert-ContainsLiteral 'local deploy uses real ZWCAD SDK path' $deployLocal 'C:\Program Files\ZWSOFT\ZWCAD 2020'
Assert-Contains 'local deploy builds real GstarCAD SDK path without source encoding risk' $deployLocal '0x6D69.*0x8FB0.*0x8F6F.*0x4EF6'
Assert-Contains 'local deploy publishes default config template' $deployLocal 'CadToolkit\.default\.ini'
Assert-Contains 'local deploy publishes user manual without source encoding risk' $deployLocal '0x4F7F.*0x7528.*0x624B.*0x518C'
Assert-Contains 'local deploy protects user config by hashing' $deployLocal 'Get-FileHash'
Assert-Contains 'local deploy explains locked CAD dll failures' $deployLocal 'Close running CAD'
Assert-Contains 'local deploy only creates user config when missing' $deployLocal 'Test-Path\s+(-LiteralPath\s+)?\$UserConfigPath'
Assert-NotContains 'local deploy does not reference CI stubs' $deployLocal '(?i)(\.github\\stubs|\\stubs\\|/stubs/)'
Assert-NotContains 'local deploy does not overwrite user config from repo' $deployLocal 'Copy-Item\s+.*CadToolkit\.ini'

Assert-Contains 'GitHub Action continues to use CI stubs' $workflow '\.github\\stubs'
Assert-Contains 'release package includes default config template' $workflow 'CadToolkit\.default\.ini'
Assert-Contains 'release package includes user manual' $workflow 'CadToolkit\u4F7F\u7528\u624B\u518C\.html'
Assert-NotContains 'release package does not include user config name' $workflow 'Copy-Item "\$\{\{ github\.workspace \}\}\\CadToolkit\\CadToolkit\.ini" "\$pkg\\?"'
Assert-Contains 'release package writes autoload without UTF8 BOM' $workflow 'New-Object\s+System\.Text\.UTF8Encoding\(\$false\)'
Assert-Contains 'release package writes autoload through explicit encoder' $workflow '\[System\.IO\.File\]::WriteAllText\("\$pkg\\autoload\.lsp",\s*\$autoload,\s*\$utf8NoBom\)'

Assert-NotContains 'project config omits version marker' $projectConfig '(?m)^Version='
Assert-NotContains 'default config omits version marker' $defaultConfig '(?m)^Version='
Assert-NotContains 'embedded default config omits version marker' $configSource 'AppendLine\("Version='
Assert-ContainsLiteral 'project config warns about preserving order' $projectConfig $preserveOrderNote
Assert-ContainsLiteral 'default config warns about preserving order' $defaultConfig $preserveOrderNote
Assert-Contains 'embedded default config warns about preserving order' $configSource '\\u4E0D\\u8981\\u968F\\u610F\\u8C03\\u6574\\u914D\\u7F6E\\u9879\\u548C\\u5206\\u7EC4\\u987A\\u5E8F'
Assert-ContainsLiteral 'project config documents layer standard format' $projectConfig $layerStandardFormatNote
Assert-ContainsLiteral 'default config documents layer standard format' $defaultConfig $layerStandardFormatNote
Assert-ContainsLiteral 'project config documents text style standard format' $projectConfig $textStyleStandardFormatNote
Assert-ContainsLiteral 'default config documents text style standard format' $defaultConfig $textStyleStandardFormatNote
Assert-CommandsSectionHasNoEqualsComments 'project config keeps non-command docs out of Commands section' $projectConfig
Assert-CommandsSectionHasNoEqualsComments 'default config keeps non-command docs out of Commands section' $defaultConfig
Assert-Contains 'command groups skip comment lines with equals' $configSource 'if\s+\(t\.StartsWith\("#"\)\)\s+continue;'
Assert-Contains 'assembly version is 1.25' $assemblyInfo 'AssemblyVersion\("1\.25\.0\.0"\)'
Assert-Contains 'assembly file version is 1.25' $assemblyInfo 'AssemblyFileVersion\("1\.25\.0\.0"\)'
Assert-Contains 'config fallback version is v1.25' $configSource 'return "v1\.25";'
Assert-Contains 'local deploy fallback version is v1.25' $deployLocal "return 'v1\.25'"
Assert-Contains 'autoload announces v1.25' $autoload 'CadToolkit v1\.25 ready'
Assert-Contains 'manual title uses v1.25' $manual 'CadToolkit v1\.25'
Assert-NotContains 'manual no longer references v1.24' $manual 'v1\.24'
