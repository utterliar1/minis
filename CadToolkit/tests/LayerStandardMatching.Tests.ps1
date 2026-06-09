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

$equipmentAlias = '*' + (-join ([char[]](0x8BBE, 0x5907))) + '*'
$equipmentLayer = (-join ([char[]](0x4E00, 0x5C42))) + '-' + (-join ([char[]](0x8BBE, 0x5907))) + '-' + (-join ([char[]](0x65E7)))

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

$layerCommands = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit\LayerCommands.cs') -Raw
Assert-Contains 'layer standard gathers block/layout scopes' $layerCommands 'GetLayerStandardScopeIds'
Assert-Contains 'layer standard migrates all gathered scopes' $layerCommands 'MoveLayerStandardEntities'
