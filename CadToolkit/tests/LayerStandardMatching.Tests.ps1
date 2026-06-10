$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$src = Join-Path $repo 'CadToolkit\src'
$stubSrc = Join-Path $repo '.github\stubs'
$stubOut = Join-Path $repo 'stubs\acad'
$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        throw "$name expected '$expected' but got '$actual'"
    }
    Write-Host "PASS $name"
}

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name did not find pattern: $pattern"
    }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) {
        throw "$name found forbidden pattern: $pattern"
    }
    Write-Host "PASS $name"
}

function Assert-NotNull($name, $value) {
    if ($null -eq $value) {
        throw "$name expected a value but got null"
    }
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
$ruleType = $core.GetType('CadToolkit.Core.Config+LayerStandardRule', $true)

$whitelist = $commandsType.GetMethod('IsLayerWhitelisted', [Reflection.BindingFlags]'NonPublic, Static')
Assert-Equal 'whitelist 0 exact' $true ([bool]$whitelist.Invoke($null, @('0', '0,Defpoints,*FRAME*')))
Assert-Equal 'whitelist does not match 0-4' $false ([bool]$whitelist.Invoke($null, @('0-4', '0,Defpoints,*FRAME*')))
Assert-Equal 'whitelist does not match 0-equipment' $false ([bool]$whitelist.Invoke($null, @('0-EQUIPMENT', '0,Defpoints,*FRAME*')))
Assert-Equal 'whitelist keeps wildcard contains' $true ([bool]$whitelist.Invoke($null, @('MAIN-FRAME', '0,Defpoints,*FRAME*')))

$listType = [Collections.Generic.List``1].MakeGenericType($ruleType)
$rules = [Activator]::CreateInstance($listType)

function New-LayerRule($name, [string[]]$aliases) {
    $rule = [Activator]::CreateInstance($ruleType)
    $ruleType.GetField('Name').SetValue($rule, $name)
    $aliasList = $ruleType.GetField('Aliases').GetValue($rule)
    foreach ($alias in $aliases) { [void]$aliasList.Add($alias) }
    return $rule
}

$equipmentText = -join ([char[]](0x8BBE, 0x5907))
$equipmentAlias = '*' + $equipmentText + '*'
$equipmentLayer = (-join ([char[]](0x4E00, 0x5C42))) + '-' + $equipmentText + '-' + (-join ([char[]](0x65E7)))
$wildcardMode = -join ([char[]](0x901A, 0x914D, 0x5339, 0x914D))
$exactMode = -join ([char[]](0x5168, 0x5B57, 0x5339, 0x914D))

[void]$rules.Add((New-LayerRule '0-EQUIPMENT' @('EQUIP', $equipmentAlias, '0-4')))
[void]$rules.Add((New-LayerRule '1-CENTER' @('CENTER', '0-1')))

$match = $commandsType.GetMethod('MatchLayerRule', [Reflection.BindingFlags]'NonPublic, Static')

function Match-Name($layer) {
    $rule = $match.Invoke($null, @($layer, $rules))
    if ($null -eq $rule) { return '' }
    return [string]$ruleType.GetField('Name').GetValue($rule)
}

Assert-Equal 'alias exact standard layer' '0-EQUIPMENT' (Match-Name '0-EQUIPMENT')
Assert-Equal 'plain text alias exact match' '0-EQUIPMENT' (Match-Name 'EQUIP')
Assert-Equal 'plain text alias does not match contains' '' (Match-Name 'EQUIP-SPARE')
Assert-Equal 'wildcard alias matches contains' '0-EQUIPMENT' (Match-Name $equipmentLayer)
Assert-Equal 'numeric alias exact token' '0-EQUIPMENT' (Match-Name '0-4')
Assert-Equal 'numeric alias does not match 0-40' '' (Match-Name '0-40')
Assert-Equal 'numeric alias does not match A0-4' '' (Match-Name 'A0-4')
Assert-Equal 'numeric alias does not match separated token without wildcard' '' (Match-Name 'A-0-4-B')

$matchDetail = $commandsType.GetMethod('MatchLayerRuleDetail', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'match detail helper exists' $matchDetail
$detail = $matchDetail.Invoke($null, @($equipmentLayer, $rules))
Assert-NotNull 'wildcard detail returns match' $detail
Assert-Equal 'wildcard detail reports alias' $equipmentAlias ([string]$detail.GetType().GetField('Pattern').GetValue($detail))
Assert-Equal 'wildcard detail reports match mode' $wildcardMode ([string]$detail.GetType().GetField('MatchMode').GetValue($detail))

$exactDetail = $matchDetail.Invoke($null, @('0-4', $rules))
Assert-NotNull 'exact detail returns match' $exactDetail
Assert-Equal 'exact detail reports alias' '0-4' ([string]$exactDetail.GetType().GetField('Pattern').GetValue($exactDetail))
Assert-Equal 'exact detail reports match mode' $exactMode ([string]$exactDetail.GetType().GetField('MatchMode').GetValue($exactDetail))

$whitelistDetail = $commandsType.GetMethod('MatchWhitelistPattern', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'whitelist detail helper exists' $whitelistDetail
$white = $whitelistDetail.Invoke($null, @('MAIN-FRAME', '0,Defpoints,*FRAME*'))
Assert-NotNull 'whitelist detail returns match' $white
Assert-Equal 'whitelist detail reports pattern' '*FRAME*' ([string]$white.GetType().GetField('Pattern').GetValue($white))
Assert-Equal 'whitelist detail reports match mode' $wildcardMode ([string]$white.GetType().GetField('MatchMode').GetValue($white))

$planType = $commandsType.GetNestedType('LayerStandardPlan', [Reflection.BindingFlags]'NonPublic')
Assert-NotNull 'layer standard plan type exists' $planType

function New-Plan($source, $target, $count, $reason) {
    $plan = [Activator]::CreateInstance($planType)
    $planType.GetField('SourceLayer').SetValue($plan, $source)
    $planType.GetField('TargetLayer').SetValue($plan, $target)
    $planType.GetField('Count').SetValue($plan, $count)
    $planType.GetField('Reason').SetValue($plan, $reason)
    return $plan
}

$planListType = [Collections.Generic.List``1].MakeGenericType($planType)
$plansForPreview = [Activator]::CreateInstance($planListType)
$fallbackForPreview = [Activator]::CreateInstance($planListType)
$whitelistForPreview = [Activator]::CreateInstance($planListType)
[void]$plansForPreview.Add((New-Plan $equipmentLayer '0-EQUIPMENT' 3 ('hit ' + $equipmentAlias + ' ' + $wildcardMode)))
[void]$fallbackForPreview.Add((New-Plan 'UNKNOWN-LAYER' '0' 2 'fallback reason'))
[void]$whitelistForPreview.Add((New-Plan 'MAIN-FRAME' '' 1 'white reason'))

$format = $commandsType.GetMethod('FormatLayerPlan', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'format layer plan helper exists' $format
$previewWithFallback = [string]$format.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $true))
$previewWithoutFallback = [string]$format.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false))
Assert-Contains 'preview includes match reason' $previewWithFallback ([regex]::Escape($equipmentAlias))
Assert-Contains 'preview includes whitelist reason' $previewWithFallback 'white reason'
Assert-Contains 'preview with fallback explains layer 0 move' $previewWithFallback 'UNKNOWN-LAYER\s+->\s+0'
Assert-Contains 'preview without fallback explains preserve' $previewWithoutFallback 'UNKNOWN-LAYER.*fallback reason'

