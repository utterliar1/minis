# Superpowers Skills Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a GitHub Actions workflow that mirrors upstream Superpowers skills into this repository and localizes skill list descriptions into Chinese.

**Architecture:** A small PowerShell script downloads the upstream repository archive, copies `skills/` into `codex-skills/skills/`, and applies Chinese descriptions from `codex-skills/translations.json`. A scheduled/manual GitHub Actions workflow runs the script and commits generated skill changes when they differ from the repository copy.

**Tech Stack:** PowerShell, GitHub Actions, JSON, Git.

---

### Task 1: Translation Table

**Files:**
- Create: `codex-skills/translations.json`

- [ ] **Step 1: Create a JSON translation map**

Add one key per currently installed personal skill. Each value contains the Chinese `description` that should replace the upstream `SKILL.md` frontmatter description.

- [ ] **Step 2: Validate JSON syntax**

Run: `Get-Content .\codex-skills\translations.json -Raw | ConvertFrom-Json | Out-Null`

Expected: command exits successfully with no output.

### Task 2: Sync Script

**Files:**
- Create: `codex-skills/sync-superpowers-skills.ps1`

- [ ] **Step 1: Create a PowerShell script**

The script should:
- Locate the repository root from its own path.
- Load `codex-skills/translations.json`.
- Download `https://github.com/obra/superpowers/archive/refs/heads/main.zip`.
- Extract the archive to a temporary directory.
- Replace `codex-skills/skills/` with the upstream `skills/` directory.
- Replace frontmatter `description` lines when a translation exists.
- Print GitHub Actions warnings for missing translations.

- [ ] **Step 2: Run the script locally**

Run: `powershell -ExecutionPolicy Bypass -File .\codex-skills\sync-superpowers-skills.ps1`

Expected: `codex-skills/skills/` is populated and missing translations, if any, are warnings rather than fatal errors.

### Task 3: GitHub Actions Workflow

**Files:**
- Create: `.github/workflows/sync-superpowers-skills.yml`

- [ ] **Step 1: Add scheduled and manual triggers**

The workflow should run on `workflow_dispatch` and a weekly schedule.

- [ ] **Step 2: Run sync and commit changed generated files**

The workflow should run the PowerShell script, stage `codex-skills/skills/`, and commit with `github-actions[bot]` only when staged changes exist.

### Task 4: Verification

**Files:**
- Verify: `codex-skills/skills/*/SKILL.md`
- Verify: `.github/workflows/sync-superpowers-skills.yml`

- [ ] **Step 1: Confirm translated descriptions**

Run: `Select-String -Path .\codex-skills\skills\*\SKILL.md -Pattern '^description:'`

Expected: known skills such as `brainstorming` have Chinese description text.

- [ ] **Step 2: Inspect git status**

Run: `git status --short`

Expected: new sync files are listed, and existing unrelated `CadToolkit` modifications remain untouched.
