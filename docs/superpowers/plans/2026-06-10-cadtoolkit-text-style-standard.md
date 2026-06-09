# CadToolkit Text Style Standard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a configuration-driven `CT_TEXTSTYLESTANDARD` command that previews and safely standardizes CAD text styles across normal text, block attributes, and block definition text.

**Architecture:** Mirror the existing layer standard workflow, but keep text style logic in focused helpers and command code. Extend `CadToolkit.Core.Config` with text style standard/map parsing, add `TextStyleCommands.cs` for scanning, previewing, and applying plans, then wire command/menu/config/docs/tests.

**Tech Stack:** C# .NET Framework 4.8, AutoCAD/ZWCAD/GstarCAD conditional APIs, WinForms `TreeView`, PowerShell reflection/static tests.

---

## File Structure

- Modify `CadToolkit/src/CadToolkit.Core/Config.cs`: root defaults, text style standard/map parsing types and helpers, embedded default config.
- Modify `CadToolkit/CadToolkit.ini` and `CadToolkit/CadToolkit.default.ini`: default root keys, command entry, `[TextStyleStandard]`, `[TextStyleMap]`.
- Create `CadToolkit/src/CadToolkit/TextStyleCommands.cs`: `CT_TEXTSTYLESTANDARD`, scanning, preview helpers, execution helpers.
- Modify `CadToolkit/src/CadToolkit/CadToolkit.AutoCAD.csproj`, `.ZWCAD.csproj`, `.GstarCAD.csproj`: include new command file if needed.
- Modify `CadToolkit/src/CadToolkit/Plugin.cs`: panel wiring only if automatic command list does not cover it; otherwise reuse `[Commands]`.
- Modify `CadToolkit/README.md` and `CadToolkit/CadToolkit使用手册.html`: document command and config.
- Create `CadToolkit/tests/TextStyleStandard.Tests.ps1`: config, parser, preview, and static execution assertions.
- Modify `CadToolkit/tests/ConfigUpgrade.Tests.ps1`: ensure new defaults and command are appended without merging full default sections.

## Task 1: Config and Command Surface Tests

- [ ] Add `CadToolkit/tests/TextStyleStandard.Tests.ps1` with assertions that fail because the feature is missing:
  - `Config+TextStyleStandardRule` type exists.
  - `Config.GetTextStyleStandards()` exists.
  - `Config.GetTextStyleMapRules()` exists.
  - project/default config contain `TextStyleFallbackToStandard=false`, `TextStyleFallbackStyle=STANDARD-TEXT`, `TextStyleWhitelist=Standard,Annotative,*DIM*`, all normalize toggles, and `TextStyleDeleteUnusedOldStyles=false`.
  - project/default config contain `文字样式规范=CT_TEXTSTYLESTANDARD`.
  - project/default config contain `[TextStyleStandard]` and `[TextStyleMap]`.
  - README and manual mention `CT_TEXTSTYLESTANDARD`.