[void]$plansForPreview.Add((New-Plan 'TEXT-OLD-A' '3-TEXT' 7 'text reason A'))
[void]$plansForPreview.Add((New-Plan 'TEXT-OLD-B' '3-TEXT' 11 'text reason B'))
[void]$plansForPreview.Add((New-Plan 'EQUIP-LOW' '0-EQUIPMENT' 1 'equipment low reason'))
[void]$fallbackForPreview.Add((New-Plan 'UNKNOWN-BIG' '0' 9 'fallback big reason'))
[void]$whitelistForPreview.Add((New-Plan 'FRAME-BIG' '' 13 'white big reason'))

$buildTree = $commandsType.GetMethod('BuildLayerPlanTreeNodes', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'tree preview helper exists' $buildTree
$buildFilteredTree = $commandsType.GetMethod('BuildFilteredLayerPlanTreeNodes', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'filtered tree preview helper exists' $buildFilteredTree
$buildSearchTree = $commandsType.GetMethod('BuildSearchedLayerPlanTreeNodes', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'searched tree preview helper exists' $buildSearchTree
$formatTreeReport = $commandsType.GetMethod('FormatLayerPlanTreeReport', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'tree report formatter exists' $formatTreeReport
$filterType = $commandsType.GetNestedType('LayerPlanTreeFilter', [Reflection.BindingFlags]'NonPublic')
Assert-NotNull 'tree preview filter enum exists' $filterType

function Node-Text($node) { return [string]$node.Text }

$treeWithoutFallback = $buildTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false))
Assert-Equal 'tree preview top node count' 4 $treeWithoutFallback.Length
Assert-Contains 'tree summary node text' (Node-Text $treeWithoutFallback[0]) '^\u6458\u8981'
Assert-Equal 'tree summary does not repeat detail children' 0 $treeWithoutFallback[0].Nodes.Count
Assert-Contains 'tree unknown node preserves layers' (Node-Text $treeWithoutFallback[1]) '\u4FDD\u6301\u539F\u6837'
Assert-Contains 'tree migration node exists' (Node-Text $treeWithoutFallback[2]) '^\u5C06\u8FC1\u79FB\u56FE\u5C42'
Assert-Contains 'tree whitelist node exists' (Node-Text $treeWithoutFallback[3]) '^\u767D\u540D\u5355\u56FE\u5C42'

$treeWithFallback = $buildTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $true))
Assert-Contains 'tree unknown node moves to 0' (Node-Text $treeWithFallback[1]) '\u5C06\u5F52\u5230 0 \u5C42'
Assert-Contains 'tree unknown child moves to 0' (Node-Text $treeWithFallback[1].Nodes[0]) '-> 0'

