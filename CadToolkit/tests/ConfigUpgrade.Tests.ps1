$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$coreProject = Join-Path $repo 'CadToolkit\src\CadToolkit.Core\CadToolkit.Core.csproj'
$coreDll = Join-Path $repo 'CadToolkit\src\CadToolkit.Core\bin\Release\CadToolkit.Core.dll'
$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'

function Assert-Contains($name, $text, $pattern) {
    $text = $text -replace "`r", ""
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    $text = $text -replace "`r", ""
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-Before($name, $text, $first, $second) {
    $firstIndex = $text.IndexOf($first)
    $secondIndex = $text.IndexOf($second)
    if ($firstIndex -lt 0 -or $secondIndex -lt 0 -or $firstIndex -gt $secondIndex) {
        throw "$name expected '$first' before '$second'"
    }
    Write-Host "PASS $name"
}

& $msbuild $coreProject /p:Configuration=Release /p:Platform=x64 /t:Rebuild /v:minimal | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'CadToolkit.Core build failed' }

$assembly = [Reflection.Assembly]::LoadFrom($coreDll)
$configType = $assembly.GetType('CadToolkit.Core.Config', $true)
$init = $configType.GetMethod('Init', [Reflection.BindingFlags]'Public, Static')
$changeBasepointCommand = (-join ([char[]](0x6539, 0x5757, 0x57FA, 0x70B9))) + '=CT_CHANGEBASEPOINT'
$changeBasepointPattern = '(?m)^' + [regex]::Escape($changeBasepointCommand) + '$'
$oldTextStyleCommand = (-join ([char[]](0x6587, 0x5B57, 0x6837, 0x5F0F, 0x89C4, 0x8303))) + '=CT_TEXTSTYLESTANDARD'
$textStyleCommand = (-join ([char[]](0x6587, 0x5B57, 0x89C4, 0x8303))) + '=CT_TEXTSTYLESTANDARD'
$textStyleCommandPattern = '(?m)^' + [regex]::Escape($textStyleCommand) + '$'
$textNumberCommand = (-join ([char[]](0x6587, 0x5B57, 0x7F16, 0x53F7))) + '=CT_TEXTNUMBER'
$incCopyCommand = (-join ([char[]](0x9012, 0x589E, 0x590D, 0x5236))) + '=CT_INCCOPY'
$batchPlotCommand = (-join ([char[]](0x6279, 0x91CF, 0x6253, 0x5370))) + '=CT_BATCHPLOT'
$flattenCommand = 'Z' + (-join ([char[]](0x8F74, 0x5F52, 0x96F6))) + '=CT_FLATTEN'
$layerZeroCommand = (-join ([char[]](0x56FE, 0x5C42, 0x5F52, 0x96F6))) + '=CT_SETLAYER0'
$configMaintenanceCommand = (-join ([char[]](0x914D, 0x7F6E, 0x7EF4, 0x62A4))) + '=CT_CONFIGMAINTAIN'
$standardCenterCommand = (-join ([char[]](0x89C4, 0x8303, 0x4E2D, 0x5FC3))) + '=CT_STANDARDCENTER'
$layerStandardCommand = (-join ([char[]](0x56FE, 0x5C42, 0x89C4, 0x8303))) + '=CT_LAYERSTANDARD'
$configMaintenancePattern = '(?m)^' + [regex]::Escape($configMaintenanceCommand) + '$'
$standardCenterPattern = '(?m)^' + [regex]::Escape($standardCenterCommand) + '$'
$batchPlotComment = '# ' + (-join ([char[]](0x6279, 0x91CF, 0x6253, 0x5370)))
$commandListComment = '# ' + (-join ([char[]](0x547D, 0x4EE4, 0x5217, 0x8868)))

$tmpRoot = Join-Path ([IO.Path]::GetTempPath()) ('CadToolkitConfigUpgrade-' + [Guid]::NewGuid().ToString('N'))
$platformDir = Join-Path $tmpRoot 'acad'
$iniPath = Join-Path $tmpRoot 'CadToolkit.ini'

