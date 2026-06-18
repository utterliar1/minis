# CadToolkit Batch Plot Preflight Design

## Goal

Make batch plot safer before printing by showing exactly what will be printed.

This design adds a lightweight preflight list to the existing batch plot dialog. It also restricts plot style choices to `.ctb` files only.

## Scope

Included:

- Show selected frame count and frame block name.
- Show a preflight list with one row per frame.
- Show output filename for PDF devices.
- Show printer target text for physical/system printers.
- Warn when frame sizes are noticeably inconsistent.
- Add a copy button for the preflight text.
- Filter plot style choices to `.ctb`.

Not included:

- Graphical preview.
- Dragging to reorder print frames.
- Selecting output directory.
- File overwrite policies.
- Printer-paper live refresh.

## User Flow

1. User runs `CT_BATCHPLOT`.
2. User chooses a frame block template.
3. User window-selects a scope.
4. CadToolkit filters matching frame blocks and sorts them.
5. The dialog opens with settings and a preflight list.
6. User checks:
   - frame count;
   - frame order;
   - frame size;
   - orientation;
   - output filename or printer target.
7. User can copy the preflight text.
8. User confirms printing.

## Preflight Rows

Each frame row should contain:

- index: `001`, `002`, ...
- size: rounded drawing-unit width x height;
- orientation: `横向` or `纵向`;
- target:
  - PDF device: output file name, such as `Drawing-001.pdf`;
  - non-file printer: `发送到打印机`.

The row content is informational only. It does not change the existing sort or plot behavior.

## Size Warning

CadToolkit should compare frame sizes before printing.

Rule:

- Use the first frame as the reference.
- If another frame width or height differs by more than 3%, show:
  - `检测到图框尺寸不一致，请确认是否混选。`

This is a warning only. It does not block printing.

## Plot Style List

Only `.ctb` styles should appear in the dropdown.

Rules:

- Keep the current configured style if it ends with `.ctb`.
- Add CAD API style names only when they end with `.ctb`.
- Keep safe fallback styles:
  - `monochrome.ctb`
  - `acad.ctb`
  - `grayscale.ctb`
- Do not show `.stb`.
- Do not merge names without extension.

## UI Shape

Keep the dialog compact but slightly taller.

Suggested layout:

- Top summary: one line with frame block, count, and output mode.
- Existing settings controls remain above the preflight list.
- Preflight list appears below the settings controls.
- Warning label appears above or below the list.
- Bottom buttons:
  - `复制预检`
  - `确定`
  - `取消`

## Testing

Update `CadToolkit/tests/BatchPlot.Tests.ps1` to check:

- `BatchPlotDialog` accepts preflight rows.
- A preflight row model exists.
- PDF preflight rows use `BuildBatchPlotOutputPath`.
- Printer preflight rows show printer target text.
- Dialog contains a list view for preflight rows.
- Dialog contains a copy preflight button.
- Dialog contains the inconsistent size warning text.
- Plot style filtering keeps `.ctb`.
- Plot style filtering excludes `.stb`.

## Acceptance Criteria

- The user can see every planned print frame before printing.
- PDF output names match the names that will be generated.
- Non-file printer rows clearly show that output goes to printer.
- Mixed frame sizes are called out before printing.
- The plot style dropdown shows `.ctb` choices only.
- Existing printing behavior remains unchanged after confirmation.