- [ ] Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\TextStyleStandard.Tests.ps1
```

Expected: fails because config API and command/docs do not exist.

## Task 2: Implement Config Defaults and Parsers

- [ ] Add root defaults in `Config.cs`:
  - `TextStyleFallbackToStandard=false`
  - `TextStyleFallbackStyle=STANDARD-TEXT`
  - `TextStyleWhitelist=Standard,Annotative,*DIM*`
  - `TextStyleNormalizeHeight=false`
  - `TextStyleNormalizeWidthFactor=false`
  - `TextStyleNormalizeOblique=false`
  - `TextStyleNormalizeColorByLayer=false`
  - `TextStyleDeleteUnusedOldStyles=false`

- [ ] Add `TextStyleStandardRule` with fields:

```csharp
public string Name;
public string FontFile;
public string BigFontFile;
public double FixedHeight;
public double WidthFactor;
public double ObliqueAngle;
```

- [ ] Add `TextStyleMapRule` with fields:

```csharp
public string TargetStyle;
public List<string> Aliases = new List<string>();
```

- [ ] Add properties:

```csharp
public static bool TextStyleFallbackToStandard { get { return GetBool("TextStyleFallbackToStandard", false); } }
public static string TextStyleFallbackStyle { get { return GetString("TextStyleFallbackStyle", "STANDARD-TEXT"); } }
public static string TextStyleWhitelist { get { return GetString("TextStyleWhitelist", "Standard,Annotative,*DIM*"); } }
public static bool TextStyleNormalizeHeight { get { return GetBool("TextStyleNormalizeHeight", false); } }
public static bool TextStyleNormalizeWidthFactor { get { return GetBool("TextStyleNormalizeWidthFactor", false); } }
public static bool TextStyleNormalizeOblique { get { return GetBool("TextStyleNormalizeOblique", false); } }
public static bool TextStyleNormalizeColorByLayer { get { return GetBool("TextStyleNormalizeColorByLayer", false); } }
public static bool TextStyleDeleteUnusedOldStyles { get { return GetBool("TextStyleDeleteUnusedOldStyles", false); } }
```

- [ ] Implement `GetTextStyleStandards()` parsing `[TextStyleStandard]` entries as `font|bigFont|height|width|oblique`.
- [ ] Implement `GetTextStyleMapRules()` parsing `[TextStyleMap]` aliases.
- [ ] Update embedded default config and config files.
- [ ] Run TextStyleStandard test; expect parser/config assertions pass and command/docs assertions still fail.
- [ ] Commit `feat(CadToolkit): add text style standard config`.

## Task 3: Preview Matching Helpers

- [ ] In new `TextStyleCommands.cs`, add plan/helper types:
  - `TextStyleStandardPlan`
  - `TextStylePlanTargetGroup`
  - `TextStylePlanTreeFilter`
  - `TextStyleMatchDetail`

- [ ] Implement exact/wildcard matching helpers matching layer standard semantics:
  - `MatchTextStyleMapDetail(string styleName, List<TextStyleMapRule> rules)`
  - `MatchTextStyleWhitelistPattern(string styleName, string whitelist)`
  - `PatternMatchesTextStyle(string styleName, string pattern)`

- [ ] Implement tree helpers:
  - `BuildTextStylePlanTreeNodes(...)`
  - `BuildFilteredTextStylePlanTreeNodes(...)`
  - `BuildSearchedTextStylePlanTreeNodes(...)`
  - `FormatTextStylePlanTreeReport(...)`

- [ ] Add tests in `TextStyleStandard.Tests.ps1` for:
  - exact aliases do not contain-match.
  - wildcard aliases do contain-match.
  - whitelist exact `Standard` does not match `Standard-OLD`.
  - preview tree node shapes and search/filter behavior.

- [ ] Run focused test and commit `test/feat(CadToolkit): cover text style preview helpers`.

## Task 4: Command UI and Static Execution Surface

- [ ] Register `[CommandMethod("CT_TEXTSTYLESTANDARD")]`.
- [ ] Build preview dialog with:
  - filter radio buttons `全部 / 未识别 / 将归并 / 白名单`
  - search box
  - tree preview
  - checkboxes for scope and optional actions
  - `复制当前 / 执行 / 取消`
- [ ] Default checkbox states:
  - current-space text: checked
  - block attributes: unchecked
  - block definition text: unchecked
  - fallback: config value, default false
  - normalize options: config values, default false
  - delete unused old styles: config value, default false
- [ ] Add static tests that source contains the command, expected checkbox labels, `Clipboard.SetText`, and `tree.ExpandAll`.
- [ ] Run focused test and commit `feat(CadToolkit): add text style standard preview UI`.

## Task 5: Execution Helpers

- [ ] Implement scanning:
  - current-space `DBText` and `MText`
  - optional `AttributeReference`
  - optional block definition `DBText`, `MText`, `AttributeDefinition`
  - skip external/dependent/layout/anonymous block definitions for block-definition processing.
- [ ] Implement `ApplyTextStyleStandard(...)`:
  - create or update standard `TextStyleTableRecord`s.
  - set object `TextStyleId`.
  - optional height/width/oblique/color ByLayer.
  - optional unused old style cleanup.
- [ ] Keep execution inside `RunWithUndo("CT_TEXTSTYLESTANDARD", ...)`.
- [ ] Add static tests for scan scopes and mutation helpers.
- [ ] Run focused tests and commit `feat(CadToolkit): apply text style standard plans`.

## Task 6: Config Upgrade and Documentation

- [ ] Update `EnsureOfficialCommands()` to append `文字样式规范=CT_TEXTSTYLESTANDARD` without merging the full command list.
- [ ] Update `ConfigUpgrade.Tests.ps1` for root defaults and command append.
- [ ] Update README and manual.
- [ ] Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ConfigUpgrade.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\TextStyleStandard.Tests.ps1
```

- [ ] Commit `docs(CadToolkit): document text style standard`.

## Task 7: Final Verification and Local Deploy

- [ ] Run all CadToolkit tests:

```powershell
Get-ChildItem CadToolkit\tests -Filter *.ps1 | ForEach-Object { Write-Host "RUN $($_.Name)"; & powershell -NoProfile -ExecutionPolicy Bypass -File $_.FullName; if ($LASTEXITCODE) { exit $LASTEXITCODE } }
```

- [ ] Run `git diff --check`.
- [ ] Check CAD processes:

```powershell
Get-Process | Where-Object { $_.ProcessName -match 'acad|zwcad|gcad|gstar|cad' } | Select-Object ProcessName,Id,MainWindowTitle
```

- [ ] If no CAD process is open, run:

```powershell
.\CadToolkit\build-all.bat
```

- [ ] Commit any final fixups.

## Self-Review

- Spec coverage: config, matching, preview, scopes, default safety, optional normalization, cleanup, docs, tests, and deployment are covered.
- Placeholder scan: no TBD/TODO entries.
- Type consistency: plan uses `TextStyleStandardRule`, `TextStyleMapRule`, `TextStyleStandardPlan`, and `CT_TEXTSTYLESTANDARD` consistently.
