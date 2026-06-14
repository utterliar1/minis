$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$coreProject = Join-Path $repo 'CadToolkit\src\CadToolkit.Core\CadToolkit.Core.csproj'
$coreDll = Join-Path $repo 'CadToolkit\src\CadToolkit.Core\bin\Release\CadToolkit.Core.dll'
$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'

function Assert-NotNull($name, $value) {
    if ($null -eq $value) { throw "$name was null" }
    Write-Host "PASS $name"
}

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-TextContains($name, $text, $literal) {
    if (-not $text.Contains($literal)) { throw "$name missing literal: $literal" }
    Write-Host "PASS $name"
}

function Assert-TextNotContains($name, $text, $literal) {
    if ($text.Contains($literal)) { throw "$name found forbidden literal: $literal" }
    Write-Host "PASS $name"
}

function Invoke-Analyze($text) {
    return $diagnosticsType.GetMethod('Analyze', [Reflection.BindingFlags]'Public, Static').Invoke($null, [object[]]@($text, 'test.ini'))
}

function Invoke-Repair($text, $path = 'test.ini') {
    return $diagnosticsType.GetMethod('Repair', [Reflection.BindingFlags]'Public, Static').Invoke($null, [object[]]@($text, $path))
}

function Get-Issues($result) {
    return @($result.Issues)
}

function Assert-Issue($name, $result, $code, $severity, $canFix) {
    $matches = @(Get-Issues $result | Where-Object { $_.Code -eq $code -and $_.Severity.ToString() -eq $severity -and $_.CanFix -eq $canFix })
    if ($matches.Count -eq 0) { throw "$name did not find issue $severity $code CanFix=$canFix" }
    Write-Host "PASS $name"
}

function Assert-NoIssue($name, $result, $code) {
    $matches = @(Get-Issues $result | Where-Object { $_.Code -eq $code })
    if ($matches.Count -ne 0) { throw "$name unexpectedly found issue $code" }
    Write-Host "PASS $name"
}

function New-MinimalConfig {
    return @'
QuickBlockPrefix=BK
DeleteOriginal=true
KeepOriginal=false
AlignHorizontal=0
AlignUseFirstBase=true
AlignLineSpacing=0
IsoLayerKeepLayer0=false
LayerStandardFallbackTo0=false
LayerStandardWhitelist=0,Defpoints
TextStyleFallbackToStandard=false
TextStyleFallbackStyle=STANDARD-TEXT
TextStyleWhitelist=Standard
TextStyleNormalizeHeight=false
TextStyleNormalizeWidthFactor=false
TextStyleNormalizeOblique=false
TextStyleNormalizeColorByLayer=false
TextStyleDeleteUnusedOldStyles=false

[Commands]
查找替换=CT_FINDREPLACE
文字对齐=CT_ALIGN
加下划线=CT_UNDERLINE
格式复制=CT_TEXTBRUSH
文字合并=CT_TEXTMERGE
文字编号=CT_TEXTNUMBER
文字规范=CT_TEXTSTYLESTANDARD
图层归零=CT_SETLAYER0
图层规范=CT_LAYERSTANDARD
孤立图层=CT_ISOLAYER
按层选择=CT_SELECTBYLAYER
按色选择=CT_SELECTBYCOLOR
重命名块=CT_RENAMEBLOCK
快捷建块=CT_QUICKBLOCK
改块基点=CT_CHANGEBASEPOINT
按块选择=CT_SELECTBYBLOCK
画中心线=CT_CENTERLINE
快速标注=CT_QUICKDIM
递增复制=CT_INCCOPY
Z轴归零=CT_FLATTEN
配置体检=CT_CONFIGCHECK

[LayerStandard]
LAYER-EQUIPMENT=4|CONTINUOUS|Default|true

[LayerMap]
LAYER-EQUIPMENT=EQUIPMENT

[TextStyleStandard]
STANDARD-TEXT=gbenor.shx|gbcbig.shx|0|1.0|0

[TextStyleMap]
STANDARD-TEXT=Standard
'@
}

function New-RepairFixture {
    $oldTextStyleLabel = -join ([char[]](0x6587,0x5B57,0x6837,0x5F0F,0x89C4,0x8303))
    $quickBlockLabel = -join ([char[]](0x5FEB,0x6377,0x5EFA,0x5757))
    $formatComment = '# ' + (-join ([char[]](0x683C,0x5F0F,0xFF1A,0x663E,0x793A,0x540D,0x79F0))) + '=CAD' + (-join ([char[]](0x547D,0x4EE4)))
    $sampleComment = '# ' + (-join ([char[]](0x793A,0x4F8B))) + '=CAD'

    return @"
QuickBlockPrefix=USER
DeleteOriginal=false
KeepOriginal=false
AlignHorizontal=0
AlignUseFirstBase=true
AlignLineSpacing=0
IsoLayerKeepLayer0=false
LayerStandardFallbackTo0=false
LayerStandardWhitelist=0,Defpoints
TextStyleFallbackToStandard=false
TextStyleFallbackStyle=STANDARD-TEXT
TextStyleWhitelist=Standard
TextStyleNormalizeHeight=false
TextStyleNormalizeWidthFactor=false
TextStyleNormalizeOblique=false
TextStyleNormalizeColorByLayer=false
TextStyleDeleteUnusedOldStyles=false

[Commands]
$formatComment
$oldTextStyleLabel=CT_TEXTSTYLESTANDARD
$quickBlockLabel=CT_QUICKBLOCK
$sampleComment

[LayerStandard]
LAYER-EQUIPMENT=4|CONTINUOUS|Default|true

[LayerMap]
LAYER-EQUIPMENT=EQUIPMENT
BROKEN-TARGET=UNKNOWN-LAYER

[TextStyleStandard]
STANDARD-TEXT=gbenor.shx|gbcbig.shx|0|1.0|0

[TextStyleMap]
STANDARD-TEXT=Standard
EXTRA-TEXT=MissingTarget
"@
}

