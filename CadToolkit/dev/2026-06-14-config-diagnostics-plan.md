# CadToolkit Config Diagnostics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a conservative CadToolkit configuration diagnostics and repair feature with both `CT_CONFIGCHECK` in CAD and `CadToolkit\tools\check-config.ps1` for terminal checks.

**Architecture:** Add a CAD-independent diagnostics module in `CadToolkit.Core` that reads/writes UTF-8 INI text, reports stable issue codes, and applies only safe repairs. Add a thin CAD command dialog and a PowerShell wrapper that both call the same core logic.

**Tech Stack:** C# .NET Framework 4.8, WinForms, PowerShell 5-compatible scripts, existing CadToolkit PowerShell tests.

---

## File Structure

- Create `CadToolkit/src/CadToolkit.Core/ConfigDiagnostics.cs`
  - Owns diagnostic types, text parsing, issue reporting, safe repair, report formatting, and file backup/write helpers.
- Modify `CadToolkit/src/CadToolkit.Core/CadToolkit.Core.csproj`
  - Compile the new core diagnostics file.
- Create `CadToolkit/src/CadToolkit/ConfigCommands.cs`
  - Adds `CT_CONFIGCHECK` command and WinForms report dialog.
- Modify `CadToolkit/src/CadToolkit/CadToolkit.AutoCAD.csproj`
  - Compile `ConfigCommands.cs`.
- Modify `CadToolkit/src/CadToolkit/CadToolkit.ZWCAD.csproj`
  - Compile `ConfigCommands.cs`.
- Modify `CadToolkit/src/CadToolkit/CadToolkit.GstarCAD.csproj`
  - Compile `ConfigCommands.cs`.
- Create `CadToolkit/tools/check-config.ps1`
  - Terminal entry point for diagnostics and optional repair.
- Modify `CadToolkit/CadToolkit.ini`
  - Add `配置体检=CT_CONFIGCHECK` in `[Commands]`.
- Modify `CadToolkit/CadToolkit.default.ini`
  - Add `配置体检=CT_CONFIGCHECK` in `[Commands]`.
- Modify `CadToolkit/src/CadToolkit.Core/Config.cs`
  - Add the official command to embedded default config and startup command upgrade.
  - Expose `ConfigPath` and `DefaultConfigText` wrappers if needed by diagnostics/command.
- Modify `CadToolkit/README.md`
  - Briefly document the new config check command and script.
- Modify `CadToolkit/CadToolkit使用手册.html`
  - Briefly document the new config check command and script.
- Create `CadToolkit/tests/ConfigDiagnostics.Tests.ps1`
  - Tests core diagnostics, repair, command registration, script parsing, and backup behavior.
- Modify `CadToolkit/tests/DeploymentConfig.Tests.ps1`
  - Assert release package includes `tools\check-config.ps1` if package script starts copying `tools`.
- Modify `.github/workflows/cadtoolkit.yml`
  - Include `tools\check-config.ps1` in release package if missing from packaging.
- Modify `CadToolkit/deploy-local.ps1`
  - Copy `tools\check-config.ps1` into `C:\CadToolkit\tools\check-config.ps1`.

---

### Task 1: Core Diagnostics Types and Project Wiring

**Files:**
- Create: `D:\Documents\GitHub\minis\CadToolkit\src\CadToolkit.Core\ConfigDiagnostics.cs`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\src\CadToolkit.Core\CadToolkit.Core.csproj`
- Test: `D:\Documents\GitHub\minis\CadToolkit\tests\ConfigDiagnostics.Tests.ps1`

- [ ] **Step 1: Write the failing test for exported diagnostics types**

Create `CadToolkit/tests/ConfigDiagnostics.Tests.ps1` with the initial build/type checks:

```powershell
$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$coreProject = Join-Path $repo 'CadToolkit\src\CadToolkit.Core\CadToolkit.Core.csproj'
$coreDll = Join-Path $repo 'CadToolkit\src\CadToolkit.Core\bin\Release\CadToolkit.Core.dll'
$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'

function Assert-NotNull($name, $value) {
    if ($null -eq $value) { throw "$name was null" }
    Write-Host "PASS $name"
}

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) { throw "$name did not find pattern: $pattern" }
    Write-Host "PASS $name"
}

& $msbuild $coreProject /p:Configuration=Release /p:Platform=x64 /t:Rebuild /v:minimal | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'CadToolkit.Core build failed' }

$assembly = [Reflection.Assembly]::LoadFrom($coreDll)
$diagnosticsType = $assembly.GetType('CadToolkit.Core.ConfigDiagnostics', $true)
$severityType = $assembly.GetType('CadToolkit.Core.ConfigDiagnosticSeverity', $true)
$issueType = $assembly.GetType('CadToolkit.Core.ConfigDiagnosticIssue', $true)
$resultType = $assembly.GetType('CadToolkit.Core.ConfigDiagnosticResult', $true)

Assert-NotNull 'config diagnostics type exists' $diagnosticsType
Assert-NotNull 'config diagnostic severity type exists' $severityType
Assert-NotNull 'config diagnostic issue type exists' $issueType
Assert-NotNull 'config diagnostic result type exists' $resultType
Assert-NotNull 'config diagnostics analyze method exists' ($diagnosticsType.GetMethod('Analyze', [Reflection.BindingFlags]'Public, Static'))
Assert-NotNull 'config diagnostics repair method exists' ($diagnosticsType.GetMethod('Repair', [Reflection.BindingFlags]'Public, Static'))
Assert-NotNull 'config diagnostics analyze file method exists' ($diagnosticsType.GetMethod('AnalyzeFile', [Reflection.BindingFlags]'Public, Static'))
Assert-NotNull 'config diagnostics repair file method exists' ($diagnosticsType.GetMethod('RepairFile', [Reflection.BindingFlags]'Public, Static'))

