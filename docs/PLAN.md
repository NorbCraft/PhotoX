# PhotoX — Project Plan (Source of Truth)

| | |
|---|---|
| **Plan revision** | **P-2** (2026-10-29), updated with the owner's feedback of [prompt_03.md](prompts/prompt_03.md) (marked "prompt_03") the requirements of [prompt_04.md](prompts/prompt_04.md) (marked "prompt_04", 2026-10-30) [prompt_05.md](prompts/prompt_05.md) (direct printing and the new Photos workflow, marked "prompt_05") and [prompt_06.md](prompts/prompt_06.md) (UI consistency, light and dark mode, the Printing workspace, marked "prompt_06"). Supersedes P-1. |
| **Approval** | Approved for implementation by the owner in [prompt_02.md](prompts/prompt_02.md), which supersedes conflicting parts of [prompt_01.md](prompts/prompt_01.md). |
| **Current version** | see `Directory.Build.props` and [CHANGELOG.md](CHANGELOG.md) |

This file states **what** PhotoX must do. Supporting documents reference its IDs:

| Document | Purpose |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | How it is built: projects, image processing, saving, ID photos |
| [ROADMAP.md](ROADMAP.md) | Milestones and versions |
| [OPEN_QUESTIONS.md](OPEN_QUESTIONS.md) | Questions and their decisions (all P-1 questions decided) |
| [DECISIONS.md](DECISIONS.md) | Decision log |
| [TEST_PLAN.md](TEST_PLAN.md) | Verification |
| [DEVELOPMENT.md](DEVELOPMENT.md) | Conventions and agent rules |
| [mockups/README.md](mockups/README.md) | UI concepts |

If documents conflict, this plan wins. The conflict is recorded in OPEN_QUESTIONS.md and fixed.

## 1. Vocabulary

Requirements have two **independent** attributes:

- **Source** (provenance): **Owner** means stated by the owner in a prompt. **Planner** means a detail added during planning and approved with the plan.
- **Progress:**
  - **Planned:** not started.
  - **In progress.**
  - **Implemented:** code exists and is covered by automated tests.
  - **Verified:** also confirmed manually or physically, as stated in [TEST_PLAN.md](TEST_PLAN.md).
  - **Deferred:** after 1.0.

Removed requirements are listed in §9 and are not active. Priority: **M**ust / **S**hould / **C**ould. **MS** is the target version ([ROADMAP.md](ROADMAP.md)).

## 2. Product and scope

PhotoX is a simple, offline desktop tool for **one small photo studio**, used daily by the owner's mother. It is not a product for sale or distribution. Possible future sharing must not add requirements now.

**Priorities:**
1. A simple, reliable daily workflow.
2. Good photographic output.
3. Straightforward implementation and maintenance.
4. Responsive operation on typical studio hardware.
5. Minimal unnecessary complexity.

The operator works on **copies** of customer photos, and independent backups exist outside PhotoX. PhotoX keeps essential safeguards against incomplete saves, but no transaction or crash-recovery framework.

**Not in scope:**
- a general-purpose photo editor;
- a file manager;
- a printing platform or printer-management application (PhotoX prints through the installed Windows driver, prompt_05);
- an extensible framework;
- batch editing;
- HEIC/HEIF;
- country-specific biometric templates;
- monitor colour management;
- touch optimisation.

## 3. Environment

| Item | Value |
|---|---|
| Production | Windows 11 x64 (reference: 4-core CPU, 16 GB RAM, SSD, 1080p display) |
| Development | Linux Mint. Windows 10, macOS and ARM are not required. |
| Stack | C#, .NET 10, Avalonia UI, MVVM, Magick.NET ([ARCHITECTURE.md](ARCHITECTURE.md)) |
| Printing | Saved files are printed with Citizen's software (CY-02 printer). Future integration via Citizen LinkServer hot folders. |
| Languages | Romanian and English, switchable on the main screen and remembered |

## 4. Workflows

**W-1: Ordinary order** (prompt_05: Open folder → Select photos → Edit if needed → Print or Discard Originals).
1. Open the order folder in the built-in explorer and see thumbnails with status, print count and progress.
2. Select photos (Ctrl/Shift + click, Ctrl + A). **Print selected** or **Print all** opens the **Printing** workspace with them as they are now, including unsaved edits, without saving (prompt_06).
3. To edit, open a photo. On an unedited photo the crop tool is active with **10×15**, oriented like the photo, using the largest centred rectangle; the ratio is locked.
4. Adjust the crop and corrections. Press **Enter** (= **Save & Next**). PhotoX saves with *exactly* the current crop and edits, **waits for success**, then opens the next photo. At the end there is no wrap-around.

**W-2: Re-edit.** Reopening a saved photo restores its saved crop and adjustments, and re-renders from the preserved original (no cumulative JPEG loss).

**W-3: Discard Originals / Restore Original** (prompt_05, replaces Finish Order; named *Save Changes* until prompt_06). **Discard Originals** saves the unsaved edits of the selected photos and, after a clear warning, permanently deletes the originals PhotoX kept for them. **Restore Original** puts the kept original back after a warning that the edits are discarded.

**W-4: ID photo** (prompt_05/prompt_06: Choose format → Select photo in explorer → Align/Edit → Save or Print).
1. Choose a size in the toolbar (3 × 4 cm by default, prompt_06).
2. Click the portrait in the explorer on the left (or *Use for ID photo* in Photos). Turn it by 90° if it was taken in the wrong orientation.
3. Frame the face using the oval and guides; optionally press **Auto Align**, then correct by hand.
4. Adjust the photo (optionally **Auto Adjust**) in the same sidebar as in Photos.
5. Go to the sheet (*2 Print*), choose the copy count and options, and press **Save** (a timestamped JPG in the remembered ID output folder) or **Print**. The portrait is never modified.

