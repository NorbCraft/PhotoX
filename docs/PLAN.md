# PhotoX — Project Plan (Source of Truth)

| | |
|---|---|
| **Document status** | DRAFT for owner review. Nothing in this plan is approved for implementation yet. |
| **Plan revision** | P-1 (initial planning) |
| **Date** | 2026-10-29 |
| **Intended first implementation version** | 0.1.0 (not yet started, not tagged) |
| **Originating prompt** | [docs/prompts/prompt_01.md](prompts/prompt_01.md) |

This file is the **authoritative** description of what PhotoX must do. Supporting documents explain *how* and *when*, and they reference requirement IDs from here instead of restating them:

| Document | Purpose |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Technical architecture, technology choices, image pipeline, save algorithm, diagrams |
| [ROADMAP.md](ROADMAP.md) | Milestones, version boundaries, dependencies, risks |
| [OPEN_QUESTIONS.md](OPEN_QUESTIONS.md) | Unresolved decisions (`OQ-xxx`) with options and recommended defaults |
| [DECISIONS.md](DECISIONS.md) | Decision log (`DEC-xxx`) |
| [TEST_PLAN.md](TEST_PLAN.md) | Verification strategy, test suites (`TS-xx`), release acceptance |
| [DEVELOPMENT.md](DEVELOPMENT.md) | Conventions: Git, versioning, coding, agent workflow |
| [mockups/README.md](mockups/README.md) | UI concept mockups and navigation structure |
| [IMPLEMENTATION_LOG.md](IMPLEMENTATION_LOG.md) / [CHANGELOG.md](CHANGELOG.md) | Progress and release history (empty until implementation starts) |

**Conflict rule:** if a supporting document contradicts this plan, this plan wins. The contradiction must be recorded in [OPEN_QUESTIONS.md](OPEN_QUESTIONS.md) and fixed, never silently resolved.

---

## 1. Status vocabulary

Each requirement carries one status:

| Status | Meaning |
|---|---|
| **Owner** | Stated by the project owner in the originating prompt. Treated as required. The planner may still have raised a question about its exact form; see the linked `OQ`. |
| **Proposed** | Added or made concrete by the planner. Becomes required only after the owner approves the plan. |
| **Deferred** | Deliberately out of 1.0 scope. Architecture leaves room for it. |
| **Open** | Cannot be specified until an `OQ` is decided. The default in brackets is what implementation will assume if the owner accepts the recommendation. |

Priorities follow MoSCoW: **M**ust, **S**hould, **C**ould. Each requirement also names its milestone (see [ROADMAP.md](ROADMAP.md)) and its verifying test suites (see [TEST_PLAN.md](TEST_PLAN.md)).

---

## 2. Product vision and scope

PhotoX is a lightweight, offline desktop application for a small professional photo studio. It replaces a workflow built on ACDSee, Photoshop and older Mitsubishi kiosk software. The operator prepares customer photos for printing quickly and safely: open a folder, crop to the print format, correct exposure and colour where needed, press **Save & Next**, and repeat for 100+ photos. A second workspace produces ID-photo print sheets.

Priorities, in order: **reliability and safe saving → photo quality → speed of the workflow → simplicity → feature count.**

### 2.1 Version 1.0 goals

1. An operator can process a 100–300-photo order with crop and basic corrections and never loses an original.
2. Every edited photo is saved at full resolution with no unintended resampling, colour shift or metadata loss.
3. Moving to the next photo after Save & Next feels instant, because saving runs in the background.
4. ID photos can be framed with guides, aligned automatically or by hand, and exported as an exact-size 10×15 print sheet.
5. The UI is fully usable in Romanian and English, switchable from the main screen.
6. It runs natively on Windows 11 x64 (production) and Linux (development), built and tested in CI on both.

### 2.2 Out of scope for 1.0

- Direct printing and printer integration (deferred; see FR-050).
- HEIC/HEIF, TIFF and RAW input (deferred; see FR-031 and OQ-004).
- Layers, retouching, healing, text, filters, and anything else Photoshop-like.
- Cloud features, accounts, telemetry, online updates that send data. No customer photo ever leaves the machine.
- A general-purpose file manager. The explorer serves photo orders only.
- Windows 10, macOS and touch-first operation.

---

## 3. Users and environment