$coreProjectText = Get-Content -Encoding UTF8 $coreProject -Raw
Assert-Contains 'core project compiles config diagnostics' $coreProjectText 'Compile Include="ConfigDiagnostics\.cs"'
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
```

Expected: build succeeds or fails before reflection, then the test fails because `ConfigDiagnostics` does not exist or the project does not compile it.

- [ ] **Step 3: Add minimal diagnostics types**

Create `CadToolkit/src/CadToolkit.Core/ConfigDiagnostics.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CadToolkit.Core
{
    public enum ConfigDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public class ConfigDiagnosticIssue
    {
        public ConfigDiagnosticSeverity Severity;
        public string Code;
        public string Message;
        public int LineNumber;
        public string Section;
        public bool CanFix;
    }

    public class ConfigDiagnosticResult
    {
        public string Path;
        public List<ConfigDiagnosticIssue> Issues = new List<ConfigDiagnosticIssue>();
        public string RepairedText;
        public bool HasChanges;
        public string BackupPath;

        public bool HasErrors
        {
            get
            {
                foreach (var issue in Issues)
                    if (issue.Severity == ConfigDiagnosticSeverity.Error) return true;
                return false;
            }
        }
    }

    public static class ConfigDiagnostics
    {
        public static ConfigDiagnosticResult Analyze(string text, string path)
        {
            return new ConfigDiagnosticResult { Path = path, RepairedText = text ?? "" };
        }

        public static ConfigDiagnosticResult Repair(string text, string path)
        {
            return Analyze(text, path);
        }

        public static ConfigDiagnosticResult AnalyzeFile(string path)
        {
            string text = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
            return Analyze(text, path);
        }

        public static ConfigDiagnosticResult RepairFile(string path)
        {
            string text = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
            var result = Repair(text, path);
            return result;
        }
    }
}
```

Modify `CadToolkit/src/CadToolkit.Core/CadToolkit.Core.csproj`:

```xml
<ItemGroup>
  <Compile Include="Config.cs" />
  <Compile Include="ConfigDiagnostics.cs" />
  <Compile Include="Properties\AssemblyInfo.cs" />
</ItemGroup>
```

- [ ] **Step 4: Run the test to verify it passes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
```

Expected: all initial `PASS` lines appear.

- [ ] **Step 5: Commit**

```powershell
git add CadToolkit\src\CadToolkit.Core\ConfigDiagnostics.cs CadToolkit\src\CadToolkit.Core\CadToolkit.Core.csproj CadToolkit\tests\ConfigDiagnostics.Tests.ps1
git commit -m "test: add CadToolkit config diagnostics skeleton"
```

---

### Task 2: INI Parsing and Read-Only Diagnostics

**Files:**
- Modify: `D:\Documents\GitHub\minis\CadToolkit\src\CadToolkit.Core\ConfigDiagnostics.cs`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\tests\ConfigDiagnostics.Tests.ps1`

- [ ] **Step 1: Add failing tests for diagnostics issue codes**

Append these helpers and cases to `ConfigDiagnostics.Tests.ps1` after type checks:

```powershell
function Invoke-Analyze($text) {
    return $diagnosticsType.GetMethod('Analyze').Invoke($null, [object[]]@($text, 'C:\CadToolkit\CadToolkit.ini'))
}

function Invoke-Repair($text) {
    return $diagnosticsType.GetMethod('Repair').Invoke($null, [object[]]@($text, 'C:\CadToolkit\CadToolkit.ini'))
}

function Issue-Codes($result) {
    $codes = @()
    foreach ($issue in $result.Issues) { $codes += [string]$issue.Code }
    return $codes
}

function Assert-HasCode($name, $result, $code) {
    $codes = Issue-Codes $result
    if ($codes -notcontains $code) { throw "$name missing issue code $code. Codes: $($codes -join ',')" }
    Write-Host "PASS $name"
}

function Assert-NoCode($name, $result, $code) {
    $codes = Issue-Codes $result
    if ($codes -contains $code) { throw "$name found forbidden issue code $code" }
    Write-Host "PASS $name"
}

function Assert-HasError($name, $result, $code) {
    foreach ($issue in $result.Issues) {
        if ($issue.Code -eq $code -and [string]$issue.Severity -eq 'Error') {
            Write-Host "PASS $name"
            return
        }
    }
    throw "$name did not find error $code"
}

$validConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.ini') -Raw
$validResult = Invoke-Analyze $validConfig
Assert-NoCode 'valid project config has no missing commands section' $validResult 'MissingCommandsSection'
Assert-NoCode 'valid project config has no bad command docs' $validResult 'CommandDocCommentWithEquals'
Assert-NoCode 'valid project config has no malformed layer standard' $validResult 'MalformedLayerStandard'

$missingRoot = @(
    '[Commands]',
    '文字编号=CT_TEXTNUMBER',
    '',
    '[LayerStandard]',
    '0-设备层=4|CONTINUOUS|Default|true',
    '',
    '[LayerMap]',
    '0-设备层=*设备*',
    '',
    '[TextStyleStandard]',
    'STANDARD-TEXT=gbenor.shx|gbcbig.shx|0|1.0|0',
    '',
    '[TextStyleMap]',
    'STANDARD-TEXT=Standard'
) -join "`r`n"
$missingRootResult = Invoke-Analyze $missingRoot
Assert-HasCode 'missing root setting is reported' $missingRootResult 'MissingRootSetting'

$missingCommands = @(
    'QuickBlockPrefix=BK',
    '',
    '[LayerStandard]',
    '0-设备层=4|CONTINUOUS|Default|true'
) -join "`r`n"
Assert-HasCode 'missing commands section is reported' (Invoke-Analyze $missingCommands) 'MissingCommandsSection'

$badCommandDocs = @(
    'QuickBlockPrefix=BK',
    '',
    '[Commands]',
    '# 格式：显示名称=CAD命令',
    '文字编号=CT_TEXTNUMBER',
    '',
    '[LayerStandard]',
    '0-设备层=4|CONTINUOUS|Default|true'
) -join "`r`n"
Assert-HasCode 'equals comment in commands is reported' (Invoke-Analyze $badCommandDocs) 'CommandDocCommentWithEquals'

