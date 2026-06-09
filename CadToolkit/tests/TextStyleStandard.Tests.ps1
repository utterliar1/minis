$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$src = Join-Path $repo 'CadToolkit\src'
$stubSrc = Join-Path $repo '.github\stubs'
$stubOut = Join-Path $repo 'stubs\acad'
$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-ContainsLiteral($name, $text, $literal) {
    if (-not $text.Contains($literal)) { throw "$name did not find literal: $literal" }
    Write-Host "PASS $name"
}

function Assert-NotNull($name, $value) {
    if ($null -eq $value) { throw "$name expected a value but got null" }
    Write-Host "PASS $name"
}

New-Item -ItemType Directory -Path $stubOut -Force | Out-Null
dotnet build (Join-Path $stubSrc 'AutoCAD.csproj') -c Release --nologo -v quiet -o $stubOut | Out-Host
Copy-Item (Join-Path $stubOut 'acdbmgd.dll') (Join-Path $stubOut 'acmgd.dll') -Force
Copy-Item (Join-Path $stubOut 'acdbmgd.dll') (Join-Path $stubOut 'accoremgd.dll') -Force

& $msbuild (Join-Path $src 'CadToolkit\CadToolkit.AutoCAD.csproj') /p:Configuration=Release /p:Platform=x64 "/p:AutoCADDir=$stubOut" /t:Rebuild /v:minimal | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'CadToolkit build failed' }

foreach ($dll in @(
    (Join-Path $stubOut 'acdbmgd.dll'),
    (Join-Path $stubOut 'acmgd.dll'),
    (Join-Path $stubOut 'accoremgd.dll'),
    (Join-Path $src 'CadToolkit.Core\bin\Release\CadToolkit.Core.dll'),
    (Join-Path $src 'CadToolkit.UI\bin\Release\CadToolkit.UI.dll')
)) {
    [void][Reflection.Assembly]::LoadFrom($dll)
}

$plugin = [Reflection.Assembly]::LoadFrom((Join-Path $src 'CadToolkit\bin\Release\CadToolkit.dll'))
$core = [Reflection.Assembly]::LoadFrom((Join-Path $src 'CadToolkit.Core\bin\Release\CadToolkit.Core.dll'))
$commandsType = $plugin.GetType('CadToolkit.CadCommands', $true)
$configType = $core.GetType('CadToolkit.Core.Config', $true)

$projectConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.ini') -Raw
$defaultConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.default.ini') -Raw
$configSource = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit.Core\Config.cs') -Raw
$readme = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\README.md') -Raw
$manualFileName = 'CadToolkit' + (-join ([char[]](0x4F7F, 0x7528, 0x624B, 0x518C))) + '.html'
$manual = Get-Content -Encoding UTF8 (Join-Path (Join-Path $repo 'CadToolkit') $manualFileName) -Raw
$textStyleCommandLabel = (-join ([char[]](0x6587, 0x5B57, 0x6837, 0x5F0F, 0x89C4, 0x8303)))
$textStyleCommandLine = $textStyleCommandLabel + '=CT_TEXTSTYLESTANDARD'

$standardRuleType = $configType.GetNestedType('TextStyleStandardRule', [Reflection.BindingFlags]'Public')
Assert-NotNull 'text style standard rule type exists' $standardRuleType
$mapRuleType = $configType.GetNestedType('TextStyleMapRule', [Reflection.BindingFlags]'Public')
Assert-NotNull 'text style map rule type exists' $mapRuleType

Assert-NotNull 'text style standards parser exists' ($configType.GetMethod('GetTextStyleStandards', [Reflection.BindingFlags]'Public, Static'))
Assert-NotNull 'text style map parser exists' ($configType.GetMethod('GetTextStyleMapRules', [Reflection.BindingFlags]'Public, Static'))

$rootKeys = @(
    'TextStyleFallbackToStandard=false',
    'TextStyleFallbackStyle=STANDARD-TEXT',
    'TextStyleWhitelist=Standard,Annotative,*DIM*',
    'TextStyleNormalizeHeight=false',
    'TextStyleNormalizeWidthFactor=false',
    'TextStyleNormalizeOblique=false',
    'TextStyleNormalizeColorByLayer=false',
    'TextStyleDeleteUnusedOldStyles=false'
)

foreach ($key in $rootKeys) {
    Assert-ContainsLiteral "project config contains $key" $projectConfig $key
    Assert-ContainsLiteral "default config contains $key" $defaultConfig $key
    Assert-ContainsLiteral "embedded default contains $key" $configSource $key
}

Assert-ContainsLiteral 'project config contains text style command' $projectConfig $textStyleCommandLine
Assert-ContainsLiteral 'default config contains text style command' $defaultConfig $textStyleCommandLine
Assert-Contains 'embedded default contains text style command' $configSource '文字样式规范=CT_TEXTSTYLESTANDARD|\\u6587\\u5B57\\u6837\\u5F0F\\u89C4\\u8303=CT_TEXTSTYLESTANDARD'

Assert-ContainsLiteral 'project config has text style standard section' $projectConfig '[TextStyleStandard]'
Assert-ContainsLiteral 'project config has text style map section' $projectConfig '[TextStyleMap]'
Assert-ContainsLiteral 'default config has text style standard section' $defaultConfig '[TextStyleStandard]'
Assert-ContainsLiteral 'default config has text style map section' $defaultConfig '[TextStyleMap]'
Assert-ContainsLiteral 'project config has standard text style' $projectConfig 'STANDARD-TEXT='
Assert-ContainsLiteral 'project config has title text style' $projectConfig 'TITLE-TEXT='

Assert-Contains 'text style standard command is registered' (Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit\TextStyleCommands.cs') -Raw) '\[CommandMethod\("CT_TEXTSTYLESTANDARD"\)\]'
Assert-NotNull 'text style standard method exists' ($commandsType.GetMethod('TextStyleStandard', [Reflection.BindingFlags]'Public, Instance'))

Assert-ContainsLiteral 'readme documents text style command label' $readme $textStyleCommandLabel
Assert-ContainsLiteral 'readme documents text style command name' $readme 'CT_TEXTSTYLESTANDARD'
Assert-ContainsLiteral 'manual documents text style command label' $manual $textStyleCommandLabel
Assert-ContainsLiteral 'manual documents text style command name' $manual 'CT_TEXTSTYLESTANDARD'