**W-5: Collage** (prompt_05: Select template → Add photos → Adjust layout → Save or Print).
1. Choose the sheet (10×15 or 15×20), its orientation and a template.
2. Add photos from the built-in explorer, swap them, and frame each one inside its slot.
3. Adjust the layout: drag the gaps or type a slot's size in cm.
4. Press **Save JPG** (a timestamped, print-ready file in the remembered collage folder) or **Print**. The photos are never modified.

**W-6: Printing** (prompt_06). **Print** in Photos, ID Photos or Collages opens the **Printing** workspace with what was chosen: check the preview, the printer, the size (10 × 15 or 15 × 20, fixed for sheets and collages), the paper and the copies, then press **Print**. The queue of this session, the history (today, this week, this month) and the statistics are in the same workspace; *Back* returns to the workspace that sent the print.

## 5. Functional requirements

Columns: P = priority; Src = source; MS = target version; Progress; Tests = suites in [TEST_PLAN.md](TEST_PLAN.md).

### 5.1 Shell, browsing and language

| ID | Requirement | P | Src | MS | Progress | Tests |
|---|---|---|---|---|---|---|
| FR-001 | **Built-in folder explorer:** drives (fixed and removable), folder tree, remembered last folder, the **four most recent orders in a collapsible section**. The tree behaves like a desktop explorer: clicking a folder selects, opens and expands it; subfolders are nested; folders without subfolders have no arrow; the selected folder is clearly marked (prompt_03). Hidden/system folders and `Original` are not shown as part of an order. **The same explorer serves Photos, ID Photos and Collages**; ID Photos shows it on the left with the folder's photos and a click on a photo loads it (prompt_05, prompt_06), Collages chooses photos with it (prompt_04). The explorer panel can be resized by dragging its separator (prompt_06). | M | Owner | 0.1.0, 0.5.0, 0.7.0, 0.8.0 | Implemented | TS-10, TS-18 |
| FR-002 | **Thumbnail gallery:** JPEG/PNG photos with filename and basic info. Clear status: To do, Edited, Reviewed, Unsaved, Error/Unsupported. Filters **All / To do / Done**: *To do* shows only photos not yet edited or reviewed. Natural filename sort. Generated ID sheets (`ID_*` timestamp names) are excluded by default. Order position and progress (e.g. 23 / 85) are shown. The selected photo is clearly highlighted (prompt_03). The lower-right corner of each thumbnail shows its print count (FR-082). A compact slider sets the thumbnail size (double-click: the default size; remembered). The details panel shows **Edit progress** (edited / total) and the copies of the folder's photos printed through PhotoX, kept apart (prompt_06). No permanent hint about multiple selection (prompt_06). | M | Owner | 0.1.0, 0.8.0 | Implemented | TS-10, TS-18 |
| FR-008 | **Four workspaces**, *Photos*, *ID Photos*, *Collages* (prompt_04) and *Printing* (prompt_06), each with a small icon, switched from a persistent top bar (workspaces on the left, the selected one in blue; the Printing tab shows the number of jobs in progress, amber when Windows reports a problem). | M | Owner | 0.1.0, 0.7.0, 0.9.0 | Implemented | TS-18 |
| FR-009 | **English/Romanian**, switchable at runtime from the top bar; choice remembered; culture-correct numbers (RO `3,5 × 4,5 cm`); comma-below ș/ț; consistent terminology and form of address. | M | Owner | 0.1.0 | Implemented | TS-16 |
| FR-039 | **Light and dark mode** (prompt_06): chosen in Settings, applied at once and remembered; the same layout, controls and selection colours in both, with a neutral grey behind photos. Large clear primary actions, a browse details panel whose photo preview folds away while the actions (FR-041, FR-050, FR-072) stay visible, the PhotoX logo and name (the X in the lens colours) at the right of the top bar and the selected workspace in blue (prompt_05), a recognisable folder/back icon for *Back to folder*, and side panels in every workspace (explorer on the left, settings on the right) that are resized by dragging their separator, with sensible minimum widths, remembered, and the image using the remaining space (prompt_03, prompt_06). | M | Owner | 0.1.0, 0.5.0, 0.9.0 | Implemented | TS-18 |
| FR-040 | **Settings** for infrequent options: light or dark mode (prompt_06), print-format and ID-size dimensions (data files, location shown in Settings), JPEG quality (default **100**; a quality chosen by the operator is kept, while the old default 95 stored by earlier versions becomes 100, prompt_06), ID print scale correction, the program folder and how to uninstall, thumbnail cache (size and age shown, *Clear cache*), ID output folder (on the sheet step). Not needed for daily work. | M | Planner | 0.2.0, 0.6.0 | Implemented | TS-18 |
| FR-041 | **Multiple selection** in the gallery (prompt_05): Ctrl + click, Shift + click and Ctrl + A, clearly highlighted. **Print selected**, **Discard Originals** and **Restore Original** act on the selection; **Edit this photo** and **Use for ID photo** need one photo. | M | Owner | 0.8.0 | Implemented | TS-18 |
| FR-053 | **Thumbnails:** generated in the background (visible first); persistent cache capped at 1 GB, 30-day retention, with *Clear cache*. Cache entries of an order are removed by Finish Order where practical. | M | Owner | 0.1.0 (gen), 0.2.0 (cache) | Implemented | TS-11 |
| FR-054 | **Removable-media warning:** opening a folder on removable media shows a clear warning recommending a local working copy. Editing is allowed. | S | Owner | 0.2.0 | Implemented | TS-18 |
| FR-077 | **Logo, application icon and version:** a flat PhotoX icon recreated from the owner's reference (prompt_06): a light rounded tile, a camera outline in the app's primary blue (#3B82F6, the colour of the primary buttons and selections) and dark PX lettering; SVG master, PNG exports and a multi-resolution ICO, with a 16–24 px variant; used for the window, taskbar and executable, the Windows installer, the Linux desktop entry, the About screen and the top bar, where the logo and the name "PhotoX" (the X in the lens colours) sit at the right, after Settings, the X in the primary blue (prompt_04, prompt_05, prompt_06, DEC-050, DEC-061). The native title bar shows the name and version once (prompt_03, DEC-048); the installer file names include the version (prompt_04), and the installer shows the name and version while it installs (prompt_06). | S | Owner | 0.4.0, 0.7.0, 0.8.0 | Implemented | TS-18 |
| FR-078 | **Duplicate image** in the gallery's right-click menu: copies the photo in its folder as `name_copy.ext` (then `name_copy_2.ext`, …), never overwrites, and shows the copy in the gallery (prompt_04). | S | Owner | 0.7.0 | Implemented | TS-10 |

### 5.2 Editor navigation and saving workflow

| ID | Requirement | P | Src | MS | Progress | Tests |
|---|---|---|---|---|---|---|
| FR-003 | **Default crop on an unedited photo:** crop tool active, preset **10×15**, orientation matching the photo, **largest centred rectangle** of the ratio, ratio locked. If the photo already has the ratio, the whole image. Opening a photo never writes to disk. | M | Owner | 0.1.0 | Implemented | TS-01, TS-07 |
| FR-004 | **Save & Next (Enter and button):** save using the **current edits and exactly the currently selected crop** (never reset, re-centred or enlarged), **wait until the save succeeds**, then open the next photo. On failure: stay on the photo, keep the edits, explain the problem. No background save queue. Work runs off the UI thread so the UI stays responsive (a progress indicator is shown). | M | Owner | 0.1.0 | Implemented | TS-07 |
| FR-017 | **Navigation:** Save (Ctrl+S, stays on the photo), Previous, Next/Skip (no save), Reset, Back to folder, position and progress. No wrap from the last photo to the first; the end of the order is announced. When every photo is saved or reviewed, **Save & Next becomes Close** (back to the gallery, still on Enter). The gallery offers Print selected, Print all, Edit this photo (Enter), Use for ID photo, Discard Originals and Restore Original instead of Start editing / Finish Order (prompt_05, prompt_06). The buttons carry no descriptive lines under their labels (prompt_04). | M | Owner | 0.1.0, 0.5.0, 0.7.0 | Implemented | TS-07, TS-18 |
| FR-018 | **Keyboard:** Enter = Save & Next; Ctrl+S = Save; ←/→ (or PgUp/PgDn) = Previous / Next; Ctrl+Z / Ctrl+Y = Undo / Redo; Space (hold) = before/after; **+ / −** = exposure (prompt_04). Auto-repeat of held keys is ignored. Editor shortcuts never act while a dialog, text field, numeric input or a control that uses the key has focus. | M | Owner | 0.1.0, 0.7.0 | Implemented | TS-07, TS-18 |
| FR-019 | **Unsaved changes** are clearly indicated (editor and filmstrip/gallery). Skip/Previous keep *manual* edits in memory for the session (marked Unsaved) and never save them silently. Skipping never accepts or saves the default crop. | M | Owner | 0.1.0 | Implemented | TS-07 |
| FR-016 | **Undo/redo** of edit steps for the current photo. One **Reset** button clears the image adjustments (levels, exposure, contrast, white balance, tint, saturation) as one undoable step and **keeps the crop** (prompt_04). Undo, Redo and Reset sit at the top of the editing sidebar, the same in Photos and ID Photos (prompt_06). | M | Owner | 0.2.0, 0.7.0 | Implemented | TS-04 |
| FR-042 | **Before/after** comparison: the original shows only while Space or the before/after button is held, never as a toggle (prompt_04). In ID Photos only Space is used. | M | Owner | 0.2.0, 0.7.0 | Implemented | TS-18 |
| FR-052 | **Preloading** of adjacent photos where useful, within a memory budget. | S | Owner | 0.2.0 | Implemented | TS-11, TS-12 |
| FR-047 | **Low-resolution warning:** effective ppi of the crop at the selected print size is shown, with a warning below 200 ppi and a strong warning below 150 ppi. No automatic upscaling. | S | Planner | 0.2.0 | Implemented | TS-01 |

### 5.3 Crop, composition and adjustments

| ID | Requirement | P | Src | MS | Progress | Tests |
|---|---|---|---|---|---|---|
| FR-010 | **Crop presets:** **10×15** (default), **15×20**, **13×18**, **Custom** (W×H in cm). A custom size keeps exactly its physical dimensions and is centred on the smallest print sheet it fits (10×15, 15×20, 20×30) with white around it, so the saved file has the sheet's size and ratio (9×13 on 10×15, 14×17 on 15×20; prompt_04, DEC-051). Definitions are simple configuration data (§8.1): their physical dimensions are read from `print-formats.json` in the settings folder, which is created with the defaults (DEC-049). | M | Owner | 0.1.0 (10×15, 15×20), 0.2.0 (13×18, Custom), 0.6.0 (data file), 0.7.0 (custom on a sheet) | Implemented | TS-01 |
| FR-011 | **Print-format data:** ratio and physical size per preset (10×15 = 4×6 in = 101.6×152.4 mm, 2:3; 15×20 = 6×8 in = 152.4×203.2 mm, 3:4). Used for ratio, effective ppi and the 13×18 canvas. Independent of printer overscan. | M | Owner | 0.1.0 | Implemented | TS-01 |
| FR-012 | **Crop interaction:** move; resize with locked ratio; portrait/landscape toggle shown as recognisable rectangle icons; **switching orientation gives the largest valid crop of the new orientation** (prompt_04); dimmed outside area; thirds grid; *Fit* (largest frame) and *Reset crop* (four arrows to the corners). The frame stays inside the image. Output pixel size and ppi are shown. **Applying the crop** (double-click inside the frame, the crop button, or after Save) shows the actual result fitted to the viewer without the frame; editing and undo continue; double-click does not save; each photo opens with the crop frame (prompt_03, DEC-045). One fixed toolbar row keeps its order and scrolls rather than wrapping on narrow windows. | M | Owner | 0.1.0, 0.5.0 | Implemented | TS-01, TS-18 |
| FR-049 | **Composition modes:** **Crop to fill** (default) and **Whole photo with white borders**, shown as one segmented button with the active part highlighted (prompt_03). The latter keeps the entire photo and extends the canvas with white to the output ratio, without resampling its pixels. | M | Owner | 0.2.0 | Implemented | TS-01, TS-06 |
| FR-073 | **13×18 on 15×20:** crop to exactly 130:180 (or 180:130 landscape), then centre it on a white 15×20 canvas at a consistent physical scale, so that printing the canvas at 15×20 reproduces the photo at 130×180 mm. Photo pixels are preserved. | M | Owner | 0.2.0 | Implemented | TS-01, TS-06 |
| FR-006 | **Exposure and histogram correction** (umbrella): detailed by FR-013, FR-043, FR-044 and FR-045. | M | Owner | 0.1.0–0.2.0 | Implemented | TS-03, TS-04 |
| FR-013 | **Exposure:** slider (range ±2 EV) and the keyboard **+ / −** keys (±1/6 EV per press), which stay in sync with the slider. No on-screen − / + buttons (prompt_04). | M | Owner | 0.1.0, 0.7.0 | Implemented | TS-04 |
| FR-014 | **Contrast, white balance (temperature), tint and saturation** sliders, in that order below exposure, with neutral centre and double-click reset. Slider tracks show their effect: exposure dark → bright, contrast low → high (grey to black/white), white balance blue → orange, tint magenta → green, saturation grey → colourful (prompt_03). Slider handles are large enough to grab easily (prompt_04). | M | Owner | 0.2.0, 0.5.0, 0.7.0 | Implemented | TS-04 |
| FR-015 | **Rotation** 90° left/right and **straightening** (±10°) in the toolbar, straightening right after the rotation buttons, with smooth preview while dragging and the angle shown centred beside the slider; the crop stays inside the valid area. | M | Owner | 0.2.0, 0.5.0 | Implemented | TS-01, TS-02 |
| FR-043 | **Live histogram:** compact luminance histogram inside the crop on a linear scale (separate RGB curves are not needed, prompt_03). It shows the tones that enter the levels step (after exposure and white balance), so each levels handle sits on the tones it acts on (prompt_04, DEC-054). | M | Owner | 0.2.0, 0.5.0, 0.7.0 | Implemented | TS-03 |
| FR-044 | **Levels:** one horizontal bar under the histogram with three draggable triangles for black point, midtones and white point; the preview updates while dragging; reset (prompt_03). | M | Owner | 0.2.0, 0.5.0 | Implemented | TS-03 |
| FR-045 | **Auto Adjust (single photo;** called Auto Correct before prompt_06, now one name in both workspaces**):** conservative exposure/levels (and mild colour) correction. It sets ordinary slider values, so the result stays adjustable and undoable. No histogram equalisation. | M | Owner | 0.2.0 | Implemented | TS-04 |

### 5.4 Files, saving and order lifecycle

| ID | Requirement | P | Src | MS | Progress | Tests |
|---|---|---|---|---|---|---|
| FR-005 | **Temporary original:** on the first effective save, the untouched source is preserved in `Original/` (inside the opened folder), and the edited photo is written at the original location and filename. Later saves keep that original and re-render from it. Originals are temporary, per order (see FR-072). | M | Owner | 0.1.0 | Implemented | TS-08 |
| FR-021 | **Backup identity:** an existing file in `Original/` whose identity is uncertain is never overwritten. A fresh collision-safe name is used and the association is recorded in the sidecar. Names are compared case-insensitively. | M | Owner | 0.1.0 | Implemented | TS-08 |
| FR-022 | **Safe write:** output is written to a temporary file in the same folder, then replaces the destination only after the write and a read-back check succeed. If anything fails, the existing photo is unchanged. | M | Owner | 0.1.0 | Implemented | TS-08 |
| FR-023 | **Ordinary failures** are explained in plain language: insufficient disk space, read-only file or folder, file changed or removed externally since loading, unreadable source. | M | Owner | 0.1.0 | Implemented | TS-08 |
| FR-024 | **Effectively unchanged photo:** if the recipe is a no-op (the crop keeps all pixels and there are no adjustments), Save marks the photo **Reviewed** without re-encoding or creating a backup. A crop that removes pixels is an effective change. | M | Owner | 0.1.0 | Implemented | TS-07, TS-08 |
| FR-026 | **Edit history (sidecar):** a small, versioned `Original/.photox-order.json` stores the backup mapping, saved crop and adjustments, and Reviewed marks. Reopening restores them. No database. | M | Owner | 0.1.0 | Implemented | TS-08 |
| FR-027 | **Order state** (To do / Edited / Reviewed) survives restarts via `Original/` and the sidecar. Unsaved edits exist only in memory. | M | Owner | 0.1.0 | Implemented | TS-07 |
| FR-072 | **Discard Originals** (prompt_05, replaces Finish Order; named *Save Changes* until prompt_06): for the selected photos, saves the unsaved edits (verified saves; the first failure stops and is reported), then, after one warning that the originals are deleted permanently, deletes each original PhotoX kept for them, one photo at a time and only when PhotoX can identify it: the sidecar names it with plain file names inside `Original/`, it still has the size and write time recorded at creation, and the edited photo is the file PhotoX saved and reads as an image (DEC-056). Anything uncertain is kept, stays recorded and is reported with the reason. Nothing is deleted recursively; edited photos, ID exports and unrelated files (including unknown files in `Original/`) are never touched; `Original/` is removed only when empty. Afterwards those photos count as To do; editing starts from the saved photo. | M | Owner | 0.2.0, 0.8.0 | Implemented | TS-09 |
| FR-083 | **Restore Original** (prompt_05): after a warning that the edits are discarded, discards the unsaved edits of the selected photos and copies each kept original back: copied to a temp file next to the photo, compared byte for byte, swapped in atomically; only then are the backup and its history removed. A backup that is not PhotoX's (size or time changed) is not restored. | M | Owner | 0.8.0 | Implemented | TS-09 |
| FR-030 | **Formats:** JPEG and PNG in, same format out. The real format is detected from content. Supported variants are listed in §8.3. | M | Owner | 0.1.0 | Implemented | TS-06, TS-09 |
| FR-031 | **HEIC/HEIF:** unsupported initially; shown in the gallery as *Unsupported (HEIC)* with an explanation instead of disappearing. Future point. | M (message) | Owner | 0.2.0 (message) | Implemented | TS-09 |
| FR-032 | **EXIF orientation** applied on load; saved pixels are upright with Orientation = 1. | M | Owner | 0.1.0 | Implemented | TS-02 |
| FR-033 | **Metadata:** keep appropriate source metadata (EXIF/IPTC/XMP); update orientation and dimensions; remove the stale embedded EXIF preview. Effective-ppi density is written when the format defines a physical size. Metadata is not treated as a guarantee of printed size. | M | Owner | 0.1.0 | Implemented | TS-06 |
| FR-034 | **Colour:** honour embedded ICC profiles on load; work in sRGB; **edited outputs are sRGB with an embedded profile**. Preview assumes an sRGB display (documented). | M | Owner | 0.1.0 | Implemented | TS-05 |
| FR-035 | **Output quality:** JPEG quality 95, 4:4:4. **Native crop resolution is preserved.** Interpolation only where straightening or rotation geometry requires it. PNG alpha and bit depth preserved where supported. | M | Owner | 0.1.0 | Implemented | TS-06 |
| FR-036 | **Bad input** (corrupt, truncated, zero-byte, unsupported variant, decompression bomb) gives an error state and explanation, never a crash or hang. | M | Owner | 0.1.0 | Implemented | TS-09 |
| FR-037 | **Image size policy:** up to 64 MP normal; 64–210 MP allowed but slower; above 210 MP refused clearly. Available memory is checked before expensive operations. If measurements show the upper range is unsafe, a conservative documented limit applies. | M | Owner | 0.2.0 | Implemented | TS-12 |

### 5.5 ID Photo Creator

| ID | Requirement | P | Src | MS | Progress | Tests |
|---|---|---|---|---|---|---|
| FR-007 | **ID workspace** with two steps, shown as numbered tabs in the toolbar: **1. Framing & adjustment → 2. Print** (prompt_06). The toolbar also holds the ID format (a compact list; 3 × 4 cm by default), the 90° rotation, Auto Align, zoom, rotation and background brightness, like the crop tools of the Photos editor; the right sidebar is the editing sidebar of Photos (prompt_06). The portrait is chosen in the built-in explorer on the left (prompt_05, prompt_06) and is never overwritten. | M | Owner | 0.3.0, 0.7.0, 0.8.0 | Implemented | TS-14, TS-18 |
| FR-061 | **Geometric sizes:** 3×4 cm, 3.5×4.5 cm, **5 × 5 cm** (US visa: exactly 2 × 2 in = 50.8 × 50.8 mm, shown as 5 × 5 cm, prompt_06), **4×6 cm**, **5×7 cm**, 9×12 cm. No country-specific templates and no compliance claims. Data, not code: the sizes are read from `id-sizes.json` in the settings folder, created with these defaults (DEC-049); unedited entries written by 0.6.0 are updated to the current defaults (DEC-052). **+ New size** adds custom sizes in centimetres, remembered and removable, listed apart from the built-in sizes. The size list shows centimetres, not pixels (prompt_04). | M | Owner | 0.3.0, 0.6.0 (data file), 0.7.0 (custom sizes) | Implemented | TS-14 |
| FR-062 | **Framing:** fixed frame of the size's exact ratio (3:4, square for 2×2 in, prompt_04); the photo is moved, zoomed and rotated underneath (mouse, wheel, keys, sliders; double-click resets the zoom or rotation slider) and can be **turned by 90° left or right** for photos taken in the wrong orientation; framing, zoom and Auto Align stay valid after turning (prompt_06). Face oval plus crown, eye, chin and centre guides; the Romanian 3×4 frame puts the head at 70 % of the height under the unchanged crown line (prompt_04). The 9×12 police photo has no guides and is framed by hand. | M | Owner | 0.3.0, 0.7.0 | Implemented | TS-14 |
| FR-063 | **Auto Align** only on button press: offline face detection positions, scales and levels the face to the guides (not offered for sizes without guides). The result is adjustable and undoable. **With several faces the operator chooses** (or keeps manual framing). No face found gives a clear message. | M | Owner | 0.4.0 | Implemented | TS-13 |
| FR-064 | **ID adjustments:** the editing sidebar of the Photos editor (Undo, Redo and Reset at the top; histogram with levels handles, **Auto Adjust**, exposure slider and + / − keys, contrast, white balance, tint, saturation; same look, order and behaviour, prompt_03, prompt_06, DEC-047, DEC-062), plus manual **background brightness** in the toolbar. Reset clears the adjustments and keeps the framing; before/after while Space or the toolbar button is held. | M | Owner | 0.3.0, 0.5.0, 0.7.0 | Implemented | TS-04, TS-14 |
| FR-065 | **Automatic background whitening:** experimental; bounded investigation. Included only if useful, otherwise the limitation is recorded. It never delays other work. | C | Owner | 0.4.0 (evaluate) | Not included (evaluated; DEC-041) | TS-13 |
| FR-066 | **Sheet layouts:** simple uniform grids on a 4×6 in page (101.6×152.4 mm), calculated from photo size, margins and spacing. **3×4 photos use a landscape page; other sizes use landscape when it fits more copies** (prompt_03, DEC-046). Photos keep their size and upright orientation. Selectable copy count, with familiar lower counts available even when more fit; 3 × 4 starts with 8 photos (prompt_06). Photo dimensions are never shrunk to fit more. A size may bring its own sheet margins: **four 2×2 in photos fit only edge to edge** (2 × 50.8 mm = the 101.6 mm short edge), so 2×2 in has no margin or spacing by default (prompt_04, DEC-052). | M | Owner | 0.3.0, 0.5.0, 0.7.0 | Implemented | TS-15 |
| FR-074 | **3×4 with 7 mm strip:** an option, offered only for 3×4 and on by default, reserves **7 mm of white space below each photo** (image exactly 30×40 mm), so a photo can be cut as 30×47 mm. The strip is also the gap to the next row (DEC-046). The copy count is reduced if needed. | M | Owner | 0.3.0, 0.5.0 | Implemented | TS-15 |
| FR-067 | **Sheet preview and options:** true-proportion preview; copy count; spacing; margins; the 3×4 strip option. No general cut-guide option (prompt_03). | M | Owner | 0.3.0, 0.5.0 | Implemented | TS-15, TS-18 |
| FR-068 | **ID export:** **Save** (called *Save sheet* before prompt_06) writes JPEG only (prompt_04); **Print** prints the same sheet at its exact size on 10×15 (prompt_05); at **300 ppi**; page 1200×1800 px (portrait) or 1800×1200 px (landscape); each photo exactly `round(mm × 300 / 25.4)` px; sRGB. Written to the **operator-chosen, remembered output folder**. | M | Owner | 0.3.0 | Implemented | TS-15 |
| FR-075 | **ID filenames:** `ID_yyyyMMdd_HHmm.jpg` (local time). If the name exists, `_1`, `_2`, … is appended. Never overwrites. | M | Owner | 0.3.0 | Implemented | TS-15 |
| FR-079 | **Collages** (prompt_04): a third workspace for simple, print-ready photo collages. Sheets 10×15 and 15×20 (prompt_05 removed 20×30), portrait or landscape. A template-based workflow, not a free-form editor: templates for two to nine photos (side by side, stacked, three in a row, one large with two or three small, grids), filling the sheet inside a small margin with equal gaps (adjustable, default 3 mm); a template's cells may differ in shape, so portrait and landscape photos mix on one sheet. Photos come from the built-in explorer and are **dragged into a frame** (or double-clicked into the selected frame; files from the system's file manager can be dropped too); they can be replaced, removed and moved to another frame (drag the round handle), framed in their slot (drag to move, wheel to zoom, double-click to reset) and **turned by 90°** from the right-click menu without distortion or layout change (prompt_06). The preview shows the whole collage in true proportions with each slot's size in cm; slots can be resized by dragging the gaps between them or typing the selected slot's width and height (columns and rows of the template change, every cell keeps at least 1 cm, the photos stay within the sheet; Reset layout returns to the template, prompt_05). **Save JPG** and **Print** use the same composition: it renders at 300 ppi from the full-resolution photos (JPEG at the quality set in Settings, 100 by default since prompt_06; sRGB) as `Collage_yyyyMMdd_HHmm.jpg` in a remembered output folder, never overwriting; such files are excluded from order galleries (DEC-053). | S | Owner | 0.7.0, 0.8.0, 0.9.0 | Implemented | TS-19, TS-21 |
| FR-071 | **Calibration test sheet:** a printable page with mm rulers, the 7 mm strip, a centred 13×18-on-15×20 frame and ID reference rectangles, for checking physical output; since prompt_06 it is printed from the Printing workspace (*Calibration sheet…*, 10×15 or 15×20) through the same path as photos, or saved as files from the ID sheet step. A simple configurable scale correction for ID export (default none) can be applied if physical tests require it: *ID print scale correction (%)* in Settings scales the photos and their positions about the page centre. | S | Owner | 0.3.0, 0.6.0 (scale correction) | Implemented (physical print check pending) | TS-15 |

