$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$configPath = Join-Path $repo 'CadToolkit\src\CadToolkit.Core\Config.cs'
$iniPath = Join-Path $repo 'CadToolkit\CadToolkit.ini'
$defaultIniPath = Join-Path $repo 'CadToolkit\CadToolkit.default.ini'
$layerCommandsPath = Join-Path $repo 'CadToolkit\src\CadToolkit\LayerCommands.cs'

$config = Get-Content -Encoding UTF8 $configPath -Raw
$ini = Get-Content -Encoding UTF8 $iniPath -Raw
$defaultIni = Get-Content -Encoding UTF8 $defaultIniPath -Raw
$layerCommands = Get-Content -Encoding UTF8 $layerCommandsPath -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name found forbidden pattern: $pattern" }
    Write-Host "PASS $name"
}

Assert-Contains 'project config defaults keep layer 0 off' $ini 'IsoLayerKeepLayer0=false'
Assert-Contains 'default template keeps layer 0 off' $defaultIni 'IsoLayerKeepLayer0=false'
Assert-Contains 'embedded default keeps layer 0 off' $config 'IsoLayerKeepLayer0=false'
Assert-Contains 'config exposes iso layer option' $config 'IsoLayerKeepLayer0'

Assert-Contains 'iso layer compares names case-insensitively' $layerCommands 'IsIsoTargetLayer'
Assert-Contains 'iso layer switches current layer before freeze' $layerCommands 'EnsureIsoCurrentLayer'
Assert-Contains 'iso layer can restore previous current layer' $layerCommands 'PreviousCurrentLayer'
Assert-NotContains 'iso layer does not preserve arbitrary current layer' $layerCommands 'lid == Db\.Clayer'
