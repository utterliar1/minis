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

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) { throw "$name expected [$expected] but got [$actual]" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name unexpectedly found pattern: $pattern" }
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
$oldTextStyleCommandLabel = (-join ([char[]](0x6587, 0x5B57, 0x6837, 0x5F0F, 0x89C4, 0x8303)))
$textStyleCommandLabel = (-join ([char[]](0x6587, 0x5B57, 0x89C4, 0x8303)))
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
Assert-Contains 'embedded default contains text style command' $configSource '文字规范=CT_TEXTSTYLESTANDARD|\\u6587\\u5B57\\u89C4\\u8303=CT_TEXTSTYLESTANDARD'
Assert-NotContains 'project config removes old text style command label' $projectConfig ([regex]::Escape($oldTextStyleCommandLabel + '=CT_TEXTSTYLESTANDARD'))
Assert-NotContains 'default config removes old text style command label' $defaultConfig ([regex]::Escape($oldTextStyleCommandLabel + '=CT_TEXTSTYLESTANDARD'))
Assert-NotContains 'embedded default removes old text style command label' $configSource '文字样式规范=CT_TEXTSTYLESTANDARD|\\u6587\\u5B57\\u6837\\u5F0F\\u89C4\\u8303=CT_TEXTSTYLESTANDARD'

Assert-ContainsLiteral 'project config has text style standard section' $projectConfig '[TextStyleStandard]'
Assert-ContainsLiteral 'project config has text style map section' $projectConfig '[TextStyleMap]'
Assert-ContainsLiteral 'default config has text style standard section' $defaultConfig '[TextStyleStandard]'
Assert-ContainsLiteral 'default config has text style map section' $defaultConfig '[TextStyleMap]'
Assert-ContainsLiteral 'project config has standard text style' $projectConfig 'STANDARD-TEXT='
Assert-ContainsLiteral 'project config has title text style' $projectConfig 'TITLE-TEXT='
$standardTextStyleLine = 'STANDARD-TEXT=gbenor.shx|gbcbig.shx|0|1.0|0'
$embeddedStandardTextStyleLine = 'STANDARD-TEXT=gbenor.shx|gbcbig.shx|0|1.0|0'
Assert-ContainsLiteral 'project config uses gb shx standard text style' $projectConfig $standardTextStyleLine
Assert-ContainsLiteral 'default config uses gb shx standard text style' $defaultConfig $standardTextStyleLine
Assert-ContainsLiteral 'embedded default uses gb shx standard text style' $configSource $embeddedStandardTextStyleLine
Assert-ContainsLiteral 'readme documents gb shx standard text style' $readme $standardTextStyleLine
Assert-ContainsLiteral 'manual documents gb shx standard text style' $manual $standardTextStyleLine

Assert-Contains 'text style standard command is registered' (Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit\TextStyleCommands.cs') -Raw) '\[CommandMethod\("CT_TEXTSTYLESTANDARD"\)\]'
Assert-NotNull 'text style standard method exists' ($commandsType.GetMethod('TextStyleStandard', [Reflection.BindingFlags]'Public, Instance'))

Assert-ContainsLiteral 'readme documents text style command label' $readme $textStyleCommandLabel
Assert-NotContains 'readme removes old text style command label' $readme ([regex]::Escape($oldTextStyleCommandLabel))
Assert-ContainsLiteral 'readme documents text style command name' $readme 'CT_TEXTSTYLESTANDARD'
Assert-ContainsLiteral 'manual documents text style command label' $manual $textStyleCommandLabel
Assert-NotContains 'manual removes old text style command label' $manual ([regex]::Escape($oldTextStyleCommandLabel))
Assert-ContainsLiteral 'manual documents text style command name' $manual 'CT_TEXTSTYLESTANDARD'

$textStyleCommandsSource = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit\TextStyleCommands.cs') -Raw

$matchDetail = $commandsType.GetMethod('MatchTextStyleMapDetail', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'text style map detail helper exists' $matchDetail
$whitelistDetail = $commandsType.GetMethod('MatchTextStyleWhitelistPattern', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'text style whitelist detail helper exists' $whitelistDetail

$mapRulesType = [Collections.Generic.List``1].MakeGenericType($mapRuleType)
$mapRules = [Activator]::CreateInstance($mapRulesType)

function New-TextStyleMapRule($target, $aliases) {
    $rule = [Activator]::CreateInstance($mapRuleType)
    $mapRuleType.GetField('TargetStyle').SetValue($rule, $target)
    $aliasList = $mapRuleType.GetField('Aliases').GetValue($rule)
    foreach ($alias in $aliases) { [void]$aliasList.Add($alias) }
    return $rule
}

[void]$mapRules.Add((New-TextStyleMapRule 'STANDARD-TEXT' @('Standard', 'txt', '*宋体*', 'HZTXT')))
[void]$mapRules.Add((New-TextStyleMapRule 'TITLE-TEXT' @('*标题*', 'TITLE')))

function Match-TextStyleTarget($styleName) {
    $detail = $matchDetail.Invoke($null, @($styleName, $mapRules))
    if ($null -eq $detail) { return '' }
    $rule = $detail.GetType().GetField('Rule').GetValue($detail)
    return [string]$mapRuleType.GetField('TargetStyle').GetValue($rule)
}

Assert-Equal 'text style standard name matches target' 'STANDARD-TEXT' (Match-TextStyleTarget 'STANDARD-TEXT')
Assert-Equal 'text style plain alias exact match' 'STANDARD-TEXT' (Match-TextStyleTarget 'txt')
Assert-Equal 'text style plain alias does not contain-match' '' (Match-TextStyleTarget 'my-txt-style')
Assert-Equal 'text style wildcard alias contains-match' 'STANDARD-TEXT' (Match-TextStyleTarget '仿宋体_GB2312')

$exactMode = '全字匹配'
$wildcardMode = '通配匹配'
$wildcardDetail = $matchDetail.Invoke($null, @('仿宋体_GB2312', $mapRules))
Assert-NotNull 'text style wildcard detail returns match' $wildcardDetail
Assert-Equal 'text style wildcard detail reports alias' '*宋体*' ([string]$wildcardDetail.GetType().GetField('Pattern').GetValue($wildcardDetail))
Assert-Equal 'text style wildcard detail reports match mode' $wildcardMode ([string]$wildcardDetail.GetType().GetField('MatchMode').GetValue($wildcardDetail))

$exactDetail = $matchDetail.Invoke($null, @('txt', $mapRules))
Assert-NotNull 'text style exact detail returns match' $exactDetail
Assert-Equal 'text style exact detail reports alias' 'txt' ([string]$exactDetail.GetType().GetField('Pattern').GetValue($exactDetail))
Assert-Equal 'text style exact detail reports match mode' $exactMode ([string]$exactDetail.GetType().GetField('MatchMode').GetValue($exactDetail))

$whiteExact = $whitelistDetail.Invoke($null, @('Standard', 'Standard,Annotative,*DIM*'))
Assert-NotNull 'text style whitelist exact returns match' $whiteExact
Assert-Equal 'text style whitelist exact reports pattern' 'Standard' ([string]$whiteExact.GetType().GetField('Pattern').GetValue($whiteExact))
Assert-Equal 'text style whitelist exact reports match mode' $exactMode ([string]$whiteExact.GetType().GetField('MatchMode').GetValue($whiteExact))
$whiteMiss = $whitelistDetail.Invoke($null, @('Standard-OLD', 'Standard,Annotative,*DIM*'))
Assert-Equal 'text style whitelist exact does not contain-match' $true ($null -eq $whiteMiss)
$whiteWildcard = $whitelistDetail.Invoke($null, @('A-DIM-TEXT', 'Standard,Annotative,*DIM*'))
Assert-NotNull 'text style whitelist wildcard returns match' $whiteWildcard
Assert-Equal 'text style whitelist wildcard reports match mode' $wildcardMode ([string]$whiteWildcard.GetType().GetField('MatchMode').GetValue($whiteWildcard))

$planType = $commandsType.GetNestedType('TextStyleStandardPlan', [Reflection.BindingFlags]'NonPublic')
Assert-NotNull 'text style standard plan type exists' $planType
$filterType = $commandsType.GetNestedType('TextStylePlanTreeFilter', [Reflection.BindingFlags]'NonPublic')
Assert-NotNull 'text style tree preview filter enum exists' $filterType
$buildTree = $commandsType.GetMethod('BuildTextStylePlanTreeNodes', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'text style tree preview helper exists' $buildTree
$buildFilteredTree = $commandsType.GetMethod('BuildFilteredTextStylePlanTreeNodes', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'text style filtered tree preview helper exists' $buildFilteredTree
$buildSearchTree = $commandsType.GetMethod('BuildSearchedTextStylePlanTreeNodes', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'text style searched tree preview helper exists' $buildSearchTree
$formatTreeReport = $commandsType.GetMethod('FormatTextStylePlanTreeReport', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'text style tree report formatter exists' $formatTreeReport

function New-TextStylePlan($source, $target, $count, $reason) {
    $plan = [Activator]::CreateInstance($planType)
    $planType.GetField('SourceStyle').SetValue($plan, $source)
    $planType.GetField('TargetStyle').SetValue($plan, $target)
    $planType.GetField('Count').SetValue($plan, $count)
    $planType.GetField('Reason').SetValue($plan, $reason)
    return $plan
}

$standardRulesType = ([Collections.Generic.List``1].MakeGenericType($standardRuleType))
$standardRules = [Activator]::CreateInstance($standardRulesType)
function New-TextStyleStandardRule($name, $fontFile = '', $bigFontFile = '', $fixedHeight = 0.0, $widthFactor = 1.0, $obliqueAngle = 0.0) {
    $rule = [Activator]::CreateInstance($standardRuleType)
    $standardRuleType.GetField('Name').SetValue($rule, $name)
    $standardRuleType.GetField('FontFile').SetValue($rule, $fontFile)
    $standardRuleType.GetField('BigFontFile').SetValue($rule, $bigFontFile)
    $standardRuleType.GetField('FixedHeight').SetValue($rule, [double]$fixedHeight)
    $standardRuleType.GetField('WidthFactor').SetValue($rule, [double]$widthFactor)
    $standardRuleType.GetField('ObliqueAngle').SetValue($rule, [double]$obliqueAngle)
    return $rule
}
[void]$standardRules.Add((New-TextStyleStandardRule 'STANDARD-TEXT' 'gbenor.shx' 'gbcbig.shx' 0 1.0 0))
[void]$standardRules.Add((New-TextStyleStandardRule 'TITLE-TEXT' 'gbcbig.shx' '' 350 0.8 0.1))

$planListType = ([Collections.Generic.List``1].MakeGenericType($planType))
$plansForPreview = [Activator]::CreateInstance($planListType)
$fallbackForPreview = [Activator]::CreateInstance($planListType)
$whitelistForPreview = [Activator]::CreateInstance($planListType)
[void]$plansForPreview.Add((New-TextStylePlan 'txt' 'STANDARD-TEXT' 4 '命中别名 "txt"（全字匹配）'))
[void]$plansForPreview.Add((New-TextStylePlan 'OLD-TITLE-A' 'TITLE-TEXT' 7 'title reason A'))
[void]$plansForPreview.Add((New-TextStylePlan 'OLD-TITLE-B' 'TITLE-TEXT' 11 'title reason B'))
[void]$fallbackForPreview.Add((New-TextStylePlan 'UNKNOWN-STYLE' 'STANDARD-TEXT' 2 '未识别且未命中白名单'))
[void]$fallbackForPreview.Add((New-TextStylePlan 'UNKNOWN-BIG' 'STANDARD-TEXT' 9 'fallback big reason'))
[void]$whitelistForPreview.Add((New-TextStylePlan 'Standard' '' 1 'white exact reason'))
[void]$whitelistForPreview.Add((New-TextStylePlan 'A-DIM-TEXT' '' 13 'white wildcard reason'))

function Node-Text($node) { return [string]$node.Text }

$treeWithoutFallback = $buildTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $standardRules, $false, 'STANDARD-TEXT'))
Assert-Equal 'text style tree preview top node count' 4 $treeWithoutFallback.Length
Assert-Contains 'text style tree summary node text' (Node-Text $treeWithoutFallback[0]) '^摘要'
Assert-Equal 'text style tree summary does not repeat detail children' 0 ($treeWithoutFallback[0].Nodes.Count)
Assert-Contains 'text style tree unknown node preserves styles' (Node-Text $treeWithoutFallback[1]) '保持原样'
Assert-Contains 'text style tree merge node exists' (Node-Text $treeWithoutFallback[2]) '^将归并文字样式'
Assert-Contains 'text style tree whitelist node exists' (Node-Text $treeWithoutFallback[3]) '^白名单文字样式'

$treeWithFallback = $buildTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $standardRules, $true, 'STANDARD-TEXT'))
Assert-Contains 'text style tree unknown node moves to fallback style' (Node-Text $treeWithFallback[1]) '将归到 STANDARD-TEXT'
Assert-Contains 'text style tree unknown child moves to fallback style' (Node-Text ($treeWithFallback[1].Nodes[0])) '-> STANDARD-TEXT'

$mergeNode = $treeWithoutFallback[2]
Assert-Contains 'text style first merge group sorted by object count' (Node-Text ($mergeNode.Nodes[0])) '^TITLE-TEXT'
Assert-Contains 'text style second merge group sorted by object count' (Node-Text ($mergeNode.Nodes[1])) '^STANDARD-TEXT'
Assert-Contains 'text style merge group shows target font detail' (Node-Text ($mergeNode.Nodes[1])) 'gbenor\.shx'
Assert-Contains 'text style merge group shows target big font detail' (Node-Text ($mergeNode.Nodes[1])) 'gbcbig\.shx'
Assert-Contains 'text style merge group shows target fixed height detail' (Node-Text ($mergeNode.Nodes[1])) '字高 0'
Assert-Contains 'text style merge group shows target width detail' (Node-Text ($mergeNode.Nodes[1])) '宽度 1(\.0)?'
Assert-Contains 'text style merge group shows target oblique detail' (Node-Text ($mergeNode.Nodes[1])) '倾斜 0'
Assert-Contains 'text style title group shows non-default target detail' (Node-Text ($mergeNode.Nodes[0])) '字高 350'
Assert-Contains 'text style fallback node shows target font detail' (Node-Text $treeWithFallback[1]) 'gbenor\.shx'
Assert-Contains 'text style first source sorted by object count' (Node-Text ($mergeNode.Nodes[0].Nodes[0])) '^OLD-TITLE-B'
Assert-Contains 'text style whitelist child includes reason' (Node-Text ($treeWithoutFallback[3].Nodes[0])) 'white wildcard reason'

$filterAll = [Enum]::Parse($filterType, 'All')
$filterUnknown = [Enum]::Parse($filterType, 'Unknown')
$filterMigration = [Enum]::Parse($filterType, 'Migration')
$filterWhitelistOnly = [Enum]::Parse($filterType, 'Whitelist')

$filteredUnknown = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $standardRules, $false, 'STANDARD-TEXT', $filterUnknown))
Assert-Equal 'text style filtered tree unknown node count' 2 $filteredUnknown.Length
Assert-Contains 'text style filtered tree unknown keeps summary first' (Node-Text $filteredUnknown[0]) '^摘要'
Assert-Contains 'text style filtered tree unknown shows only unknown section' (Node-Text $filteredUnknown[1]) '^未识别文字样式'

$filteredMigration = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $standardRules, $false, 'STANDARD-TEXT', $filterMigration))
Assert-Equal 'text style filtered tree migration node count' 2 $filteredMigration.Length
Assert-Contains 'text style filtered tree migration shows only merge section' (Node-Text $filteredMigration[1]) '^将归并文字样式'

