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

function Assert-NotNull($name, $value) {
    if ($null -eq $value) { throw "$name expected a value but got null" }
    Write-Host "PASS $name"
}

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) { throw "$name expected [$expected] but got [$actual]" }
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
$commandsType = $plugin.GetType('CadToolkit.CadCommands', $true)

$modelType = $commandsType.GetNestedType('StandardPreviewModel', [Reflection.BindingFlags]'NonPublic')
Assert-NotNull 'standard preview model type exists' $modelType
$itemType = $commandsType.GetNestedType('StandardPreviewItem', [Reflection.BindingFlags]'NonPublic')
Assert-NotNull 'standard preview item type exists' $itemType

Assert-NotNull 'standard preview model has summary text' ($modelType.GetField('SummaryText', [Reflection.BindingFlags]'Public, Instance'))
Assert-NotNull 'standard preview model has unknown title' ($modelType.GetField('UnknownTitle', [Reflection.BindingFlags]'Public, Instance'))
Assert-NotNull 'standard preview model has migration title' ($modelType.GetField('MigrationTitle', [Reflection.BindingFlags]'Public, Instance'))
Assert-NotNull 'standard preview model has whitelist title' ($modelType.GetField('WhitelistTitle', [Reflection.BindingFlags]'Public, Instance'))
Assert-NotNull 'standard preview item has source text' ($itemType.GetField('SourceText', [Reflection.BindingFlags]'Public, Instance'))
Assert-NotNull 'standard preview item has target text' ($itemType.GetField('TargetText', [Reflection.BindingFlags]'Public, Instance'))
Assert-NotNull 'standard preview item has target label' ($itemType.GetField('TargetLabel', [Reflection.BindingFlags]'Public, Instance'))
Assert-NotNull 'standard preview item has count' ($itemType.GetField('Count', [Reflection.BindingFlags]'Public, Instance'))
Assert-NotNull 'standard preview item has reason' ($itemType.GetField('Reason', [Reflection.BindingFlags]'Public, Instance'))

$buildMethod = $commandsType.GetMethod('BuildStandardPreviewTreeNodes', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'standard preview tree builder exists' $buildMethod
$filterMethod = $commandsType.GetMethod('BuildFilteredStandardPreviewTreeNodes', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'standard preview filtered tree builder exists' $filterMethod
$searchMethod = $commandsType.GetMethod('BuildSearchedStandardPreviewTreeNodes', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'standard preview searched tree builder exists' $searchMethod

$model = [Activator]::CreateInstance($modelType)
$modelType.GetField('SummaryText').SetValue($model, 'Summary: standards 1; migrate 2 / 7; unknown 1 / 3; whitelist 1 / 2')
$modelType.GetField('UnknownTitle').SetValue($model, 'Unknown items (1 / 3, preserve)')
$modelType.GetField('MigrationTitle').SetValue($model, 'Migration items (2 / 7)')
$modelType.GetField('WhitelistTitle').SetValue($model, 'Whitelist items (1 / 2, preserve)')

$unknownItems = $modelType.GetField('UnknownItems').GetValue($model)
$migrationItems = $modelType.GetField('MigrationItems').GetValue($model)
$whitelistItems = $modelType.GetField('WhitelistItems').GetValue($model)

function New-PreviewItem($source, $target, $targetLabel, $count, $reason) {
    $item = [Activator]::CreateInstance($itemType)
    $itemType.GetField('SourceText').SetValue($item, $source)
    $itemType.GetField('TargetText').SetValue($item, $target)
    $itemType.GetField('TargetLabel').SetValue($item, $targetLabel)
    $itemType.GetField('Count').SetValue($item, $count)
    $itemType.GetField('Reason').SetValue($item, $reason)
    return $item
}

[void]$unknownItems.Add((New-PreviewItem 'UNKNOWN-A' '' '' 3 'unknown reason'))
[void]$migrationItems.Add((New-PreviewItem 'SRC-B' 'TARGET-2' 'TARGET-2 label' 2 'reason b'))
[void]$migrationItems.Add((New-PreviewItem 'SRC-A' 'TARGET-1' 'TARGET-1 label' 5 'reason a'))
[void]$whitelistItems.Add((New-PreviewItem 'WHITE-A' '' '' 2 'white reason'))

$nodes = $buildMethod.Invoke($null, @($model, $true))
Assert-Equal 'standard preview tree has four root nodes' 4 $nodes.Length
Assert-Equal 'standard preview tree keeps summary first' 'Summary: standards 1; migrate 2 / 7; unknown 1 / 3; whitelist 1 / 2' $nodes[0].Text
Assert-Equal 'standard preview tree keeps unknown second' 'Unknown items (1 / 3, preserve)' $nodes[1].Text
Assert-Equal 'standard preview tree keeps migration third' 'Migration items (2 / 7)' $nodes[2].Text
Assert-Equal 'standard preview tree keeps whitelist fourth' 'Whitelist items (1 / 2, preserve)' $nodes[3].Text
Assert-Equal 'standard preview migration target groups sort by object count' 'TARGET-1 label (1 items / 5 objects)' $nodes[2].Nodes[0].Text
Assert-Equal 'standard preview migration child text uses shared format' 'SRC-A -> TARGET-1    5 objects    reason a' $nodes[2].Nodes[0].Nodes[0].Text
Assert-Equal 'standard preview unknown preserve text uses shared format' 'UNKNOWN-A    3 objects    preserve    unknown reason' $nodes[1].Nodes[0].Text
Assert-Equal 'standard preview whitelist text uses shared format' 'WHITE-A    2 objects    white reason' $nodes[3].Nodes[0].Text

$filtered = $filterMethod.Invoke($null, @($model, 2, $true))
Assert-Equal 'standard preview migration filter keeps summary and section' 2 $filtered.Length
Assert-Equal 'standard preview migration filter returns migration section' 'Migration items (2 / 7)' $filtered[1].Text

$searched = $searchMethod.Invoke($null, @($model, 2, 'SRC-A', $true))
Assert-Equal 'standard preview search keeps one section' 2 $searched.Length
Assert-Equal 'standard preview search keeps matching migration child' 'SRC-A -> TARGET-1    5 objects    reason a' $searched[1].Nodes[0].Nodes[0].Text

$standardPreviewSource = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit\StandardPreviewUi.cs') -Raw
$layerSource = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit\Plugin.cs') -Raw
$textSource = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit\TextStyleCommands.cs') -Raw
Assert-Contains 'standard preview model owns target grouping' $standardPreviewSource 'BuildStandardPreviewTargetGroups'
Assert-Contains 'layer tree delegates to shared preview builder' $layerSource 'BuildStandardPreviewTreeNodes\(BuildLayerStandardPreviewModel'
Assert-Contains 'layer filtered tree delegates to shared preview builder' $layerSource 'BuildFilteredStandardPreviewTreeNodes\(BuildLayerStandardPreviewModel'
Assert-Contains 'layer searched tree delegates to shared preview builder' $layerSource 'BuildSearchedStandardPreviewTreeNodes\(BuildLayerStandardPreviewModel'
Assert-Contains 'text tree delegates to shared preview builder' $textSource 'BuildStandardPreviewTreeNodes\(BuildTextStyleStandardPreviewModel'
Assert-Contains 'text filtered tree delegates to shared preview builder' $textSource 'BuildFilteredStandardPreviewTreeNodes\(BuildTextStyleStandardPreviewModel'
Assert-Contains 'text searched tree delegates to shared preview builder' $textSource 'BuildSearchedStandardPreviewTreeNodes\(BuildTextStyleStandardPreviewModel'