### 5.6 Printing and diagnostics

| ID | Requirement | P | Src | MS | Progress | Tests |
|---|---|---|---|---|---|---|
| FR-050 | **Direct printing through the Windows driver** (prompt_05, replaces "printing deferred"): photos (single, selected, all; unsaved edits included without saving), ID sheets and collages print through the installed printer driver (Citizen CY-02) and the Windows print spooler, without LinkServer and without the USB protocol. Printing never writes to the photos or their folder and uses the save pipeline at full quality. Linux keeps editing and exporting; it has no direct printing. Driver options (quality, glossy/matte, cutting, the two-up 15×20 paper) are left to the driver: its defaults, or its own dialog opened from PhotoX (remembered per printer). PhotoX sends sRGB and applies no printer profile, so the driver does the only colour conversion (DEC-057). | M | Owner | 0.8.0 | Implemented (Citizen CY-02 not yet tested) | TS-20 |
| FR-080 | **Printing workspace** (prompt_06, replaces the print dialog): Print elsewhere opens it with the chosen photos or the prepared sheet; nothing is sent before **Print** is pressed there. It shows the preview of the pages with the print layout, the printer (the last used, else a Citizen, else the Windows default), print size **10×15** or **15×20** (fixed for ID sheets and collages), the driver's paper of that size chosen automatically (or chosen by the operator when the driver has none; then the print is centred at true size), copies, and a confirmation that names the paper to load (the loaded media cannot be detected). *Back* returns to the workspace that sent the print; closing PhotoX asks first while jobs are not yet handed to Windows. | M | Owner | 0.8.0, 0.9.0 | Implemented | TS-20, TS-21 |
| FR-081 | **Print queue:** in the Printing workspace (prompt_06), this session's jobs in progress with name, size, copies, printer and status (waiting, preparing, sending, in the Windows queue, needs attention, done in the Windows queue, removed, cancelled, failed with the reason). Pending jobs can be cancelled (in PhotoX or in the Windows queue). "Done" means Windows finished the job, not that a photo came out of the printer. Jobs are never resubmitted automatically (DEC-058). Finished, cancelled and failed jobs move to the history (FR-084). | M | Owner | 0.8.0, 0.9.0 | Implemented | TS-20, TS-21 |
| FR-082 | **Print counter:** each gallery thumbnail shows in its lower-right corner the copies of that photo that Windows accepted through PhotoX (a dash when none). It changes only after Windows accepts a job and goes back down when the job is cancelled through PhotoX or seen removed from the Windows queue before finishing. Kept per photo path in PhotoX's user data folder (`print-history.json`), not in the customer's folder and not in the installation folder (DEC-058, DEC-063). | S | Owner | 0.8.0 | Implemented | TS-20 |
| FR-084 | **Print history and statistics** (prompt_06): every print job sent through PhotoX with time, name, size, copies, printer and outcome (*accepted by Windows*, *done in the Windows queue*, *cancelled*, *removed from the Windows queue*, *failed* with the reason), kept across sessions in monthly files in the user data folder and listed for today, this week (from Monday) or this month. Statistics show the prints of today, this week, this month and all time, split by 10 × 15 and 15 × 20. Prints are counted with the rule of the gallery counters (accepted by Windows and not cancelled or removed); cancelled and failed jobs are listed but not counted. Nothing claims that paper came out of the printer (DEC-063). | S | Owner | 0.9.0 | Implemented | TS-21 |
| FR-051 | **Logging and errors:** rolling local log file; plain-language localized error messages. The log never contains image content. | M | Planner | 0.1.0 | Implemented | TS-17 |

