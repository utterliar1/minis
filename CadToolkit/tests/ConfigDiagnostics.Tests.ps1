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

function Invoke-Analyze($text, $path = 'test.ini') {
    return $diagnosticsType.GetMethod('Analyze', [Reflection.BindingFlags]'Public, Static').Invoke($null, [object[]]@($text, $path))
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
Assert-NotNull 'config diagnostics format report method exists' ($diagnosticsType.GetMethod('FormatReport', [Reflection.BindingFlags]'Public, Static'))

$coreProjectText = Get-Content -Encoding UTF8 $coreProject -Raw
Assert-Contains 'core project compiles config diagnostics' $coreProjectText 'Compile Include="ConfigDiagnostics\.cs"'

$minimalConfig = New-MinimalConfig

$missingRoot = Invoke-Analyze ($minimalConfig -replace 'QuickBlockPrefix=BK\r?\n', '')
Assert-Issue 'missing root setting is reported' $missingRoot 'MissingRootSetting' 'Warning' $true

$reportPath = 'C:\CadToolkit\CadToolkit.ini'
$reportResult = Invoke-Analyze ($minimalConfig -replace 'QuickBlockPrefix=BK\r?\n', '') $reportPath
$report = [string]$diagnosticsType.GetMethod('FormatReport', [Reflection.BindingFlags]'Public, Static').Invoke($null, [object[]]@($reportResult))
$reportTitle = 'CadToolkit ' + (-join ([char[]](0x914D,0x7F6E,0x4F53,0x68C0)))
$reportWarning = -join ([char[]](0x8B66,0x544A))
$reportFixable = -join ([char[]](0x53EF,0x81EA,0x52A8,0x4FEE,0x590D))
$reportConclusion = -join ([char[]](0x7ED3,0x8BBA,0xFF1A,0x53D1,0x73B0))
$reportTip = -join ([char[]](0x63D0,0x793A,0xFF1A,0x6807,0x8BB0,0x4E3A))
$missingRootChinese = -join ([char[]](0x7F3A,0x5C11,0x6839,0x914D,0x7F6E,0x9879))
Assert-TextContains 'report title is Chinese' $report $reportTitle
Assert-TextContains 'report includes config path' $report $reportPath
Assert-TextContains 'report includes Chinese conclusion' $report $reportConclusion
Assert-TextContains 'report includes Chinese hint' $report $reportTip
Assert-TextContains 'report includes warning group' $report $reportWarning
Assert-TextContains 'report marks fixable issue' $report $reportFixable
Assert-TextContains 'report localizes issue message' $report $missingRootChinese
Assert-TextNotContains 'report does not show raw English issue message' $report 'Missing root setting'

$missingCommands = Invoke-Analyze ($minimalConfig -replace '\[Commands\]', '[NotCommands]')
Assert-Issue 'missing commands section is reported with dedicated code' $missingCommands 'MissingCommandsSection' 'Warning' $true

$commandFormatComment = '# ' + (-join ([char[]](0x683C,0x5F0F,0xFF1A,0x663E,0x793A,0x540D,0x79F0))) + '=CAD' + (-join ([char[]](0x547D,0x4EE4)))
$commandDocComment = Invoke-Analyze ($minimalConfig -replace '\[Commands\]', "[Commands]`n$commandFormatComment")
Assert-Issue 'known commands doc comment with equals is auto-fixable' $commandDocComment 'CommandDocCommentWithEquals' 'Warning' $true

$genericCommandComment = Invoke-Analyze ($minimalConfig -replace '\[Commands\]', "[Commands]`n# note=value")
Assert-Issue 'generic commands doc comment with equals is not auto-fixable' $genericCommandComment 'CommandDocCommentWithEquals' 'Warning' $false

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
Assert-TextNotContains 'repair does not add config check command to panel list' $repairedText ($configCheckLabel + '=CT_CONFIGCHECK')
Assert-TextNotContains 'repair removes known equals command doc comment' $repairedText $formatComment

$aliasCollisionFixture = $repairFixture -replace '\[Commands\]', "[Commands]`r`nCustomConfigCheck=CT_CONFIGCHECK"
$aliasCollisionRepair = Invoke-Repair $aliasCollisionFixture
$aliasCollisionText = [string]$aliasCollisionRepair.RepairedText
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
Assert-TextContains 'RepairFile writes repaired text' $writtenText ($newTextStyleLabel + '=CT_TEXTSTYLESTANDARD')
Assert-TextNotContains 'RepairFile keeps config check out of command list' $writtenText ($configCheckLabel + '=CT_CONFIGCHECK')
Assert-TextContains 'RepairFile backup preserves original text' ([IO.File]::ReadAllText($backupPath, [Text.Encoding]::UTF8)) ($oldTextStyleLabel + '=CT_TEXTSTYLESTANDARD')

[IO.File]::WriteAllText($repairPath, $repairFixture, [Text.Encoding]::UTF8)
$secondRepairFileResult = $diagnosticsType.GetMethod('RepairFile', [Reflection.BindingFlags]'Public, Static').Invoke($null, [object[]]@([string]$repairPath))
$secondBackupPath = [string]$secondRepairFileResult.BackupPath
if ([string]::IsNullOrEmpty($secondBackupPath)) { throw 'second RepairFile should preserve BackupPath in returned result' }
if ($secondBackupPath -eq $backupPath) { throw 'RepairFile should create a unique backup path on repeated repair' }
if (-not (Test-Path $secondBackupPath)) { throw "second RepairFile backup was not created: $secondBackupPath" }
Write-Host 'PASS RepairFile creates unique backup path on repeated repair'

Remove-Item -Recurse -Force $repairDir

$projectConfig = Join-Path $repo 'CadToolkit\CadToolkit.default.ini'
if (Test-Path $projectConfig) {
    $current = $diagnosticsType.GetMethod('AnalyzeFile', [Reflection.BindingFlags]'Public, Static').Invoke($null, [object[]]@([string]$projectConfig))
    Assert-NoIssue 'current project config has commands section' $current 'MissingCommandsSection'
    Assert-NoIssue 'current project config has no command comment equals' $current 'CommandDocCommentWithEquals'
    Assert-NoIssue 'current project config has no malformed layer standard' $current 'MalformedLayerStandard'
}

$configCheckCommandLine = (-join ([char[]](0x914D,0x7F6E,0x4F53,0x68C0))) + '=CT_CONFIGCHECK'
$projectConfigText = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.ini') -Raw
$defaultConfigText = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.default.ini') -Raw
$configSource = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.Core\Config.cs') -Raw
Assert-TextNotContains 'project config keeps config check out of command list' $projectConfigText $configCheckCommandLine
Assert-TextNotContains 'default config keeps config check out of command list' $defaultConfigText $configCheckCommandLine
Assert-Contains 'embedded default keeps config check out of command list' $configSource 'RemoveOfficialCommand\(lines,\s*"\\u914D\\u7F6E\\u4F53\\u68C0"'
Assert-Contains 'startup upgrade removes old config check command button' $configSource 'RemoveOfficialCommand\(lines,\s*"\\u914D\\u7F6E\\u4F53\\u68C0"'

$configCommandsPath = Join-Path $repo 'CadToolkit\src\CadToolkit\ConfigCommands.cs'
if (-not (Test-Path $configCommandsPath)) { throw 'ConfigCommands.cs is missing' }
$configCommandsSource = Get-Content -Encoding UTF8 $configCommandsPath -Raw
$copyReportText = -join ([char[]](0x590D,0x5236,0x62A5,0x544A))
$repairText = -join ([char[]](0x81EA,0x52A8,0x4FEE,0x590D))
Assert-Contains 'config check command is registered' $configCommandsSource '\[CommandMethod\("CT_CONFIGCHECK"\)\]'
Assert-Contains 'config check command analyzes file' $configCommandsSource 'ConfigDiagnostics\.AnalyzeFile'
Assert-Contains 'config check command can repair file' $configCommandsSource 'ConfigDiagnostics\.RepairFile'
Assert-Contains 'config check command formats report' $configCommandsSource 'ConfigDiagnostics\.FormatReport'
Assert-Contains 'panel action runs config check command' (Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit\Plugin.cs') -Raw) 'action\.Kind == "CONFIGCHECK"'
Assert-TextContains 'config check dialog has copy button' $configCommandsSource $copyReportText
Assert-TextContains 'config check dialog has repair button' $configCommandsSource $repairText
Assert-Contains 'config check dialog supports Esc close' $configCommandsSource 'CancelButton\s*=\s*close'
Assert-Contains 'config check close button is cancel action' $configCommandsSource 'close\.DialogResult\s*=\s*DialogResult\.Cancel'
Assert-Contains 'config exposes config path' $configSource 'public\s+static\s+string\s+ConfigPath'

foreach ($projectName in @('CadToolkit.AutoCAD.csproj', 'CadToolkit.ZWCAD.csproj', 'CadToolkit.GstarCAD.csproj')) {
    $projectText = Get-Content -Encoding UTF8 (Join-Path $repo "CadToolkit\src\CadToolkit\$projectName") -Raw
    Assert-Contains "$projectName compiles config commands" $projectText 'Compile Include="ConfigCommands\.cs"'
}

$toolPath = Join-Path $repo 'CadToolkit\tools\check-config.ps1'
if (-not (Test-Path $toolPath)) { throw 'check-config.ps1 is missing' }
$toolText = Get-Content -Encoding UTF8 $toolPath -Raw
$toolParseErrors = $null
[System.Management.Automation.Language.Parser]::ParseFile($toolPath, [ref]$null, [ref]$toolParseErrors) | Out-Null
if ($toolParseErrors.Count -gt 0) { throw "check-config.ps1 parse error: $($toolParseErrors[0].Message)" }
Write-Host 'PASS check-config script parses'
Assert-Contains 'check-config supports Path parameter' $toolText 'param\s*\([^)]*\$Path'
Assert-Contains 'check-config supports Fix switch' $toolText '\[switch\]\s*\$Fix'
Assert-Contains 'check-config calls AnalyzeFile' $toolText 'AnalyzeFile'
Assert-Contains 'check-config calls RepairFile' $toolText 'RepairFile'
Assert-Contains 'check-config prints formatted report' $toolText 'FormatReport'

$panelBuilderPath = Join-Path $repo 'CadToolkit\src\CadToolkit.UI\PanelBuilder.cs'
$panelBuilderSource = Get-Content -Encoding UTF8 $panelBuilderPath -Raw
$configIcon = [string][char]0x2699
Assert-Contains 'panel builder has config check action' $panelBuilderSource 'CONFIGCHECK'
Assert-TextContains 'panel builder shows config check icon button' $panelBuilderSource $configIcon
Assert-Contains 'panel builder anchors config check with right controls' $panelBuilderSource 'btnConfigCheck\.Anchor\s*=\s*AnchorStyles\.Top\s*\|\s*AnchorStyles\.Right'
Assert-Contains 'panel builder places config check left of add button' $panelBuilderSource 'btnConfigCheck\.Location\s*=\s*new Point\(btnAdd\.Left\s*-\s*UiScale\(6\)\s*-\s*btnConfigCheck\.Width'
Assert-Contains 'panel builder handles config check action' $panelBuilderSource 'Kind = "CONFIGCHECK"'

$readme = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\README.md') -Raw
$manualFileName = 'CadToolkit' + (-join ([char[]](0x4F7F,0x7528,0x624B,0x518C))) + '.html'
$manual = Get-Content -Encoding UTF8 (Join-Path (Join-Path $repo 'CadToolkit') $manualFileName) -Raw
$configCheckLabel = -join ([char[]](0x914D,0x7F6E,0x4F53,0x68C0))
Assert-TextContains 'readme documents config check command' $readme $configCheckLabel
Assert-TextContains 'readme documents CT_CONFIGCHECK' $readme 'CT_CONFIGCHECK'
Assert-TextContains 'readme documents check-config script' $readme 'check-config.ps1'
Assert-TextContains 'manual documents config check command' $manual $configCheckLabel
Assert-TextContains 'manual documents CT_CONFIGCHECK' $manual 'CT_CONFIGCHECK'
Assert-TextContains 'manual documents check-config script' $manual 'check-config.ps1'
