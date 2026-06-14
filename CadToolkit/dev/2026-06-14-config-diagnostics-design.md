# CadToolkit Config Diagnostics Design

## Goal

CadToolkit is close to daily use, and the main upgrade risk has moved from command behavior to configuration safety. Users may already have a customized `CadToolkit.ini`, while new versions keep adding commands and settings. The feature should help users check and safely repair configuration problems without overwriting their standards, mappings, or local choices.

The first version provides both an in-CAD command and a PowerShell entry point. Both should use the same diagnostics rules so local use, deployment checks, and future automation stay consistent.

## Scope

This version checks:

- Whether `CadToolkit.ini` exists.
- Whether required root settings are present.
- Whether `[Commands]` exists.
- Whether official commands are missing.
- Whether old official command labels need renaming, such as `文字样式规范=CT_TEXTSTYLESTANDARD`.
- Whether `[Commands]` contains documentation comments with `=`, which previously could confuse command panels.
- Whether `[LayerStandard]`, `[LayerMap]`, `[TextStyleStandard]`, and `[TextStyleMap]` exist.
- Whether `LayerMap` targets exist in `[LayerStandard]`.
- Whether `TextStyleMap` targets exist in `[TextStyleStandard]`.
- Whether layer standard rows follow `color|linetype|lineweight|plot`.
- Whether text style standard rows follow `font|bigfont|height|width|oblique`.

This version does not validate CAD runtime availability of linetypes, fonts, big fonts, or drawing-dependent objects.

## Repair Rules

Automatic repair must stay conservative. It may:

- Create a missing `CadToolkit.ini` from the embedded default config.
- Add missing root settings before the first section.
- Add a missing `[Commands]` section.
- Insert missing official commands in the expected command group area.
- Rename known old official labels to current labels.
- Remove or relocate duplicated explanatory `# ...=...` comments inside `[Commands]` when they are known template comments.

Automatic repair must not:

- Replace user-defined `[LayerStandard]`, `[LayerMap]`, `[TextStyleStandard]`, or `[TextStyleMap]`.
- Add default standard or map sections into an existing user config.
- Rewrite user aliases, whitelist rules, command order, or custom commands.
- Guess fixes for malformed standard rows.
- Guess missing map targets.

Before any repair writes to disk, create a timestamped backup next to the config file:

```text
CadToolkit.ini.bak-YYYYMMDD-HHMMSS
```

## Core Design

Add a config diagnostics module in `CadToolkit.Core`, for example `ConfigDiagnostics`. It should operate on text and paths, not CAD APIs.

Suggested public shape:

- `ConfigDiagnosticSeverity`: `Info`, `Warning`, `Error`.
- `ConfigDiagnosticIssue`: severity, code, message, line number, section, and whether it can be fixed automatically.
- `ConfigDiagnosticResult`: config path, issue list, fixed text preview, and whether changes are available.
- `Analyze(string text, string path)`: returns diagnostics only.
- `Repair(string text, string path)`: returns diagnostics and repaired text for safe fixes.
- `AnalyzeFile(string path)` and `RepairFile(string path)`: file helpers that use UTF-8 and create backups before writing.

Existing startup upgrade logic in `Config.EnsureConfig()` can keep its current behavior, but the implementation should share small rule helpers with diagnostics where practical. The diagnostics command should not silently run broad repairs on startup; explicit repair should happen only when the user chooses it or passes `-Fix`.

## CAD Command

Add a command:

```text
配置体检=CT_CONFIGCHECK
```

The command opens a simple WinForms report dialog. It should show grouped results:

- Errors
- Warnings
- Info

Buttons:

- `复制报告`
- `自动修复`
- `关闭`

When the user clicks `自动修复`, the command creates a backup, applies only safe fixes, reruns diagnostics, and refreshes the report. If no fixable issues exist, the button can be disabled or show a short message.

## PowerShell Tool

Add:

```text
CadToolkit\tools\check-config.ps1
```

Usage:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tools\check-config.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tools\check-config.ps1 -Path C:\CadToolkit\CadToolkit.ini
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tools\check-config.ps1 -Path C:\CadToolkit\CadToolkit.ini -Fix
```

Default path should be `C:\CadToolkit\CadToolkit.ini`. The script should build or load `CadToolkit.Core`, call the shared diagnostics logic, print a plain text report, and return:

- Exit code `1` when errors remain.
- Exit code `0` when there are only warnings or info.
- Exit code `0` when repair succeeds and no errors remain.

## Reporting

Report text should be useful in both CAD and terminal output:

```text
CadToolkit 配置体检
配置文件：C:\CadToolkit\CadToolkit.ini

错误 1 项
- [LayerMap] 8-受力点：找不到对应 [LayerStandard]

警告 2 项
- 缺少官方命令：改块基点=CT_CHANGEBASEPOINT（可自动修复）
- [Commands] 中存在说明性注释：# 格式：显示名称=CAD命令（可自动修复）

信息
- 已检查 17 个基础配置项、18 个命令、12 个图层标准、2 个文字样式标准
```

Use stable issue codes in tests, but keep user-facing messages in Chinese.

## Testing

Add `CadToolkit/tests/ConfigDiagnostics.Tests.ps1`.

Cover:

- Valid current project config has no errors.
- Missing root settings are reported as fixable and repaired before the first section.
- Missing `[Commands]` is reported and repaired.
- Missing official commands are reported and repaired.
- Old `文字样式规范=CT_TEXTSTYLESTANDARD` is renamed.
- Documentation comments with `=` inside `[Commands]` are reported as fixable and do not become commands.
- `LayerMap` target missing from `[LayerStandard]` is an error and is not automatically repaired.
- `TextStyleMap` target missing from `[TextStyleStandard]` is an error and is not automatically repaired.
- Malformed layer standard and text style standard rows are errors and are not automatically repaired.
- File repair creates a timestamped backup.
- CAD command registration and config command line are present.
- PowerShell script parses and supports `-Path` and `-Fix`.

Before merging implementation, run all CadToolkit tests and `git diff --check`.

## Risks

- Repair that is too aggressive could damage user standards. The first version only fixes known safe structural issues.
- A second set of upgrade rules could drift from `Config.EnsureConfig()`. Shared helper methods or tests must keep them aligned.
- Terminal encoding can hide Chinese messages in older PowerShell sessions. Tests should assert stable issue codes where possible, and scripts should use UTF-8 reads.
- CAD UI should not block startup. Diagnostics is an explicit command, not an automatic modal dialog.
