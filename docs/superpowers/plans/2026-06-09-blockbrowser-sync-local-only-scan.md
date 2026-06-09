# BlockBrowser Local-Only Sync Scan Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make BBSYNC upload local mirror DWG files that are missing from NAS even when no local change journal entry exists.

**Architecture:** Add a focused sync helper that discovers local-only DWG files and returns synthetic Add journal entries. Merge those entries with the real journal before planning sync, while keeping NAS duplicate protection intact.

**Tech Stack:** C# .NET Framework 4.8 plugin code, PowerShell test scripts, MSBuild batch deployment.

---

### Task 1: Regression Test

**Files:**
- Modify: `BlockBrowser/tests/SyncLogic.Tests.ps1`
- Create: `BlockBrowser/Sync/LocalOnlySyncDiscovery.cs`

- [ ] **Step 1: Add the new sync source to the test compile list**

Add `Sync\LocalOnlySyncDiscovery.cs` to `$syncFiles` so PowerShell can compile and exercise the helper.

- [ ] **Step 2: Write the failing test**

Create a temporary local mirror with `Electrical\LocalOnly.dwg`, a temporary NAS directory without that file, and assert discovery returns one Add entry with path `Electrical\LocalOnly.dwg`.

- [ ] **Step 3: Verify RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\Documents\GitHub\minis\.worktrees\blockbrowser-sync-local-scan\BlockBrowser\tests\SyncLogic.Tests.ps1'
```

Expected: fail because `LocalOnlySyncDiscovery.cs` does not exist yet.

### Task 2: Discovery Helper

**Files:**
- Create: `BlockBrowser/Sync/LocalOnlySyncDiscovery.cs`
- Modify: `BlockBrowser/BlockBrowser.csproj`
- Modify: `BlockBrowser/BlockBrowser.AutoCAD.csproj`
- Modify: `BlockBrowser/BlockBrowser.ZWCAD.csproj`

- [ ] **Step 1: Implement `LocalOnlySyncDiscovery`**

The helper enumerates `*.dwg` under the local mirror, skips `.blockbrowser` and `.thumbs` folders, normalizes relative paths with backslashes, ignores paths already present in the journal, and only returns entries when NAS does not already have the same relative file.

- [ ] **Step 2: Include helper in all project files**

Add `<Compile Include="Sync\LocalOnlySyncDiscovery.cs" />` next to the other sync files in all three csproj files.

- [ ] **Step 3: Verify GREEN for targeted test**

Run the sync test again and expect it to pass.

### Task 3: Wire Into BBSYNC

**Files:**
- Modify: `BlockBrowser/BlockBrowserPlugin.cs`

- [ ] **Step 1: Merge discovered entries before planning**

In `SyncSafeUploadsToNas`, load real journal entries, append discovered local-only entries, build snapshots from the combined entries, and create the sync plan from that combined list.

- [ ] **Step 2: Preserve journal cleanup semantics**

Only remove uploaded paths from the original journal entries. Synthetic entries should not require creating or saving an empty journal.

- [ ] **Step 3: Run targeted sync test**

Run `SyncLogic.Tests.ps1` and expect pass.

### Task 4: Docs and Verification

**Files:**
- Modify: `BlockBrowser/使用手册.html`

- [ ] **Step 1: Document the behavior**

Add a short note in the NAS/BBSYNC section: BBSYNC also scans the local mirror for DWG files missing from NAS, but never overwrites existing NAS files.

- [ ] **Step 2: Run full PowerShell tests**

Run all `BlockBrowser/tests/*.Tests.ps1` scripts and expect exit code 0.

- [ ] **Step 3: Build and deploy**

Run:

```cmd
D:\Documents\GitHub\minis\.worktrees\blockbrowser-sync-local-scan\BlockBrowser\build-all.bat
```

Expected: release artifacts copied to `C:\BlockBrowser` while preserving local `config.ini`.
