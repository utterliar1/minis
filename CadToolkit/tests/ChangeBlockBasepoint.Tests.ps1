$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$blockCommands = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit\BlockCommands.cs') -Raw
$projectConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.ini') -Raw
$defaultConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.default.ini') -Raw
$configSource = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.Core\Config.cs') -Raw
$readme = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\README.md') -Raw
$manual = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit使用手册.html') -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name did not find pattern: $pattern"
    }
    Write-Host "PASS $name"
}

function Assert-ContainsLiteral($name, $text, $literal) {
    if (-not $text.Contains($literal)) {
        throw "$name did not find literal: $literal"
    }
    Write-Host "PASS $name"
}

Assert-Contains 'change basepoint command is registered' $blockCommands '\[CommandMethod\("CT_CHANGEBASEPOINT"\)\]'
Assert-Contains 'change basepoint method exists' $blockCommands 'public\s+void\s+ChangeBlockBasepoint\s*\('

$changeBlockBasepointMatch = [regex]::Match(
    $blockCommands,
    'public\s+void\s+ChangeBlockBasepoint\s*\([^)]*\)\s*\{[\s\S]*?(?=\r?\n\s*static\s+bool\s+CanChangeBlockBasepoint\s*\()'
)

if (-not $changeBlockBasepointMatch.Success) {
    throw 'change basepoint method body could not be extracted before CanChangeBlockBasepoint'
}

$changeBody = $changeBlockBasepointMatch.Value

Assert-Contains 'change basepoint asks for block reference' $changeBody 'AddAllowedClass\(typeof\(BlockReference\),\s*true\)'
Assert-Contains 'change basepoint asks for new base point' $changeBody '指定新的块基点'
Assert-Contains 'change basepoint converts world point to definition coordinates' $changeBody 'BlockTransform\.Inverse\(\)'
Assert-Contains 'change basepoint updates block definition origin' $changeBody '\.Origin\s*='
Assert-Contains 'change basepoint compensates references by position' $changeBody '(\.Position\s*=[\s\S]*(GetVectorTo|Vector3d|TransformBy))|((GetVectorTo|Vector3d|TransformBy)[\s\S]*\.Position\s*=)'
Assert-Contains 'change basepoint counts affected references' $changeBody 'affectedReferences'
Assert-Contains 'change basepoint rejects unsupported block records' $blockCommands 'CanChangeBlockBasepoint'
Assert-Contains 'change basepoint scans all block references' $blockCommands 'GetBlockReferencesForDefinition'

Assert-ContainsLiteral 'project config contains command label' $projectConfig '改块基点=CT_CHANGEBASEPOINT'
Assert-ContainsLiteral 'default config contains command label' $defaultConfig '改块基点=CT_CHANGEBASEPOINT'
Assert-Contains 'embedded default config contains command label' $configSource '改块基点=CT_CHANGEBASEPOINT|\\u6539\\u5757\\u57FA\\u70B9=CT_CHANGEBASEPOINT'
Assert-ContainsLiteral 'readme documents command label' $readme '改块基点'
Assert-ContainsLiteral 'readme documents command name' $readme 'CT_CHANGEBASEPOINT'
Assert-ContainsLiteral 'readme shows four block tools' $readme '图块操作（4 个）'
Assert-ContainsLiteral 'manual documents command label' $manual '改块基点'
Assert-ContainsLiteral 'manual documents command name' $manual 'CT_CHANGEBASEPOINT'