$filteredWhitelistOnly = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $standardRules, $false, 'STANDARD-TEXT', $filterWhitelistOnly))
Assert-Equal 'text style filtered tree whitelist node count' 2 $filteredWhitelistOnly.Length
Assert-Contains 'text style filtered tree whitelist shows only whitelist section' (Node-Text $filteredWhitelistOnly[1]) '^白名单文字样式'

$searchedUnknown = $buildSearchTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $standardRules, $false, 'STANDARD-TEXT', $filterAll, 'UNKNOWN-BIG'))
Assert-Equal 'text style searched tree keeps summary and unknown section' 2 $searchedUnknown.Length
Assert-Equal 'text style searched tree unknown keeps one matching child' 1 ($searchedUnknown[1].Nodes.Count)
Assert-Contains 'text style searched tree unknown child matches keyword' (Node-Text ($searchedUnknown[1].Nodes[0])) 'UNKNOWN-BIG'

$searchedMigration = $buildSearchTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $standardRules, $false, 'STANDARD-TEXT', $filterAll, 'OLD-TITLE-B'))
Assert-Equal 'text style searched tree keeps summary and merge section' 2 $searchedMigration.Length
Assert-Equal 'text style searched tree merge keeps one target group' 1 ($searchedMigration[1].Nodes.Count)
Assert-Contains 'text style searched tree merge group is target style' (Node-Text ($searchedMigration[1].Nodes[0])) '^TITLE-TEXT'
Assert-Equal 'text style searched tree merge group keeps one child' 1 ($searchedMigration[1].Nodes[0].Nodes.Count)
Assert-Contains 'text style searched tree merge child matches keyword' (Node-Text ($searchedMigration[1].Nodes[0].Nodes[0])) 'OLD-TITLE-B'