$filterAll = [Enum]::Parse($filterType, 'All')
$filterUnknown = [Enum]::Parse($filterType, 'Unknown')
$filterMigration = [Enum]::Parse($filterType, 'Migration')
$filterWhitelistOnly = [Enum]::Parse($filterType, 'Whitelist')

$migrationNode = $treeWithoutFallback[2]
Assert-Contains 'tree first migration group sorted by object count' (Node-Text $migrationNode.Nodes[0]) '^3-TEXT'
Assert-Contains 'tree second migration group sorted by object count' (Node-Text $migrationNode.Nodes[1]) '^0-EQUIPMENT'
Assert-Contains 'tree first source sorted by object count' (Node-Text $migrationNode.Nodes[0].Nodes[0]) '^TEXT-OLD-B'
Assert-Contains 'tree second source sorted by object count' (Node-Text $migrationNode.Nodes[0].Nodes[1]) '^TEXT-OLD-A'
Assert-Contains 'tree whitelist child includes reason' (Node-Text $treeWithoutFallback[3].Nodes[0]) 'white big reason'

$filteredAll = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false, $filterAll))
Assert-Equal 'filtered tree all node count' 4 $filteredAll.Length
Assert-Contains 'filtered tree all includes unknown' (Node-Text $filteredAll[1]) '^\u672A\u8BC6\u522B\u56FE\u5C42'

$filteredUnknown = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false, $filterUnknown))
Assert-Equal 'filtered tree unknown node count' 2 $filteredUnknown.Length
Assert-Contains 'filtered tree unknown keeps summary first' (Node-Text $filteredUnknown[0]) '^\u6458\u8981'
Assert-Contains 'filtered tree unknown shows only unknown section' (Node-Text $filteredUnknown[1]) '^\u672A\u8BC6\u522B\u56FE\u5C42'

