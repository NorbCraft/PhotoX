# Changelog

All notable changes to PhotoX are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow the policy in [DEVELOPMENT.md](DEVELOPMENT.md#2-versioning).

## [Unreleased]

## [0.1.0] - 2026-10-29

First working version (vertical slice). Verified with automated tests on Linux. Windows use is pending.

### Added
- Built-in explorer with drives, folder tree, recent orders and remembered last folder.
- Thumbnail gallery with To do / Edited / Reviewed / Unsaved / Error states, filters All / To do / Done, progress and a collapsible details panel. HEIC and other unsupported files are shown with an explanation, and generated ID sheets are hidden.
- Editor with the crop active on open: 10×15 by default, oriented like the photo, largest centred rectangle, ratio locked, move and resize handles. Also 15×20 and 13×18 (centred on a 15×20 canvas), portrait/landscape, reset crop, output pixels and effective ppi, and a low-resolution warning.
- Exposure slider with − / + buttons, undo/redo, reset, before/after (Space).
- **Enter = Save & Next**: saves exactly the selected crop and edits, waits for success, then opens the next photo. Also Save (Ctrl+S), Previous/Next without saving (←/→), Back to folder (Esc), no wrap-around, held-key repeat ignored, and unsaved edits kept and marked when skipping.
- Safe saving: temp file, read-back check, the original preserved in `Original/` (collision-safe), a sidecar with saved edits, and an atomic replace. Unchanged photos are marked Reviewed without writing. Read-only, changed-externally, missing-file and disk-full cases give clear messages.
- Output: JPEG quality 95 with 4:4:4 chroma, or PNG with alpha and 16-bit kept; sRGB profile embedded; EXIF kept with orientation and dimensions updated and the stale thumbnail removed; effective-ppi density.
- English/Romanian, switchable live from the top bar and remembered.
- Neutral dark theme, PhotoX icon, logging to a local file.

### Changed
- Plan revision P-2 (prompt_02): simplified saving and adjustments, all open questions decided, new crop presets (13×18 on 15×20, whole photo with borders), tint, Finish Order, revised ID sizes with a 7 mm strip for 3×4, timestamped ID exports. Mockups updated; logo added.

### Added
- Planning documentation (plan revision P-1), concept mockups and the prompt archive.