## 6. Non-functional requirements

| ID | Requirement | Src | Progress | Tests |
|---|---|---|---|---|
| NFR-001 | **Offline and private.** No network use. No customer data leaves the machine. | Owner | Implemented | review |
| NFR-002 | **Platforms:** Windows 11 x64 production; Linux development. | Owner | In progress (owner ran 0.4.0 on Windows 11; automated tests run on Windows in CI; studio trial pending) | TS-17 |
| NFR-003 | **Responsiveness:** the UI never freezes during loading, thumbnails or saving; sliders, straightening, cropping and navigation feel immediate on a four-core studio PC (prompt_03). Performance is reported from **measurements**, not assumed. | Owner | In progress (measured on the development PC limited to 4 cores; studio PC pending) | TS-11 |
| NFR-004 | **Memory:** bounded caches. Memory is checked before large operations. | Owner | Implemented | TS-12 |
| NFR-005 | **Save safety:** a failed save never damages the existing photo or its preserved original (FR-005, FR-021–FR-023). | Owner | Implemented | TS-08 |
| NFR-006 | **Image fidelity:** no unintended resampling; no colour shift for sRGB sources; one JPEG generation from the original while history is kept. | Owner | Implemented (exact crop pixels, neutral recipe leaves pixels unchanged, re-edits render from the original; fast tone path equal to Magick within one 16-bit level) | TS-05, TS-06 |
| NFR-007 | **Usability:** large primary actions; usable at common Windows resolutions (1366×768 to 4K) and 100–200 % scaling; mouse and keyboard first; one consistent look across workspaces in light and dark mode (prompt_06). | Owner | In progress (headless 1280×720 to 1920×1080 checked in both modes; Windows scaling pending) | TS-18 |
| NFR-008 | **Localization quality:** no hard-coded UI text; key parity between EN and RO; Romanian wording reviewed by the owner. | Owner | In progress (owner review pending) | TS-16 |
| NFR-009 | **Maintainability:** modest separation of UI, image processing, file handling and pure geometry. No abstractions without a concrete use. | Owner | Implemented | review |
| NFR-010 | **Licences:** free dependencies whose terms permit use in a paid studio (personal-use-only terms do not qualify). Notices retained. | Owner | Implemented (notices) | review |
| NFR-011 | **Builds:** pinned package versions. Windows/Linux CI once the GitHub remote exists. | Owner | Implemented (GitHub Actions on Linux and Windows) | TS-17 |