$filteredMigration = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false, $filterMigration))
Assert-Equal 'filtered tree migration node count' 2 $filteredMigration.Length
Assert-Contains 'filtered tree migration shows only migration section' (Node-Text $filteredMigration[1]) '^\u5C06\u8FC1\u79FB\u56FE\u5C42'

$filteredWhitelistOnly = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false, $filterWhitelistOnly))
Assert-Equal 'filtered tree whitelist node count' 2 $filteredWhitelistOnly.Length
Assert-Contains 'filtered tree whitelist shows only whitelist section' (Node-Text $filteredWhitelistOnly[1]) '^\u767D\u540D\u5355\u56FE\u5C42'

$filteredUnknownFallback = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $true, $filterUnknown))
Assert-Contains 'filtered unknown respects fallback to 0' (Node-Text $filteredUnknownFallback[1]) '\u5C06\u5F52\u5230 0 \u5C42'

$searchedUnknown = $buildSearchTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false, $filterAll, 'UNKNOWN-BIG'))
Assert-Equal 'searched tree keeps summary and unknown section' 2 $searchedUnknown.Length
Assert-Contains 'searched tree unknown summary remains first' (Node-Text $searchedUnknown[0]) '^\u6458\u8981'
Assert-Contains 'searched tree unknown section matches keyword' (Node-Text $searchedUnknown[1]) '^\u672A\u8BC6\u522B\u56FE\u5C42'
Assert-Equal 'searched tree unknown keeps one matching child' 1 $searchedUnknown[1].Nodes.Count
Assert-Contains 'searched tree unknown child text matches keyword' (Node-Text $searchedUnknown[1].Nodes[0]) 'UNKNOWN-BIG'

$searchedMigration = $buildSearchTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false, $filterAll, 'TEXT-OLD-B'))
Assert-Equal 'searched tree migration keeps summary and migration section' 2 $searchedMigration.Length
Assert-Contains 'searched tree migration section exists' (Node-Text $searchedMigration[1]) '^\u5C06\u8FC1\u79FB\u56FE\u5C42'
Assert-Equal 'searched tree migration keeps one matching target group' 1 $searchedMigration[1].Nodes.Count
Assert-Contains 'searched tree migration group is target layer' (Node-Text $searchedMigration[1].Nodes[0]) '^3-TEXT'
Assert-Equal 'searched tree migration group keeps one matching source child' 1 $searchedMigration[1].Nodes[0].Nodes.Count
Assert-Contains 'searched tree migration child text matches keyword' (Node-Text $searchedMigration[1].Nodes[0].Nodes[0]) 'TEXT-OLD-B'

$searchedWhitelist = $buildSearchTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false, $filterAll, 'FRAME-BIG'))
Assert-Equal 'searched tree whitelist keeps summary and whitelist section' 2 $searchedWhitelist.Length
Assert-Contains 'searched tree whitelist section exists' (Node-Text $searchedWhitelist[1]) '^\u767D\u540D\u5355\u56FE\u5C42'
Assert-Equal 'searched tree whitelist keeps one matching child' 1 $searchedWhitelist[1].Nodes.Count
Assert-Contains 'searched tree whitelist child text matches keyword' (Node-Text $searchedWhitelist[1].Nodes[0]) 'FRAME-BIG'

$searchedNoMatch = $buildSearchTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false, $filterAll, 'NO-SUCH-LAYER'))
Assert-Equal 'searched tree without matches keeps summary only' 1 $searchedNoMatch.Length

$searchedMigrationReportArgs = New-Object 'object[]' 1
$searchedMigrationReportArgs[0] = [System.Windows.Forms.TreeNode[]]$searchedMigration
$searchedMigrationReport = [string]$formatTreeReport.Invoke($null, $searchedMigrationReportArgs)
Assert-Contains 'tree report includes visible migration child' $searchedMigrationReport 'TEXT-OLD-B'
Assert-NotContains 'tree report excludes hidden migration child' $searchedMigrationReport 'TEXT-OLD-A'
Assert-NotContains 'tree report excludes hidden unknown child' $searchedMigrationReport 'UNKNOWN-BIG'