| Item | Assumption | Status |
|---|---|---|
| Primary user | Photo-studio operator. Not an expert computer user. Works mostly with mouse and keyboard and processes orders many times a day. | Owner |
| Production machine | Windows 11 x64 desktop. Specs unknown; see OQ-016. Proposed reference machine: 4-core CPU, 16 GB RAM, SSD, 1920×1080 display at 100–150 % scaling. | Open |
| Development machine | Linux Mint. Testing on a Windows 11 VM, a physical Windows PC and a dual-boot laptop. | Owner |
| Photo sources | Phones (12–50 MP; some 108–200 MP modes), messaging apps (often below 2 MP), compact cameras. The operator copies them to a local folder first. | Owner / Proposed |
| Printer (future) | Citizen CY-02 dye-sublimation (owner). Research: 4×6 in (≈101×152 mm) and 6×8 in (≈152×203 mm) media, 300 dpi (600×300 dpi “high quality” mode), **borderless with an estimated 1.7–1.9 mm overscan per side** (printer canvas ≈ 1240×1844 px for 4×6). Overscan and exact sizes must be confirmed by a calibration print (OQ-001). No direct integration in 1.0. | Owner / research |
| Languages | Romanian (primary user language) and English. | Owner |

---

## 4. Core workflows

**W-1: Ordinary order.** Customer photos have been copied to `…\Comenzi\<order>`. The operator:

1. Opens PhotoX and goes to the folder in the built-in explorer. The last folder is pre-selected.
2. Sees thumbnails with edited/unedited status and progress (for example 23 / 85).
3. Presses **Start editing** (or Enter). The first unedited photo opens with the 10×15 crop frame already active and oriented to match the photo.
4. Adjusts the crop and, if needed, exposure, contrast, colour or levels.
5. Presses **Save & Next** (Enter). The next photo appears immediately and the save completes in the background.
6. Repeats until done. A photo that needs nothing can be skipped (→) or saved with the default crop.

**W-2: Re-edit.** The operator reopens an already-edited photo. PhotoX starts again from the backed-up original with the previously saved settings, so quality does not degrade with each save (FR-026).

**W-3: ID photo.** The operator:

1. Switches to **ID Photos** and selects or drops in a portrait.
2. Chooses a size (for example 3.5×4.5 cm). A fixed frame with an oval, crown, eye and chin guides appears.
3. Optionally presses **Auto Align**, then corrects by dragging, zooming and rotating.
4. Adjusts exposure, contrast, white balance and background brightness.
5. Continues to the sheet: chooses the layout and copy count, checks cut guides, and presses **Save layout**. A new print-ready file is written and the portrait is never modified.

---

## 5. Assumptions

| ID | Assumption | Linked |
|---|---|---|
| A-01 | The operator always works on a local copy of the customer's photos, never on the customer's phone or stick directly. Removable media still triggers a warning (FR-054). | OQ-019 |
| A-02 | One operator per machine, one PhotoX instance at a time, and no concurrent editing of the same folder from another computer. | |
| A-03 | Printing will happen later through the Windows Citizen driver, which accepts sRGB images. Until then, exported files are printed with other software (unknown: OQ-024), which affects exact physical size. | OQ-012, OQ-024 |
| A-04 | “10×15 cm” in the studio means the 4×6 in (101.6×152.4 mm, 2:3) photo format. “15×20 cm” means 6×8 in (152.4×203.2 mm, 3:4). | OQ-001 |
| A-05 | Output for ordinary photos stays in the customer's folder, under the original filename (owner backup strategy, FR-005). | |
| A-06 | Real customer photos will be supplied separately for testing and are never committed to the repository. | |

---

## 6. Functional requirements

Column key: **P** = priority; **MS** = milestone/version from [ROADMAP.md](ROADMAP.md); **Verified by** = test suites from [TEST_PLAN.md](TEST_PLAN.md).

### 6.1 Application shell, navigation and language

| ID | Requirement | P | Status | MS | Verified by |
|---|---|---|---|---|---|
| FR-001 | **Built-in folder explorer.** Navigate local drives, removable drives and folders inside the app (not only an OS dialog). Remember the last opened folder. Show a short list of recent order folders with their progress. Hide system and hidden folders and the `Original` backup folder. Purpose-built for photo orders, not a general file manager. | M | Owner (recent list Proposed) | 0.2.0 | TS-10, TS-18, TS-19 |
| FR-002 | **Photo thumbnail gallery.** Show the JPEG/PNG files of the current folder as thumbnails with filename and basic info (pixel size, size on disk). Clearly distinguish edited, unedited, unsaved-changes and failed states. Filters: All / To do / Edited. Default sort is natural filename order. The `Original` folder never appears as part of the order. | M | Owner | 0.2.0 | TS-11, TS-18 |
| FR-008 | **Two workspaces**, *Photos* and *ID Photos*, reachable with one click from a persistent top bar, with a consistent look and shared image components. | M | Owner | 0.1.0 | TS-18 |
| FR-009 | **Localization: English and Romanian.** Every UI string is externalized. The language switch is a one-click EN/RO control in the top bar, not in Settings. It switches at runtime without restarting and is remembered. Numbers and dates use the language's culture (Romanian `3,5 × 4,5 cm`). Romanian uses comma-below ș/ț. | M | Owner | 0.1.0 | TS-16 |
| FR-039 | **Theme.** A neutral dark-grey theme by default, so surrounding UI colour does not bias how the photo is judged. A light theme is optional and comes later. | S | Proposed | 0.1.0 (dark) | TS-18 |
| FR-040 | **Settings page** for infrequent options: print media definitions (FR-011), JPEG quality, ID templates, thumbnail-cache size and retention, theme, and folders for logs and cache. No setting is required for the daily workflow. | M | Proposed | 0.1.0 → grows | TS-18 |
| FR-041 | **Multiple selection** in the gallery (Ctrl/Shift-click) with simple actions: *Edit selected* (restricts the Save & Next sequence to the selection) and *Use for ID photo*. | S | Owner (“consider”) | 0.2.0 | TS-18 |