## 7. Removed or superseded requirements (P-1 → P-2)

These are **not active**. They are kept for traceability.

| ID | P-1 content | Reason |
|---|---|---|
| FR-020 | (number unused after P-1 renumbering) | — |
| FR-025 | Revert-to-original action | Not required. Reset + Save covers re-editing while history exists. |
| FR-046 | Batch Auto Correct / batch apply | Owner decision (OQ-008): not implemented. |
| FR-048 | Print-safe (overscan) guide in crop | Owner: crop ratios are independent of printer overscan (prompt_02 §13). |
| FR-060 | (moved to FR-007 in P-1) | — |
| FR-069 | Single ID photo export | Not requested. Sheets with one copy cover it. |
| FR-070 | Printer-exact canvas (1240×1844) | Owner: no speculative canvas/overscan assumptions. Replaced by the simple calibration in FR-071. |
| NFR-012 | Cache privacy as a separate NFR | Merged into FR-053. |
| — | Journalled transaction, recovery service, background save queue, session-recovery file, custom LUT/SIMD pipeline, IFileSystem fault-injection seam | Owner: disproportionate (prompt_02 §§2, 4, 8). |

## 8. Domain rules

### 8.1 Ordinary print formats (configuration data `print-formats.json`)

| Preset | Ratio (portrait) | Physical size | Output |
|---|---|---|---|
| 10×15 (default) | 2:3 | 101.6 × 152.4 mm (4×6 in) | crop at native pixels |
| 15×20 | 3:4 | 152.4 × 203.2 mm (6×8 in) | crop at native pixels |
| 13×18 | 130:180 | 130 × 180 mm photo on the 15×20 canvas | photo at native pixels, centred on a white canvas |
| Custom | W×H cm | W×H mm on the smallest sheet that fits | photo at native pixels, centred on a white sheet |
| 20×30 (sheet only) | 2:3 | 203.2 × 304.8 mm (8×12 in) | used by custom sizes and collages |