$unknownOnlyReportArgs = New-Object 'object[]' 1
$unknownOnlyReportArgs[0] = [System.Windows.Forms.TreeNode[]]$filteredUnknownFallback
$unknownOnlyReport = [string]$formatTreeReport.Invoke($null, $unknownOnlyReportArgs)
Assert-Contains 'tree report follows current fallback state' $unknownOnlyReport '\u5C06\u5F52\u5230 0 \u5C42'
Assert-NotContains 'tree report excludes migration section when unknown filter is active' $unknownOnlyReport '\u5C06\u8FC1\u79FB\u56FE\u5C42'

$layerCommands = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit\LayerCommands.cs') -Raw
Assert-Contains 'layer standard gathers block/layout scopes' $layerCommands 'GetLayerStandardScopeIds'
Assert-Contains 'layer standard migrates all gathered scopes' $layerCommands 'MoveLayerStandardEntities'
Assert-Contains 'layer standard preview uses tree view' $layerCommands 'new\s+TreeView\s*\('
Assert-Contains 'layer standard fallback rebuilds tree preview' $layerCommands 'BuildLayerPlanTreePreview'
Assert-Contains 'layer standard preview has keyword filter box' $layerCommands 'new\s+TextBox\s*\('
Assert-Contains 'layer standard preview has compact search box' $layerCommands 'search\.Width\s*=\s*UiScale\(180\)'
Assert-Contains 'layer standard preview rebuilds tree on keyword change' $layerCommands 'TextChanged\s*\+='
Assert-Contains 'layer standard focused preview expands tree' $layerCommands 'tree\.ExpandAll\s*\('
Assert-Contains 'layer standard filter expands preview tree' $layerCommands 'filter\s*!=\s*LayerPlanTreeFilter\.All'
Assert-Contains 'layer standard search expands preview tree' $layerCommands 'SafeStr\(searchText\)\.Trim\(\)\.Length\s*>\s*0'
Assert-Contains 'layer standard preview has all filter button' $layerCommands '\\u5168\\u90e8'
Assert-Contains 'layer standard preview has unknown filter button' $layerCommands '\\u672a\\u8bc6\\u522b'
Assert-Contains 'layer standard preview has migration filter button' $layerCommands '\\u5c06\\u8fc1\\u79fb'
Assert-Contains 'layer standard preview has whitelist filter button' $layerCommands '\\u767d\\u540d\\u5355'
Assert-Contains 'layer standard preview has copy current button' $layerCommands '\\u590d\\u5236\\u5f53\\u524d'
Assert-Contains 'layer standard copy report uses clipboard' $layerCommands 'Clipboard\.SetText'
Assert-Contains 'layer standard copy report formats current tree' $layerCommands 'FormatLayerPlanTreeReport'
Assert-NotContains 'layer standard copy report no longer copies full plan directly' $layerCommands 'Clipboard\.SetText\(FormatLayerPlan\('
Assert-NotContains 'layer standard no longer creates text preview variable' $layerCommands 'var\s+txt\s*=\s*new\s+TextBox\s*\('

$projectConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.ini') -Raw
$defaultConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.default.ini') -Raw
$configSource = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit.Core\Config.cs') -Raw
$readme = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\README.md') -Raw
$manualFileName = 'CadToolkit' + (-join ([char[]](0x4F7F, 0x7528, 0x624B, 0x518C))) + '.html'
$manual = Get-Content -Encoding UTF8 (Join-Path (Join-Path $repo 'CadToolkit') $manualFileName) -Raw
$localConfigPath = 'C:\CadToolkit\CadToolkit.ini'
$localConfig = if (Test-Path -LiteralPath $localConfigPath) { Get-Content -Encoding UTF8 -LiteralPath $localConfigPath -Raw } else { '' }

