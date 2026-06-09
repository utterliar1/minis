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

$layerCommands = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit\LayerCommands.cs') -Raw
Assert-Contains 'layer standard gathers block/layout scopes' $layerCommands 'GetLayerStandardScopeIds'
Assert-Contains 'layer standard migrates all gathered scopes' $layerCommands 'MoveLayerStandardEntities'

$projectConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.ini') -Raw
$defaultConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.default.ini') -Raw
$configSource = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit.Core\Config.cs') -Raw
$readme = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\README.md') -Raw
$manualFileName = 'CadToolkit' + (-join ([char[]](0x4F7F, 0x7528, 0x624B, 0x518C))) + '.html'
$manual = Get-Content -Encoding UTF8 (Join-Path (Join-Path $repo 'CadToolkit') $manualFileName) -Raw
$localConfigPath = 'C:\CadToolkit\CadToolkit.ini'
$localConfig = if (Test-Path -LiteralPath $localConfigPath) { Get-Content -Encoding UTF8 -LiteralPath $localConfigPath -Raw } else { '' }

$equipmentLinePattern = '0-\u8BBE\u5907\u5C42=\*\u8BBE\u5907\*,0-4,VIS35'
$oldEquipmentLinePattern = '(?m)^0-\u8BBE\u5907\u5C42=\u8BBE\u5907,0-4,VIS35$'
$embeddedEquipmentLinePattern = '0-\\u8BBE\\u5907\\u5C42=\*\\u8BBE\\u5907\*,0-4,VIS35'

Assert-Contains 'project config uses explicit wildcard layer map' $projectConfig $equipmentLinePattern
Assert-Contains 'default config uses explicit wildcard layer map' $defaultConfig $equipmentLinePattern
Assert-Contains 'embedded default uses explicit wildcard layer map' $configSource $embeddedEquipmentLinePattern
Assert-Contains 'local config uses explicit wildcard layer map' $localConfig $equipmentLinePattern
Assert-NotContains 'project config removes old contains-style layer map' $projectConfig $oldEquipmentLinePattern
Assert-NotContains 'default config removes old contains-style layer map' $defaultConfig $oldEquipmentLinePattern
Assert-Contains 'readme documents exact default matching' $readme '\u5168\u5B57\u5339\u914D'
Assert-Contains 'manual documents exact default matching' $manual '\u5168\u5B57\u5339\u914D'
Assert-NotContains 'readme removes old contains alias wording' $readme '\u53EA\u8981\u5305\u542B\u67D0\u4E2A\u522B\u540D'
Assert-NotContains 'manual removes old contains alias wording' $manual '\u53EA\u8981\u5305\u542B\u67D0\u4E2A\u522B\u540D'
