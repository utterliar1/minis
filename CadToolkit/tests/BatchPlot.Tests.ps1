$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$config = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.Core\Config.cs') -Raw
$diagnostics = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.Core\ConfigDiagnostics.cs') -Raw
$dialogs = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.UI\Dialogs.cs') -Raw
$batchPlotCommandsPath = Join-Path $repo 'CadToolkit\src\CadToolkit\BatchPlotCommands.cs'
$batchPlotCommands = if (Test-Path $batchPlotCommandsPath) { Get-Content -Encoding UTF8 $batchPlotCommandsPath -Raw } else { '' }
$projectConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.ini') -Raw
$defaultConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.default.ini') -Raw
$readme = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\README.md') -Raw
$manualPath = Get-ChildItem -LiteralPath (Join-Path $repo 'CadToolkit') -Filter '*.html' | Select-Object -First 1
$manual = Get-Content -Encoding UTF8 $manualPath.FullName -Raw
$batchPlotLabel = -join ([char[]](0x6279, 0x91CF, 0x6253, 0x5370))
$batchPlotCommand = $batchPlotLabel + '=CT_BATCHPLOT'
$frameBlockTemplatePrompt = -join ([char[]](0x9009, 0x62E9, 0x4E00, 0x4E2A, 0x56FE, 0x6846, 0x5757, 0x4F5C, 0x4E3A, 0x6A21, 0x677F, 0xFF1A))
$batchPlotScopePrompt = -join ([char[]](0x6846, 0x9009, 0x8981, 0x6253, 0x5370, 0x7684, 0x8303, 0x56F4, 0xFF1A))
$copyPreflightLabel = -join ([char[]](0x590D, 0x5236, 0x9884, 0x68C0))
$sizeMismatchWarning = -join ([char[]](0x68C0, 0x6D4B, 0x5230, 0x56FE, 0x6846, 0x5C3A, 0x5BF8, 0x4E0D, 0x4E00, 0x81F4, 0xFF0C, 0x8BF7, 0x786E, 0x8BA4, 0x662F, 0x5426, 0x6DF7, 0x9009, 0x3002))
$sendToPrinterText = -join ([char[]](0x53D1, 0x9001, 0x5230, 0x6253, 0x5370, 0x673A))
$outputPdfText = -join ([char[]](0x8F93, 0x51FA, 0xFF1A, 0x50, 0x44, 0x46))
$outputPrinterText = -join ([char[]](0x8F93, 0x51FA, 0xFF1A, 0x6253, 0x5370, 0x673A))

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

function Assert-Order($name, $text, $first, $second) {
    $firstIndex = $text.IndexOf($first)
    $secondIndex = $text.IndexOf($second)
    if ($firstIndex -lt 0) { throw "$name did not find first literal: $first" }
    if ($secondIndex -lt 0) { throw "$name did not find second literal: $second" }
    if ($firstIndex -ge $secondIndex) { throw "$name expected '$first' before '$second'" }
    Write-Host "PASS $name"
}

foreach ($setting in @(
    'BatchPlotDevice=DWG To PDF.pc3',
    'BatchPlotPaper=A3',
    'BatchPlotStyle=monochrome.ctb',
    'BatchPlotAutoRotate=true',
    'BatchPlotCenter=true',
    'BatchPlotMarginPercent=2',
    'BatchPlotMarginMm=5',
    'BatchPlotFileNameMode=DrawingDashIndex',
    'BatchPlotSortMode=Position',
    'BatchPlotSortReverse=false'
)) {
    Assert-Literal "project config contains $setting" $projectConfig $setting
    Assert-Literal "default config contains $setting" $defaultConfig $setting
    $key = $setting.Split('=')[0]
    Assert-Match "embedded default contains $key" $config "$([regex]::Escape($key))"
    Assert-Match "diagnostics knows $key" $diagnostics "$([regex]::Escape($key))"
}

Assert-Match 'config exposes batch plot device property' $config 'public\s+static\s+string\s+BatchPlotDevice'
Assert-Match 'config exposes batch plot paper property' $config 'public\s+static\s+string\s+BatchPlotPaper'
Assert-Match 'config exposes batch plot style property' $config 'public\s+static\s+string\s+BatchPlotStyle'
Assert-Match 'config exposes batch plot auto rotate property' $config 'public\s+static\s+bool\s+BatchPlotAutoRotate'
Assert-Match 'config exposes batch plot center property' $config 'public\s+static\s+bool\s+BatchPlotCenter'
Assert-Match 'config exposes batch plot margin property' $config 'public\s+static\s+double\s+BatchPlotMarginPercent'
Assert-Match 'config exposes batch plot margin mm property' $config 'public\s+static\s+double\s+BatchPlotMarginMm'
Assert-Match 'config exposes batch plot file name mode property' $config 'public\s+static\s+string\s+BatchPlotFileNameMode'
Assert-Match 'config exposes batch plot sort mode property' $config 'public\s+static\s+string\s+BatchPlotSortMode'
Assert-Match 'config exposes batch plot sort reverse property' $config 'public\s+static\s+bool\s+BatchPlotSortReverse'

Assert-Literal 'project config contains batch plot command' $projectConfig $batchPlotCommand
Assert-Literal 'default config contains batch plot command' $defaultConfig $batchPlotCommand
Assert-Match 'embedded default contains batch plot command' $config '批量打印=CT_BATCHPLOT|\\u6279\\u91CF\\u6253\\u5370=CT_BATCHPLOT'
Assert-Literal 'diagnostics knows batch plot command' $diagnostics $batchPlotLabel
Assert-Literal 'diagnostics knows CT_BATCHPLOT' $diagnostics 'CT_BATCHPLOT'

