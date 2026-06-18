# CadToolkit Batch Plot Polish Design

## Goal

Make batch plotting more reliable and easier to use after the first working GstarCAD path:

- Keep `DWG To PDF.pc3` working.
- Make PDF24 and other system printers use the right command-line answers.
- Offer more plot style choices instead of only `monochrome.ctb`.
- Add a small printable margin so frames do not sit directly on the sheet edge.

## Scope

This polish stays inside the existing batch plot feature. It does not add sheet set publishing, layout scanning, title block parsing, or a print preview.

## Design

### 1. Device-Specific Plot Flow

GstarCAD command-line plotting uses different prompts for file devices and system printers.

- File output devices such as `DWG To PDF.pc3` should:
  - use the expanded GstarCAD paper name when needed, such as `ISO full bleed A3 (420.00 x 297.00 毫米)`;
  - pass the target PDF file path;
  - answer page setup save as `N`;
  - answer continue plotting as `Y`.
- System printers such as `PDF24` should:
  - keep the configured short paper name such as `A3`;
  - answer print-to-file as `N`;
  - answer page setup save as `N`;
  - answer continue plotting as `Y`.

The existing helper that detects true file-output PDF devices remains conservative. Names like `PDF24`, `Adobe PDF`, and `Microsoft Print to PDF` stay in the system-printer path unless explicitly supported later.

### 2. Plot Style List

The dialog should populate the plot style dropdown from real `.ctb` and `.stb` files when possible.

Search order:

1. CAD plot style support paths if available through APIs or environment.
2. Common local plot style folders under CAD support directories.
3. Safe built-in fallback list:
   - `monochrome.ctb`
   - `acad.ctb`
   - `grayscale.ctb`

The combo box remains editable so a user can type a custom style name. The last chosen value is still saved to `CadToolkit.ini`.

### 3. Printable Margin

Add a batch plot margin percentage setting. Default: `2`.

Implementation approach:

- Keep using window plot and fit-to-paper.
- Expand the selected frame window around its center before sending it to CAD.
- A 2% margin means the plot window grows slightly, so the actual frame content becomes slightly smaller on paper.
- Apply the same expanded window to all platforms and devices.

This is safer than switching away from fit-to-paper because it preserves the current working scale behavior and only changes the plot window extents.

### 4. Configuration

Add one root config key:

```ini
BatchPlotMarginPercent=2
```

Rules:

- Missing config gets the default through the existing config upgrade mechanism.
- Existing user config values are preserved.
- The dialog saves the last margin value.

### 5. Error Handling

- If plot style discovery fails, log the error and use fallback styles.
- If margin parsing fails or is negative, treat it as `0`.
- Keep command text logging for GstarCAD so future prompt mismatches can be diagnosed from `%TEMP%\CadToolkit.log`.

### 6. Tests

Update `CadToolkit/tests/BatchPlot.Tests.ps1` to cover:

- `PDF24` and system printers do not use expanded GstarCAD paper names.
- System printer path answers print-to-file with `N`.
- File-output path still sends output filename and final confirmation.
- Plot style dropdown includes fallback styles and supports discovered styles helper.
- Config exposes and persists `BatchPlotMarginPercent`.
- Plot window expansion helper exists and is used before command construction / plot API calls.

## Acceptance Criteria

- `DWG To PDF.pc3` still prints to PDF without prompt mismatch.
- `PDF24` no longer asks for a `.plt` filename.
- The style dropdown offers more than `monochrome.ctb`.
- Default output has a small visible margin around the selected frame.
- Existing local `CadToolkit.ini` is not overwritten during deployment.