try {
    New-Item -ItemType Directory -Path $platformDir -Force | Out-Null
    $existingConfig = @(
        '# User config',
        'QuickBlockPrefix=USER',
        '',
        '[Commands]',
        'Custom=MY_CUSTOM_CMD',
        $textNumberCommand,
        $oldTextStyleCommand,
        $layerZeroCommand,
        $standardCenterCommand,
        $layerStandardCommand,
        $batchPlotCommand,
        $incCopyCommand,
        $flattenCommand,
        '',
        '[LayerStandard]',
        'CUSTOM-LAYER=2|CONTINUOUS|Default|true',
        '',
        '[LayerMap]',
        'CUSTOM-LAYER=CUSTOM'
    ) -join "`r`n"
    $existingConfig | Set-Content -Encoding UTF8 $iniPath

    $assemblyPath = [string](Join-Path $platformDir 'CadToolkit.dll')
    [void]$init.Invoke($null, [object[]]@($assemblyPath))

    $upgraded = Get-Content -Encoding UTF8 $iniPath -Raw

    Assert-Contains 'upgrade preserves existing scalar value' $upgraded '(?m)^QuickBlockPrefix=USER$'
    Assert-Contains 'upgrade appends missing delete original default' $upgraded '(?m)^DeleteOriginal=true$'
    Assert-Contains 'upgrade appends missing iso layer default' $upgraded '(?m)^IsoLayerKeepLayer0=false$'
    Assert-Contains 'upgrade appends missing layer fallback default' $upgraded '(?m)^LayerStandardFallbackTo0=false$'
    Assert-Contains 'upgrade appends missing layer whitelist default' $upgraded '(?m)^LayerStandardWhitelist=0,Defpoints,'
    Assert-Contains 'upgrade appends missing text style fallback default' $upgraded '(?m)^TextStyleFallbackToStandard=false$'
    Assert-Contains 'upgrade appends missing text style fallback style default' $upgraded '(?m)^TextStyleFallbackStyle=STANDARD-TEXT$'
    Assert-Contains 'upgrade appends missing text style whitelist default' $upgraded '(?m)^TextStyleWhitelist=Standard,Annotative,\*DIM\*$'
    Assert-Contains 'upgrade appends missing text style normalize height default' $upgraded '(?m)^TextStyleNormalizeHeight=false$'
    Assert-Contains 'upgrade appends missing text style normalize width default' $upgraded '(?m)^TextStyleNormalizeWidthFactor=false$'
    Assert-Contains 'upgrade appends missing text style normalize oblique default' $upgraded '(?m)^TextStyleNormalizeOblique=false$'
    Assert-Contains 'upgrade appends missing text style normalize color default' $upgraded '(?m)^TextStyleNormalizeColorByLayer=false$'
    Assert-Contains 'upgrade appends missing text style delete default' $upgraded '(?m)^TextStyleDeleteUnusedOldStyles=false$'
    Assert-Before 'upgrade keeps scalar defaults before sections' $upgraded 'DeleteOriginal=true' '[Commands]'
    Assert-Before 'upgrade writes batch plot comment before batch settings' $upgraded $batchPlotComment 'BatchPlotDevice='
    Assert-Before 'upgrade keeps batch settings before command comments' $upgraded 'BatchPlotSortReverse=' $commandListComment
    Assert-Before 'upgrade keeps command comments before commands section' $upgraded $commandListComment '[Commands]'
    Assert-Contains 'upgrade preserves custom command section' $upgraded '(?m)^Custom=MY_CUSTOM_CMD$'
    Assert-Contains 'upgrade appends missing official new command' $upgraded $changeBasepointPattern
    Assert-NotContains 'upgrade does not add config maintenance to panel commands' $upgraded $configMaintenancePattern
    Assert-NotContains 'upgrade does not add standard center to panel commands' $upgraded $standardCenterPattern
    Assert-NotContains 'upgrade removes old standard center panel command' $upgraded $standardCenterPattern
    Assert-Contains 'upgrade renames official text style command label' $upgraded $textStyleCommandPattern
    Assert-NotContains 'upgrade removes old text style command label' $upgraded ('(?m)^' + [regex]::Escape($oldTextStyleCommand) + '$')
    Assert-Contains 'upgrade preserves increment copy command' $upgraded ('(?m)^' + [regex]::Escape($incCopyCommand) + '$')
    Assert-Before 'upgrade inserts new block command before next section' $upgraded $changeBasepointCommand '[LayerStandard]'
    Assert-Before 'upgrade inserts new text style command before next section' $upgraded $textStyleCommand '[LayerStandard]'
    Assert-Before 'upgrade moves increment copy into text group after text number' $upgraded $textNumberCommand $incCopyCommand
    Assert-Before 'upgrade places increment copy before text style standard' $upgraded $incCopyCommand $textStyleCommand
    Assert-Before 'upgrade keeps batch plot after quick dim instead of before increment copy' $upgraded $incCopyCommand $batchPlotCommand
    Assert-Contains 'upgrade preserves custom layer standard' $upgraded '(?m)^CUSTOM-LAYER=2\|CONTINUOUS\|Default\|true$'
    Assert-Contains 'upgrade preserves custom layer map' $upgraded '(?m)^CUSTOM-LAYER=CUSTOM$'
    Assert-NotContains 'upgrade does not merge default text style standard section into existing user config' $upgraded '(?m)^\[TextStyleStandard\]$'
    Assert-NotContains 'upgrade does not merge default text style map section into existing user config' $upgraded '(?m)^\[TextStyleMap\]$'
    Assert-NotContains 'upgrade does not merge default command list into existing user config' $upgraded '(?m)=CT_FINDREPLACE$'
    Assert-NotContains 'upgrade still omits version marker' $upgraded '(?m)^Version='
}
finally {
    Remove-Item -LiteralPath $tmpRoot -Recurse -Force -ErrorAction SilentlyContinue
}
