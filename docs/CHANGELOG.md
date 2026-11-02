# Changelog

All notable changes to PhotoX are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow the policy in [DEVELOPMENT.md](DEVELOPMENT.md#2-versioning).

## [Unreleased]

## [0.14.0] - 2026-11-02

### Fixed
- **Printing on the Citizen CY-02 without a white strip.**
  - **The driver's papers are now recognised:** its 10×15 page is 156.1 × 105.0 mm, with about 1.7 mm of bleed on each side that the printer cuts off. PhotoX expected at most 2.5 mm of difference, so it found no 10×15 paper, and a paper chosen by hand got the photo at exactly 10×15 in the middle of the larger page, so any slightly off-centre cut could leave a thin white strip.
  - **Crop now fills the whole page, bleed included.** So do edits without borders.
  - **Exact sizes stay exact:** Whole, photos with borders, 9×13, 13×18 and custom sizes, ID sheets, collages and the calibration sheet keep their true size, with their edge carried into the bleed.
  - **Paper choice:** PhotoX picks the plain paper, (6x4) or (6x8), before the "PR" and ×2 papers.
- When the mouse rested on the selected thumbnail its border turned grey; it stays blue now. The black square focus frame no longer appears over the selection when moving with ↑ / ↓.
- After Backspace (or Up) the keyboard stays in the gallery instead of jumping to the folder tree, and the folder just left is selected.

### Changed
- **Print preview:** shows each print as it will look after the printer's cut, laid out on the selected paper.
- **Editor keys:**
  - **Space** = next photo, **Backspace** = previous photo (Page Down / Page Up still work).
  - **Hold #** for before/after: the key left of Enter on a German keyboard, `\` on a US keyboard (found by its position); ID Photos accepts it too, besides Space.
- **Escape** closes Settings.

## [0.13.0] - 2026-11-02

### Added
- **Editor from the keyboard:** the arrow keys move the crop frame in small steps (Shift: larger), keeping its size and stopping at the photo's edge; a held arrow keeps moving. **Enter** saves exactly that crop and opens the next photo; **Page Up / Page Down** change the photo; **Escape** returns to the gallery.
- **Previous** and **Next** buttons in the editor again, above Close, Print and Save.
- **Copies in the print preview:** select pages (click, Ctrl/Shift + click, Ctrl + A or *Select all*) and change their copies with − / + or the keys + / − and 0–9. It is the same setting as in Photos, so both always agree; 0 leaves a photo out (its page stays, faded, so it can be set back).
- **Thumbnail size buttons:** − and + (magnifier icons) beside the slider give one more or one fewer thumbnail per row.
- **↑ / ↓** in the gallery move to the thumbnail above or below, following the grid as it is laid out.
- **Escape in ID Photos** clears the loaded photo, like Clear (an open dialog closes first).

### Changed
- The **print preview shows every chosen photo** as a page in a scrolling list, not only the first 20.
- The crop label over the photo shows only the print size in cm and the ppi, on a lighter background.
- Before/After looks pressed while Space shows the original, as when it is held with the mouse (editor and ID Photos).
- **Dialogs take the keyboard:** focus starts on Cancel, ← / → move between the buttons, Enter presses the focused one, Escape cancels; the gallery behind no longer moves.
- After **Delete**, the gallery keeps its place and selects the photo next to the deleted ones.
- **Back from the editor**, the gallery selects and shows the photo that was open last.
- **Releases:** one Windows installer per version. The portable zip is no longer built, and the build artifact and the GitHub Release carry only the installer and its checksum (about 79 MB instead of about 220 MB).

## [0.12.1] - 2026-11-02

### Fixed
- **ID Photos could crash PhotoX** when the photo was cleared or replaced while its sheet preview was still being drawn, or while a printed ID sheet was still waiting in Printing: both read the portrait's image after it had been freed. The sheet preview and Save now wait for the photo's lock, and a printed sheet reads the portrait file itself. Found as a crash of the Windows test run while releasing 0.12.0, which was therefore not released.

## [0.12.0] - 2026-11-02

### Added
- **Copies per photo** in Photos: every photo has a requested number of copies, **1** by default.
  - **0** leaves the photo out of Print selected and Print all; at most 99.
  - Each thumbnail shows it in its lower-left corner (×0, ×1, ×2 …), next to the edit status (upper right) and the printed copies (lower right). The new badge is white with a blue outline, grey at ×0.
  - **Copies [− n +]** in the sidebar changes the selected photo, or all selected photos at once (Ctrl/Shift + click, Ctrl + A). With different values it shows *Mixed* and sets them all alike.
  - In the gallery, **+** and **−** change the copies of the selection and **0–9** set them.
  - Kept between sessions in PhotoX's own data folder, never in the photos or the customer's folder. Printing does not reset them.
- **Printing** section in the Photos sidebar: *Selected for print* (photos with at least one copy) and *Printed* (copies printed through PhotoX).

### Changed
- **Print all** prints every photo of the folder with at least one copy; **Print selected** the selected ones; each photo with its own copies. When all of them are at 0, a message says so.
- **Printing workspace:** each preview page shows its copies (×N), photos beyond the first 20 are listed by name with their copies, and the summary reads like *4 photos · 7 copies on 10 × 15 cm*. Photos have no extra Copies setting any more, so nothing is multiplied; ID sheets, collages and the calibration sheet keep theirs. A change made in Photos while photos wait is followed.
- **ID Photos, step 1:** a click selects a photo, a double-click (or Enter) opens it for framing. The hint line is gone.
- **Collages:** the drag hint above the thumbnails is gone; the line under the collage names only zoom, move, swap and rotation.
- The Photos sidebar's photo preview shrinks in a short window, so the information, Copies and the actions stay in view.

### Fixed
- Clicking the open workspace again no longer removes its blue highlight; the same for the language, theme, ID steps, Crop/Whole and orientation switches.
- The folder tree button is a grey icon on light grey while the tree is hidden, instead of a black icon.

## [0.11.0] - 2026-11-01

### Added
- **Folders in the galleries:**
  - Photos, ID Photos and Collages list the open folder's subfolders as folder cards before the photos.
  - Double-click or Enter opens a folder, and the tree follows.
  - **Up** above the gallery, or Backspace, opens the parent folder.
- **Folder tree button** left of the workspaces: shows or hides the folder tree. Each workspace remembers its choice and its own folder, also after a restart.
- **ID Photos, step 1 Select photo:** the folder's subfolders and photos beside the tree, like the Photos gallery. A click on a photo opens step 2 *Framing & adjustment*; step 3 is *Print*.
- **Collages layout:** folder tree | photo thumbnails | collage | settings. The thumbnail panel can be resized, and it and the settings fold to a strip with a clear button to bring them back.
- **Delete photos** from the gallery: right-click menu or the Delete key, for one photo or the selection, after a confirmation with the count.
  - Nothing is deleted: the photos move into the `Original` folder, under a name of their own that never replaces a file there.
  - The new **Deleted** filter lists them; **Restore** brings them back to the folder with their edits, under a new name if the old one is taken.
- **Check for updates** in Settings: shows the installed version and asks GitHub for the latest published version.
  - **Windows:** after a confirmation it downloads the installer and checks it against its published checksum. It then asks about unsent print jobs and unsaved edits, starts the installer and closes PhotoX. Settings, print history, logs and photos stay.
  - **Linux:** opens the download page.
- **GitHub Releases:** a version tag prepares a draft release with the Windows installer (`PhotoX-X.Y.Z-win-Setup.exe`), its `.sha256` checksum and the portable zip. Publishing the draft makes the version available to Check for updates.
- **About** in Settings: the logo, the name, the version of the running build, "Photo preparation, made simple." with a short description, and a **Third-party licences** link that lists the components inside Settings.

### Changed
- **Discard originals** applies to the **whole open folder**, not only the selected photos.
  - It also permanently removes the photos marked Deleted; the warning says so and highlights Cancel.
  - Only files PhotoX recorded are deleted, each one checked first.
- Opening another folder while photos have **unsaved edits** now asks first (save, discard or stay); before, the edits were lost.
- The ID steps row spans the photo and the sidebar.
- **Logs on Windows** moved to `%APPDATA%\PhotoX\logs`, outside the folder an update replaces.

## [0.10.2] - 2026-11-01

### Changed
- **More icons beside button labels**, the buttons where they were:
  - **Photos gallery:** Print selected, Print all, Edit this photo, Use for ID photo, Discard originals (bin) and Restore original; icons in the thumbnails' right-click menu too.
  - **Photos editor and ID Photos:** Auto Adjust (wand).
  - **Collages:** Place in selected frame, Remove photo, Clear all, Change… for the output folder, and Fit photo and Remove photo in the photo menu.
  - **ID Photos:** Change… for the output folder.
  - **Printing:** Cancel on jobs in the queue.
  - **Settings:** Clear cache.
- Romanian: the Print buttons say **Imprimă**, a verb like Salvează and Închide beside them.

### Fixed
- Romanian labels beside icons no longer get cut off in narrow buttons (the editor's Imprimare/Salvează in 0.10.1, the gallery's paired buttons). A long label now wraps between words. The editor's side panel is at least 350 px wide, as is the ID panel, so their three buttons always fit.

## [0.10.1] - 2026-11-01

### Changed
- **Icons beside button labels** (the owner's choice B of the [button mockups](mockups/buttons/README.md)), with the buttons where they were:
  - **Photos editor:** Close, Print, Save, Save & Next and Reset; Undo and Redo as icon buttons.
  - **ID Photos:** Clear, Continue, and Back, Save and Print on the sheet step; the same Undo, Redo and Reset.
  - **Collages:** Save and Print.
  - **Printing:** Clear and Print.

## [0.10.0] - 2026-11-01

Fixes and refinements from [prompt_07](prompts/prompt_07.md). Verified with automated tests on Linux (including the owner's example photos, on copies); Windows CI runs on push. **Not yet printed on the Citizen CY-02 and not yet tried on the studio PC.**

### Fixed
- **Saved photos partly black.** A photo saved with its whole frame (no crop, for example a 2:3 camera photo at 10 × 15) and any adjustment came out black below the first eighth or so. The gallery thumbnail, ID Photos, Collages and printing showed it black; the editor showed the kept original. Printing an unsaved edit could be affected the same way. Fixed at the cause, with regression tests; photos saved like this by earlier versions still have their original in `Original` and come out right when saved again (or with Restore original).

### Added
- **9 × 13** crop preset: the photo at 9 × 13 cm, centred on a white 10 × 15 sheet (the calibration sheet shows its frame).
- **Editor actions:** Close, Print and Save above Save & Next. **Print** prints the photo as shown, with unsaved changes, without saving it.
- **ID Photos: Clear** removes the photo from the workspace (the file is not touched).
- **Printing:**
  - **Crop | Whole** for photos from Photos: Crop (the default of every new print) fills the paper; Whole shows the whole photo with white borders. Photos edited in PhotoX keep their own crop.
  - A clear message when the printer has no paper of the chosen size.
  - The calibration sheet no longer replaces the prepared photos for good: Back, Escape or *Cancel calibration* (and printing the sheet) bring them back.
  - **Escape** returns to the workspace the print came from, with its state.
  - All-time statistics show since when ("since 01.11.2026").
- Button and icon proposals for review in [docs/mockups/buttons](mockups/buttons/README.md) (not implemented).

### Changed
- **Faster saving** of turned or straightened photos: only the kept part is processed (straightening 3–4 s → about 1 s on the development PC).
- *Crop to fill* and *Whole + borders* are now **Crop** and **Whole** (Decupare, Întreagă).
- The Previous and Next buttons are gone from the editor (the arrow keys and the filmstrip still work).
- **Reset** also resets the straightening and the crop (the format and 90° turns stay).
- **ID Photos:** background brightness removed; the steps sit above the photo again, with the format and Clear beside them and the framing tools below.
- **Printing:** the paper is chosen by its actual size whatever the driver calls it; statistics are folded away when Print opens the workspace; the Printing tab moved to the right of the top bar.
- Collages: *Save JPG* is now **Save**.
- **Light mode** is the default for new installations; a theme already chosen is kept.

## [0.9.0] - 2026-10-30

A consistent interface, light and dark mode and a Printing workspace ([prompt_06](prompts/prompt_06.md)). Verified with automated tests and headless screenshots on Linux; Windows CI runs on push. **Not yet printed on the Citizen CY-02 and not yet tried on the studio PC.**

### Added
- **Light and dark mode**, chosen in Settings and remembered.
- **Printing workspace** (replaces the print dialog and the queue popup): Print in Photos, ID Photos or Collages opens it with the photos or the sheet; check the preview, printer, size, paper and copies, then Print. Next to it: the **queue** of jobs in progress (cancel while not printed), the **history** of today, this week or this month with each job's outcome, and **statistics** (prints today, this week, this month and in total, split by 10 × 15 and 15 × 20). *Back* returns to where the print came from. Closing PhotoX asks first if jobs have not yet been handed to Windows.
- **Icons** for the workspaces Photos, ID Photos, Collages and Printing; the Printing tab shows the jobs in progress.
- **Photos:** a slider for the thumbnail size (double-click: default); the folder's printed copies under Edit progress.
- **ID Photos:** **90° rotation** left and right; Redo.
- **Collages:** **drag photos** from the thumbnails (or the file manager) into a frame, replacing what was there; the move handle also appears on the photo under the mouse; a **right-click menu** to rotate a photo 90° left or right, fit it or remove it.
- The installer shows the PhotoX icon, name and **version** while it installs. Settings shows the program folder and how to uninstall.

### Changed
- **New PhotoX icon**, recreated from the owner's reference: a light tile, a camera outline in the app's own blue and dark PX. The X of the name is the same blue.
- **One editing sidebar** for Photos and ID Photos: Undo, Redo and Reset at the top, then the histogram and levels, **Auto Adjust** (the editor's Auto Correct is now called Auto Adjust too) and the sliders, same order and look.
- **ID Photos:**
  - the format, zoom, rotation, Auto Align and background brightness are in the toolbar;
  - the explorer and the folder's photos are on the left;
  - **3 × 4 cm** is the default format, with **8** photos per sheet;
  - the US visa format is shown as **5 × 5 cm** (it stays exactly 50.8 × 50.8 mm);
  - the steps are *1 Framing & adjustment* and *2 Print*; *Save sheet* is now **Save**.
- **Photos:** *Order progress* is now **Edit progress**; *Save changes* is now **Discard originals** (same warning and checks); the permanent multiple-selection hint is gone.
- Side panels can be resized in every workspace and keep their width.
- **JPEG quality** defaults to **100**. A quality chosen in Settings is kept; the old default 95 stored by earlier versions becomes 100 once.
- Print counters and the print history are kept in the user's PhotoX settings folder, outside the installation folder, so uninstalling or reinstalling keeps them (the 0.8 counters are copied once).

### Fixed
- ID Photos: undoing a slider change now restores the previous value (it kept the new one).
- Disabled buttons are no longer brighter than enabled ones in dark mode.

## [0.8.1] - 2026-10-30

### Fixed
- Print queue updates, print counters and thumbnails are handed to the screen through the dispatcher captured on the screen thread, never looked up from a worker thread.
- Tests: the headless UI tests share one application (a per-test application could be disturbed by background work of the previous test on Linux CI), and the preview-versus-saved check compares against a deliberately shifted photo instead of a fixed limit that Windows rendering exceeded (3.8 %).

## [0.8.0] - 2026-10-30

Direct printing and a simpler Photos workflow ([prompt_05](prompts/prompt_05.md)). Verified with automated tests on Linux and with a virtual printer in Windows CI. **Not yet printed on the Citizen CY-02.**

### Added
- **Print directly from PhotoX** through the installed Windows printer driver (the Citizen CY-02), without LinkServer:
  - **Print selected** and **Print all** in the gallery; **Print** on the ID print sheet and in Collages.
  - A print dialog: printer, 10 × 15 or 15 × 20, the driver's paper for that size, copies, a preview of the pages, and a reminder of which paper must be loaded.
  - **Printer settings…** opens the driver's own options (quality, glossy or matte, cutting); PhotoX remembers them per printer.
  - Photos with unsaved changes print as they are shown, without being saved. Printing never changes a photo.
- **Print queue** (printer button in the top bar): each job with its status in the Windows queue, cancel for pending jobs, and failures with the reason.
- **Print counter** in the lower-right corner of every thumbnail: how many copies Windows accepted from PhotoX.
- **Multiple selection** in the gallery: Ctrl + click, Shift + click, Ctrl + A.
- **Save changes:** saves the selected photos' changes and, after a clear warning, deletes the originals PhotoX kept for them.
- **Restore original:** puts the kept original back, after a warning that the edits are discarded.
- **Collages:** each photo's size in cm on the collage; change sizes by dragging the gaps between photos or typing the selected photo's width and height; Reset layout.

### Changed
- **Finish Order is gone.** Save changes and Restore original replace it; the gallery shows Print selected, Print all, Edit this photo (Enter), Use for ID photo, Save changes and Restore original.
- The top bar: workspaces on the left with the selected one in blue; the print queue, language, settings and the PhotoX logo and name on the right. The logo's PX is larger.
- **ID Photos:**
  - the sizes are compact buttons, with the explorer and the folder's photos right under them; a click on a photo loads it;
  - the "Source photo" section and the "Geometric sizes…" note are gone;
  - the print sheet has **Save sheet** and **Print**.
- Collages offer 10 × 15 and 15 × 20 only (20 × 30 removed).
- The "Originals are kept until…" note under the explorer is gone; the selected-photo panel folds away, but its actions stay.

## [0.7.0] - 2026-10-30

New requirements and refinements from [prompt_04](prompts/prompt_04.md). Verified with automated tests on Linux; Windows CI runs on push. Not yet tried on the studio PC or printed.

### Added
- **Collages**, a third workspace: choose photos in the built-in explorer, pick a 10×15, 15×20 or 20×30 sheet (portrait or landscape) and one of eleven templates (two to nine photos, side by side, stacked, one large with two or three small, grids). Double-click a thumbnail to place it in the selected frame; drag a photo to move it in its frame, use the wheel to zoom, double-click to reset, and drag the round handle onto another frame to swap. **Save JPG** writes a print-ready 300 ppi file named `Collage_<date>_<time>.jpg`.
- **Duplicate image** in the gallery's right-click menu (with Edit and Use for ID photo): the copy is named `name_copy.jpg` (then `name_copy_2.jpg` …), never overwrites anything, and appears in the gallery.
- **New PhotoX icon** (flat camera with a blue lens ring and PX) for the window, taskbar, program file, installer, Linux menu entry and the top bar.
- **Version visible:** the title bar shows "PhotoX 0.7.0"; the installer and portable zip carry the version in their names.
- **ID Photos:** **Auto Adjust**; **+ New size** for custom sizes in centimetres, remembered and removable, listed apart from the built-in sizes.
- Linux: a script that adds PhotoX with its icon to the application menu.

### Changed
- **One explorer for all workspaces.** ID Photos now chooses the portrait in the built-in explorer instead of the system file dialog.
- **Editor:**
  - **+ / −** on the keyboard change exposure; the − / + buttons beside the slider are gone.
  - One **Reset** button clears levels, exposure, contrast, white balance, tint and saturation. It keeps the crop and can be undone.
  - Switching portrait/landscape always gives the largest crop for the new orientation.
  - **Custom** is now a size in cm: the photo keeps exactly that size and is centred on the smallest sheet it fits, with white around it (9×13 on 10×15, 14×17 on 15×20).
  - **Before/After** shows the original only while the button is held, like Space.
  - The reset-crop icon is four arrows pointing to the corners; the angle display is centred; slider handles, Back and the orientation icons are larger; icons sit centred in their buttons.
  - Save & Next and Close have no description line.
- **Histogram:** it now shows the tones the levels triangles act on (after exposure and white balance), so moving a triangle no longer reshapes the histogram under it. It is drawn on a linear scale.
- **ID Photos:**
  - The frame always has the size's real proportions; 2×2 in is square.
  - Romanian 3×4: the head is slightly smaller (70 % of the height instead of 75 %) under the same top line.
  - 9×12 has no face guides and is framed by hand.
  - Four 2×2 in photos fit on one 10×15 sheet (two side by side are exactly 10.16 cm): that size uses no margin or spacing by default.
  - Double-click the zoom or rotation slider to reset it. Before/after uses Space only.
  - **Continue** instead of "Continue to sheet"; larger, numbered step tabs.
  - Sheets are saved as JPG only.
  - The size list shows centimetres, not pixels.
- **Gallery and top bar:** Start editing and Finish without description lines; "Finish order…" is now **Finish**; a clean settings icon.
- Unedited ID sizes in `id-sizes.json` from 0.6.0 are updated to the new defaults; edited ones are kept.

### Fixed
- ID Photos: changing the size did not change the frame's proportions on screen (the frame kept the previous width).

## [0.6.0] - 2026-10-29

Remaining approved plan items, after reviewing the plan against the implementation (prompt_03 §11). Verified with automated tests on Linux; Windows CI runs on push.

### Added
- **Print and ID sizes as data files:** `print-formats.json` and `id-sizes.json` in the PhotoX settings folder, created with the planned values on first start. The physical size of a print preset or the ID sizes can be corrected after print tests without a new version. Settings shows where the files are.
- **ID print scale correction (%)** in Settings (default 100, no change). If a calibration print measures 102 mm for 100 mm, setting 98 makes ID photos print at their nominal size.

### Changed
- The thumbnail-cache description in Settings shows the configured limits.
- Data files are written with readable characters (× and Romanian letters) instead of escape codes.

## [0.5.0] - 2026-10-29

The owner's feedback from testing 0.4.0 on Linux and Windows 11 ([prompt_03](prompts/prompt_03.md)). Verified with automated tests on Linux and in CI on Windows; the studio trial is pending.

### Changed: speed
- Sliders, levels and Auto Correct update the preview in about 45 ms instead of 0.3–0.6 s, and the straighten slider redraws immediately instead of taking about 1 s per step. Saving a 12 MP photo with Save & Next takes about 0.5 s instead of 1.9 s. Measured on a PC limited to 4 cores; see the implementation log.
- Opening a photo is faster (about 0.27 s instead of 0.44 s). Next uses the preloaded photo, and Previous reuses the photo just left.
- Thumbnails are decoded off the screen thread. The gallery is no longer rebuilt when returning from the editor or changing filters, and opening a folder no longer blocks the window.
- ID Photo Creator: rotating, moving, zooming and adjusting are about three times faster.

### Changed: editing
- Histogram and levels: one compact luminance histogram with a single bar and three draggable triangles (black point, midtones, white point).
- Sliders show their effect on the track: dark → bright, low → high contrast, blue → orange, magenta → green, grey → colourful. Saturation is now below Tint. The − / + exposure buttons change exposure directly.
- One fixed toolbar row: back-to-folder icon, print sizes, portrait/landscape rectangle icons, **Crop to fill | Whole + borders** as one segmented button, rotation, **Straighten**, reset crop, apply crop and before/after. On narrow windows it scrolls instead of wrapping.
- **Applied crop:** after Save, or by double-clicking inside the crop frame (which does not save), the preview shows the cropped result fitted to the viewer. Editing and undo continue; changing the crop brings the frame back; the next photo opens with the crop frame as usual.
- The adjustment panel (Photos and ID) is resized by dragging its separator; the width is remembered.
- When every photo of the order is done, **Save & Next becomes Close** (back to the gallery), and the gallery's **Start editing** button becomes **Finish Order**.

### Changed: browsing
- Only the four most recent orders, in a collapsible section.
- Folder tree like a desktop explorer: clicking a folder opens and expands it (clicking it again folds it); folders without subfolders have no arrow; the selected folder is highlighted.
- The selected photo in the gallery is clearly highlighted.

### Changed: ID Photo Creator
- The same histogram, levels and adjustment sliders as the editor (now including saturation and levels).
- 3×4 photos go on a **landscape** 10×15 sheet (8 copies). Other sizes use landscape when more copies fit (3.5×4.5: 8 instead of 6).
- The general cut-guide option is removed. 3×4 has a single option: the 7 mm white strip below each photo (on by default).

### Changed: window
- The PhotoX name and icon appear only once, in the window's own title bar. The logo is shown in Settings › About.

### Not changed
- Automatic background whitening: still waiting for suitable sample photos.

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