Assert-Match 'batch plot dialog class exists' $dialogs 'public\s+class\s+BatchPlotDialog\s*:\s*Form'
Assert-Match 'batch plot preflight row model exists' $dialogs 'public\s+class\s+BatchPlotPreflightRow'
Assert-Match 'batch plot preflight row has index' $dialogs 'public\s+string\s+Index'
Assert-Match 'batch plot preflight row has size' $dialogs 'public\s+string\s+Size'
Assert-Match 'batch plot preflight row has sheet number' $dialogs 'public\s+string\s+SheetNumber'
Assert-Match 'batch plot preflight row has sheet name' $dialogs 'public\s+string\s+SheetName'
Assert-Match 'batch plot preflight row has orientation' $dialogs 'public\s+string\s+Orientation'
Assert-Match 'batch plot preflight row has target' $dialogs 'public\s+string\s+Target'
Assert-Match 'batch plot preflight row has status' $dialogs 'public\s+string\s+Status'
Assert-Match 'batch plot preflight row has size mismatch flag' $dialogs 'public\s+bool\s+SizeMismatched'
Assert-Match 'batch plot dialog title is Chinese' $dialogs 'Text\s*=\s*"\\u6279\\u91CF\\u6253\\u5370"'
Assert-Match 'batch plot dialog uses wider professional layout' $dialogs 'ClientSize\s*=\s*new\s+Size\(680,\s*560\)'
Assert-Match 'batch plot dialog accepts preflight rows and drawing name' $dialogs 'BatchPlotDialog\(int\s+frameCount,\s*string\s+frameBlockName,\s*List<BatchPlotPreflightRow>\s+preflightRows,\s*string\s+outputDirectory,\s*string\s+drawingName\)'
Assert-Match 'batch plot dialog loads device from config' $dialogs 'DeviceName\s*=\s*Config\.BatchPlotDevice'
Assert-Match 'batch plot dialog loads paper from config' $dialogs 'PaperName\s*=\s*Config\.BatchPlotPaper'
Assert-Match 'batch plot dialog loads style from config' $dialogs 'PlotStyle\s*=\s*Config\.BatchPlotStyle'
Assert-Match 'batch plot dialog loads auto rotate from config' $dialogs 'AutoRotate\s*=\s*Config\.BatchPlotAutoRotate'
Assert-Match 'batch plot dialog loads center from config' $dialogs 'CenterPlot\s*=\s*Config\.BatchPlotCenter'
Assert-Match 'batch plot dialog exposes margin percent' $dialogs 'public\s+double\s+MarginPercent'
Assert-Match 'batch plot dialog exposes margin mm' $dialogs 'public\s+double\s+MarginMm'
Assert-Match 'batch plot dialog exposes file name mode' $dialogs 'public\s+string\s+FileNameMode'
Assert-Match 'batch plot dialog exposes sort mode' $dialogs 'public\s+string\s+SortMode'
Assert-Match 'batch plot dialog exposes reverse order' $dialogs 'public\s+bool\s+ReverseOrder'
Assert-Match 'batch plot dialog loads margin from config' $dialogs 'MarginPercent\s*=\s*Config\.BatchPlotMarginPercent'
Assert-Match 'batch plot dialog loads margin mm from config' $dialogs 'MarginMm\s*=\s*Config\.BatchPlotMarginMm'
Assert-Match 'batch plot dialog loads file name mode from config' $dialogs 'FileNameMode\s*=\s*Config\.BatchPlotFileNameMode'
Assert-Match 'batch plot dialog loads sort mode from config' $dialogs 'SortMode\s*=\s*Config\.BatchPlotSortMode'
Assert-Match 'batch plot dialog loads reverse order from config' $dialogs 'ReverseOrder\s*=\s*Config\.BatchPlotSortReverse'
Assert-Match 'batch plot dialog saves device to config' $dialogs 'Config\.BatchPlotDevice\s*=\s*DeviceName'
Assert-Match 'batch plot dialog saves paper to config' $dialogs 'Config\.BatchPlotPaper\s*=\s*PaperName'
Assert-Match 'batch plot dialog saves style to config' $dialogs 'Config\.BatchPlotStyle\s*=\s*PlotStyle'
Assert-Match 'batch plot dialog saves auto rotate to config' $dialogs 'Config\.BatchPlotAutoRotate\s*=\s*AutoRotate'
Assert-Match 'batch plot dialog saves center to config' $dialogs 'Config\.BatchPlotCenter\s*=\s*CenterPlot'
Assert-Match 'batch plot dialog saves margin to config' $dialogs 'Config\.BatchPlotMarginPercent\s*=\s*MarginPercent'
Assert-Match 'batch plot dialog saves margin mm to config' $dialogs 'Config\.BatchPlotMarginMm\s*=\s*MarginMm'
Assert-Match 'batch plot dialog saves file name mode to config' $dialogs 'Config\.BatchPlotFileNameMode\s*=\s*FileNameMode'
Assert-Match 'batch plot dialog saves sort mode to config' $dialogs 'Config\.BatchPlotSortMode\s*=\s*SortMode'
Assert-Match 'batch plot dialog saves reverse order to config' $dialogs 'Config\.BatchPlotSortReverse\s*=\s*ReverseOrder'
Assert-Match 'batch plot dialog has page margin label' $dialogs '\\u9875\\u8FB9\\u8DDD'
Assert-Match 'batch plot dialog labels margin as mm' $dialogs 'mm'
Assert-Match 'batch plot dialog parses margin text as double' $dialogs 'double\.TryParse\(txtMargin\.Text\.Trim\(\)'
Assert-Match 'batch plot dialog has file name mode label' $dialogs '\\u6587\\u4EF6\\u540D'
Assert-Match 'batch plot dialog has file name mode combo' $dialogs 'cmbFileNameMode'
Assert-Match 'batch plot dialog has sort mode combo' $dialogs 'cmbSortMode'
Assert-Match 'batch plot dialog supports selection order sort mode' $dialogs 'AddFileNameMode\(cmbSortMode,\s*"SelectionOrder",\s*"\\u9009\\u62E9\\u987A\\u5E8F"\)'
Assert-Match 'batch plot dialog has sort forward radio' $dialogs 'rbSortForward'
Assert-Match 'batch plot dialog has sort reverse radio' $dialogs 'rbSortReverse'
Assert-Match 'batch plot dialog places sort direction under plot toggles' $dialogs 'rbSortForward\.Left\s*=\s*430;\s*rbSortForward\.Top\s*=\s*170[\s\S]*rbSortReverse\.Left\s*=\s*500;\s*rbSortReverse\.Top\s*=\s*170'
Assert-NotMatch 'batch plot dialog does not show duplicate lower summary' $dialogs '\\u6458\\u8981'
Assert-NotMatch 'batch plot dialog top summary is not ellipsized' $dialogs 'lblInfo\.AutoEllipsis\s*=\s*true'
Assert-Match 'batch plot dialog has preflight list view' $dialogs 'new\s+ListView\s*\('
Assert-Match 'batch plot dialog uses compact left column output directory' $dialogs 'txtDir\.Left\s*=\s*96;\s*txtDir\.Top\s*=\s*44;\s*txtDir\.Width\s*=\s*230'
Assert-Match 'batch plot dialog keeps output directory tooltip alive' $dialogs 'readonly\s+ToolTip\s+outputDirectoryToolTip\s*=\s*new\s+ToolTip\(\);[\s\S]*outputDirectoryToolTip\.SetToolTip\(txtDir,\s*outputDirectory\);'
Assert-Match 'batch plot dialog uses balanced first row paper' $dialogs 'cmbPaper\.Left\s*=\s*430;\s*cmbPaper\.Top\s*=\s*44;\s*cmbPaper\.Width\s*=\s*78'
Assert-Match 'batch plot dialog uses balanced first row margin' $dialogs 'txtMargin\.Left\s*=\s*590;\s*txtMargin\.Top\s*=\s*44;\s*txtMargin\.Width\s*=\s*32'
Assert-Match 'batch plot dialog keeps margin unit comfortably inside window' $dialogs 'lblMarginUnit\.Left\s*=\s*626;\s*lblMarginUnit\.Top\s*=\s*48'
Assert-Match 'batch plot dialog right labels align as a compact group' $dialogs 'lblPaper\.Left\s*=\s*360;\s*lblPaper\.Top\s*=\s*48;\s*lblPaper\.AutoSize\s*=\s*true[\s\S]*lblFileName\.Left\s*=\s*360;\s*lblFileName\.Top\s*=\s*82;\s*lblFileName\.AutoSize\s*=\s*true[\s\S]*lblSortMode\.Left\s*=\s*360;\s*lblSortMode\.Top\s*=\s*116;\s*lblSortMode\.AutoSize\s*=\s*true'
Assert-Match 'batch plot dialog uses compact device and balanced filename' $dialogs 'cmbDevice\.Left\s*=\s*96;\s*cmbDevice\.Top\s*=\s*78;\s*cmbDevice\.Width\s*=\s*230[\s\S]*cmbFileNameMode\.Left\s*=\s*430;\s*cmbFileNameMode\.Top\s*=\s*78;\s*cmbFileNameMode\.Width\s*=\s*194'
Assert-Match 'batch plot dialog uses compact style balanced sort and toggles' $dialogs 'cmbStyle\.Left\s*=\s*96;\s*cmbStyle\.Top\s*=\s*112;\s*cmbStyle\.Width\s*=\s*230[\s\S]*cmbSortMode\.Left\s*=\s*430;\s*cmbSortMode\.Top\s*=\s*112;\s*cmbSortMode\.Width\s*=\s*194[\s\S]*chkRotate\.Left\s*=\s*430;\s*chkRotate\.Top\s*=\s*146'
Assert-Match 'batch plot dialog keeps filename dropdown visually white' $dialogs 'cmbFileNameMode\.DropDownStyle\s*=\s*ComboBoxStyle\.DropDown'
Assert-Match 'batch plot dialog keeps sort dropdown visually white' $dialogs 'cmbSortMode\.DropDownStyle\s*=\s*ComboBoxStyle\.DropDown'
Assert-Match 'batch plot dialog places preflight list compact high and wide' $dialogs 'preflightList\.Left\s*=\s*16;\s*preflightList\.Top\s*=\s*194;\s*preflightList\.Width\s*=\s*648;\s*preflightList\.Height\s*=\s*298'
Assert-Match 'batch plot dialog disables scrolling after dpi scaling' $dialogs 'DpiUtil\.Apply\(this\);\s*AutoScroll\s*=\s*false;'
Assert-Match 'batch plot dialog has live preflight refresh helper' $dialogs 'RefreshBatchPlotPreflight'
Assert-Match 'batch plot dialog refreshes on device change' $dialogs 'cmbDevice\.TextChanged\s*\+='
Assert-Match 'batch plot dialog refreshes on filename mode change' $dialogs 'cmbFileNameMode\.SelectedIndexChanged\s*\+='
Assert-Match 'batch plot dialog has sort rule note label' $dialogs 'lblSortRule'
Assert-Match 'batch plot dialog places sort rule in left blank row' $dialogs 'lblSortRule\.Left\s*=\s*16;\s*lblSortRule\.Top\s*=\s*146;\s*lblSortRule\.Width\s*=\s*388;\s*lblSortRule\.Height\s*=\s*20'
Assert-Match 'batch plot dialog explains position sort rule' $dialogs '\\u4ECE\\u4E0A\\u5230\\u4E0B[\s\S]*\\u4ECE\\u5DE6\\u5230\\u53F3'
Assert-Match 'batch plot dialog explains sheet number sort rule' $dialogs '\\u6309\\u56FE\\u53F7[\s\S]*\\u56FE\\u540D[\s\S]*\\u4F4D\\u7F6E'
Assert-Match 'batch plot dialog explains selection order sort rule' $dialogs '\\u6309\\u9009\\u62E9\\u56FE\\u6846\\u7684\\u5148\\u540E\\u987A\\u5E8F'
Assert-Match 'batch plot dialog refreshes on sort mode change' $dialogs 'cmbSortMode\.SelectedIndexChanged\s*\+='
Assert-Match 'batch plot dialog passes sort mode and direction into refresh' $dialogs 'RefreshBatchPlotPreflight\(lblInfo,\s*lblSortRule,\s*preflightList,\s*preflightRows,\s*frameBlockName,\s*frameCount,\s*drawingName,\s*cmbDevice\.Text,\s*GetDialogSelectedFileNameMode\(cmbFileNameMode\),\s*GetDialogSelectedFileNameMode\(cmbSortMode\),\s*rbSortReverse\.Checked\)'
Assert-Match 'batch plot dialog refresh helper accepts sort rule label mode and direction' $dialogs 'RefreshBatchPlotPreflight\(Label\s+summary,\s*Label\s+sortRule,\s*ListView\s+list,\s*List<BatchPlotPreflightRow>\s+rows,[\s\S]*string\s+fileNameMode,\s*string\s+sortMode,\s*bool\s+reverseOrder\)'
Assert-Match 'batch plot dialog sorts preflight rows by selected mode' $dialogs 'SortDialogBatchPlotPreflightRows\(rows,\s*sortMode\)'
Assert-Match 'batch plot dialog reverses preflight rows when requested' $dialogs 'if\s*\(reverseOrder\)\s*rows\.Reverse\(\)'
Assert-Match 'batch plot dialog rebuilds preflight list on refresh' $dialogs 'RebuildDialogBatchPlotPreflightList\(list,\s*rows\)'
Assert-Match 'batch plot dialog builds live output filename' $dialogs 'BuildDialogBatchPlotOutputFileName'
Assert-Match 'batch plot dialog supports sheet number name filename mode' $dialogs 'SheetNumberName'
Assert-Match 'batch plot dialog summary can show pdf output' $dialogs '\\u8F93\\u51FA\\uFF1APDF'
Assert-Match 'batch plot dialog summary can show printer output' $dialogs '\\u8F93\\u51FA\\uFF1A\\u6253\\u5370\\u673A'
Assert-Match 'batch plot dialog does not treat adobe pdf as file output' $dialogs 'ADOBE PDF'
Assert-Match 'batch plot dialog does not treat pdf24 as file output' $dialogs 'PDF24'
Assert-Match 'batch plot dialog does not treat microsoft print to pdf as file output' $dialogs 'MICROSOFT PRINT TO PDF'
Assert-Match 'batch plot dialog adds preflight index column' $dialogs 'Columns\.Add\("\\u5E8F\\u53F7"'
Assert-Match 'batch plot dialog widens sheet number column' $dialogs 'Columns\.Add\("\\u56FE\\u53F7",\s*80\)'
Assert-Match 'batch plot dialog widens sheet name column' $dialogs 'Columns\.Add\("\\u56FE\\u540D",\s*170\)'
Assert-Match 'batch plot dialog sets size column width' $dialogs 'Columns\.Add\("\\u5C3A\\u5BF8",\s*95\)'
Assert-Match 'batch plot dialog sets orientation column width' $dialogs 'Columns\.Add\("\\u65B9\\u5411",\s*55\)'
Assert-Match 'batch plot dialog sets target column width' $dialogs 'Columns\.Add\("\\u76EE\\u6807",\s*120\)'
Assert-Match 'batch plot dialog sets status column width' $dialogs 'Columns\.Add\("\\u72B6\\u6001",\s*80\)'
Assert-Match 'batch plot dialog has shorter copy list button' $dialogs 'copyPreflight\.Text\s*=\s*"\\u590D\\u5236\\u5217\\u8868"'
Assert-Match 'batch plot dialog disables copy list button when empty' $dialogs 'copyPreflight\.Enabled\s*=\s*preflightRows\s*!=\s*null\s*&&\s*preflightRows\.Count\s*>\s*0'
Assert-Match 'batch plot dialog explains copy list button' $dialogs 'outputDirectoryToolTip\.SetToolTip\(copyPreflight,\s*"\\u590D\\u5236\\u9884\\u68C0\\u5217\\u8868\\u5230\\u526A\\u8D34\\u677F"\)'
Assert-Match 'batch plot dialog has size mismatch warning' $dialogs '\\u68C0\\u6D4B\\u5230\\u56FE\\u6846\\u5C3A\\u5BF8\\u4E0D\\u4E00\\u81F4'
Assert-Match 'batch plot dialog discovers plot styles helper' $dialogs 'static\s+List<string>\s+GetPlotStyleNames'
Assert-Match 'batch plot dialog reads cad plot style list first' $dialogs 'AddCadPlotStyleNames\(names\)'
Assert-Match 'batch plot dialog uses plot settings validator for styles' $dialogs 'PlotSettingsValidator'
Assert-Match 'batch plot dialog reads current style sheet list' $dialogs 'GetPlotStyleSheetList'
Assert-NotMatch 'batch plot dialog does not recursively scan ctb files' $dialogs 'Directory\.GetFiles\(dir,\s*"\*\.ctb"\)'
Assert-NotMatch 'batch plot dialog does not recursively scan stb files' $dialogs 'Directory\.GetFiles\(dir,\s*"\*\.stb"\)'
Assert-Match 'batch plot dialog includes grayscale fallback style' $dialogs 'grayscale\.ctb'
Assert-Match 'batch plot dialog has ctb plot style filter' $dialogs 'IsCtbPlotStyleName'
Assert-Match 'batch plot dialog filters plot styles to ctb' $dialogs 'EndsWith\("\.ctb"'
Assert-NotMatch 'batch plot dialog should not add stb fallback style' $dialogs 'AddPlotStyleName\(names,\s*"[^"]+\.stb"\)'
Assert-Match 'batch plot dialog keeps style dropdown editable' $dialogs 'cmbStyle\.DropDownStyle\s*=\s*ComboBoxStyle\.DropDown'
Assert-Match 'batch plot dialog places style in left column' $dialogs 'lblStyle\.Left\s*=\s*16;\s*lblStyle\.Top\s*=\s*116'
Assert-Match 'batch plot dialog keeps compact style dropdown in left column' $dialogs 'cmbStyle\.Left\s*=\s*96;\s*cmbStyle\.Top\s*=\s*112;\s*cmbStyle\.Width\s*=\s*230'
Assert-Match 'batch plot dialog top summary has enough height' $dialogs 'lblInfo\.Height\s*=\s*24'
Assert-Match 'batch plot dialog sorts plot styles by user relevance' $dialogs 'SortPlotStyleNames\(names,\s*Config\.BatchPlotStyle\)'
Assert-Match 'batch plot dialog detects built in plot styles' $dialogs 'IsBuiltInPlotStyle'
Assert-Match 'batch plot dialog detects chinese plot style names' $dialogs 'ContainsCjk'
Assert-Match 'batch plot dialog reads installed printers' $dialogs 'PrinterSettings\.InstalledPrinters'
Assert-Match 'batch plot dialog keeps cad pdf device' $dialogs 'DWG To PDF\.pc3'
Assert-Match 'batch plot dialog removes duplicate printer names' $dialogs 'ContainsPrinterName'