& $msbuild $coreProject /p:Configuration=Release /p:Platform=x64 /t:Rebuild /v:minimal | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'CadToolkit.Core build failed' }

$assembly = [Reflection.Assembly]::LoadFrom($coreDll)
$diagnosticsType = $assembly.GetType('CadToolkit.Core.ConfigDiagnostics', $true)
$severityType = $assembly.GetType('CadToolkit.Core.ConfigDiagnosticSeverity', $true)
$issueType = $assembly.GetType('CadToolkit.Core.ConfigDiagnosticIssue', $true)
$resultType = $assembly.GetType('CadToolkit.Core.ConfigDiagnosticResult', $true)

Assert-NotNull 'config diagnostics type exists' $diagnosticsType
Assert-NotNull 'config diagnostic severity type exists' $severityType
Assert-NotNull 'config diagnostic issue type exists' $issueType
Assert-NotNull 'config diagnostic result type exists' $resultType
Assert-NotNull 'config diagnostics analyze method exists' ($diagnosticsType.GetMethod('Analyze', [Reflection.BindingFlags]'Public, Static'))
Assert-NotNull 'config diagnostics repair method exists' ($diagnosticsType.GetMethod('Repair', [Reflection.BindingFlags]'Public, Static'))
Assert-NotNull 'config diagnostics analyze file method exists' ($diagnosticsType.GetMethod('AnalyzeFile', [Reflection.BindingFlags]'Public, Static'))
Assert-NotNull 'config diagnostics repair file method exists' ($diagnosticsType.GetMethod('RepairFile', [Reflection.BindingFlags]'Public, Static'))

$coreProjectText = Get-Content -Encoding UTF8 $coreProject -Raw
Assert-Contains 'core project compiles config diagnostics' $coreProjectText 'Compile Include="ConfigDiagnostics\.cs"'

$minimalConfig = New-MinimalConfig

$missingRoot = Invoke-Analyze ($minimalConfig -replace 'QuickBlockPrefix=BK\r?\n', '')
Assert-Issue 'missing root setting is reported' $missingRoot 'MissingRootSetting' 'Warning' $true

$missingCommands = Invoke-Analyze ($minimalConfig -replace '\[Commands\]', '[NotCommands]')
Assert-Issue 'missing commands section is reported with dedicated code' $missingCommands 'MissingCommandsSection' 'Warning' $true

$commandDocComment = Invoke-Analyze ($minimalConfig -replace '\[Commands\]', "[Commands]`n# sample=comment")
Assert-Issue 'commands doc comment with equals is reported' $commandDocComment 'CommandDocCommentWithEquals' 'Warning' $true

$badLayerMap = Invoke-Analyze ($minimalConfig -replace '\[LayerMap\]', "[LayerMap]`nMissingLayer=old")
Assert-Issue 'layer map target missing is an error' $badLayerMap 'LayerMapTargetMissing' 'Error' $false

$badTextStyleMap = Invoke-Analyze ($minimalConfig -replace 'STANDARD-TEXT=Standard', 'MISSING-TEXT=Standard')
Assert-Issue 'text style map target missing is an error' $badTextStyleMap 'TextStyleMapTargetMissing' 'Error' $false

$badLayerStandard = Invoke-Analyze ($minimalConfig -replace '4\|CONTINUOUS\|Default\|true', 'red|CONTINUOUS|Default|yes')
Assert-Issue 'malformed layer standard is an error' $badLayerStandard 'MalformedLayerStandard' 'Error' $false

$badTextStyleStandard = Invoke-Analyze ($minimalConfig -replace 'STANDARD-TEXT=gbenor\.shx\|gbcbig\.shx\|0\|1\.0\|0', 'STANDARD-TEXT=gbenor.shx|gbcbig.shx|tall|1.0|0')
Assert-Issue 'malformed text style standard is an error' $badTextStyleStandard 'MalformedTextStyleStandard' 'Error' $false

$oldTextStyleLabel = -join ([char[]](0x6587,0x5B57,0x6837,0x5F0F,0x89C4,0x8303))
$newTextStyleLabel = -join ([char[]](0x6587,0x5B57,0x89C4,0x8303))
$configCheckLabel = -join ([char[]](0x914D,0x7F6E,0x4F53,0x68C0))
$formatComment = '# ' + (-join ([char[]](0x683C,0x5F0F,0xFF1A,0x663E,0x793A,0x540D,0x79F0))) + '=CAD' + (-join ([char[]](0x547D,0x4EE4)))