### 6.2 Viewing and navigation

| ID | Requirement | P | Status | MS | Verified by |
|---|---|---|---|---|---|
| FR-003 | **Default crop behaviour.** When a photo opens in the editor, the crop tool is active, using the default preset (10×15). Orientation follows the photo: portrait photo, portrait frame. The frame is the largest centred rectangle of that ratio. Activating the crop never changes the file; only an explicit save does. | M | Owner | 0.3.0 | TS-01, TS-07 |
| FR-004 | **Save & Next workflow.** One action (button and Enter) saves the current photo and opens the next photo in the sequence. The next photo is displayed without waiting for the save to finish (background save queue). When a save fails, the photo is flagged, its edits are kept, and a persistent non-modal message is shown. | M | Owner | 0.3.0 | TS-07, TS-08, TS-11 |
| FR-017 | **Sequential navigation controls:** Save & Next; Previous; Next/Skip (no save); Save (stay); Reset current photo; and a progress indicator (`24 / 85` plus a bar). Wraps nowhere: at the end of the order it shows an “Order complete” summary. | M | Owner | 0.3.0 | TS-07, TS-18 |
| FR-018 | **Keyboard shortcuts** (proposed set in [mockups/README.md](mockups/README.md#keyboard-shortcuts)): Enter = Save & Next, Ctrl+S = Save, ← / → = Previous / Skip, Ctrl+Z / Ctrl+Y = Undo / Redo, R = rotate, Space (hold) = before/after. **Accidental-save protections:** auto-repeat of Enter is ignored, Enter is ignored for 300 ms after a photo opens, Enter does nothing while a text field has focus, and every save is reversible through the Original backup (FR-025). | M | Owner (exact keys Proposed) | 0.3.0 | TS-07, TS-18 |
| FR-019 | **Unsaved-changes indication.** The editor and the gallery/filmstrip show whether the current photo has unsaved edits. Leaving a photo with unsaved edits (Previous/Skip) keeps the edits in the session without a dialog, marks the photo “unsaved”, and lets the operator return to them. Closing the app with unsaved edits shows **one** confirmation: Save all / Discard / Cancel. | M | Owner (behaviour Proposed) | 0.3.0 | TS-07, TS-18 |
| FR-052 | **Preloading.** The next and previous photos in the sequence are decoded in the background, so Next/Previous is responsive. Never hold every full-resolution image in memory at once. | M | Owner | 0.3.0 | TS-11, TS-12 |
| FR-053 | **Thumbnail generation and caching.** Thumbnails are generated in the background (visible area first) and cached on local disk outside the customer folder, with size and age limits (OQ-018). | M | Owner | 0.2.0 | TS-11 |
| FR-054 | **Removable-media and read-only warnings.** Opening a folder on a removable drive or a read-only location shows a clear, non-blocking warning recommending a copy to a local folder first. | S | Proposed | 0.2.0 | TS-08 |

### 6.3 Ordinary photo editing

| ID | Requirement | P | Status | MS | Verified by |
|---|---|---|---|---|---|
| FR-006 | **Exposure and histogram correction** (owner umbrella requirement), detailed by FR-013 (exposure), FR-043 (histogram), FR-044 (levels) and FR-045 (Auto Correct). | M | Owner | 0.4.0–0.5.0 | TS-03, TS-04 |
| FR-010 | **Crop presets.** 10×15 (default) and 15×20 exist from the first editing version, plus a **Custom** ratio or size (W×H in cm or in, or a free ratio). Presets come from configurable print-format definitions (FR-011), not hard-coded numbers. | M | Owner | 0.3.0 | TS-01 |
| FR-011 | **Print-format model.** These are separate configurable values: **aspect ratio**, **nominal name** (“10×15”), **physical print size in mm** (exact media size, printer-specific, OQ-001), **target ppi** (for rendered outputs such as ID sheets) and, for the future, **printable area / bleed**. Ordinary-photo crops use only the aspect ratio. Physical size and ppi are used for effective-ppi display, DPI metadata and ID sheets. | M | Owner | 0.3.0 | TS-01, TS-14 |
| FR-012 | **Crop interaction:** move, resize from corners and edges with the ratio locked, portrait/landscape toggle, live preview with the outside dimmed, rule-of-thirds guides, Reset crop, and Fit crop to photo (largest frame of the ratio). The frame cannot extend beyond the image (no padding, no upscaling). It shows output pixel size and effective ppi. | M | Owner | 0.3.0 | TS-01, TS-18 |
| FR-049 | **Whole photo with border** (no crop): place the entire photo on a canvas of the print ratio with white borders, for panoramas and group photos. | S | Open (OQ-029) | 0.4.0 | TS-01, TS-06 |
| FR-013 | **Exposure:** a slider plus “−” and “+” quick buttons (±1/6 EV per step, proposed), range ±2 EV. | M | Owner | 0.4.0 | TS-04 |
| FR-014 | **Contrast, saturation and white balance (colour temperature)** sliders, with a neutral centre, double-click to reset, and range limits that avoid harsh results. A tint slider is not planned for 1.0 (OQ-021). | M | Owner | 0.4.0 (contrast), 0.5.0 (WB, saturation) | TS-04, TS-05 |
| FR-015 | **Rotation and straightening:** 90° left/right, and a straighten slider (±10°) with automatic constraint of the crop to the valid rotated area. | M | Owner | 0.3.0 (90°), 0.5.0 (straighten) | TS-01, TS-02 |
| FR-016 | **Undo/redo** of every edit step for the current photo, and **Reset adjustments** / **Reset photo**. Slider drags count as one step. | M | Owner | 0.4.0 | TS-04, TS-18 |
| FR-042 | **Before/after comparison:** hold Space or toggle the button to show the unedited photo in the same crop. | M | Owner | 0.4.0 | TS-18 |
| FR-043 | **Live histogram:** luminance plus R, G and B channels of the *edited* output inside the crop, updated live. | M | Owner | 0.5.0 | TS-03 |
| FR-044 | **Levels:** black point, white point and midtone (gamma) handles on the histogram, with live preview and Reset levels. | M | Owner | 0.5.0 | TS-03, TS-04 |
| FR-045 | **Auto Correct (single photo).** One button that sets exposure/levels (and a mild white-balance correction) to produce a natural result. It writes ordinary slider values, so everything stays manually adjustable, and it can be undone. Its strength is limited and it protects skin tones; it never uses full histogram equalization. See [ARCHITECTURE.md §7.6](ARCHITECTURE.md#76-auto-correct). | M | Owner (algorithm Proposed) | 0.5.0 | TS-04, TS-19 |
| FR-046 | **Batch Auto Correct and batch apply** (apply the same tonal settings to selected photos). *Not in 1.0.* The architecture supports it because edits are stored as parameter sets. Recommended as a post-1.0 milestone, 1.1.0 (OQ-008). | C | Deferred | 1.1.0 (proposed) | — |
| FR-048 | **Print-safe guide.** When the selected print format defines a printer overscan/bleed (FR-011), the crop frame shows a thin inner line marking the area that is safe from trimming on a borderless print. | S | Proposed | 0.3.0 | TS-01 |
| FR-047 | **Low-resolution warning.** When the effective print resolution of the crop is below a threshold (proposed: warn below 200 ppi, strong warning below 150 ppi), show it in the editor and the gallery. Never upscale automatically. | S | Proposed | 0.3.0 | TS-01 |

### 6.4 Saving, backup and file handling

| ID | Requirement | P | Status | MS | Verified by |
|---|---|---|---|---|---|
| FR-005 | **Original-file backup (owner strategy).** On the first save of a photo, an `Original` subfolder is created in the order folder if missing. The untouched original is moved into it, and the edited photo is written under the original filename and location. Later saves of the same photo keep the backup untouched. | M | Owner | 0.3.0 | TS-08 |
| FR-021 | **An existing backup is never overwritten**, silently or otherwise. Name collisions (for example `Original/IMG_1.jpg` already exists but is not known to belong to this photo, or case-only differences) are resolved without data loss: see [ARCHITECTURE.md §9.4](ARCHITECTURE.md#94-collisions-and-special-cases) and OQ-007. | M | Owner | 0.3.0 | TS-08, TS-10 |
| FR-022 | **Crash-safe save transaction.** Write to a temporary file in the same folder, flush, verify by decoding, then do atomic renames recorded in a small journal. If the app or PC dies at any instant, at least one complete copy of the original exists on disk and the next start completes or rolls back the operation automatically. See [ARCHITECTURE.md §9](ARCHITECTURE.md#9-saving-backup-and-recovery). | M | Owner | 0.3.0 | TS-08 |
| FR-023 | **Pre-save checks with clear errors:** free disk space, read-only file or folder, file changed or removed by another program since loading, and path too long. On failure nothing is changed and the operator gets a plain-language message (EN/RO). | M | Owner | 0.3.0 | TS-08, TS-10 |
| FR-024 | **Unchanged photos.** Default (OQ-006): pressing Save & Next on a photo with no effective change (crop equals the full image and no adjustments) writes nothing, creates no backup, and marks the photo “Reviewed”. Any crop or adjustment counts as a change. | M | Open | 0.3.0 | TS-07, TS-08 |
| FR-025 | **Revert to original.** In the editor, restores the backed-up original to the order folder (the edited version is discarded, after confirmation). | S | Proposed | 0.4.0 | TS-08 |
| FR-026 | **No cumulative degradation.** Re-editing a saved photo starts from the pristine file in `Original/` and the previously saved edit settings, which are kept in a hidden sidecar file (OQ-009). Each saved photo is therefore exactly one encode away from the original. | M | Owner (mechanism Proposed) | 0.4.0 | TS-06, TS-08 |
| FR-027 | **Order state persistence.** Edited / Reviewed / Unsaved / Failed state and saved edit settings survive restarts. They are derived from the `Original` folder plus the sidecar, and edits still pending are kept in a local session-recovery file. | M | Proposed | 0.3.0 | TS-07, TS-08 |

### 6.5 Formats, quality, metadata and colour

| ID | Requirement | P | Status | MS | Verified by |
|---|---|---|---|---|---|
| FR-030 | **Input/output formats: JPEG and PNG.** The real format is detected from file content, not the extension. A mismatched extension is handled (read correctly and written in the *same* format as the source, keeping the filename) and logged. Output format equals input format. | M | Owner | 0.2.0 (read), 0.3.0 (write) | TS-09 |
| FR-031 | **HEIC/HEIF** support. *Deferred*: decoding is technically possible (the Magick.NET bundle includes a decoder) but writing is not, and the licensing and workflow questions are open (OQ-004). | C | Deferred | post-1.0 | — |
| FR-032 | **EXIF orientation** is applied on load, and the saved file has its pixels physically oriented with Orientation = 1. | M | Owner | 0.2.0 | TS-02 |
| FR-033 | **Metadata preservation:** keep EXIF (date, camera, GPS and so on, policy in OQ-020), IPTC and XMP where practical. Update orientation and pixel dimensions, and drop the stale embedded EXIF thumbnail. Set the DPI field to the effective ppi of the chosen print format (OQ-005). | M | Owner | 0.3.0 | TS-06 |
| FR-034 | **Colour management.** Embedded ICC profiles (such as Display P3 or Adobe RGB) are honoured on load. Preview and processing work in sRGB. Default output is sRGB with an embedded sRGB profile (OQ-012). Images without a profile are treated as sRGB. No unexpected colour shift between source, preview and output. | M | Owner | 0.2.0 (preview), 0.3.0 (output) | TS-05 |
| FR-035 | **Output quality.** High-quality interpolation (Lanczos or bicubic) for any geometric resampling, and no resizing of ordinary photos: the crop is in native pixels. JPEG quality 95 with 4:4:4 chroma by default, configurable in Settings (OQ-005). PNG output is lossless. 16-bit PNG is preserved where practical. | M | Owner | 0.3.0 | TS-06 |
| FR-036 | **Resilience to bad input:** truncated or corrupt files, zero-byte files, unsupported variants (CMYK JPEG, 16-bit PNG, huge dimensions) and decompression bombs give a per-file error state in the gallery, never a crash or freeze. | M | Owner | 0.2.0 | TS-09 |
| FR-037 | **Large images.** Fully supported up to 64 MP. Supported but slower from 64 to 210 MP. Above that, files are refused with a message (OQ-016). | M | Proposed | 0.3.0 | TS-11, TS-12 |

### 6.6 ID Photo Creator

| ID | Requirement | P | Status | MS | Verified by |
|---|---|---|---|---|---|
| FR-007 | **ID Photo Creator workspace** (owner umbrella requirement, detailed by FR-061–FR-071): open an existing portrait (from the explorer, the gallery's *Use for ID photo*, or drag-and-drop) and go through two steps: **1. Framing & adjustments → 2. Print sheet**. The source portrait is never modified. | M | Owner | 0.6.0 | TS-18, TS-19 |
| FR-061 | **ID templates:** 3×4, 3.5×4.5, 5×5 and 9×12 cm, plus custom dimensions. Templates are data, not code (JSON): size, optional head-height range, eye-line range, top margin and background hint. Default guide values for 3.5×4.5 follow common ISO/IEC 19794-5 practice: chin-to-crown 32–36 mm (70–80 %), eye line 50–70 % from the bottom. They are labelled as **geometric presets with no claim of compliance** with any official document. Country-specific templates are possible later (OQ-011). | M | Owner | 0.6.0 | TS-14 |
| FR-062 | **Framing UI:** a fixed frame of the template ratio. The photo is moved, zoomed and rotated underneath (drag, scroll wheel, Shift+scroll, arrow keys, sliders). Overlay guides: head oval, crown line, eye line, chin line, centre line, and tolerance bands where the template defines them. | M | Owner | 0.6.0 | TS-14, TS-18 |
| FR-063 | **Auto Align (on request only).** Detects the face and eye/landmark positions offline, levels the eyes (roll), and scales and positions the head to the template. The result is an ordinary manual-framing state that can be adjusted, and it can be undone. It never runs automatically and never presents itself as guaranteeing biometric compliance. It fails gracefully with no face, several faces or a low-confidence face. | M | Owner | 0.7.0 | TS-13 |
| FR-064 | **ID adjustments:** exposure (with ± buttons), contrast, white balance, and **background brightness**. The background-brightness slider brightens light, low-saturation areas near the frame edges; it is mask-free and manual. | M | Owner | 0.6.0 (exposure, contrast, WB), 0.7.0 (background brightness) | TS-04 |
| FR-065 | **Automatic background whitening, experimental.** A person-segmentation model lifts the background to a uniform light tone. It is off by default, labelled *Experimental*, and requires visual confirmation. Its limitations (hair, light clothing, shadows) are shown in the UI. It ships only if it passes the validation in TS-13 (OQ-010). | C | Owner (“investigate”) | 0.7.0 (experimental) | TS-13, TS-19 |
| FR-066 | **Sheet-layout engine:** a general algorithm, not per-size code. It computes the arrangements of N photos of size W×H on a page (default 10×15 / 4×6 in) given margins, spacing, page orientation and photo rotation, and offers the feasible layouts (for example “6 = 2×3”, or “8 = 4×2, tight margins”). It validates physical fit and printable area. | M | Owner | 0.6.0 | TS-15 |
| FR-067 | **Sheet preview and options:** page preview at true proportions, layout choice, copy count (1 … maximum), spacing, margins (auto-centred), optional cut guides, and the resulting pixel size and ppi. | M | Owner | 0.6.0 | TS-15, TS-18 |
| FR-068 | **Sheet export:** a new JPEG (default) or PNG at the exact pixel size of the page at the target ppi (default 300 ppi → 1200×1800 px for 4×6 in), sRGB, with correct DPI metadata. Each photo is exactly W×H mm (±1 px). The file is saved next to the source with a descriptive name and never overwrites an existing file. | M | Owner | 0.6.0 | TS-15 |
| FR-069 | **Single ID photo export** (one photo at template size, without a sheet). | C | Proposed | 0.6.0 | TS-14 |
| FR-070 | **Printer-exact sheet canvas.** In addition to the nominal page (1200×1800 px), sheets can be exported at the **printer's native canvas including overscan**, as defined in the media definition (proposed for CY-02 4×6: 1240×1844 px at 300 dpi). Photos are then positioned inside the trimmed area, so that 1:1 printing gives exact physical sizes. Which canvas is the default depends on how sheets are printed in 1.0 (OQ-024). | M | Proposed | 0.6.0 | TS-15 |
| FR-071 | **Calibration page export.** Generates a test page (mm rulers on all edges, crosshairs, 30×40 / 35×45 / 50×50 mm reference rectangles) for a chosen media definition. Printing and measuring it confirms media size, overscan and scaling (OQ-001). | S | Proposed | 0.6.0 | TS-15, TS-19 |

### 6.7 Printing (deferred) and diagnostics

| ID | Requirement | P | Status | MS | Verified by |
|---|---|---|---|---|---|
| FR-050 | **Printing: deferred.** No printer communication in 1.0. The architecture defines the boundary (`IPrintService`, print-format model, render-to-media) so that later work can add Windows printer discovery, the Citizen driver, paper and media selection, quantities, queue/status, error handling, colour management, exact physical size, individual and batch printing, and ID sheets without restructuring the app. See [ARCHITECTURE.md §13](ARCHITECTURE.md#13-future-printing-boundary-fr-050). | — | Deferred | post-1.0 | — |
| FR-051 | **Logging and error reporting:** rolling local log files, plain-language error messages in the UI, and a “Copy diagnostic info” action. The log never contains image content. | M | Proposed | 0.1.0 | TS-17 |

---

## 7. Non-functional requirements

| ID | Requirement | Status | Verified by |
|---|---|---|---|
| NFR-001 | **Offline and private.** No network access is needed for any function. No telemetry. No customer image, thumbnail or metadata is ever transmitted. (Any future update check must be opt-in and send no customer data.) | Owner | TS-17 (network-off run), review |
| NFR-002 | **Platforms.** Windows 11 x64 is the production target and takes priority for UX decisions. Linux x64 (Mint 22 / Ubuntu 24.04) is the development target. Windows 10, macOS and ARM are not targets. | Owner | TS-17 |
| NFR-003 | **Responsiveness** (proposed targets on the reference machine, OQ-016): the UI thread is never blocked for more than 100 ms; the preview updates at 25 fps or better while dragging sliders; a preloaded next photo appears in under 300 ms; a cold-open 24 MP JPEG appears in under 1.5 s; a 100-photo folder shows its first screen of thumbnails in under 2 s (cold) and all thumbnails in under 20 s (cold) or 2 s (cached); a background save of a 24 MP JPEG takes under 3 s. | Proposed | TS-11 |
| NFR-004 | **Memory.** Working set under 2 GB in normal use with 24 MP photos and a 500-photo folder. Memory stays bounded and does not grow with folder size beyond the thumbnail index. | Proposed | TS-12 |
| NFR-005 | **Data safety.** No operation can lose an original. Writes are atomic and journalled, and backups are never overwritten (FR-005, FR-021–FR-023). | Owner | TS-08 |
| NFR-006 | **Image fidelity.** Ordinary photos are never resampled. Preview and export use the same processing code. A saved photo is one JPEG generation from the original. There is no colour shift for sRGB sources (ΔE00 < 1 against reference). | Owner | TS-05, TS-06 |
| NFR-007 | **Usability.** Primary actions are visible without menus. Controls are at least 32 px high (primary buttons 48 px or more). Body text is at least 13 px at 100 % scaling. The UI works at 1366×768 and up to 3840×2160 at 100–200 % Windows scaling. Every main action is keyboard-operable. Touch is not required, but nothing should be hover-only. | Owner / Proposed | TS-18, TS-19 |
| NFR-008 | **Localization quality.** No hard-coded UI text. Every key exists in both languages. Romanian text is reviewed by a native speaker before 1.0 (OQ-022). Layout tolerates text about 40 % longer than English. | Proposed | TS-16 |
| NFR-009 | **Maintainability.** Image processing, geometry and file safety are independent of the UI and unit-testable. The module structure is modest, with no microservices. Documentation is kept current by every milestone. | Owner | review, CI |
| NFR-010 | **Licensing.** Every dependency allows commercial, closed-source use, and attribution and notice files are shipped. No GPL-only components. LGPL components only as dynamically replaceable libraries or after owner/legal review (OQ-013). Model weights need explicit commercial-use permission. | Owner | dependency review checklist |
| NFR-011 | **Reproducible builds and CI.** GitHub Actions builds and tests on `windows-latest` and `ubuntu-latest` for every push and PR. Package versions are pinned centrally. | Owner | CI |
| NFR-012 | **Privacy of local caches.** Thumbnail cache and session files live in the user's local app-data folder, are size-limited, are purged after a retention period (proposed 30 days) and can be cleared from Settings. | Proposed | TS-11 |

---

## 8. Key domain rules

### 8.1 Print format vs. aspect ratio vs. pixels vs. ppi (FR-011)

| Concept | Example (10×15) | Used for |
|---|---|---|
| Nominal name | “10×15” (label only) | UI |
| Aspect ratio | 2 : 3 (exact) | Crop frame |
| Physical size | 101.6 × 152.4 mm by default; **printer-specific and configurable** (OQ-001) | Effective ppi, DPI metadata, sheets, future printing |
| Target ppi | 300 by default (OQ-005); used only when PhotoX *renders* a page (ID sheets) | ID sheet pixel size |
| Pixel size | Ordinary photos: whatever the crop yields in native pixels (for example 3696×2464). ID sheet: physical size × target ppi (for example 1200×1800). | Output |

Nominal metric names do not always match the inch-based media ratio. 10×15 matches 4×6 exactly (2:3), and 15×20 matches 6×8 exactly (3:4). 13×18 (0.722) does **not** match 5×7 (0.714). Presets are therefore defined by the configured physical size, never by the label.

### 8.2 Order state model

| State | Meaning | Determined by |
|---|---|---|
| To do | Never saved or reviewed | Default |
| Unsaved | Edited in this session, not saved yet | Session state (memory plus recovery file) |
| Saving | In the background save queue | Save queue |
| Edited | Saved at least once; original in `Original/` | `Original/<name>` exists, plus a sidecar entry |
| Reviewed | Operator confirmed with no change (FR-024) | Sidecar entry |
| Failed | Last save failed; edits kept | Session state |
| Error | File cannot be read | Load result |

“Progress” counts **Edited + Reviewed** out of the total.

### 8.3 Conflicts and tensions found in the originating prompt

These are recorded and handled here, not silently resolved:

1. **“Enter = Save & Next” vs. “shortcuts must not create accidental overwrites.”** Resolved by the guards in FR-018, the Original backup, and Revert (FR-025). The owner should confirm Enter (OQ-014).
2. **“Move original into `Original`” vs. “policy for unchanged photos.”** Saving an unchanged photo would create a needless backup and a lossy re-encode. Proposed default: write nothing (FR-024, OQ-006).
3. **Example ID layouts vs. geometry.** On a 4×6 in page with 2 mm spacing and 3 mm margins, **nine** 3×4 cm photos fit (3×3), not eight. **Six** 5×5 cm photos fit only edge to edge with zero margin and zero spacing. That is not printable on a borderless dye-sub printer with about 1.8 mm overscan per side, so the realistic maximum is two (two columns of 50 mm need 102 mm including spacing). Eight 3.5×4.5 cm photos fit on a true 4×6 in page (4×2, 3.2 mm side margins), but only six fit on a nominal 100×150 mm page, so the exact media size matters (OQ-001). The layout engine computes these from the media's *safe area* instead of hard-coding them (FR-066); see the table in [ARCHITECTURE.md §11.3](ARCHITECTURE.md#113-sheet-layout-engine-and-validated-example-layouts-fr-066).
4. **“The Original folder must not appear as part of the order” vs. a built-in explorer.** The explorer hides `Original` from the gallery and folder tree. A small “23 originals kept in ‘Original’” note gives transparency instead.
5. **“Save into the customer's original folder” vs. the safety of working on removable media.** Allowed, but warned (FR-054, OQ-019).
6. **Exact physical ID sizes vs. borderless printing.** A 1200×1800 px page printed “fit to page” on a printer whose borderless canvas is 1240×1844 px is enlarged by about 3 %, so a 35 mm photo becomes about 36 mm. Exact sizes need either a printer-exact canvas (FR-070) or 1:1 printing software. How files are printed before integration is open (OQ-024).
7. **Version 0.1.0 as “initial implementation version.”** Interpreted as the first implementation milestone (Foundation), not the first usable release. The first usable (pilot) version is 0.4.0; see [ROADMAP.md](ROADMAP.md).

---

## 9. Deferred functionality (not in 1.0)

| Item | Reason | Earliest |
|---|---|---|
| Direct printing / Citizen integration (FR-050) | Owner deferral; media sizes unconfirmed | 1.x |
| Batch Auto Correct / batch apply (FR-046) | Keep 1.0 focused; architecture-ready | 1.1.0 |
| HEIC/HEIF (FR-031) | Licensing and workflow open | after OQ-004 |
| TIFF, RAW | Not needed by the studio workflow | not planned |
| Country-specific biometric templates | Need authoritative requirements per country | after OQ-011 |
| Light theme, touch optimization | Lower priority | 1.x |
| Installer auto-update | Distribution model open (OQ-015) | 0.8.0 decision |
| Lossless JPEG crop (no re-encode for crop-only edits) | Needs an extra native component; quality 95 at 4:4:4 is visually lossless | evaluate post-1.0 |
| Monitor ICC profile (display colour management) | Avalonia has no colour management; sRGB preview first | 1.x (OQ-023) |

---

## 10. Release acceptance for 1.0 (summary)

1.0.0 is released only when **all** of the following hold. Details are in [TEST_PLAN.md §8](TEST_PLAN.md#8-release-acceptance).

1. Every **Must** requirement above is implemented, with its test suites passing on Windows and Linux CI.
2. The TS-08 fault-injection tests (crash at every step of the save transaction) pass with zero lost originals.
3. A full manual acceptance run (TS-19) on the physical Windows 11 studio PC has been completed on at least three real orders (one of 200+ photos) without data loss or a blocking defect.
4. Image-quality checks (TS-05, TS-06) pass, and test prints are approved by the owner for 10×15, 15×20 and one ID sheet.
5. Romanian translation has been reviewed by a native speaker.
6. Third-party notices are included, and the dependency licence review is signed off (OQ-013).
7. No open defect of severity Critical or High remains.

---

## 11. Change control

- This plan changes only through a reviewed edit. The plan revision (P-n) goes up and an entry is added to [DECISIONS.md](DECISIONS.md) when a decision changes.
- New requirements receive the next free ID in the relevant range. IDs are never reused or renumbered.
- Each implementation milestone updates the **Status** and **MS** columns here, together with [IMPLEMENTATION_LOG.md](IMPLEMENTATION_LOG.md).