$badLayerMap = @(
    'QuickBlockPrefix=BK',
    '',
    '[Commands]',
    '文字编号=CT_TEXTNUMBER',
    '',
    '[LayerStandard]',
    '0-设备层=4|CONTINUOUS|Default|true',
    '',
    '[LayerMap]',
    '8-受力点=POINT',
    '',
    '[TextStyleStandard]',
    'STANDARD-TEXT=gbenor.shx|gbcbig.shx|0|1.0|0',
    '',
    '[TextStyleMap]',
    'STANDARD-TEXT=Standard'
) -join "`r`n"
Assert-HasError 'missing layer map target is error' (Invoke-Analyze $badLayerMap) 'LayerMapTargetMissing'

$badTextStyleMap = $badLayerMap -replace '8-受力点=POINT', '0-设备层=POINT' -replace 'STANDARD-TEXT=Standard', 'TITLE-TEXT=Title'
Assert-HasError 'missing text style map target is error' (Invoke-Analyze $badTextStyleMap) 'TextStyleMapTargetMissing'

$malformedLayer = $badLayerMap -replace '0-设备层=4\|CONTINUOUS\|Default\|true', '0-设备层=4|CONTINUOUS'
Assert-HasError 'malformed layer standard is error' (Invoke-Analyze $malformedLayer) 'MalformedLayerStandard'

$malformedTextStyle = $badLayerMap -replace 'STANDARD-TEXT=gbenor\.shx\|gbcbig\.shx\|0\|1\.0\|0', 'STANDARD-TEXT=gbenor.shx|gbcbig.shx|abc|1.0|0'
Assert-HasError 'malformed text style standard is error' (Invoke-Analyze $malformedTextStyle) 'MalformedTextStyleStandard'
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
```

Expected: fails on missing issue codes.

- [ ] **Step 3: Implement INI parsing and diagnostics**

Replace `ConfigDiagnostics.cs` with a real analyzer that includes these members:

```csharp
static readonly string[] RequiredSections = { "Commands", "LayerStandard", "LayerMap", "TextStyleStandard", "TextStyleMap" };

static readonly KeyValuePair<string, string>[] RootSettings = new KeyValuePair<string, string>[]
{
    new KeyValuePair<string, string>("QuickBlockPrefix", "BK"),
    new KeyValuePair<string, string>("DeleteOriginal", "true"),
    new KeyValuePair<string, string>("KeepOriginal", "false"),
    new KeyValuePair<string, string>("AlignHorizontal", "0"),
    new KeyValuePair<string, string>("AlignUseFirstBase", "true"),
    new KeyValuePair<string, string>("AlignLineSpacing", "0"),
    new KeyValuePair<string, string>("IsoLayerKeepLayer0", "false"),
    new KeyValuePair<string, string>("LayerStandardFallbackTo0", "false"),
    new KeyValuePair<string, string>("LayerStandardWhitelist", "0,Defpoints,*图框*,*视口*,*原有*,*新增*"),
    new KeyValuePair<string, string>("TextStyleFallbackToStandard", "false"),
    new KeyValuePair<string, string>("TextStyleFallbackStyle", "STANDARD-TEXT"),
    new KeyValuePair<string, string>("TextStyleWhitelist", "Standard,Annotative,*DIM*"),
    new KeyValuePair<string, string>("TextStyleNormalizeHeight", "false"),
    new KeyValuePair<string, string>("TextStyleNormalizeWidthFactor", "false"),
    new KeyValuePair<string, string>("TextStyleNormalizeOblique", "false"),
    new KeyValuePair<string, string>("TextStyleNormalizeColorByLayer", "false"),
    new KeyValuePair<string, string>("TextStyleDeleteUnusedOldStyles", "false")
};

static readonly KeyValuePair<string, string>[] OfficialCommands = new KeyValuePair<string, string>[]
{
    new KeyValuePair<string, string>("查找替换", "CT_FINDREPLACE"),
    new KeyValuePair<string, string>("文字对齐", "CT_ALIGN"),
    new KeyValuePair<string, string>("加下划线", "CT_UNDERLINE"),
    new KeyValuePair<string, string>("格式复制", "CT_TEXTBRUSH"),
    new KeyValuePair<string, string>("文字合并", "CT_TEXTMERGE"),
    new KeyValuePair<string, string>("文字编号", "CT_TEXTNUMBER"),
    new KeyValuePair<string, string>("文字规范", "CT_TEXTSTYLESTANDARD"),
    new KeyValuePair<string, string>("配置体检", "CT_CONFIGCHECK"),
    new KeyValuePair<string, string>("图层归零", "CT_SETLAYER0"),
    new KeyValuePair<string, string>("图层规范", "CT_LAYERSTANDARD"),
    new KeyValuePair<string, string>("孤立图层", "CT_ISOLAYER"),
    new KeyValuePair<string, string>("按层选择", "CT_SELECTBYLAYER"),
    new KeyValuePair<string, string>("按色选择", "CT_SELECTBYCOLOR"),
    new KeyValuePair<string, string>("重命名块", "CT_RENAMEBLOCK"),
    new KeyValuePair<string, string>("快捷建块", "CT_QUICKBLOCK"),
    new KeyValuePair<string, string>("改块基点", "CT_CHANGEBASEPOINT"),
    new KeyValuePair<string, string>("按块选择", "CT_SELECTBYBLOCK"),
    new KeyValuePair<string, string>("画中心线", "CT_CENTERLINE"),
    new KeyValuePair<string, string>("快速标注", "CT_QUICKDIM"),
    new KeyValuePair<string, string>("递增复制", "CT_INCCOPY"),
    new KeyValuePair<string, string>("Z轴归零", "CT_FLATTEN")
};
```

Implement helper classes inside `ConfigDiagnostics`:

```csharp
class IniLine
{
    public string Text;
    public string Trimmed;
    public int Number;
    public string Section;
    public bool IsSection;
    public bool IsComment;
    public string Key;
    public string Value;
}
```

Parsing rules:

- Split on `\r\n` normalized by `text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')`.
- Section header is `[`...`]`.
- Comment is trimmed line starting `#` or `;`.
- Key/value is first `=` outside comments, same simple parser style as current `Config`.
- Track current section for every line.

