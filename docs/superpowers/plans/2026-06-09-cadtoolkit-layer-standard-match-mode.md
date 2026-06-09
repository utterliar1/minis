# CadToolkit Layer Standard Match Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change CadToolkit layer standardization so `[LayerMap]` aliases use exact matching by default, wildcard matching only when `*` is present, and previews explain match reasons.

**Architecture:** Keep the existing WinForms preview and command flow. Add small helper result types in `Plugin.cs` so matching can return the winning pattern and match mode while preserving the existing `MatchLayerRule` API for compatibility.

**Tech Stack:** C# .NET Framework 4.8 plugin code, PowerShell reflection tests, INI configuration files.

---

### Task 1: LayerMap Match Semantics

**Files:**
- Modify: `CadToolkit/tests/LayerStandardMatching.Tests.ps1`
- Modify: `CadToolkit/src/CadToolkit/Plugin.cs`

- [ ] **Step 1: Write failing tests**

Update `LayerStandardMatching.Tests.ps1` so ordinary aliases are exact, wildcard aliases match contains, and numeric aliases remain exact unless wildcarded.

- [ ] **Step 2: Run the matching test**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\LayerStandardMatching.Tests.ps1`

Expected: fail because the current matcher still treats ordinary text aliases as contains matches.

- [ ] **Step 3: Implement minimal matcher change**

In `Plugin.cs`, make `IsLayerAliasMatch` use `SimpleWildcardMatch` for all aliases. Keep `MatchLayerRule` returning `LayerStandardRule`.

- [ ] **Step 4: Run the matching test**

Expected: matching tests pass.

### Task 2: Preview Reason Text

**Files:**
- Modify: `CadToolkit/tests/LayerStandardMatching.Tests.ps1`
- Modify: `CadToolkit/src/CadToolkit/Plugin.cs`
- Modify: `CadToolkit/src/CadToolkit/LayerCommands.cs`

- [ ] **Step 1: Write failing tests**

Add reflection tests for helper methods that return:

- Winning alias and match mode for `[LayerMap]`
- Winning whitelist item and match mode
- Preview text containing match reasons and fallback wording

- [ ] **Step 2: Run the matching test**

Expected: fail because helper result methods and reason text are not implemented.

- [ ] **Step 3: Implement helper result types**

Add non-public helper result classes in `Plugin.cs`:

- `LayerRuleMatch`
- `LayerPatternMatch`

Use them from `LayerCommands.cs` to fill `LayerStandardPlan.Reason`.

- [ ] **Step 4: Update preview formatting**

Update `FormatLayerPlan` so each migrated, fallback, and whitelist line includes a reason.

- [ ] **Step 5: Run the matching test**

Expected: preview and helper tests pass.

### Task 3: Default Config Migration

**Files:**
- Modify: `CadToolkit/CadToolkit.default.ini`
- Modify: `CadToolkit/CadToolkit.ini`
- Modify: `CadToolkit/src/CadToolkit.Core/Config.cs`
- Modify outside repo: `C:\CadToolkit\CadToolkit.ini`
- Modify tests: `CadToolkit/tests/LayerStandardMatching.Tests.ps1` or `CadToolkit/tests/ConfigUpgrade.Tests.ps1`

- [ ] **Step 1: Write failing config tests**

Assert project config files and embedded defaults contain wildcard aliases such as `*设备*`, and do not contain the old plain Chinese alias line.

- [ ] **Step 2: Run config tests**

Expected: fail because configs still use old aliases.

- [ ] **Step 3: Update repo configs**

Replace only `[LayerMap]` aliases with the approved wildcard/default-exact version.

- [ ] **Step 4: Update local config**

Record hash of `C:\CadToolkit\CadToolkit.ini`, replace only its `[LayerMap]` section, and verify other sections remain unchanged.

- [ ] **Step 5: Run config tests**

Expected: config tests pass.

### Task 4: Full Verification And Commit

**Files:**
- All changed files.

- [ ] **Step 1: Run all CadToolkit tests**

Run: `Get-ChildItem CadToolkit\tests -Filter *.ps1 | ForEach-Object { powershell -NoProfile -ExecutionPolicy Bypass -File $_.FullName }`

Expected: all tests exit 0.

- [ ] **Step 2: Run local deploy**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\deploy-local.ps1`

Expected: AutoCAD, ZWCAD, and GstarCAD deploy successfully without overwriting unrelated local config sections.

- [ ] **Step 3: Review diff and status**

Run: `git diff --check`, `git status --short --branch`.

Expected: no whitespace errors and only intended files changed.

- [ ] **Step 4: Commit implementation**

Commit message: `feat(CadToolkit): make layer map matching explicit`
