# Changelog

All notable changes to PhotoX are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow the policy in [DEVELOPMENT.md](DEVELOPMENT.md#2-versioning).

## [Unreleased]

## [0.4.1] - 2026-10-29

Critical fix for photos disappearing after Finish Order on Windows (owner report, prompt_03 §6).

### Fixed
- **Saved photos became hidden on Windows.** PhotoX marked its temporary save file as Hidden, and replacing the photo with that file kept the attribute. Every saved photo was therefore hidden: Windows Explorer and the PhotoX gallery stopped showing it, which became visible when the folder was reloaded after Finish Order. With *Keep*, nothing was deleted; with *Erase*, only the backups in `Original/` were deleted, as designed. Saved photos are now never hidden, and opening a folder whose sidecar still lists hidden photos makes them visible again.
- Finish Order *Erase* is stricter. It deletes a backup only if the finished photo is still in the order folder and the backup still has the size and time recorded when PhotoX created it. Names in the sidecar must be plain file names inside `Original/`. Anything kept is listed, and its history stays so it can be identified later.
- A UI test switched the language off the UI thread. This caused an intermittent failure on the Linux CI.

## [0.4.0] - 2026-10-29

Auto Align and Windows packaging. Verified with automated tests on Linux. Windows installation is pending.

### Added
- **Auto Align** in the ID Photo Creator. It runs only when the button is pressed. It uses offline face detection (YuNet on ONNX Runtime, both MIT) to level the eyes, scale the head to the guides and centre it. The result is undoable and adjustable. When several faces are found, the operator chooses a person or keeps the manual framing; when none is found, a clear message is shown.
- Windows installer and portable package built with Velopack (unsigned; updates are manual).
- A CI workflow (Linux and Windows build and test, plus the Windows package) that runs once the GitHub remote exists.

### Not included
- Automatic background whitening: evaluated and not included (DEC-041). The manual background-brightness lift remains.

## [0.3.0] - 2026-10-29

ID Photo Creator. Verified with automated tests on Linux. Windows use and physical prints are pending.

### Added
- ID workspace in two steps: framing and adjustments, then the print sheet. Open a portrait, or use *Use for ID photo* from the Photos gallery. The portrait is never modified.
- Sizes 3×4, 3.5×4.5, 2×2 in (≈ 5.1×5.1 cm), 4×6 cm, 5×7 cm, 9×12 cm and a custom size in mm.
- Fixed frame with face oval, crown, eye, chin and centre guides. Drag to move, scroll to zoom, Shift+scroll to rotate, arrow keys to nudge; zoom and rotation sliders; undo and reset.
- Exposure (− / +), contrast, white balance and tint, plus a manual background-brightness lift that protects the face. Before/after.
- Print sheet on 4×6 in at 300 ppi (1200×1800 px): uniform grid, copy count with familiar counts (3×4: 4/6/8, up to 9), spacing, margins and cut guides. **3×4 photos keep a 7 mm white strip below each photo** (cut 30×47 mm).
- Saving to a remembered output folder as `ID_yyyyMMdd_HHmm.jpg` (or PNG), adding `_1`, `_2` … instead of overwriting.
- Calibration sheets (10×15 with ID reference rectangles and the 7 mm strip; 15×20 with the centred 13×18 frame) for checking physical output through the Citizen software.

## [0.2.0] - 2026-10-29

Complete ordinary-photo editing. Verified with automated tests on Linux. Windows use is pending.

### Added
- Crop presets: 13×18 (photo centred on a white 15×20 canvas at true scale) and Custom ratio. Composition modes *Crop to fill* and *Whole photo with white borders*, the latter previewed as it will be saved.
- Contrast, saturation, white balance and tint; levels (black, midtones, white); live luminance + RGB histogram of the crop.
- Auto Correct: conservative exposure, levels and mild colour from the histogram; one undo step, fully adjustable.
- 90° rotation and straightening (±10°). The crop stays inside the image content, and the geometry is identical in preview and output.
- Finish Order: unsaved edits are saved or the operator returns to editing, then one confirmation with *Erase originals and edit history* (default) or *Keep*. Only PhotoX's own files are removed; unknown files and ID exports are kept.
- Close prompt when photos have unsaved changes (save them, close without saving, or cancel).
- Persistent thumbnail cache (1 GB, 30 days, Clear cache in Settings; Finish Order removes the order's entries); preloading of the next photo.
- Settings: JPEG quality, thumbnail cache, about and log location.
- HEIC and other unsupported files are listed with an explanation.

### Changed
- Straightening uses Magick.NET `Distort` with an explicit viewport, so the crop maps to the same content in the preview and the saved file.

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
