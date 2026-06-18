# Batch Plot Professional Polish Plan

Goal: make batch plot safer for architectural sheet delivery by improving preflight, title block metadata, filenames, and sorting.

Scope for this pass:
- Read sheet number and sheet title from selected title block attributes.
- Add preflight columns for sheet number, sheet title, and status.
- Add filename mode `SheetNumberName` for `图号 图名.pdf`.
- Add sort mode `Position` and `SheetNumber`.
- Detect duplicate output filenames, size mismatch, missing plot style, missing device, and invalid output directory in preflight status.
- Keep existing print engine behavior unchanged.

Implementation steps:
- Extend config defaults and diagnostics for `BatchPlotSortMode`.
- Extend frame and preflight models with title block metadata and status.
- Extract title block attributes when collecting frames.
- Build output filenames from each frame when metadata filename mode is selected.
- Refresh dialog preflight targets/status when device or filename mode changes.
- Add structural tests before implementation and verify red/green.
