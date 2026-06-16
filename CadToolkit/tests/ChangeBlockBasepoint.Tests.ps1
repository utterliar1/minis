$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$blockCommands = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit\BlockCommands.cs') -Raw
$coordinateHelpers = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit\CoordinateHelpers.cs') -Raw
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

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) {
        throw "$name found forbidden pattern: $pattern"
    }
    Write-Host "PASS $name"
}

function Assert-ContainsLiteral($name, $text, $literal) {
    if (-not $text.Contains($literal)) {
        throw "$name did not find literal: $literal"
    }
    Write-Host "PASS $name"
}

Assert-Contains 'change basepoint command is registered with pickfirst support' $blockCommands '\[CommandMethod\("CT_CHANGEBASEPOINT",\s*CommandFlags\.UsePickSet\)\]'
Assert-Contains 'change basepoint method exists' $blockCommands 'public\s+void\s+ChangeBlockBasepoint\s*\('

$changeBlockBasepointMatch = [regex]::Match(
    $blockCommands,
    'public\s+void\s+ChangeBlockBasepoint\s*\([^)]*\)\s*\{[\s\S]*?(?=\r?\n\s*static\s+ObjectId\s+GetImpliedBlockReferenceOrPrompt\s*\()'
)

if (-not $changeBlockBasepointMatch.Success) {
    throw 'change basepoint method body could not be extracted before GetImpliedBlockReferenceOrPrompt'
}

$changeBody = $changeBlockBasepointMatch.Value

Assert-Contains 'change basepoint asks for block reference' $changeBody 'AddAllowedClass\(typeof\(BlockReference\),\s*true\)'
Assert-Contains 'change basepoint reads pickfirst selection before prompting' $changeBody 'GetImpliedBlockReferenceOrPrompt\('
Assert-Contains 'change basepoint uses picked block id from pickfirst or prompt' $changeBody 'pickedId'
Assert-NotContains 'change basepoint does not rely directly on prompt entity result id' $changeBody 'per\.ObjectId'
Assert-Contains 'change basepoint asks for new base point' $changeBody '指定新的块基点'
Assert-Contains 'change basepoint converts selected point from UCS to WCS' $changeBody 'GetPointInWorld\(ppr\.Value\)'
Assert-Contains 'change basepoint converts world point to definition coordinates' $changeBody 'TransformPointByInverse\([^,]+,\s*selectedBr\.BlockTransform\)'
Assert-Contains 'change basepoint updates block definition origin' $changeBody '\.Origin\s*='
Assert-Contains 'change basepoint transforms old base point per reference' $changeBody 'oldOrigin\.TransformBy\(br\.BlockTransform\)'
Assert-Contains 'change basepoint transforms new base point per reference' $changeBody 'newOrigin\.TransformBy\(br\.BlockTransform\)'
Assert-Contains 'change basepoint compensates references by position' $changeBody 'VectorBetween\(oldBasePoint,\s*newBasePoint\)[\s\S]*\.Position\s*=\s*AddVector\([^,]+\.Position'
Assert-Contains 'change basepoint counts affected references' $changeBody 'affectedReferences'
Assert-Contains 'change basepoint rejects unsupported block records' $blockCommands 'CanChangeBlockBasepoint'
Assert-Contains 'change basepoint helper consumes panel pending selection' $blockCommands 'static\s+ObjectId\s+GetImpliedBlockReferenceOrPrompt[\s\S]*_pendingSelection'
Assert-Contains 'change basepoint helper clears panel pending selection' $blockCommands 'static\s+ObjectId\s+GetImpliedBlockReferenceOrPrompt[\s\S]*_pendingSelection\s*=\s*null'
Assert-Contains 'change basepoint helper checks implied selection' $blockCommands 'static\s+ObjectId\s+GetImpliedBlockReferenceOrPrompt[\s\S]*SelectImplied\(\)'
Assert-Contains 'change basepoint helper falls back to prompt entity' $blockCommands 'static\s+ObjectId\s+GetImpliedBlockReferenceOrPrompt[\s\S]*GetEntity\(peo\)'
Assert-Contains 'shared helper converts UCS point to world' $coordinateHelpers 'static\s+Point3d\s+GetPointInWorld'
Assert-Contains 'shared helper reads editor current UCS' $coordinateHelpers 'CurrentUserCoordinateSystem'
Assert-Contains 'change basepoint inverse helper avoids direct SDK-only call' $blockCommands 'static\s+Point3d\s+TransformPointByInverse'
Assert-Contains 'change basepoint scans all block references' $blockCommands 'GetBlockReferencesForDefinition'
Assert-Contains 'quick block converts picked point from UCS to WCS' $blockCommands 'CT_QUICKBLOCK[\s\S]*GetPointInWorld\(ppr\.Value\)'

Assert-ContainsLiteral 'project config contains command label' $projectConfig '改块基点=CT_CHANGEBASEPOINT'
Assert-ContainsLiteral 'default config contains command label' $defaultConfig '改块基点=CT_CHANGEBASEPOINT'
Assert-Contains 'embedded default config contains command label' $configSource '改块基点=CT_CHANGEBASEPOINT|\\u6539\\u5757\\u57FA\\u70B9=CT_CHANGEBASEPOINT'
Assert-ContainsLiteral 'readme documents command label' $readme '改块基点'
Assert-ContainsLiteral 'readme documents command name' $readme 'CT_CHANGEBASEPOINT'
Assert-ContainsLiteral 'readme shows four block tools' $readme '图块操作（4 个）'
Assert-ContainsLiteral 'manual documents command label' $manual '改块基点'
Assert-ContainsLiteral 'manual documents command name' $manual 'CT_CHANGEBASEPOINT'