$repairFixture = New-RepairFixture
$repairResult = Invoke-Repair $repairFixture
$repairedText = [string]$repairResult.RepairedText
if (-not $repairResult.HasChanges) { throw 'repair should report HasChanges=true when text changes' }
Write-Host 'PASS repair reports changes'
Assert-TextContains 'repair preserves existing root value' $repairedText 'QuickBlockPrefix=USER'
Assert-TextContains 'repair preserves unfixable layer map target' $repairedText 'BROKEN-TARGET=UNKNOWN-LAYER'
Assert-TextContains 'repair renames old text style command' $repairedText ($newTextStyleLabel + '=CT_TEXTSTYLESTANDARD')
Assert-TextNotContains 'repair removes old text style command label' $repairedText ($oldTextStyleLabel + '=CT_TEXTSTYLESTANDARD')
Assert-TextContains 'repair adds config check command' $repairedText ($configCheckLabel + '=CT_CONFIGCHECK')
Assert-TextNotContains 'repair removes known equals command doc comment' $repairedText $formatComment

$aliasCollisionFixture = $repairFixture -replace '\[Commands\]', "[Commands]`r`nCustomConfigCheck=CT_CONFIGCHECK"
$aliasCollisionRepair = Invoke-Repair $aliasCollisionFixture
$aliasCollisionText = [string]$aliasCollisionRepair.RepairedText
Assert-TextContains 'repair adds official command even when command value is used by custom alias' $aliasCollisionText ($configCheckLabel + '=CT_CONFIGCHECK')
Assert-TextContains 'repair preserves custom command alias with same command value' $aliasCollisionText 'CustomConfigCheck=CT_CONFIGCHECK'

$missingRootFixture = @"
QuickBlockPrefix=USER

[Commands]
$newTextStyleLabel=CT_TEXTSTYLESTANDARD
"@
$missingRootRepair = Invoke-Repair $missingRootFixture
$missingRootText = [string]$missingRootRepair.RepairedText
$firstSectionIndex = $missingRootText.IndexOf('[Commands]')
$deleteOriginalIndex = $missingRootText.IndexOf('DeleteOriginal=true')
if ($deleteOriginalIndex -lt 0 -or $deleteOriginalIndex -gt $firstSectionIndex) { throw 'repair should add missing root setting before first section' }
Write-Host 'PASS repair adds missing root setting before first section'

$textStyleMapCount = ([regex]::Matches($repairedText, '\[TextStyleMap\]')).Count
if ($textStyleMapCount -ne 1) { throw "repair should not duplicate TextStyleMap; found $textStyleMapCount" }
Write-Host 'PASS repair does not duplicate TextStyleMap'
Assert-TextNotContains 'repair does not add default text style standard section rows' $repairedText 'STANDARD-TEXT=txt.shx'
Assert-TextNotContains 'repair does not add default text style map rows' $repairedText 'STANDARD-TEXT=StandardText'

$repairDir = Join-Path ([IO.Path]::GetTempPath()) ('CadToolkitRepairTests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $repairDir | Out-Null
$repairPath = Join-Path $repairDir 'CadToolkit.ini'
[IO.File]::WriteAllText($repairPath, $repairFixture, [Text.Encoding]::UTF8)
$repairFileResult = $diagnosticsType.GetMethod('RepairFile', [Reflection.BindingFlags]'Public, Static').Invoke($null, [object[]]@([string]$repairPath))
$backupPath = [string]$repairFileResult.BackupPath
if ([string]::IsNullOrEmpty($backupPath)) { throw 'RepairFile should preserve BackupPath in returned result' }
if (-not (Test-Path $backupPath)) { throw "RepairFile backup was not created: $backupPath" }
if (-not ($backupPath.StartsWith($repairPath + '.bak-'))) { throw "RepairFile backup path was not timestamped next to config: $backupPath" }
$writtenText = [IO.File]::ReadAllText($repairPath, [Text.Encoding]::UTF8)
Assert-TextContains 'RepairFile writes repaired text' $writtenText ($configCheckLabel + '=CT_CONFIGCHECK')
Assert-TextContains 'RepairFile backup preserves original text' ([IO.File]::ReadAllText($backupPath, [Text.Encoding]::UTF8)) ($oldTextStyleLabel + '=CT_TEXTSTYLESTANDARD')
Remove-Item -Recurse -Force $repairDir

$projectConfig = Join-Path $repo 'CadToolkit\CadToolkit.default.ini'
if (Test-Path $projectConfig) {
    $current = $diagnosticsType.GetMethod('AnalyzeFile', [Reflection.BindingFlags]'Public, Static').Invoke($null, [object[]]@([string]$projectConfig))
    Assert-NoIssue 'current project config has commands section' $current 'MissingCommandsSection'
    Assert-NoIssue 'current project config has no command comment equals' $current 'CommandDocCommentWithEquals'
    Assert-NoIssue 'current project config has no malformed layer standard' $current 'MalformedLayerStandard'
}