Analyzer rules:

- For each root setting missing outside sections, add warning code `MissingRootSetting`, `CanFix=true`.
- For missing section, add warning code `MissingSection` except `[Commands]`, which uses `MissingCommandsSection`.
- For each missing official command in `[Commands]`, add warning `MissingOfficialCommand`, `CanFix=true`.
- If `文字样式规范=CT_TEXTSTYLESTANDARD` exists, add warning `OldOfficialCommandLabel`, `CanFix=true`.
- If a comment inside `Commands` contains `=`, add warning `CommandDocCommentWithEquals`, `CanFix=true`.
- If a `LayerMap` key is not in `LayerStandard`, add error `LayerMapTargetMissing`, `CanFix=false`.
- If a `TextStyleMap` key is not in `TextStyleStandard`, add error `TextStyleMapTargetMissing`, `CanFix=false`.
- If a `LayerStandard` value does not have exactly 4 `|` parts, invalid color integer, or invalid bool plot flag, add error `MalformedLayerStandard`, `CanFix=false`.
- If a `TextStyleStandard` value does not have exactly 5 `|` parts, invalid height/width/oblique numbers, add error `MalformedTextStyleStandard`, `CanFix=false`.
- Add one info issue `Summary` with checked counts.

- [ ] **Step 4: Run the test to verify it passes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
```

Expected: all diagnostics code tests pass.

- [ ] **Step 5: Commit**

```powershell
git add CadToolkit\src\CadToolkit.Core\ConfigDiagnostics.cs CadToolkit\tests\ConfigDiagnostics.Tests.ps1
git commit -m "feat: analyze CadToolkit config diagnostics"
```

---

### Task 3: Safe Text Repair and File Backups

**Files:**
- Modify: `D:\Documents\GitHub\minis\CadToolkit\src\CadToolkit.Core\ConfigDiagnostics.cs`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\tests\ConfigDiagnostics.Tests.ps1`

- [ ] **Step 1: Add failing tests for repair behavior**

Append these tests:

```powershell
function Assert-TextContains($name, $text, $literal) {
    if (-not $text.Contains($literal)) { throw "$name missing literal: $literal" }
    Write-Host "PASS $name"
}

function Assert-TextNotContains($name, $text, $literal) {
    if ($text.Contains($literal)) { throw "$name found forbidden literal: $literal" }
    Write-Host "PASS $name"
}

$repairInput = @(
    'QuickBlockPrefix=USER',
    '',
    '[Commands]',
    '# 格式：显示名称=CAD命令',
    '文字样式规范=CT_TEXTSTYLESTANDARD',
    '',
    '[LayerStandard]',
    '0-设备层=4|CONTINUOUS|Default|true',
    '',
    '[LayerMap]',
    '0-设备层=*设备*',
    '',
    '[TextStyleStandard]',
    'STANDARD-TEXT=gbenor.shx|gbcbig.shx|0|1.0|0',
    '',
    '[TextStyleMap]',
    'STANDARD-TEXT=Standard'
) -join "`r`n"

$repairResult = Invoke-Repair $repairInput
$repairedText = [string]$repairResult.RepairedText
if (-not $repairResult.HasChanges) { throw 'repair should report changes' }
Write-Host 'PASS repair reports changes'
Assert-TextContains 'repair preserves user root setting' $repairedText 'QuickBlockPrefix=USER'
Assert-TextContains 'repair adds missing root setting before commands' $repairedText 'DeleteOriginal=true'
Assert-TextContains 'repair renames old text style command' $repairedText '文字规范=CT_TEXTSTYLESTANDARD'
Assert-TextNotContains 'repair removes old text style command label' $repairedText '文字样式规范=CT_TEXTSTYLESTANDARD'
Assert-TextContains 'repair adds config check command' $repairedText '配置体检=CT_CONFIGCHECK'
Assert-TextNotContains 'repair removes equals doc comment in commands' $repairedText '# 格式：显示名称=CAD命令'
Assert-TextContains 'repair does not add default text style map section twice' $repairedText '[TextStyleMap]'

$badUnfixableRepair = Invoke-Repair $badLayerMap
$badUnfixableText = [string]$badUnfixableRepair.RepairedText
Assert-TextContains 'repair keeps unfixable bad layer map target' $badUnfixableText '8-受力点=POINT'

$tmpRoot = Join-Path ([IO.Path]::GetTempPath()) ('CadToolkitConfigDiagnostics-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $tmpRoot -Force | Out-Null
    $tmpIni = Join-Path $tmpRoot 'CadToolkit.ini'
    $repairInput | Set-Content -Encoding UTF8 $tmpIni
    $fileRepair = $diagnosticsType.GetMethod('RepairFile').Invoke($null, [object[]]@($tmpIni))
    $backupPath = [string]$fileRepair.BackupPath
    if (-not (Test-Path $backupPath)) { throw "backup path was not created: $backupPath" }
    Write-Host 'PASS repair file creates backup'
    $afterFileRepair = Get-Content -Encoding UTF8 $tmpIni -Raw
    Assert-TextContains 'repair file writes repaired command' $afterFileRepair '配置体检=CT_CONFIGCHECK'
}
finally {
    Remove-Item -LiteralPath $tmpRoot -Recurse -Force -ErrorAction SilentlyContinue
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
```

Expected: fails because repair does not change text or create backups yet.

- [ ] **Step 3: Implement safe repair**

In `ConfigDiagnostics.cs`, implement `Repair` by transforming lines in this order:

1. If text is empty or whitespace, return embedded default config text and `HasChanges=true`.
2. Add missing root settings immediately before the first section.
3. Add `[Commands]` before `[LayerStandard]` if missing; append to end if `[LayerStandard]` is also missing.
4. Rename `文字样式规范=CT_TEXTSTYLESTANDARD` to `文字规范=CT_TEXTSTYLESTANDARD`, removing the old line if the new line already exists.
5. Remove known documentation comments inside `[Commands]` that contain `=` and start with one of:
   - `# 格式`
   - `# 示例`
   - `# 标准图层`
   - `# 标准样式`