Assert-Match 'batch plot command file registers command with pickfirst' $batchPlotCommands '\[CommandMethod\("CT_BATCHPLOT",\s*CommandFlags\.UsePickSet\)\]'
Assert-Match 'batch plot command method exists' $batchPlotCommands 'public\s+void\s+BatchPlot\s*\('
Assert-Match 'batch plot command prompts for frame block template' $batchPlotCommands ('PromptEntityOptions\("\\n' + [regex]::Escape($frameBlockTemplatePrompt) + '"\)')
Assert-Match 'batch plot command restricts template to block references' $batchPlotCommands 'AddAllowedClass\(typeof\(BlockReference\),\s*true\)'
Assert-Match 'batch plot command prompts for frame scope' $batchPlotCommands ('MessageForAdding\s*=\s*"\\n' + [regex]::Escape($batchPlotScopePrompt) + '"')
Assert-Match 'batch plot command filters scope to inserts' $batchPlotCommands 'new\s+TypedValue\(0,\s*"INSERT"\)'
Assert-Match 'batch plot command has frame block key helper' $batchPlotCommands 'GetBatchPlotFrameBlockKey'
Assert-Match 'batch plot command has frame block collection helper' $batchPlotCommands 'CollectBatchPlotFrameBlockIds'
Assert-Match 'batch plot command has frame block comparison helper' $batchPlotCommands 'IsBatchPlotSameFrameBlock'
Assert-Order 'batch plot resolves template before collecting frames' $batchPlotCommands 'GetBatchPlotFrameBlockKey' 'CollectPlotFrames'
Assert-Match 'batch plot command opens settings dialog' $batchPlotCommands 'new\s+BatchPlotDialog\('
Assert-Match 'batch plot command passes frame block name preflight rows and drawing name to dialog' $batchPlotCommands 'new\s+BatchPlotDialog\(frames\.Count,\s*frameBlockKey\.DisplayName,\s*preflightRows,\s*outputDirectory,\s*drawingName\)'
Assert-Match 'batch plot command builds preflight rows from position order' $batchPlotCommands 'SortPlotFrames\(frames,\s*"Position"\);\s*List<BatchPlotPreflightRow>\s+preflightRows\s*=\s*BuildBatchPlotPreflightRows'
Assert-Match 'batch plot command builds preflight rows helper' $batchPlotCommands 'BuildBatchPlotPreflightRows'
Assert-Match 'batch plot command computes preflight size mismatch threshold' $batchPlotCommands 'IsBatchPlotFrameSizeMismatched'
Assert-Match 'batch plot command preflight uses output path helper' $batchPlotCommands 'Path\.GetFileName\(BuildBatchPlotOutputPath'
Assert-Match 'batch plot command preflight uses printer target text' $batchPlotCommands '\\u53D1\\u9001\\u5230\\u6253\\u5370\\u673A'
Assert-Match 'batch plot frame model exists' $batchPlotCommands 'class\s+BatchPlotFrame'
Assert-Match 'batch plot settings model exists' $batchPlotCommands 'class\s+BatchPlotSettings'
Assert-Match 'batch plot settings carries margin percent' $batchPlotCommands 'public\s+double\s+MarginPercent'
Assert-Match 'batch plot settings carries margin mm' $batchPlotCommands 'public\s+double\s+MarginMm'
Assert-Match 'batch plot settings carries file name mode' $batchPlotCommands 'public\s+string\s+FileNameMode'
Assert-Match 'batch plot settings carries sort mode' $batchPlotCommands 'public\s+string\s+SortMode'
Assert-Match 'batch plot settings carries reverse order' $batchPlotCommands 'public\s+bool\s+ReverseOrder'
Assert-Match 'batch plot frame carries sheet number' $batchPlotCommands 'public\s+string\s+SheetNumber'
Assert-Match 'batch plot frame carries sheet name' $batchPlotCommands 'public\s+string\s+SheetName'
Assert-Match 'batch plot frame carries selection order' $batchPlotCommands 'public\s+int\s+SelectionOrder'
Assert-Match 'batch plot reads margin percent from dialog' $batchPlotCommands 'settings\.MarginPercent\s*=\s*dlg\.MarginPercent'
Assert-Match 'batch plot reads margin mm from dialog' $batchPlotCommands 'settings\.MarginMm\s*=\s*dlg\.MarginMm'
Assert-Match 'batch plot reads file name mode from dialog' $batchPlotCommands 'settings\.FileNameMode\s*=\s*dlg\.FileNameMode'
Assert-Match 'batch plot reads sort mode from dialog' $batchPlotCommands 'settings\.SortMode\s*=\s*dlg\.SortMode'
Assert-Match 'batch plot reads reverse order from dialog' $batchPlotCommands 'settings\.ReverseOrder\s*=\s*dlg\.ReverseOrder'
Assert-Match 'batch plot reverses final frame order when requested' $batchPlotCommands 'if\s*\(dlg\.ReverseOrder\)\s*frames\.Reverse\(\)'
Assert-Match 'batch plot uses gstar batch command path for multiple frames' $batchPlotCommands 'RunGstarBatchPlotWithPlotCommand\(frames,\s*settings,\s*outputToFile\)'
Assert-Match 'batch plot collects geometric extents' $batchPlotCommands 'GeometricExtents'
Assert-Match 'batch plot reads title block attributes' $batchPlotCommands 'ReadBatchPlotTitleBlockAttributes'
Assert-Match 'batch plot detects sheet number attribute tags' $batchPlotCommands 'IsBatchPlotSheetNumberTag'
Assert-Match 'batch plot detects sheet name attribute tags' $batchPlotCommands 'IsBatchPlotSheetNameTag'
Assert-Match 'batch plot sorts frames helper exists' $batchPlotCommands 'SortPlotFrames'
Assert-Match 'batch plot position sort checks left column first' $batchPlotCommands 'int\s+byLeft\s*=\s*a\.MinX\.CompareTo\(b\.MinX\);\s*if\s*\(byLeft\s*!=\s*0\)\s*return\s+byLeft'
Assert-Match 'batch plot position sort checks top to bottom inside same column' $batchPlotCommands 'return\s+b\.MaxY\.CompareTo\(a\.MaxY\)'
Assert-Match 'batch plot supports sheet number sorting' $batchPlotCommands 'SortPlotFramesBySheetNumber'
Assert-Match 'batch plot supports selection order sorting' $batchPlotCommands 'SortPlotFramesBySelectionOrder'
Assert-Match 'batch plot expands frame helper exists' $batchPlotCommands 'static\s+BatchPlotFrame\s+ExpandBatchPlotFrame'
Assert-Match 'batch plot expands frame by millimeters helper exists' $batchPlotCommands 'static\s+BatchPlotFrame\s+ExpandBatchPlotFrameByMarginMm'
Assert-Match 'batch plot resolves paper size helper exists' $batchPlotCommands 'static\s+bool\s+TryGetBatchPlotPaperSizeMm'
Assert-Match 'batch plot clamps negative margin to zero' $batchPlotCommands 'Math\.Max\(0'
Assert-NotMatch 'batch plot margin should not add hidden safety allowance' $batchPlotCommands 'BatchPlotMarginSafetyMm'
Assert-Match 'batch plot mm margin uses paper content scale' $batchPlotCommands 'double\s+contentScale\s*=\s*Math\.Min\(contentWidth\s*/\s*frame\.Width,\s*contentHeight\s*/\s*frame\.Height\)'
Assert-Match 'batch plot mm margin expands to paper aspect window width' $batchPlotCommands 'double\s+targetWidth\s*=\s*usableWidth\s*/\s*contentScale'
Assert-Match 'batch plot mm margin expands to paper aspect window height' $batchPlotCommands 'double\s+targetHeight\s*=\s*usableHeight\s*/\s*contentScale'
Assert-Match 'batch plot output path helper exists' $batchPlotCommands 'BuildBatchPlotOutputPath'
Assert-Match 'batch plot output path accepts file name mode and frame metadata' $batchPlotCommands 'BuildBatchPlotOutputPath\(string\s+outputDirectory,\s*string\s+drawingName,\s*int\s+index,\s*string\s+fileNameMode,\s*BatchPlotFrame\s+frame\)'
Assert-Match 'batch plot supports dash index filename mode' $batchPlotCommands 'DrawingDashIndex'
Assert-Match 'batch plot supports underscore index filename mode' $batchPlotCommands 'DrawingUnderscoreIndex'
Assert-Match 'batch plot supports index only filename mode' $batchPlotCommands 'IndexOnly'
Assert-Match 'batch plot supports sheet number name filename mode' $batchPlotCommands 'SheetNumberName'
Assert-Match 'batch plot sanitizes output filename stem' $batchPlotCommands 'SanitizeBatchPlotFileNameStem'
Assert-Match 'batch plot output uses three digit index' $batchPlotCommands 'index\.ToString\("D3"'
Assert-Match 'batch plot detects duplicate output filenames' $batchPlotCommands 'MarkDuplicateBatchPlotTargets'
Assert-Match 'batch plot preflight marks normal status' $batchPlotCommands '\\u6B63\\u5E38'
Assert-Match 'batch plot preflight marks duplicate filename status' $batchPlotCommands '\\u6587\\u4EF6\\u540D\\u91CD\\u590D'
Assert-Match 'batch plot preflight marks size mismatch status' $batchPlotCommands '\\u5C3A\\u5BF8\\u5F02\\u5E38'
Assert-Match 'batch plot rejects drive-only output directory' $batchPlotCommands 'IsDriveOnlyPath'
Assert-Match 'batch plot validates pdf file header' $batchPlotCommands 'IsValidPdfFile'
Assert-Match 'batch plot requires pdf header marker' $batchPlotCommands '%PDF'
Assert-Match 'batch plot rejects tiny blank pdf output' $batchPlotCommands 'MinimumValidPdfBytes'
Assert-Match 'batch plot uses plot settings type' $batchPlotCommands 'PlotSettings'
Assert-Match 'batch plot copies settings from current layout' $batchPlotCommands 'CopyFromCurrentLayout'
Assert-Match 'batch plot calls CopyFrom on plot settings' $batchPlotCommands 'CopyFrom'
Assert-Match 'batch plot uses plot info type' $batchPlotCommands 'PlotInfo'
Assert-Match 'batch plot uses plot factory type' $batchPlotCommands 'PlotFactory'
Assert-Match 'batch plot sets window area' $batchPlotCommands 'SetPlotWindowArea'
Assert-Match 'api plot window uses mm expanded frame' $batchPlotCommands 'CreateExtents2d\(ExpandBatchPlotFrameByMarginMm\(frame,\s*settings\)\)'
Assert-Match 'batch plot resolves configured device name' $batchPlotCommands 'ResolvePlotDeviceName'
Assert-Match 'batch plot logs available devices' $batchPlotCommands 'AvailableDevices'
Assert-Match 'batch plot sets device again with resolved media' $batchPlotCommands 'SetPlotConfigurationName"\s*,\s*plotSettings\s*,\s*deviceName\s*,\s*mediaName'
if ($batchPlotCommands -match 'SetPlotConfigurationName"\s*,\s*plotSettings\s*,\s*deviceName\s*,\s*null') { throw 'batch plot should not set plot configuration with null media name' }
Assert-Match 'batch plot binds gstar device with non-empty media candidate' $batchPlotCommands 'TrySetGstarPlotConfigurationWithMediaCandidate'
Assert-Match 'batch plot tries full gstar a3 media name before listing media' $batchPlotCommands 'ISO A3 \(420\.00 x 297\.00'
Assert-Match 'batch plot falls back to gstar command paper name' $batchPlotCommands 'GetGstarPlotCommandFallbackMediaName'
Assert-Match 'batch plot command fallback uses full bleed a3 paper name' $batchPlotCommands 'ISO full bleed A3 \(420\.00 x 297\.00'
Assert-Match 'batch plot only expands gstar paper names for dwg to pdf device' $batchPlotCommands 'ShouldUseGstarExpandedPaperName'
Assert-Match 'batch plot recognizes dwg to pdf pc3 for expanded paper names' $batchPlotCommands 'DWG To PDF'
Assert-Match 'batch plot converts gstar api media to command input' $batchPlotCommands 'ToGstarPlotCommandMediaInput'
$gstarPaperLookup = [regex]::Match($batchPlotCommands, 'static\s+string\s+ResolveGstarPlotCommandPaperName[\s\S]*?static\s+bool\s+TrySetGstarPlotConfigurationWithMediaCandidate').Value
Assert-NotMatch 'gstar paper lookup does not return raw fallback after api failure' $gstarPaperLookup 'return fallback;'
Assert-Match 'gstar paper lookup does not return raw matched media directly' $gstarPaperLookup 'ToGstarPlotCommandMediaInput\(matched,\s*fallback,\s*commandFallback,\s*expandPaperName\)'
Assert-Match 'batch plot scales to fit' $batchPlotCommands 'SetStdScaleType'
Assert-Match 'batch plot centers plot' $batchPlotCommands 'SetPlotCentered'
Assert-Match 'api plot respects center setting directly' $batchPlotCommands 'SetPlotCentered",\s*plotSettings,\s*settings\.CenterPlot'
Assert-Match 'batch plot sets style sheet' $batchPlotCommands 'SetCurrentStyleSheet'
Assert-Match 'batch plot begins output document' $batchPlotCommands 'BeginDocument'
Assert-Match 'batch plot treats begin generate graphics as optional' $batchPlotCommands 'SafeInvoke\(engine,\s*"BeginGenerateGraphics"'
Assert-Match 'batch plot treats end generate graphics as optional' $batchPlotCommands 'SafeInvoke\(engine,\s*"EndGenerateGraphics"'
Assert-Match 'batch plot deletes existing pdf before overwrite' $batchPlotCommands 'File\.Delete\(outputPath\)'
Assert-Match 'batch plot detects pdf devices' $batchPlotCommands 'IsPdfPlotDevice'
Assert-Match 'batch plot treats dwg to pdf pc3 as file output' $batchPlotCommands 'DWG To PDF'
Assert-Match 'batch plot does not treat adobe pdf as file output' $batchPlotCommands 'ADOBE PDF'
Assert-Match 'batch plot does not treat pdf24 as file output' $batchPlotCommands 'PDF24'
Assert-Match 'batch plot does not treat microsoft print to pdf as file output' $batchPlotCommands 'MICROSOFT PRINT TO PDF'
Assert-Match 'batch plot does not use broad pdf contains match' $batchPlotCommands 'IndexOf\("PDF".*\)\s*<\s*0'
Assert-Match 'batch plot sends physical printers without output path' $batchPlotCommands 'PlotFrameToDevice'
Assert-Match 'batch plot uses plot to file flag from output path' $batchPlotCommands 'bool\s+plotToFile\s*=\s*!string\.IsNullOrEmpty\(outputPath\)'
Assert-Match 'gstarcad pdf plot uses plot command path' $batchPlotCommands '#if\s+GSTARCAD\s+return\s+PlotFrameToPdfWithPlotCommand\(frame,\s*settings,\s*outputPath\);'
Assert-Match 'gstarcad device plot uses plot command path' $batchPlotCommands '#if\s+GSTARCAD\s+return\s+PlotFrameToDeviceWithPlotCommand\(frame,\s*settings\);'
Assert-Match 'gstarcad plot command pdf helper exists' $batchPlotCommands 'static\s+bool\s+PlotFrameToPdfWithPlotCommand\s*\('
Assert-Match 'gstarcad plot command printer helper exists' $batchPlotCommands 'static\s+bool\s+PlotFrameToDeviceWithPlotCommand\s*\('
Assert-Match 'gstarcad batch plot helper exists' $batchPlotCommands 'static\s+int\s+RunGstarBatchPlotWithPlotCommand\s*\('
Assert-Match 'gstarcad batch plot resolves settings once' $batchPlotCommands 'BatchPlotSettings\s+resolvedSettings\s*=\s*ResolveGstarPlotCommandSettings\(settings\)'
Assert-NotMatch 'gstarcad batch plot does not send one combined command' $batchPlotCommands 'SendGstarPlotCommand\(string\.Join\(Environment\.NewLine,\s*commands\.ToArray\(\)\)\)'
Assert-Match 'gstarcad batch plot submits each sheet independently' $batchPlotCommands 'SendGstarPlotCommand\(BuildGstarPlotCommand\(frames\[i\],\s*resolvedSettings,\s*outputPath\)\)'
Assert-Match 'gstarcad plot command resolves settings before build' $batchPlotCommands 'ResolveGstarPlotCommandSettings'
Assert-Match 'gstarcad plot command uses resolved pdf settings' $batchPlotCommands 'BuildGstarPlotCommand\(frame,\s*resolvedSettings,\s*outputPath\)'
Assert-Match 'gstarcad plot command uses resolved printer settings' $batchPlotCommands 'BuildGstarPlotCommand\(frame,\s*resolvedSettings,\s*null\)'
Assert-Match 'gstarcad plot command builds command text' $batchPlotCommands 'BuildGstarPlotCommand'
Assert-Match 'gstarcad plot command sends command text' $batchPlotCommands 'SendStringToExecute'
Assert-Literal 'gstarcad plot command uses adaptive lisp plot wrapper' $batchPlotCommands '"(ct-plot (list "'
Assert-Match 'gstarcad plot command checks functions with atoms family' $batchPlotCommands 'atoms-family'
Assert-Match 'gstarcad plot command prefers vl-cmdf when available' $batchPlotCommands '\\"VL-CMDF\\"'
Assert-Match 'gstarcad plot command avoids missing command-s errors' $batchPlotCommands '\\"COMMAND-S\\"'
Assert-NotMatch 'gstarcad plot command does not use unsupported fboundp' $batchPlotCommands 'fboundp'
Assert-NotMatch 'gstarcad plot command no longer calls command-s directly' $batchPlotCommands 'return\s+"\(command-s "'
Assert-NotMatch 'gstarcad plot command no longer calls command directly per plot' $batchPlotCommands 'return\s+"\(command "'
Assert-Match 'gstarcad plot command uses command line plot' $batchPlotCommands '"_\.-PLOT"'
Assert-Match 'gstarcad plot command uses detailed plot mode' $batchPlotCommands '"Y"'
Assert-Match 'gstarcad plot command uses model layout' $batchPlotCommands '"Model"'
Assert-Match 'gstarcad plot command uses window option' $batchPlotCommands '"W"'
Assert-Match 'gstarcad plot command uses mm expanded frame' $batchPlotCommands 'BatchPlotFrame\s+plotFrame\s*=\s*ExpandBatchPlotFrameByMarginMm\(frame,\s*settings\)'
Assert-Match 'gstarcad plot lower left uses expanded frame' $batchPlotCommands 'FormatGstarPlotPoint\(plotFrame\.MinX,\s*plotFrame\.MinY\)'
Assert-Match 'gstarcad plot upper right uses expanded frame' $batchPlotCommands 'FormatGstarPlotPoint\(plotFrame\.MaxX,\s*plotFrame\.MaxY\)'
Assert-Match 'gstarcad plot command computes fit scale with margin' $batchPlotCommands 'BuildGstarPlotScaleInput\(frame,\s*settings\)'
Assert-Match 'gstarcad plot command no longer always uses fit scale' $batchPlotCommands 'inputs\.Add\(QuoteGstarLispString\(BuildGstarPlotScaleInput\(frame,\s*settings\)\)\)'
Assert-Match 'gstarcad plot command includes device name' $batchPlotCommands 'settings\.DeviceName'
Assert-Match 'gstarcad plot command includes paper name' $batchPlotCommands 'settings\.PaperName'
Assert-Match 'gstarcad plot command includes style sheet' $batchPlotCommands 'settings\.PlotStyle'
Assert-Match 'gstarcad plot command respects center setting directly' $batchPlotCommands 'settings\.CenterPlot\s*\?\s*"C"\s*:\s*"0,0"'
Assert-Match 'gstarcad plot command sends wireframe shade setting after lineweights' $batchPlotCommands 'GetGstarPlotShadeInput\(\)'
Assert-Match 'gstarcad plot command includes pdf output path' $batchPlotCommands 'outputPath'
Assert-Match 'gstarcad plot command wraps send in a quiet progn' $batchPlotCommands 'quietCommandText\s*=\s*"\(progn'
Assert-Match 'gstarcad plot command suppresses command echo' $batchPlotCommands 'setvar \\"CMDECHO\\" 0[\s\S]*setvar \\"CMDECHO\\" 1'
Assert-Match 'gstarcad plot command saves background plot setting' $batchPlotCommands 'getvar \\"BACKGROUNDPLOT\\"'
Assert-Match 'gstarcad plot command disables background plot while plotting' $batchPlotCommands 'setvar \\"BACKGROUNDPLOT\\" 0'
Assert-Match 'gstarcad plot command restores background plot setting' $batchPlotCommands 'setvar \\"BACKGROUNDPLOT\\" _ctOldBgPlot'
Assert-Match 'gstarcad plot command suppresses lisp return value noise' $batchPlotCommands '\(princ\)\)\\n"'
Assert-Match 'gstarcad plot command sends lisp input without command line echo' $batchPlotCommands 'SendStringToExecute\(quietCommandText,\s*true,\s*false,\s*false\)'
$gstarCommandBuilder = [regex]::Match($batchPlotCommands, 'static\s+string\s+BuildGstarPlotCommand[\s\S]*?static\s+BatchPlotSettings\s+ResolveGstarPlotCommandSettings').Value
Assert-NotMatch 'gstarcad plot command does not send extra no answers before pdf filename' $gstarCommandBuilder 'GetGstarPlotShadeInput\(\)\)\);\s*inputs\.Add\(QuoteGstarLispString\("N"\)\);\s*inputs\.Add\(QuoteGstarLispString\("N"\)\);\s*if \(!string\.IsNullOrEmpty\(outputPath\)\)'
Assert-Match 'gstarcad plot command confirms continue printing after page setup save answer' $gstarCommandBuilder 'inputs\.Add\(QuoteGstarLispString\("N"\)\);\s*inputs\.Add\(QuoteGstarLispString\("Y"\)\);'
Assert-Match 'gstarcad plot command does not print physical printers to file' $gstarCommandBuilder 'else\s*\{\s*inputs\.Add\(QuoteGstarLispString\("N"\)\);\s*inputs\.Add\(QuoteGstarLispString\("N"\)\);\s*inputs\.Add\(QuoteGstarLispString\("Y"\)\);'
Assert-Match 'gstarcad plot command escapes command arguments' $batchPlotCommands 'QuoteGstarLispString'
Assert-Match 'gstarcad plot command strips argument newlines' $batchPlotCommands 'Replace\("\\r",\s*" "\)\.Replace\("\\n",\s*" "\)'
Assert-Literal 'gstarcad plot command escapes lisp backslashes' $batchPlotCommands 'Replace("\\", "\\\\")'
Assert-Literal 'gstarcad plot command escapes lisp quotes' $batchPlotCommands 'Replace("\"", "\\\"")'
Assert-Match 'gstarcad plot command reads plot device list' $batchPlotCommands 'GetPlotDeviceList'
Assert-Match 'gstarcad plot command reads canonical media list' $batchPlotCommands 'GetCanonicalMediaNameList'
Assert-Match 'gstarcad plot command resolves paper name' $batchPlotCommands 'ResolveGstarPlotCommandPaperName'
Assert-Match 'gstarcad plot command matches configured paper first' $batchPlotCommands 'MatchGstarPlotMediaName\(enumerable,\s*fallback\)'
Assert-Match 'gstarcad plot command retries no-argument device list' $batchPlotCommands 'catch\s*\(MissingMethodException\)'
Assert-Match 'gstarcad plot command matches pc3 base name' $batchPlotCommands 'Path\.GetFileNameWithoutExtension'
Assert-Match 'gstarcad plot command matches configured device first' $batchPlotCommands 'MatchGstarPlotDeviceName\(enumerable,\s*fallback\)'
Assert-Match 'gstarcad plot command logs available media' $batchPlotCommands 'AvailableMedia'
Assert-NotMatch 'gstarcad plot command does not replace pc3 with driver display name' $batchPlotCommands 'DWG to PDF 1\.0'
Assert-NotMatch 'gstarcad plot command does not directly write full command text by default' $batchPlotCommands '\bLog\("BatchPlot -PLOT command text'
Assert-NotMatch 'gstarcad plot command does not directly write successful submissions by default' $batchPlotCommands '\bLog\("BatchPlot -PLOT command submitted'
Assert-Match 'gstarcad plot command can be debug logged explicitly' $batchPlotCommands 'DebugBatchPlotLog\('
Assert-Match 'batch plot reports sent to printer' $batchPlotCommands '\\u5DF2\\u53D1\\u9001\\u5230\\u6253\\u5370\\u673A'
Assert-Match 'batch plot logs device details on failure' $batchPlotCommands 'Device='
Assert-Match 'batch plot resolves types from candidates' $batchPlotCommands 'RequiredTypeFromCandidates'
Assert-Match 'batch plot resolves optional types from candidates' $batchPlotCommands 'OptionalTypeFromCandidates'
Assert-Match 'batch plot treats matching policy as optional' $batchPlotCommands 'if\s*\(MatchingPolicyEnum\s*!=\s*null\)'
Assert-Match 'batch plot resolves extents type from candidates' $batchPlotCommands 'Extents2dType\s*=\s*RequiredTypeFromCandidates'
Assert-Match 'batch plot has gstar database extents fallback' $batchPlotCommands 'GrxCAD\.DatabaseServices\.Extents2d'
Assert-Match 'batch plot has gstar extents fallback' $batchPlotCommands 'OdGeExtents2d|GcGeExtents2d'
Assert-Match 'batch plot resolves point type from candidates' $batchPlotCommands 'Point2dType\s*=\s*RequiredTypeFromCandidates'
Assert-Match 'batch plot has gstar point fallback' $batchPlotCommands 'OdGePoint2d|GcGePoint2d'
Assert-Match 'batch plot creates point helper' $batchPlotCommands 'object\s+CreatePoint2d\s*\('
Assert-Match 'batch plot tries extents numeric constructor first' $batchPlotCommands 'new object\[\]\s*\{\s*frame\.MinX\s*,\s*frame\.MinY\s*,\s*frame\.MaxX\s*,\s*frame\.MaxY\s*\}'
Assert-Match 'batch plot falls back to point constructor' $batchPlotCommands 'CreatePoint2d\(frame\.MinX,\s*frame\.MinY\)'
Assert-Match 'batch plot falls back to point constructor second point' $batchPlotCommands 'CreatePoint2d\(frame\.MaxX,\s*frame\.MaxY\)'
Assert-Match 'batch plot has gstar plot info fallback' $batchPlotCommands 'GcPlPlotInfo'
Assert-Match 'batch plot has gstar plot factory fallback' $batchPlotCommands 'GcPlPlotFactory'
Assert-Match 'batch plot invokes no-argument plot device list when needed' $batchPlotCommands 'InvokeOptionalArgumentList\(validator,\s*"GetPlotDeviceList"'

$projectFiles = @(
    'CadToolkit\src\CadToolkit\CadToolkit.AutoCAD.csproj',
    'CadToolkit\src\CadToolkit\CadToolkit.GstarCAD.csproj',
    'CadToolkit\src\CadToolkit\CadToolkit.ZWCAD.csproj'
)
foreach ($projectFile in $projectFiles) {
    $project = Get-Content -Encoding UTF8 (Join-Path $repo $projectFile) -Raw
    Assert-Match "$projectFile includes batch plot commands" $project '<Compile Include="BatchPlotCommands\.cs" />'
}

Assert-Literal 'readme documents batch plot label' $readme $batchPlotLabel
Assert-Literal 'readme documents batch plot command' $readme 'CT_BATCHPLOT'
Assert-Literal 'manual documents batch plot label' $manual $batchPlotLabel
Assert-Literal 'manual documents batch plot command' $manual 'CT_BATCHPLOT'
