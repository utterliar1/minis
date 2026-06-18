# CadToolkit Batch Plot Live Preflight Design

## Goal

Keep the batch plot preflight list consistent with the settings currently visible in the dialog.

## Problem

The preflight list is currently generated before the dialog opens. If the user changes the print device or filename rule inside the dialog, the preflight target column can no longer match the actual print action.

## Design

The dialog receives enough context to rebuild preflight targets locally:

- drawing name;
- output directory;
- original preflight rows with index, size, orientation, and size warning flag.

When the user changes:

- print device;
- filename mode;

the dialog refreshes:

- the top summary output mode;
- the target column in the preflight list;
- copy-preflight text.

Rules:

- PDF file devices show the actual PDF filename using the selected filename mode.
- Non-file devices show `发送到打印机`.
- Size and orientation columns do not change in this feature.
- Confirmation and plotting behavior stay unchanged.

## Acceptance Criteria

- Switching from `DWG To PDF.pc3` to `PDF24` changes preflight targets from PDF filenames to `发送到打印机`.
- Switching back to `DWG To PDF.pc3` shows PDF filenames again.
- Changing filename mode updates preflight filenames.
- Top summary says `输出：PDF` or `输出：打印机`.
- Copy preflight uses the current refreshed rows.