6. Insert missing official commands in `[Commands]`, before the next section. Prefer anchors:
   - Insert `配置体检=CT_CONFIGCHECK` after `文字规范=CT_TEXTSTYLESTANDARD` when present.
   - Insert `改块基点=CT_CHANGEBASEPOINT` after `快捷建块=CT_QUICKBLOCK`.
   - Other missing official commands append before next section.
7. Return repaired text with `\r\n` line endings.

Implement `RepairFile`:

```csharp
public static ConfigDiagnosticResult RepairFile(string path)
{
    string text = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
    var result = Repair(text, path);
    if (result.HasChanges)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        if (File.Exists(path))
        {
            string backup = path + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(path, backup, false);
            result.BackupPath = backup;
        }
        File.WriteAllText(path, result.RepairedText, Encoding.UTF8);
    }
    return AnalyzeFile(path);
}
```

When returning the final analyzed result from `RepairFile`, preserve `BackupPath` by assigning it onto the final result before return.

- [ ] **Step 4: Run the test to verify it passes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
```

Expected: repair and backup tests pass.

- [ ] **Step 5: Commit**

```powershell
git add CadToolkit\src\CadToolkit.Core\ConfigDiagnostics.cs CadToolkit\tests\ConfigDiagnostics.Tests.ps1
git commit -m "feat: repair CadToolkit config safely"
```

---

### Task 4: Shared Report Formatting

**Files:**
- Modify: `D:\Documents\GitHub\minis\CadToolkit\src\CadToolkit.Core\ConfigDiagnostics.cs`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\tests\ConfigDiagnostics.Tests.ps1`

- [ ] **Step 1: Add failing tests for formatted report**

Append:

```powershell
Assert-NotNull 'config diagnostics format report method exists' ($diagnosticsType.GetMethod('FormatReport', [Reflection.BindingFlags]'Public, Static'))

$report = [string]$diagnosticsType.GetMethod('FormatReport').Invoke($null, [object[]]@($missingRootResult))
Assert-TextContains 'report title is Chinese' $report 'CadToolkit 配置体检'
Assert-TextContains 'report includes config path' $report 'C:\CadToolkit\CadToolkit.ini'
Assert-TextContains 'report includes warning group' $report '警告'
Assert-TextContains 'report marks fixable issue' $report '可自动修复'
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
```

Expected: fails because `FormatReport` does not exist.

- [ ] **Step 3: Implement `FormatReport`**

Add public method:

```csharp
public static string FormatReport(ConfigDiagnosticResult result)
{
    var sb = new StringBuilder();
    sb.AppendLine("CadToolkit 配置体检");
    sb.AppendLine("配置文件：" + (result == null ? "" : result.Path));
    sb.AppendLine();
    AppendIssueGroup(sb, "错误", result, ConfigDiagnosticSeverity.Error);
    AppendIssueGroup(sb, "警告", result, ConfigDiagnosticSeverity.Warning);
    AppendIssueGroup(sb, "信息", result, ConfigDiagnosticSeverity.Info);
    if (result != null && !string.IsNullOrEmpty(result.BackupPath))
    {
        sb.AppendLine();
        sb.AppendLine("备份文件：" + result.BackupPath);
    }
    return sb.ToString();
}
```

Add private `AppendIssueGroup`:

```csharp
static void AppendIssueGroup(StringBuilder sb, string title, ConfigDiagnosticResult result, ConfigDiagnosticSeverity severity)
{
    var issues = new List<ConfigDiagnosticIssue>();
    if (result != null)
    {
        foreach (var issue in result.Issues)
            if (issue.Severity == severity) issues.Add(issue);
    }
    if (issues.Count == 0) return;
    sb.AppendLine(title + " " + issues.Count + " 项");
    foreach (var issue in issues)
    {
        string suffix = issue.CanFix ? "（可自动修复）" : "";
        string location = "";
        if (!string.IsNullOrEmpty(issue.Section)) location += "[" + issue.Section + "] ";
        if (issue.LineNumber > 0) location += "第 " + issue.LineNumber + " 行：";
        sb.AppendLine("- " + location + issue.Message + suffix);
    }
    sb.AppendLine();
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
```

Expected: report tests pass.

- [ ] **Step 5: Commit**

```powershell
git add CadToolkit\src\CadToolkit.Core\ConfigDiagnostics.cs CadToolkit\tests\ConfigDiagnostics.Tests.ps1
git commit -m "feat: format CadToolkit config diagnostics report"
```

---

### Task 5: Config Defaults and Command Registration

**Files:**
- Modify: `D:\Documents\GitHub\minis\CadToolkit\CadToolkit.ini`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\CadToolkit.default.ini`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\src\CadToolkit.Core\Config.cs`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\tests\ConfigDiagnostics.Tests.ps1`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\tests\DeploymentConfig.Tests.ps1`

- [ ] **Step 1: Add failing tests for config command presence**

Append:

```powershell
$projectConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.ini') -Raw
$defaultConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.default.ini') -Raw
$configSource = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.Core\Config.cs') -Raw

Assert-TextContains 'project config contains config check command' $projectConfig '配置体检=CT_CONFIGCHECK'
Assert-TextContains 'default config contains config check command' $defaultConfig '配置体检=CT_CONFIGCHECK'
Assert-Contains 'embedded default contains config check command' $configSource '配置体检=CT_CONFIGCHECK|\\u914D\\u7F6E\\u4F53\\u68C0=CT_CONFIGCHECK'
Assert-Contains 'startup upgrade ensures config check command' $configSource 'EnsureOfficialCommand\(lines,\s*"配置体检"|EnsureOfficialCommand\(lines,\s*"\\u914D\\u7F6E\\u4F53\\u68C0"'
```

Also add equivalent assertions to `DeploymentConfig.Tests.ps1` near existing command config checks:

```powershell
Assert-ContainsLiteral 'project config contains config check command' $projectConfig '配置体检=CT_CONFIGCHECK'
Assert-ContainsLiteral 'default config contains config check command' $defaultConfig '配置体检=CT_CONFIGCHECK'
Assert-Contains 'embedded default contains config check command' $configSource '配置体检=CT_CONFIGCHECK|\\u914D\\u7F6E\\u4F53\\u68C0=CT_CONFIGCHECK'
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\DeploymentConfig.Tests.ps1
```