$searchedWhitelist = $buildSearchTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $standardRules, $false, 'STANDARD-TEXT', $filterAll, 'A-DIM-TEXT'))
Assert-Equal 'text style searched tree keeps summary and whitelist section' 2 $searchedWhitelist.Length
Assert-Equal 'text style searched tree whitelist keeps one child' 1 ($searchedWhitelist[1].Nodes.Count)
Assert-Contains 'text style searched tree whitelist child matches keyword' (Node-Text ($searchedWhitelist[1].Nodes[0])) 'A-DIM-TEXT'

$searchedNoMatch = $buildSearchTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $standardRules, $false, 'STANDARD-TEXT', $filterAll, 'NO-SUCH-STYLE'))
Assert-Equal 'text style searched tree without matches shows explicit empty state' 2 $searchedNoMatch.Length
Assert-Contains 'text style searched tree no-match node explains empty result' (Node-Text $searchedNoMatch[1]) '无匹配结果'

$searchedMigrationReportArgs = New-Object 'object[]' 1
$searchedMigrationReportArgs[0] = [System.Windows.Forms.TreeNode[]]$searchedMigration
$searchedMigrationReport = [string]$formatTreeReport.Invoke($null, $searchedMigrationReportArgs)
Assert-Contains 'text style tree report includes visible merge child' $searchedMigrationReport 'OLD-TITLE-B'
Assert-NotContains 'text style tree report excludes hidden merge child' $searchedMigrationReport 'OLD-TITLE-A'
Assert-NotContains 'text style tree report excludes hidden unknown child' $searchedMigrationReport 'UNKNOWN-BIG'

