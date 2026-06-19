$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$config = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.Core\Config.cs') -Raw
$diagnostics = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.Core\ConfigDiagnostics.cs') -Raw
$projectConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.ini') -Raw
$defaultConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.default.ini') -Raw
$panel = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.UI\PanelBuilder.cs') -Raw
$plugin = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit\Plugin.cs') -Raw
$standardCommandsPath = Join-Path $repo 'CadToolkit\src\CadToolkit\StandardCenterCommands.cs'
$standardCommands = if (Test-Path $standardCommandsPath) { Get-Content -Encoding UTF8 $standardCommandsPath -Raw } else { '' }
$manualPath = Get-ChildItem -LiteralPath (Join-Path $repo 'CadToolkit') -Filter '*.html' | Select-Object -First 1
$manual = Get-Content -Encoding UTF8 $manualPath.FullName -Raw
$readme = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\README.md') -Raw

$standardCenterLabel = -join ([char[]](0x89C4, 0x8303, 0x4E2D, 0x5FC3))
$layerStandardLabel = -join ([char[]](0x56FE, 0x5C42, 0x89C4, 0x8303))
$textStandardLabel = -join ([char[]](0x6587, 0x5B57, 0x89C4, 0x8303))
$configCheckLabel = -join ([char[]](0x914D, 0x7F6E, 0x4F53, 0x68C0))
$standardToolsGroup = -join ([char[]](0x89C4, 0x8303, 0x5DE5, 0x5177))
$configToolsGroup = -join ([char[]](0x914D, 0x7F6E, 0x5DE5, 0x5177))
$standardCenterCommand = $standardCenterLabel + '=CT_STANDARDCENTER'

function Assert-Match($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-NotMatch($name, $text, $pattern) {
    if ($text -match $pattern) { throw "$name unexpectedly found pattern: $pattern" }
    Write-Host "PASS $name"
}

function Assert-Literal($name, $text, $literal) {
    if (-not $text.Contains($literal)) { throw "$name did not find literal: $literal" }
    Write-Host "PASS $name"
}

function Assert-NotLiteral($name, $text, $literal) {
    if ($text.Contains($literal)) { throw "$name unexpectedly found literal: $literal" }
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

Assert-NotLiteral 'project config omits standard center from panel commands' $projectConfig $standardCenterCommand
Assert-NotLiteral 'default config omits standard center from panel commands' $defaultConfig $standardCenterCommand
Assert-NotLiteral 'embedded default omits standard center from panel commands' $config $standardCenterCommand
Assert-Literal 'diagnostics knows standard center label' $diagnostics $standardCenterLabel
Assert-Literal 'diagnostics knows CT_STANDARDCENTER' $diagnostics 'CT_STANDARDCENTER'

Assert-Match 'standard center command file exists with command method' $standardCommands '\[CommandMethod\("CT_STANDARDCENTER"\)\]'
Assert-Literal 'standard center dialog class exists' $standardCommands 'StandardCenterForm'
Assert-Literal 'standard center dialog title is Chinese' $standardCommands $standardCenterLabel
Assert-Literal 'standard center has layer standard action' $standardCommands 'CT_LAYERSTANDARD'
Assert-Literal 'standard center has text standard action' $standardCommands 'CT_TEXTSTYLESTANDARD'
Assert-Literal 'standard center has config check action' $standardCommands 'CT_CONFIGCHECK'
Assert-Literal 'standard center shows layer standard label' $standardCommands $layerStandardLabel
Assert-Literal 'standard center shows text standard label' $standardCommands $textStandardLabel
Assert-Literal 'standard center shows config check label' $standardCommands $configCheckLabel
Assert-Literal 'standard center shows standard tools group' $standardCommands $standardToolsGroup
Assert-Literal 'standard center shows config tools group' $standardCommands $configToolsGroup
Assert-Match 'standard center dispatches commands after dialog closes' $standardCommands 'SendStringToExecute\(commandName \+ " "'

Assert-Match 'panel gear opens standard center' $panel 'btnConfigCheck\.Click \+= delegate \{ result = new PanelAction \{ Kind = "STANDARDCENTER" \}'
Assert-Literal 'panel gear tooltip exposes standard center' $panel $standardCenterLabel
Assert-NotMatch 'panel has no separate standard center button' $panel 'btnStandardCenter'
Assert-Match 'plugin dispatches standard center panel action' $plugin 'action\.Kind == "STANDARDCENTER"'
Assert-Match 'plugin sends standard center command' $plugin 'SendStringToExecute\("CT_STANDARDCENTER "'

foreach ($projectFile in @(
    'CadToolkit\src\CadToolkit\CadToolkit.AutoCAD.csproj',
    'CadToolkit\src\CadToolkit\CadToolkit.GstarCAD.csproj',
    'CadToolkit\src\CadToolkit\CadToolkit.ZWCAD.csproj'
)) {
    $project = Get-Content -Encoding UTF8 (Join-Path $repo $projectFile) -Raw
    Assert-Match "$projectFile includes standard center commands" $project '<Compile Include="StandardCenterCommands\.cs" />'
}

Assert-Literal 'readme documents standard center label' $readme $standardCenterLabel
Assert-Literal 'readme documents standard center command' $readme 'CT_STANDARDCENTER'
Assert-Literal 'manual documents standard center label' $manual $standardCenterLabel
Assert-Literal 'manual documents standard center command' $manual 'CT_STANDARDCENTER'
Assert-Literal 'manual documents standard tools group' $manual $standardToolsGroup