Expected: tests fail because command is not present.

- [ ] **Step 3: Add config check command to defaults and upgrade**

In both config files, insert after `文字规范=CT_TEXTSTYLESTANDARD`:

```ini
配置体检=CT_CONFIGCHECK
```

In `Config.cs`:

- Add to embedded default after `文字规范=CT_TEXTSTYLESTANDARD`:

```csharp
sb.AppendLine("\u914D\u7F6E\u4F53\u68C0=CT_CONFIGCHECK");
```

- Add to `EnsureOfficialCommands()` after text style command:

```csharp
changed |= EnsureOfficialCommand(lines, "\u914D\u7F6E\u4F53\u68C0", "CT_CONFIGCHECK", "\u6587\u5B57\u89C4\u8303");
```

Use Unicode escape strings in `Config.cs` additions, because the embedded default config in that file already uses escaped Chinese text.

- [ ] **Step 4: Run tests to verify pass**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\DeploymentConfig.Tests.ps1
```

Expected: command presence and deployment config tests pass.

- [ ] **Step 5: Commit**

```powershell
git add CadToolkit\CadToolkit.ini CadToolkit\CadToolkit.default.ini CadToolkit\src\CadToolkit.Core\Config.cs CadToolkit\tests\ConfigDiagnostics.Tests.ps1 CadToolkit\tests\DeploymentConfig.Tests.ps1
git commit -m "feat: register CadToolkit config check command"
```

---

### Task 6: CAD Config Check Command and Dialog

**Files:**
- Create: `D:\Documents\GitHub\minis\CadToolkit\src\CadToolkit\ConfigCommands.cs`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\src\CadToolkit\CadToolkit.AutoCAD.csproj`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\src\CadToolkit\CadToolkit.ZWCAD.csproj`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\src\CadToolkit\CadToolkit.GstarCAD.csproj`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\tests\ConfigDiagnostics.Tests.ps1`

- [ ] **Step 1: Add failing tests for command source and project inclusion**

Append:

```powershell
$configCommandsPath = Join-Path $repo 'CadToolkit\src\CadToolkit\ConfigCommands.cs'
if (-not (Test-Path $configCommandsPath)) { throw 'ConfigCommands.cs is missing' }
$configCommandsSource = Get-Content -Encoding UTF8 $configCommandsPath -Raw
Assert-Contains 'config check command is registered' $configCommandsSource '\[CommandMethod\("CT_CONFIGCHECK"\)\]'
Assert-Contains 'config check command analyzes file' $configCommandsSource 'ConfigDiagnostics\.AnalyzeFile'
Assert-Contains 'config check command can repair file' $configCommandsSource 'ConfigDiagnostics\.RepairFile'
Assert-Contains 'config check command formats report' $configCommandsSource 'ConfigDiagnostics\.FormatReport'
Assert-Contains 'config check dialog has copy button' $configCommandsSource '复制报告'
Assert-Contains 'config check dialog has repair button' $configCommandsSource '自动修复'

