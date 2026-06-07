# Superpowers Skills Sync Design

## Goal

Add a GitHub Actions based sync flow in this repository that mirrors skills from `obra/superpowers` and localizes the skill list descriptions into Chinese.

The synced skills will live inside this repository, not directly in the local Codex user directory. A user can then copy, install, or otherwise consume the repository copy as needed.

## Scope

- Sync upstream skills from `https://github.com/obra/superpowers/tree/main/skills`.
- Store synced skills under `codex-skills/skills/`.
- Store Chinese prompt translations in `codex-skills/translations.json`.
- Replace each synced `SKILL.md` frontmatter `description` with the Chinese translation when one exists.
- Keep English descriptions for newly added upstream skills that do not yet have a translation.
- Emit workflow warnings for missing translations without failing the sync.
- Commit sync changes back to this repository from GitHub Actions when files changed.

## Non-Goals

- Do not directly modify `C:\Users\WLUP\.codex\skills` from GitHub Actions.
- Do not machine translate during workflow runs.
- Do not change business project files such as `BlockBrowser`, `CadToolkit`, or existing release workflows.
- Do not fail the workflow only because a newly synced skill has no Chinese translation yet.

## Architecture

The repository will contain a small sync package:

- `codex-skills/translations.json`: translation map keyed by skill folder name.
- `codex-skills/sync-superpowers-skills.ps1`: PowerShell sync script.
- `codex-skills/skills/`: generated mirror of upstream skills after localization.
- `.github/workflows/sync-superpowers-skills.yml`: scheduled and manual workflow.

The workflow checks out this repository, runs the PowerShell script, then commits and pushes changed files only when the sync result differs from the current repository state.

## Data Flow

1. The workflow runs on a schedule or through `workflow_dispatch`.
2. The script downloads the latest upstream repository archive from `obra/superpowers`.
3. The script extracts the upstream `skills/` directory to a temporary location.
4. The script replaces `codex-skills/skills/` with the extracted skills.
5. The script reads `codex-skills/translations.json`.
6. For each synced skill, the script updates the `description` field in `SKILL.md` frontmatter when a Chinese translation exists.
7. If a skill has no translation, the script leaves the upstream English description in place and prints a GitHub Actions warning.
8. The workflow commits and pushes only if `git status --short` shows changes.

## Translation Format

`translations.json` should stay small and explicit:

```json
{
  "brainstorming": {
    "description": "在任何创造性工作前必须使用：创建功能、构建组件、添加功能或修改行为。"
  }
}
```

Only the frontmatter `description` is localized. The full skill body remains upstream content so the sync remains faithful and easy to audit.

## Error Handling

- Missing `translations.json`: fail, because the sync flow cannot localize known skills.
- Invalid JSON: fail with a clear PowerShell error.
- Missing upstream `SKILL.md` in a skill folder: warn and continue.
- Missing translation for a skill: warn and continue.
- Network or archive download failure: fail, because no reliable sync result can be produced.

## Testing

Implementation should include a local script dry run by executing the PowerShell script from the repository root. Verification should confirm:

- `codex-skills/skills/` is populated.
- Known translated skills have Chinese `description` text.
- Missing translations produce warnings without failing.
- The workflow YAML is syntactically valid enough for GitHub Actions conventions.

## Maintenance

When upstream adds a new skill, the next sync will include it automatically with its English description. To localize the UI prompt for that new skill, add one entry to `codex-skills/translations.json` and rerun the workflow.