$standardPreviewUiPath = Join-Path $src 'CadToolkit\StandardPreviewUi.cs'
if (-not (Test-Path $standardPreviewUiPath)) { throw 'standard preview helper file expected' }
$standardPreviewUi = Get-Content -Encoding UTF8 $standardPreviewUiPath -Raw
Assert-Contains 'text style standard preview helper defines filter controls' $standardPreviewUi 'class\s+StandardPreviewFilterControls'
Assert-Contains 'text style standard preview helper creates filter controls' $standardPreviewUi 'CreateStandardPreviewFilterControls'
Assert-Contains 'text style standard preview helper filters tree nodes' $standardPreviewUi 'FilterStandardPreviewNodes'
Assert-Contains 'text style standard preview helper formats tree report' $standardPreviewUi 'FormatStandardPreviewTreeReport'
Assert-Contains 'text style standard preview helper updates tree preview' $standardPreviewUi 'UpdateStandardPreviewTree'
Assert-Contains 'text style command source has tree preview builder' $textStyleCommandsSource 'BuildTextStylePlanTreePreview'
Assert-ContainsLiteral 'text style dialog title is short' $textStyleCommandsSource 'CreateStandardPreviewForm("文字规范")'
Assert-Contains 'standard preview helper matches layer dialog size' $standardPreviewUi 'ClientSize\s*=\s*new Size\(UiScale\(620\),\s*UiScale\(540\)\)'
Assert-Contains 'text style preview uses shared form helper' $textStyleCommandsSource 'CreateStandardPreviewForm'
Assert-Contains 'text style preview uses shared filter controls' $textStyleCommandsSource 'CreateStandardPreviewFilterControls'
Assert-Contains 'text style preview uses shared tree helper' $textStyleCommandsSource 'CreateStandardPreviewTree'
Assert-Contains 'text style standard preview helper has keyword filter box' $standardPreviewUi 'new\s+TextBox\s*\('
Assert-Contains 'text style standard preview helper has visible search label' $standardPreviewUi 'SearchLabel'
Assert-Contains 'text style standard preview helper has compact search box' $standardPreviewUi 'Search\.Width\s*=\s*UiScale\(180\)'
Assert-Contains 'text style preview disambiguates drawing font type' $textStyleCommandsSource 'new\s+System\.Drawing\.Font\s*\('
Assert-NotContains 'text style preview avoids ambiguous Font type' $textStyleCommandsSource 'new\s+Font\s*\('
Assert-ContainsLiteral 'text style preview all filter label' $textStyleCommandsSource '全部'
Assert-ContainsLiteral 'text style preview unknown filter label' $textStyleCommandsSource '未识别'
Assert-ContainsLiteral 'text style preview migration filter label' $textStyleCommandsSource '将归并'
Assert-ContainsLiteral 'text style preview whitelist filter label' $textStyleCommandsSource '白名单'
Assert-ContainsLiteral 'text style preview current-space scope checkbox' $textStyleCommandsSource '处理当前空间文字'
Assert-ContainsLiteral 'text style preview attribute scope checkbox' $textStyleCommandsSource '处理块参照属性'
Assert-ContainsLiteral 'text style preview block definition scope checkbox' $textStyleCommandsSource '处理块定义内部文字'
Assert-ContainsLiteral 'text style preview scope section label' $textStyleCommandsSource '处理范围'
Assert-ContainsLiteral 'text style preview fallback checkbox' $textStyleCommandsSource '未识别文字样式归到标准样式'
Assert-ContainsLiteral 'text style preview cleanup section label' $textStyleCommandsSource '归并清理'
Assert-ContainsLiteral 'text style preview normalize height checkbox' $textStyleCommandsSource '固定字高'
Assert-ContainsLiteral 'text style preview normalize width checkbox' $textStyleCommandsSource '宽度因子'
Assert-ContainsLiteral 'text style preview normalize oblique checkbox' $textStyleCommandsSource '倾斜角'
Assert-ContainsLiteral 'text style preview normalize color checkbox' $textStyleCommandsSource '颜色 ByLayer'
Assert-ContainsLiteral 'text style preview delete unused checkbox' $textStyleCommandsSource '删除未使用旧文字样式'
Assert-ContainsLiteral 'text style preview appearance section label' $textStyleCommandsSource '外观同步'
Assert-NotContains 'text style preview no longer uses old merge label' $textStyleCommandsSource '归并规则'
Assert-ContainsLiteral 'text style preview copy button label' $textStyleCommandsSource '复制当前'
Assert-ContainsLiteral 'text style preview execute button label' $textStyleCommandsSource '执行'
Assert-ContainsLiteral 'text style preview cancel button label' $textStyleCommandsSource '取消'
Assert-Contains 'text style preview copies current tree' $textStyleCommandsSource 'Clipboard\.SetText'
Assert-Contains 'text style preview rebuilds tree on keyword change' $textStyleCommandsSource 'TextChanged\s*\+='
Assert-Contains 'text style standard preview helper expands focused preview tree' $standardPreviewUi 'tree\.ExpandAll\s*\('
Assert-Contains 'text style searched tree delegates to shared helper' $textStyleCommandsSource 'BuildSearchedStandardPreviewTreeNodes'
Assert-Contains 'text style tree report delegates to shared helper' $textStyleCommandsSource 'FormatStandardPreviewTreeReport'
Assert-Contains 'text style current-space scope defaults checked' $textStyleCommandsSource 'chkCurrentSpace\.Checked\s*=\s*true'
Assert-Contains 'text style attribute scope defaults unchecked' $textStyleCommandsSource 'chkAttributes\.Checked\s*=\s*false'
Assert-Contains 'text style block definition scope defaults unchecked' $textStyleCommandsSource 'chkBlockDefinitions\.Checked\s*=\s*false'
Assert-Contains 'text style fallback checkbox uses config default' $textStyleCommandsSource 'chkFallback\.Checked\s*=\s*fallbackToStandard'
Assert-Contains 'text style normalize height uses config default' $textStyleCommandsSource 'chkHeight\.Checked\s*=\s*Config\.TextStyleNormalizeHeight'
Assert-Contains 'text style normalize width uses config default' $textStyleCommandsSource 'chkWidthFactor\.Checked\s*=\s*Config\.TextStyleNormalizeWidthFactor'
Assert-Contains 'text style normalize oblique uses config default' $textStyleCommandsSource 'chkOblique\.Checked\s*=\s*Config\.TextStyleNormalizeOblique'
Assert-Contains 'text style normalize color uses config default' $textStyleCommandsSource 'chkColorByLayer\.Checked\s*=\s*Config\.TextStyleNormalizeColorByLayer'
Assert-Contains 'text style delete unused uses config default' $textStyleCommandsSource 'chkDeleteUnused\.Checked\s*=\s*Config\.TextStyleDeleteUnusedOldStyles'
Assert-Contains 'text style command builds risky option confirmation' $textStyleCommandsSource 'BuildTextStyleRiskWarning'
Assert-Contains 'text style command confirms risky options before execution' $textStyleCommandsSource 'ConfirmTextStyleRiskOptions'
Assert-ContainsLiteral 'text style risk warning mentions fallback' $textStyleCommandsSource '未识别文字样式将被归到标准样式'
Assert-ContainsLiteral 'text style risk warning mentions block definitions' $textStyleCommandsSource '将修改块定义内部文字'
Assert-ContainsLiteral 'text style risk warning mentions appearance sync' $textStyleCommandsSource '将同步文字外观参数'
Assert-ContainsLiteral 'text style risk warning mentions deleting old styles' $textStyleCommandsSource '将删除未使用旧文字样式'
Assert-Contains 'text style risky confirmation can cancel execution' $textStyleCommandsSource 'MessageBoxButtons\.OKCancel'
Assert-Contains 'text style command builds preview plans from drawing' $textStyleCommandsSource 'BuildTextStyleStandardPlans'
Assert-Contains 'text style command scans current space' $textStyleCommandsSource 'CountCurrentSpaceTextStyles'
Assert-Contains 'text style command scans block reference attributes' $textStyleCommandsSource 'CountBlockReferenceAttributesTextStyles'
Assert-Contains 'text style command scans block definition text' $textStyleCommandsSource 'CountBlockDefinitionTextStyles'
Assert-Contains 'text style command applies changes inside undo' $textStyleCommandsSource 'RunWithUndo\("CT_TEXTSTYLESTANDARD"'
Assert-Contains 'text style command ensures standard records' $textStyleCommandsSource 'EnsureTextStyleRecord'
Assert-Contains 'text style command applies standard records' $textStyleCommandsSource 'ApplyTextStyleRule'
Assert-Contains 'text style command sets text style id' $textStyleCommandsSource 'TextStyleId\s*=\s*targetId'
Assert-Contains 'text style command normalizes DBText height' $textStyleCommandsSource 'dt\.Height\s*=\s*rule\.FixedHeight'
Assert-Contains 'text style command normalizes DBText width factor' $textStyleCommandsSource 'dt\.WidthFactor\s*=\s*rule\.WidthFactor'
Assert-Contains 'text style command normalizes MText height' $textStyleCommandsSource 'mt\.TextHeight\s*=\s*rule\.FixedHeight'
Assert-Contains 'text style command normalizes color by layer' $textStyleCommandsSource 'text\.ColorIndex\s*=\s*256'
Assert-Contains 'text style command deletes unused old styles' $textStyleCommandsSource 'DeleteUnusedOldTextStyles'
Assert-Contains 'text style command protects standard style during cleanup' $textStyleCommandsSource 'IsStandardTextStyle'
Assert-Contains 'text style command protects whitelisted style during cleanup' $textStyleCommandsSource 'IsTextStyleWhitelisted'
Assert-Contains 'text style plan skips standard style names independent of map' $textStyleCommandsSource 'IsStandardTextStyle\(pair\.Key,\s*standards\)'
Assert-Contains 'text style command skips external block definitions' $textStyleCommandsSource 'IsSkippedTextStyleBlockRecord'
Assert-Contains 'text style command checks layout block definitions' $textStyleCommandsSource 'IsLayout'
Assert-Contains 'text style command checks anonymous block definitions' $textStyleCommandsSource 'IsAnonymous'
Assert-Contains 'text style command uses text style table records' $textStyleCommandsSource 'TextStyleTableRecord'
Assert-Contains 'text style stubs include text style table record' (Get-Content -Encoding UTF8 (Join-Path $repo '.github\stubs\AutoCAD.cs') -Raw) 'class\s+TextStyleTableRecord'
