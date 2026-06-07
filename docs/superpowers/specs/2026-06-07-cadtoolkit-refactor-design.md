# CadToolkit Refactoring Design

**Date:** 2026-06-07
**Status:** Approved
**Scope:** CadToolkit plugin code quality and maintainability improvement

## Problem

Plugin.cs is 1710 lines with 19 command methods, helper methods, UI construction, and dialog logic all in one file. There are 21 silent `catch { }` blocks, no Undo groups for batch operations, duplicated selection patterns across 12 commands, and scattered UI construction code. Version numbers are hardcoded in 3 places.

## Phased Approach

### Phase 1: Zero-risk immediate wins

**Undo groups** for 5 batch commands:
- CT_LAYERSTANDARD
- CT_ALIGN
- CT_QUICKBLOCK
- CT_SETLAYER0
- CT_TEXTMERGE

Wrap each command body in `Db.StartUndoMark()` / `Db.EndUndoMark()` so Ctrl+Z reverts the entire operation at once.

**Catch improvements:**
Replace silent `catch { }` with tiered handling:
- Critical path (LayerStandard, QuickBlock, CenterLine): `catch (Exception ex) { Ed.WriteMessage("\nwarning: " + ex.Message); }`
- Config read/write: `catch (Exception ex) { Log("Config: " + ex.Message); }`
- Best-effort operations (EnsureLineType, ApplyLayerRule attributes): keep silent but log

### Phase 2: Structural reorganization

**File split** using `partial class CadCommands`:

| File | Content | Est. lines |
|------|---------|-----------|
| `Plugin.cs` | Entry, EnsureInit, shared helpers (CheckDoc, GetPendingOrSelection, Log, SimpleWildcardMatch, IsLayerWhitelisted, UiScale cache) | ~200 |
| `TextCommands.cs` | CT_FINDREPLACE, CT_ALIGN, CT_UNDERLINE, CT_TEXTBRUSH, CT_TEXTMERGE, CT_TEXTNUMBER | ~400 |
| `LayerCommands.cs` | CT_SETLAYER0, CT_LAYERSTANDARD, CT_ISOLAYER, CT_SELECTBYLAYER, CT_SELECTBYCOLOR | ~350 |
| `BlockCommands.cs` | CT_RENAMEBLOCK, CT_QUICKBLOCK, CT_SELECTBYBLOCK | ~200 |
| `DrawCommands.cs` | CT_CENTERLINE, CT_QUICKDIM, CT_INCCOPY, CT_FLATTEN | ~350 |

**Pattern extraction:**
Extract `GetSelectionOrAbort()` to eliminate 12 copies of:
```
EnsureInit();
if (!CheckDoc()) return;
var psr = GetPendingOrSelection();
if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n未选择对象。"); return; }
```

**UiScale caching:**
Cache DPI factor once instead of calling `Graphics.FromHwnd(IntPtr.Zero)` 33 times.

**TextNumber dialog:**
Move inline dialog code from Plugin.cs to `CadToolkit.UI/Dialogs.cs` as `TextNumberDialog`.

### Phase 3: Polish

**Version unification:**
Read version from `AssemblyVersion` attribute in csproj. Inject into autoload.lsp output and INI default config at build time via CI or build-all.bat.

**Config documentation:**
Add clear comments to `StripInlineComment` behavior and document limitations (no `#` or `;` at start of values).

**PanelBuilder:**
Extract ShowPanel layout logic (~180 lines) to `CadToolkit.UI/PanelBuilder.cs`.

## Verification

- Phase 1: Load plugin in CAD, run CT_LAYERSTANDARD + CT_ALIGN, verify Ctrl+Z reverts entire operation. Test error path by temporarily renaming CadToolkit.ini.
- Phase 2: Build all 3 platforms (build-all.bat). Verify all 18 commands still load and execute. Check that partial class compiles cleanly.
- Phase 3: Verify `CC` command shows correct version. Verify autoload.lsp prints correct version on load.

## Risks

- File split requires updating all 3 csproj files to include new .cs files
- Undo mark + Transaction nesting needs care: StartUndoMark must be before transaction, EndUndoMark after commit
- PanelBuilder extraction may break GstarCAD's IExtensionApplication registration if class visibility changes