**13×18 canvas rule (FR-073):** with a crop of `w × h` px (portrait, ratio 130:180), the canvas is `round(w × 152.4/130) × round(h × 203.2/180)` px. The photo is centred and the borders are white. The canvas ratio is then 3:4, and printing the canvas at 152.4×203.2 mm reproduces the photo at 130×180 mm. Landscape swaps the axes.

**Whole photo with white borders (FR-049):** the canvas is the smallest rectangle of the preset ratio (orientation as the photo) that contains the full photo without scaling. The photo is centred and the padding is white. Combined with 13×18, the 13×18 frame contains the whole photo and is then centred on the 15×20 canvas.

**Custom size on a sheet (FR-010, prompt_04):** a custom W×H cm photo is cropped to W:H and centred on the smallest print sheet with both sides at least as large (10×15 = 101.6 × 152.4 mm, 15×20 = 152.4 × 203.2 mm, 20×30 = 203.2 × 304.8 mm), with the same canvas rule as 13×18. A size larger than 20×30 gets no canvas. Custom crops saved before 0.7.0 have a ratio only and no canvas.

**Effective ppi** = output long-edge pixels ÷ physical long edge in inches (for 13×18 and custom sizes on a sheet, of the sheet).

### 8.1.1 Print layout (FR-050, FR-080)

