$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$src = Join-Path $repo 'CadToolkit\src'
$config = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit.Core\Config.cs') -Raw
$diagnostics = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit.Core\ConfigDiagnostics.cs') -Raw
$projectConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.ini') -Raw
$defaultConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.default.ini') -Raw
$standardCenter = Get-Content -Encoding UTF8 (Join-Path $src 'CadToolkit\StandardCenterCommands.cs') -Raw
$configCommandsPath = Join-Path $src 'CadToolkit\ConfigCommands.cs'
$configCommands = Get-Content -Encoding UTF8 $configCommandsPath -Raw
$readme = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\README.md') -Raw
$manualPath = Get-ChildItem -LiteralPath (Join-Path $repo 'CadToolkit') -Filter '*.html' | Select-Object -First 1
$manual = Get-Content -Encoding UTF8 $manualPath.FullName -Raw

$configMaintenanceLabel = -join ([char[]](0x914D, 0x7F6E, 0x7EF4, 0x62A4))
$standardCenterLabel = -join ([char[]](0x89C4, 0x8303, 0x4E2D, 0x5FC3))
$configCheckLabel = -join ([char[]](0x914D, 0x7F6E, 0x4F53, 0x68C0))
$backupHint = -join ([char[]](0x4FEE, 0x590D, 0x524D, 0x4F1A, 0x81EA, 0x52A8, 0x5907, 0x4EFD))
$openConfigHint = (-join ([char[]](0x6253, 0x5F00, 0x5F53, 0x524D))) + ' CadToolkit.ini'

function Assert-Match($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-Literal($name, $text, $literal) {
    if (-not $text.Contains($literal)) { throw "$name did not find literal: $literal" }
    Write-Host "PASS $name"
}

function Assert-Before($name, $text, $first, $second) {
    $firstIndex = $text.IndexOf($first)
    $secondIndex = $text.IndexOf($second)
    if ($firstIndex -lt 0) { throw "$name did not find first literal: $first" }
    if ($secondIndex -lt 0) { throw "$name did not find second literal: $second" }
    if ($firstIndex -ge $secondIndex) { throw "$name expected '$first' before '$second'" }
    Write-Host "PASS $name"
}

$configMaintenanceLine = $configMaintenanceLabel + '=CT_CONFIGMAINTAIN'
$standardCenterLine = $standardCenterLabel + '=CT_STANDARDCENTER'

Assert-Literal 'project config contains config maintenance command' $projectConfig $configMaintenanceLine
Assert-Literal 'default config contains config maintenance command' $defaultConfig $configMaintenanceLine
Assert-Match 'embedded default contains config maintenance command' $config 'CT_CONFIGMAINTAIN'
Assert-Literal 'diagnostics knows config maintenance label' $diagnostics $configMaintenanceLabel
Assert-Literal 'diagnostics knows CT_CONFIGMAINTAIN' $diagnostics 'CT_CONFIGMAINTAIN'
Assert-Before 'config maintenance is before standard center in project config' $projectConfig $configMaintenanceLine $standardCenterLine
Assert-Before 'config maintenance is before standard center in default config' $defaultConfig $configMaintenanceLine $standardCenterLine

Assert-Match 'config maintenance command is registered' $configCommands '\[CommandMethod\("CT_CONFIGMAINTAIN"\)\]'
Assert-Literal 'config maintenance form class exists' $configCommands 'ConfigMaintenanceForm'
Assert-Literal 'config maintenance dialog title is Chinese' $configCommands $configMaintenanceLabel
Assert-Literal 'config maintenance uses config path' $configCommands 'Config.ConfigPath'
Assert-Literal 'config maintenance can open config file' $configCommands 'OpenConfigFile'
Assert-Literal 'config maintenance can open config directory' $configCommands 'OpenConfigDirectory'
Assert-Literal 'config maintenance can run config check' $configCommands 'CT_CONFIGCHECK'
Assert-Literal 'config maintenance can repair config' $configCommands 'ConfigDiagnostics.RepairFile'
Assert-Literal 'config maintenance explains backup' $configCommands $backupHint
Assert-Literal 'config maintenance supports Esc close' $configCommands 'CancelButton = close'

Assert-Literal 'standard center has config maintenance action' $standardCenter 'CT_CONFIGMAINTAIN'
Assert-Literal 'standard center shows config maintenance label' $standardCenter $configMaintenanceLabel
Assert-Literal 'standard center still has config check action' $standardCenter 'CT_CONFIGCHECK'
Assert-Literal 'standard center still shows config check label' $standardCenter $configCheckLabel

foreach ($projectFile in @(
    'CadToolkit\src\CadToolkit\CadToolkit.AutoCAD.csproj',
    'CadToolkit\src\CadToolkit\CadToolkit.GstarCAD.csproj',
    'CadToolkit\src\CadToolkit\CadToolkit.ZWCAD.csproj'
)) {
    $project = Get-Content -Encoding UTF8 (Join-Path $repo $projectFile) -Raw
    Assert-Match "$projectFile compiles config commands" $project '<Compile Include="ConfigCommands\.cs" />'
}

Assert-Literal 'readme documents config maintenance label' $readme $configMaintenanceLabel
Assert-Literal 'readme documents config maintenance command' $readme 'CT_CONFIGMAINTAIN'
Assert-Literal 'readme documents config file open behavior' $readme $openConfigHint
Assert-Literal 'readme documents config backup behavior' $readme ((-join ([char[]](0x81EA, 0x52A8, 0x4FEE, 0x590D, 0x524D, 0x4F1A, 0x5907, 0x4EFD))))
Assert-Literal 'manual documents config maintenance label' $manual $configMaintenanceLabel
Assert-Literal 'manual documents config maintenance command' $manual 'CT_CONFIGMAINTAIN'
Assert-Literal 'manual documents config file open behavior' $manual $openConfigHint
Assert-Literal 'manual documents config backup behavior' $manual ((-join ([char[]](0x81EA, 0x52A8, 0x4FEE, 0x590D, 0x524D, 0x4F1A, 0x5907, 0x4EFD))))
