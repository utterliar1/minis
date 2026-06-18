# CadToolkit Batch Plot Frame Block Selection Design

## Goal

Make batch plot selection predictable on large drawings.

The user first picks one title block reference as the frame template, then window-selects a scope. CadToolkit only plots block references in that scope that match the template block. Other entities are ignored.

## User Flow

1. Run `CT_BATCHPLOT`.
2. Prompt: `选择一个图框块作为模板：`
3. The command accepts only block references.
4. Prompt: `框选要打印的范围：`
5. CadToolkit collects block references inside the selected scope.
6. It filters them by the template block key.
7. It sorts matching frames from top to bottom, and left to right within the same row.
8. It opens the existing batch plot settings dialog.
9. The user confirms device, paper, plot style, margin, filename mode, rotation, and centering.
10. CadToolkit prints only the matched frame blocks.

## Matching Rule

Normal blocks match by block definition name.

Dynamic blocks match by their dynamic block definition name when available. This means different visibility or stretch states of the same dynamic title block count as the same frame type.

If the dynamic block definition cannot be resolved, CadToolkit falls back to the normal block definition name.

## Dialog Changes

The dialog should make the selected target obvious and reduce repeated text.

- Show a concise summary near the top:
  - `图框块：<name>`
  - `数量：<count>`
  - `输出：PDF 到 DWG 同目录，实体打印机直接打印`
- Keep the existing remembered settings:
  - printer or PDF device
  - paper
  - plot style
  - page margin in millimeters
  - filename mode
  - auto rotate
  - center plot
- Keep controls compact enough for repeated use.
- Keep `Esc` / Cancel behavior unchanged.

## Empty and Invalid Selection Handling

- If the template pick is cancelled, the command exits quietly.
- If the picked object is not a block reference, CAD selection rejects it before the command proceeds.
- If the scope selection is cancelled, the command exits quietly.
- If no matching frame block is found in the scope, show:
  - `未在选择范围内找到同名图框块。`
- If matching blocks exist but their extents cannot be read, skip only those blocks and continue with the valid ones.
- If all matching blocks fail extents reading, show:
  - `未找到可打印的图框范围。`

## Implementation Shape

Keep `BatchPlotCommands.cs` responsible for CAD selection and plotting.

Add helpers near the existing batch plot selection code:

- Resolve a batch plot block key from a selected `BlockReference`.
- Select candidate `INSERT` entities in a user-selected scope.
- Filter candidates by the resolved block key.
- Reuse the existing frame extent collection and sorting helpers.

Keep the print engine, GstarCAD command path, PDF output path, margin logic, and config behavior unchanged.

## Tests

Update `CadToolkit/tests/BatchPlot.Tests.ps1` to check structure:

- `CT_BATCHPLOT` prompts for a block template before frame collection.
- The template prompt uses `PromptEntityOptions`.
- The template prompt restricts selection to `BlockReference`.
- Candidate scope selection filters for `INSERT`.
- There is a helper for normal and dynamic block matching.
- The dialog receives or displays the selected frame block name.
- Existing batch plot config, margin, filename, GstarCAD command, and project inclusion checks still pass.

## Acceptance Criteria

- The user can select one title block, then window-select a large area.
- Only matching title block references in the area become print frames.
- Other selected entities no longer become accidental print frames.
- Dynamic title blocks of the same source definition are treated as the same frame type.
- The batch plot dialog clearly shows the frame block name and frame count.
- Existing `DWG To PDF.pc3`, PDF24/system printer behavior, margin, filename mode, and saved settings continue to work.