| Content | On the paper |
|---|---|
| Untouched original | turned upright to the paper and **cropped to fill** with the largest centred crop of the paper's ratio (the editor's default crop) |
| Edited photo (saved or unsaved), ID sheet, collage | turned upright; **fills** the paper when its ratio is within 2 % (at most 2 % trimmed), otherwise **fits** inside it, centred on white |

On the driver's paper of the print size, the page covers the driver's whole page (its borderless area). On another paper (a PDF printer's A4) the print size is centred at its true size. Pages are rendered at the device resolution and sent unscaled; copies are sent as pages of one job.

### 8.2 Order state

| State | Meaning |
|---|---|
| To do | not saved or reviewed |
| Unsaved | manual edits in memory, not saved |
| Edited | saved by PhotoX; original preserved in `Original/` (per sidecar) |
| Reviewed | saved with no effective change (FR-024) |
| Error / Unsupported | cannot be read, or format not supported (explanation shown) |

Progress = (Edited + Reviewed) / total. After Finish Order with **Erase**, all photos count as To do again; the edits are now in the files themselves.

### 8.3 Supported image variants (FR-030, FR-035, FR-036)

| Input | Support | Output |
|---|---|---|
| JPEG baseline/progressive, 8-bit RGB or greyscale | ✓ | JPEG at the Settings quality (100 by default, prompt_06) 4:4:4, sRGB |
| JPEG CMYK/YCCK (with or without profile) | ✓ read; converted to sRGB via the embedded or a generic CMYK profile | JPEG RGB sRGB |
| PNG 8-bit greyscale/RGB/palette, with or without alpha | ✓ (palette expanded) | PNG 8-bit, alpha kept |
| PNG 16-bit | ✓ | PNG 16-bit, alpha kept |
| HEIC/HEIF, TIFF, RAW, WebP, GIF, 12-bit/lossless JPEG, animated PNG beyond the first frame | ✗ (explanation shown) | — |

### 8.4 ID sizes and sheet rules

| Size | mm | px at 300 ppi | Familiar copy counts* |
|---|---|---|---|
| 3×4 cm (+7 mm strip) | 30 × 40 (cell 30 × 47); head 70 % below a 10 % crown line | 354 × 472 | 4, 6, 8 (landscape page, 8 fit) |
| 3.5×4.5 cm | 35 × 45 | 413 × 531 | 2, 4, 6 (landscape page, 8 fit) |
| 5 × 5 cm (US visa, exactly 2 × 2 in) | 50.8 × 50.8 | 600 × 600 | 1, 2, 4 (no margin or spacing: two columns fill the 101.6 mm width, up to 6 fit) |
| 4×6 cm | 40 × 60 | 472 × 709 | 2, 4 |
| 5×7 cm | 50 × 70 | 591 × 827 | 1, 2 |
| 9×12 cm (no guides) | 90 × 120 | 1063 × 1417 | 1 |
| Custom (+ New size) | user, in cm | computed | 1, 2, 4 up to capacity |

\*The capacity is computed from margins (default 3 mm) and spacing (default 2 mm) on a 101.6×152.4 mm page, landscape for 3×4 and wherever landscape fits more (DEC-046). Other sizes fit: 2×2 in → 2 with margins, 6 edge to edge (its default), 4×6 cm → 4, 5×7 cm → 2, 9×12 cm → 1 (portrait). The exact capacities are verified by tests (TS-15). Copy counts are selectable from 1 to capacity, and the familiar counts are offered as quick choices.

## 9. Deferred

LinkServer hot-folder submission (not needed with direct printing); two 10×15 photos on one 15×20 sheet; printer-specific options inside PhotoX (they stay in the driver); HEIC/HEIF; batch editing; monitor colour management; printer overscan handling beyond FR-071; touch optimisation; country-specific templates.

## 10. Release acceptance (1.0.0)

1. All **Must** requirements are *Implemented*, with tests passing on Linux, and on Windows once CI exists.
2. The owner has used PhotoX on real orders on the Windows studio PC without data loss or blocking defects.
3. Physical prints from PhotoX on the Citizen CY-02 confirm the print sizes, the ID dimensions, the 7 mm strip and the centred 13×18 output (FR-050, FR-071), or the documented calibration has been applied.
4. Romanian wording has been reviewed by the owner.
5. Third-party notices are included.

## 11. Change control

Requirement IDs are stable and never reused. New items take new IDs. Removed items move to §7. Each change to behaviour updates this file in the same commit, along with DECISIONS.md where a decision changes.