foreach ($projectName in @('CadToolkit.AutoCAD.csproj', 'CadToolkit.ZWCAD.csproj', 'CadToolkit.GstarCAD.csproj')) {
    $projectText = Get-Content -Encoding UTF8 (Join-Path $repo "CadToolkit\src\CadToolkit\$projectName") -Raw
    Assert-Contains "$projectName compiles config commands" $projectText 'Compile Include="ConfigCommands\.cs"'
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
```

Expected: fails because `ConfigCommands.cs` is missing.

- [ ] **Step 3: Implement command and dialog**

Create `CadToolkit/src/CadToolkit/ConfigCommands.cs`:

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;
using CadToolkit.Core;

#if AUTOCAD
using Autodesk.AutoCAD.Runtime;
#elif GSTARCAD
using GrxCAD.Runtime;
#elif ZWCAD
using ZwSoft.ZwCAD.Runtime;
#endif

namespace CadToolkit
{
    public partial class CadCommands
    {
        [CommandMethod("CT_CONFIGCHECK")]
        public void ConfigCheck()
        {
            EnsureInit();
            try
            {
                using (var form = new ConfigCheckForm(Config.ConfigPath))
                {
                    form.ShowDialog();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("配置体检失败：" + ex.Message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("ConfigCheck failed: " + ex);
            }
        }

        class ConfigCheckForm : Form
        {
            readonly string _path;
            readonly TextBox _report;
            readonly Button _repair;

            public ConfigCheckForm(string path)
            {
                _path = path;
                Text = "配置体检";
                StartPosition = FormStartPosition.CenterScreen;
                Width = UiScale(760);
                Height = UiScale(560);
                MinimizeBox = false;
                MaximizeBox = false;

                _report = new TextBox();
                _report.Multiline = true;
                _report.ReadOnly = true;
                _report.ScrollBars = ScrollBars.Both;
                _report.WordWrap = false;
                _report.Font = new Font("Microsoft YaHei UI", 9f);
                _report.SetBounds(UiScale(12), UiScale(12), UiScale(720), UiScale(455));
                Controls.Add(_report);

                var copy = new Button();
                copy.Text = "复制报告";
                copy.SetBounds(UiScale(442), UiScale(480), UiScale(90), UiScale(30));
                copy.Click += delegate { Clipboard.SetText(_report.Text); };
                Controls.Add(copy);

                _repair = new Button();
                _repair.Text = "自动修复";
                _repair.SetBounds(UiScale(542), UiScale(480), UiScale(90), UiScale(30));
                _repair.Click += delegate { RepairAndRefresh(); };
                Controls.Add(_repair);

                var close = new Button();
                close.Text = "关闭";
                close.SetBounds(UiScale(642), UiScale(480), UiScale(90), UiScale(30));
                close.Click += delegate { Close(); };
                Controls.Add(close);

                RefreshReport(ConfigDiagnostics.AnalyzeFile(_path));
            }

            void RepairAndRefresh()
            {
                try
                {
                    var result = ConfigDiagnostics.RepairFile(_path);
                    RefreshReport(result);
                    MessageBox.Show("自动修复完成。修复前配置已备份。", "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("自动修复失败：" + ex.Message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            void RefreshReport(ConfigDiagnosticResult result)
            {
                _report.Text = ConfigDiagnostics.FormatReport(result);
                _repair.Enabled = result != null && HasFixableIssue(result);
            }

            static bool HasFixableIssue(ConfigDiagnosticResult result)
            {
                foreach (var issue in result.Issues)
                    if (issue.CanFix) return true;
                return false;
            }
        }
    }
}
```

Expose `Config.ConfigPath` in `Config.cs`:

```csharp
public static string ConfigPath { get { return IniPath; } }
public static string DefaultConfigText { get { return GetDefaultConfigText(); } }
```

Update all three CAD csproj files to include:

```xml
<Compile Include="ConfigCommands.cs" />
```

- [ ] **Step 4: Run command tests and build-oriented tests**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\LayerStandardMatching.Tests.ps1
```

Expected: tests pass. The second test exercises plugin project compilation through existing stubs.

- [ ] **Step 5: Commit**

```powershell
git add CadToolkit\src\CadToolkit\ConfigCommands.cs CadToolkit\src\CadToolkit\CadToolkit.AutoCAD.csproj CadToolkit\src\CadToolkit\CadToolkit.ZWCAD.csproj CadToolkit\src\CadToolkit\CadToolkit.GstarCAD.csproj CadToolkit\src\CadToolkit.Core\Config.cs CadToolkit\tests\ConfigDiagnostics.Tests.ps1
git commit -m "feat: add CadToolkit config check CAD command"
```

---

### Task 7: PowerShell Tool and Packaging

**Files:**
- Create: `D:\Documents\GitHub\minis\CadToolkit\tools\check-config.ps1`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\deploy-local.ps1`
- Modify: `D:\Documents\GitHub\minis\.github\workflows\cadtoolkit.yml`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\tests\ConfigDiagnostics.Tests.ps1`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\tests\DeploymentConfig.Tests.ps1`

- [ ] **Step 1: Add failing tests for script and packaging**

Append to `ConfigDiagnostics.Tests.ps1`:

```powershell
$toolPath = Join-Path $repo 'CadToolkit\tools\check-config.ps1'
if (-not (Test-Path $toolPath)) { throw 'check-config.ps1 is missing' }
$toolText = Get-Content -Encoding UTF8 $toolPath -Raw
$toolParseErrors = $null
[System.Management.Automation.Language.Parser]::ParseFile($toolPath, [ref]$null, [ref]$toolParseErrors) | Out-Null
if ($toolParseErrors.Count -gt 0) { throw "check-config.ps1 parse error: $($toolParseErrors[0].Message)" }
Write-Host 'PASS check-config script parses'
Assert-Contains 'check-config supports Path parameter' $toolText 'param\s*\([^)]*\$Path'
Assert-Contains 'check-config supports Fix switch' $toolText '\[switch\]\s*\$Fix'
Assert-Contains 'check-config calls AnalyzeFile' $toolText 'AnalyzeFile'
Assert-Contains 'check-config calls RepairFile' $toolText 'RepairFile'
Assert-Contains 'check-config prints formatted report' $toolText 'FormatReport'
```

Add to `DeploymentConfig.Tests.ps1`:

```powershell
$toolPath = Join-Path $repo 'CadToolkit\tools\check-config.ps1'
if (-not (Test-Path $toolPath)) { throw 'check-config.ps1 is missing' }
Assert-Contains 'local deploy publishes config check tool' $deployLocal 'check-config\.ps1'
Assert-Contains 'release package includes config check tool' $workflow 'check-config\.ps1'
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\DeploymentConfig.Tests.ps1
```

Expected: fails because the script and packaging are missing.

- [ ] **Step 3: Implement script**

Create `CadToolkit/tools/check-config.ps1`:

```powershell
$ErrorActionPreference = 'Stop'

param(
    [string]$Path = 'C:\CadToolkit\CadToolkit.ini',
    [switch]$Fix
)

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$coreProject = Join-Path $repo 'CadToolkit\src\CadToolkit.Core\CadToolkit.Core.csproj'
$coreDll = Join-Path $repo 'CadToolkit\src\CadToolkit.Core\bin\Release\CadToolkit.Core.dll'
$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'

if (-not (Test-Path -LiteralPath $coreDll)) {
    if (-not (Test-Path -LiteralPath $msbuild)) {
        throw "MSBuild not found: $msbuild"
    }
    & $msbuild $coreProject /p:Configuration=Release /p:Platform=x64 /t:Build /v:minimal | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'CadToolkit.Core build failed' }
}

$assembly = [Reflection.Assembly]::LoadFrom($coreDll)
$diagnosticsType = $assembly.GetType('CadToolkit.Core.ConfigDiagnostics', $true)

if ($Fix) {
    $result = $diagnosticsType.GetMethod('RepairFile').Invoke($null, [object[]]@($Path))
}
else {
    $result = $diagnosticsType.GetMethod('AnalyzeFile').Invoke($null, [object[]]@($Path))
}

$report = [string]$diagnosticsType.GetMethod('FormatReport').Invoke($null, [object[]]@($result))
Write-Host $report

if ($result.HasErrors) { exit 1 }
exit 0
```

- [ ] **Step 4: Update local deploy and release package**

In `deploy-local.ps1`, add after copying the manual/default config:

```powershell
$ToolsSource = Join-Path $Base 'tools'
$ToolsTarget = Join-Path $DeployRoot 'tools'
if (Test-Path -LiteralPath $ToolsSource) {
    if (-not (Test-Path -LiteralPath $ToolsTarget)) {
        New-Item -ItemType Directory -Path $ToolsTarget -Force | Out-Null
    }
    Copy-DeployItem -Source (Join-Path $ToolsSource 'check-config.ps1') -Destination (Join-Path $ToolsTarget 'check-config.ps1')
}
```

In `.github/workflows/cadtoolkit.yml`, add to package step:

```powershell
New-Item -ItemType Directory -Force "$pkg\tools" | Out-Null
Copy-Item "${{ github.workspace }}\CadToolkit\tools\check-config.ps1" "$pkg\tools\"
```

- [ ] **Step 5: Run tests to verify pass**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\DeploymentConfig.Tests.ps1
```

Expected: script and packaging tests pass.

- [ ] **Step 6: Commit**

```powershell
git add CadToolkit\tools\check-config.ps1 CadToolkit\deploy-local.ps1 .github\workflows\cadtoolkit.yml CadToolkit\tests\ConfigDiagnostics.Tests.ps1 CadToolkit\tests\DeploymentConfig.Tests.ps1
git commit -m "feat: add CadToolkit config check script"
```

---

### Task 8: Documentation

**Files:**
- Modify: `D:\Documents\GitHub\minis\CadToolkit\README.md`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\CadToolkit使用手册.html`
- Modify: `D:\Documents\GitHub\minis\CadToolkit\tests\ConfigDiagnostics.Tests.ps1`

- [ ] **Step 1: Add failing documentation tests**

Append:

```powershell
$readme = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\README.md') -Raw
$manualFileName = 'CadToolkit' + (-join ([char[]](0x4F7F, 0x7528, 0x624B, 0x518C))) + '.html'
$manual = Get-Content -Encoding UTF8 (Join-Path (Join-Path $repo 'CadToolkit') $manualFileName) -Raw
Assert-TextContains 'readme documents config check command' $readme '配置体检'
Assert-TextContains 'readme documents CT_CONFIGCHECK' $readme 'CT_CONFIGCHECK'
Assert-TextContains 'readme documents check-config script' $readme 'check-config.ps1'
Assert-TextContains 'manual documents config check command' $manual '配置体检'
Assert-TextContains 'manual documents CT_CONFIGCHECK' $manual 'CT_CONFIGCHECK'
Assert-TextContains 'manual documents check-config script' $manual 'check-config.ps1'
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
```

Expected: fails until docs mention the new command and script.

- [ ] **Step 3: Update README**

In `CadToolkit/README.md`, add a concise subsection under configuration:

```markdown
### 配置体检

运行 `CT_CONFIGCHECK` 或面板里的 `配置体检` 可以检查 `CadToolkit.ini` 是否缺少基础项、官方命令、必要 section，是否存在映射目标缺失或标准行格式错误。自动修复只会补缺失基础项、补官方命令、重命名旧官方命令和清理已知错误注释；不会覆盖图层标准、图层映射、文字样式标准或文字样式映射。

也可以在 PowerShell 中运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\CadToolkit\tools\check-config.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File C:\CadToolkit\tools\check-config.ps1 -Fix
```
```

- [ ] **Step 4: Update manual**

In `CadToolkit/CadToolkit使用手册.html`, add a short section near config instructions:

```html
<h3>配置体检</h3>
<p>运行 <code>CT_CONFIGCHECK</code> 或面板中的“配置体检”，可以检查 <code>CadToolkit.ini</code> 的基础项、官方命令、必要分组、图层映射和文字样式映射。</p>
<p>自动修复只处理安全项：补缺失基础项、补官方命令、重命名旧官方命令和清理已知错误注释；不会覆盖用户自定义的图层标准、图层映射、文字样式标准或文字样式映射。</p>
<pre><code>powershell -NoProfile -ExecutionPolicy Bypass -File C:\CadToolkit\tools\check-config.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File C:\CadToolkit\tools\check-config.ps1 -Fix</code></pre>
```

- [ ] **Step 5: Run docs test**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigDiagnostics.Tests.ps1
```

Expected: docs assertions pass.

- [ ] **Step 6: Commit**

```powershell
git add CadToolkit\README.md CadToolkit\CadToolkit使用手册.html CadToolkit\tests\ConfigDiagnostics.Tests.ps1
git commit -m "docs: document CadToolkit config diagnostics"
```

---

### Task 9: Full Verification, Local Deploy, and Final Commit Check

**Files:**
- No planned source changes unless verification reveals a defect.

- [ ] **Step 1: Run all CadToolkit tests**

Run:

```powershell
$tests = Get-ChildItem CadToolkit\tests -Filter *.Tests.ps1 | Sort-Object Name
foreach ($test in $tests) {
  Write-Host "Running $($test.Name)"
  powershell -NoProfile -ExecutionPolicy Bypass -File $test.FullName
  if ($LASTEXITCODE -ne 0) { throw "Test failed: $($test.Name)" }
}
```

Expected: every test script exits `0`. Known non-fatal warnings may mention .NET Framework v4.8 targeting pack, `AdWindows`, or CAD SDK metadata.

- [ ] **Step 2: Run whitespace check**

Run:

```powershell
git diff --check
```

Expected: no whitespace errors. LF/CRLF warnings are acceptable on this Windows repo.

- [ ] **Step 3: Deploy locally**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\deploy-local.ps1
```

Expected: deploy succeeds, existing `C:\CadToolkit\CadToolkit.ini` is preserved, and `C:\CadToolkit\tools\check-config.ps1` exists.

- [ ] **Step 4: Run config check against local config**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\CadToolkit\tools\check-config.ps1 -Path C:\CadToolkit\CadToolkit.ini
```

Expected: report prints. Exit code is `0` if no errors remain; if it exits `1`, inspect the report and only fix actual implementation bugs, not user-specific custom standards unless the user asks.

- [ ] **Step 5: Confirm final git state**

Run:

```powershell
git status --short --branch
git log --oneline -5
```

Expected: only intended commits are ahead of `origin/main`; no unstaged changes remain.

- [ ] **Step 6: Ask user to test CAD command before push**

Ask the user to run in CAD:

```text
CC
配置体检
```

or:

```text
CT_CONFIGCHECK
```

Expected: dialog opens, report is readable, copy works, automatic repair is available only when fixable issues exist.

Do not push until the user confirms local CAD testing or explicitly asks to push without it.