$equipmentLinePattern = '0-\u8BBE\u5907\u5C42=\*\u8BBE\u5907\*,0-4,\*VIS\*'
$localEquipmentLinePattern = '0-\u8BBE\u5907\u5C42=\*\u8BBE\u5907\*,0-4,\*VIS\*'
$centerLinePattern = '1-\u4E2D\u5FC3\u7EBF\u5C42=\*\u4E2D\u5FC3\*,\*\u4E2D\u5FC3\u7EBF\*,\*CENTER\*,0-1,1,\*AXIS\*,\*CLEARANCE\*,ZX,ZXX'
$textLinePattern = '3-\u6587\u5B57\u5C42=\*\u6587\u5B57\*,\*\u8BF4\u660E\*,\*\u7F16\u53F7\*,\*TEXT,\*txt'
$embeddedCenterLinePattern = '1-\\u4E2D\\u5FC3\\u7EBF\\u5C42=\*\\u4E2D\\u5FC3\*,\*\\u4E2D\\u5FC3\\u7EBF\*,\*CENTER\*,0-1,1,\*AXIS\*,\*CLEARANCE\*,ZX,ZXX'
$oldEquipmentLinePattern = '(?m)^0-\u8BBE\u5907\u5C42=\u8BBE\u5907,0-4,VIS35$'
$embeddedEquipmentLinePattern = '0-\\u8BBE\\u5907\\u5C42=\*\\u8BBE\\u5907\*,0-4,\*VIS\*'

Assert-Contains 'project config uses explicit wildcard layer map' $projectConfig $equipmentLinePattern
Assert-Contains 'default config uses explicit wildcard layer map' $defaultConfig $equipmentLinePattern
Assert-Contains 'embedded default uses explicit wildcard layer map' $configSource $embeddedEquipmentLinePattern
Assert-Contains 'readme uses explicit wildcard layer map' $readme $equipmentLinePattern
Assert-Contains 'manual uses explicit wildcard layer map' $manual $equipmentLinePattern
Assert-Contains 'project config uses local center layer aliases' $projectConfig $centerLinePattern
Assert-Contains 'default config uses local center layer aliases' $defaultConfig $centerLinePattern
Assert-Contains 'embedded default uses local center layer aliases' $configSource $embeddedCenterLinePattern
Assert-Contains 'readme uses local center layer aliases' $readme $centerLinePattern
Assert-Contains 'manual uses local center layer aliases' $manual $centerLinePattern
Assert-Contains 'project config uses local text suffix aliases' $projectConfig $textLinePattern
Assert-Contains 'default config uses local text suffix aliases' $defaultConfig $textLinePattern
if ($localConfig.Length -gt 0) {
    Assert-Contains 'local config uses explicit layer map' $localConfig $localEquipmentLinePattern
    Assert-Contains 'local config uses local center layer aliases' $localConfig $centerLinePattern
    Assert-Contains 'local config uses local text suffix aliases' $localConfig $textLinePattern
}
Assert-NotContains 'project config removes old contains-style layer map' $projectConfig $oldEquipmentLinePattern
Assert-NotContains 'default config removes old contains-style layer map' $defaultConfig $oldEquipmentLinePattern
Assert-Contains 'readme documents exact default matching' $readme '\u5168\u5B57\u5339\u914D'
Assert-Contains 'manual documents exact default matching' $manual '\u5168\u5B57\u5339\u914D'
Assert-Contains 'readme documents copying current layer preview' $readme '\u590D\u5236\u5F53\u524D'
Assert-Contains 'manual documents copying current layer preview' $manual '\u590D\u5236\u5F53\u524D'
Assert-NotContains 'readme removes old contains alias wording' $readme '\u53EA\u8981\u5305\u542B\u67D0\u4E2A\u522B\u540D'
Assert-NotContains 'manual removes old contains alias wording' $manual '\u53EA\u8981\u5305\u542B\u67D0\u4E2A\u522B\u540D'
