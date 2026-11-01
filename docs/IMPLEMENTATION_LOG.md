# PhotoX — Implementation Log

## Purpose

This log records **significant completed work** once implementation starts:
- what was done;
- which requirements it covers;
- how it was validated (tests, measurements, manual checks);
- problems found;
- related commits.

It is the running history that lets the owner and future agents see the real state of the project. [PLAN.md](PLAN.md) states *what should be*, [CHANGELOG.md](CHANGELOG.md) lists user-visible changes per version, and this log explains *what actually happened*.

Entries are added newest **last**. Existing entries are never rewritten; corrections are added as new entries.

## Entry template

```markdown
### YYYY-MM-DD: <short title> (version 0.N.M[-dev])
- **Milestone:** M<n> / <name>
- **Requirements:** FR-…, NFR-…
- **Work done:** …
- **Validation:** tests run (suites, counts, OS), measurements (e.g. NFR-003 numbers on <machine>), manual checks (MT-xx)
- **Issues / deviations:** … (link OPEN_QUESTIONS / DECISIONS entries)
- **Commits:** <hash> <subject>, …
- **Prompt:** docs/prompts/prompt_NN.md
```

---

## Log

### 2026-10-29: Planning phase, plan revision P-1 (no version)
- **Milestone:** M0 Planning
- **Requirements:** none implemented. This entry records the planning deliverables only.
- **Work done:**
  1. Initialised the local Git repository and archived `prompt_01.md`.
  2. Validated the technology stack by web research (versions and licences as of 2026-10-29).
  3. Researched offline face detection and background segmentation, Citizen CY-02 specifications, and ID-photo geometry.
  4. Wrote PLAN, ARCHITECTURE, ROADMAP, OPEN_QUESTIONS, DECISIONS, TEST_PLAN and DEVELOPMENT.
  5. Generated eight concept mockups: four screens in English and Romanian.
  6. Computed the ID sheet-layout table.
- **Validation:** layout counts computed by script and cross-checked by hand. Mockups visually reviewed for text overflow in both languages.
- **Issues / deviations:**
  - The GitHub repository could not be created (no `gh` CLI, no token, github.com not in SSH `known_hosts`). Recorded as OQ-026 / DEC-020.
  - Several owner example layouts proved infeasible or improvable; see PLAN §8.3.
- **Commits:** the initial planning commit (see `git log`).
- **Prompt:** [docs/prompts/prompt_01.md](prompts/prompt_01.md)
- **Implementation status:** **not started; awaiting owner approval.**

### 2026-10-29: Plan revision P-2 (no version)
- **Prompt:** [docs/prompts/prompt_02.md](prompts/prompt_02.md). Approves the plan revision and autonomous implementation.
- **Work done:**
  1. Simplified the plan: saving became copy-original → temp file → atomic replace (no journal or recovery framework), and Magick.NET now performs the adjustments (no custom pipeline).
  2. Applied all owner decisions (OQ-001 … OQ-029), with new requirement IDs FR-072 to FR-077 and removed items listed in PLAN §7.
  3. Updated the mockups.
  4. Installed the .NET 10 SDK 10.0.403 user-locally and pinned package versions.
- **Verification:** documentation consistency check (IDs and links); mockups re-rendered and inspected.
- **Implementation status:** starting 0.1.0 (vertical slice).

### 2026-10-29: Version 0.1.0, vertical slice
- **Milestone:** 0.1.0 ([ROADMAP](ROADMAP.md))
- **Requirements:** see PLAN.md Progress (32 FR implemented, 5 in progress).
- **Work done:**
  - Solution with four projects (Core, Imaging, Storage, App) and two test projects.
  - Magick.NET-based load, render and save.
  - The safe-save pipeline with temporary originals and sidecar.
  - Avalonia UI: explorer, gallery, editor with crop overlay, exposure, Save & Next, filmstrip, EN/RO.
- **Verification (actually run):**
  - `dotnet test` on Linux Mint (.NET SDK 10.0.403): **36 core/imaging/storage tests** and **13 headless UI tests** pass.
  - The UI tests drive the real window: open folder → Enter → crop → exposure → Enter. They check that the saved file has exactly the selected crop, the original is preserved, the edits are restored on reopen, Skip never saves, a save failure keeps the photo and its edits, held Enter saves only once, the language switches live, and Romanian plurals are correct.
  - Headless screenshots at 1366×768 and 1920×1080 were inspected (EN/RO). Fixed from that inspection: shading seams, filmstrip highlight, clipped Romanian hint, and labels not switching language.
  - Owner sample `id_photo/IMG_6153.JPG` (3456×2304), **on a copy only**: header and 1800-px proxy load 245 ms; full-resolution save (render + encode + check + backup) 1.3 s on the development machine (16 cores, 30 GB RAM). These are not reference-machine numbers. Reference checksum unchanged (`243e7ef5…`).
- **Limitations / not yet done:**
  - Windows 11 not run yet (owner).
  - No persistent thumbnail cache.
  - Custom crop ratio and whole-photo-with-borders UI are not done (the renderer supports borders).
  - Contrast, colour, histogram, levels, rotation/straighten UI and Finish Order are not done.
  - The gallery is not virtualised; to be measured with large folders in 0.2.0.
- **Commits:** see `git log v0.1.0`.

### 2026-10-29: Version 0.2.0, complete ordinary editing
- **Milestone:** 0.2.0. All ordinary-photo requirements are implemented (see PLAN.md Progress).
- **Work done:**
  - Colour adjustments with tint, levels, histogram, Auto Correct, rotation and straighten, composition modes, 13×18 canvas, Custom ratio.
  - Finish Order with keep or erase, close prompt, in-window dialogs.
  - Thumbnail cache, next-photo preload, settings.
- **Verification (actually run, Linux Mint, .NET SDK 10.0.403):**
  - 50 core tests and 20 headless UI tests pass. One opt-in benchmark is skipped by default.
  - New UI tests cover:
    - rotate → portrait output;
    - whole-photo borders → 900×600 canvas from 800×600 with white borders;
    - 13×18 canvas size and ratio;
    - Custom 9:13 saved ratio;
    - Auto Correct is undoable;
    - Finish Order saves pending edits first, has Erase selected by default, removes `Original/` and keeps finished photos;
    - Keep mode deletes nothing;
    - a HEIC file is listed with an explanation.
  - Real desktop start (X11, about 8 s): started and logged without errors.
  - A Windows x64 self-contained build was cross-published from Linux to `publish/win-x64/` (not run on Windows).
- **Measurements** (`PHOTOX_PERF=1`):
  - Setup: 300 synthetic 4000×3000 JPEGs (3.2 MB each), headless, development PC (16 logical CPUs, 30 GB). These are **not** reference-machine numbers.

  | Measurement | Result |
  |---|---|
  | Folder scan | 77 ms |
  | First 20 thumbnails, cold | 0.99 s |
  | All 300 thumbnails, cold | 3.8 s |
  | All 300 thumbnails, warm cache | 0.8 s |
  | First editor open | 0.53 s |
  | Save & Next (12 MP save, then open the preloaded next photo) | median 1.82 s, max 1.89 s |
  | Peak working set | 707 MB (482 MB after the run) |
- **Limitations:**
  - Save time is dominated by single-threaded Magick work. Expect it to be slower on a 4-core PC; candidate for optimisation in 0.4.0.
  - The gallery is not virtualised (fine at 300 photos; to be checked at 1000+).
  - Display colour assumes an sRGB monitor.
  - Windows not yet tried by the owner.

### 2026-10-29: Version 0.3.0, ID Photo Creator
- **Milestone:** 0.3.0. FR-007, FR-061, FR-062, FR-064, FR-066–FR-068, FR-071, FR-074 and FR-075 are implemented.
- **Work done:**
  - Core: ID sizes as data, sheet grid with the 3×4 7 mm strip, timestamp file names, framing model.
  - Imaging:
    - a single `Distort` scale-rotate-translate into the exact output size;
    - background lift through a Magick-built soft mask that excludes the head oval;
    - the sheet renderer;
    - calibration sheets.
  - Storage: create-new exporter.
  - App: ID workspace (framing canvas with guides, adjustments, sheet step, folder and file pickers).
- **Verification (actually run, Linux):**
  - 72 core tests and 23 headless UI tests pass. One benchmark is skipped by default.
  - ID tests check:
    - exact pixel sizes for all six sizes;
    - capacities (3×4 → 9 with the strip, 3.5×4.5 → 6, 2×2 in → 2, 4×6 cm → 4, 5×7 cm → 2, 9×12 → 1);
    - the 7 mm strip is white and the cut guide lies 7 mm below each photo;
    - photo pixels are placed exactly;
    - the framing centre maps to the chosen source point;
    - the background lift brightens the background but not the face area;
    - file names get suffixes and are never overwritten;
    - the portrait is unchanged after export.
  - The owner's sample was framed and exported **from a copy**. Screenshots at 1920×1080 in Romanian were inspected. The reference checksum is unchanged.
- **Not verified / pending:**
  - Physical prints of the calibration and ID sheets through the Citizen software (owner).
  - Auto Align (0.4.0).
  - The background lift is tuned only on synthetic images and one sample.

### 2026-10-29: Version 0.4.0, Auto Align and packaging
- **Milestone:** 0.4.0. FR-063 and FR-077 implemented; FR-065 not included (DEC-041).
- **Work done:**
  - YuNet face detector on ONNX Runtime: anchor decoding as in OpenCV's FaceDetectorYN, plus non-maximum suppression.
  - Auto Align: levels the eye line, scales the head from the eye-to-mouth distance (× 3.4) and centres it. With several faces the operator chooses; with none a message is shown.
  - Velopack hook and a Windows installer cross-built on Linux.
  - CI workflow file.
  - Third-party notices updated.
- **Verification (actually run, Linux):**
  - 76 core tests and 26 headless UI tests pass. One benchmark is skipped by default.
  - Decoder unit test with a synthetic output.
  - No face found in a plain image.
  - On a copy of the owner's sample: one face, score 0.93. After Auto Align the eye line is at 0.451 of the photo height (target 0.460) and centred horizontally, roll 0.3°. The Auto Align screenshot was inspected: chin on the chin guide, top of hair at the crown line.
  - Two copies side by side: two faces, ordered left to right; choosing "Person 2" frames the right-hand person.
  - Auto Align is undoable.
  - Layout screenshots at 1280×720 logical (1920×1080 at 150 %), in Romanian, were inspected.
  - `releases/win/PhotoX-win-Setup.exe` (79 MB) and `PhotoX-win-Portable.zip` were built. **Not run on Windows.** Unsigned, so SmartScreen may warn.
- **Limitations:**
  - Head-height estimation uses a fixed proportion and is validated on one real portrait only.
  - Save & Next takes about 1.8 s for 12 MP on the development PC (single-threaded Magick work).
  - CI has not run (no remote yet).

### 2026-10-29: Version 0.4.1, Finish Order data-loss fix
- **Prompt:** [docs/prompts/prompt_03.md](prompts/prompt_03.md) §6 (highest priority). The owner tested 0.4.0 on Windows 11: after Finish Order (both *Keep* and *Erase*) all photos seemed to disappear.
- **Root cause:**
  - `PhotoSaver` marked its temporary output file Hidden on Windows, then replaced the photo by renaming the temp file over it. A rename keeps the file's attributes, so **every saved photo became hidden**.
  - The PhotoX scanner skips hidden files, and Windows Explorer hides them by default. The photos were still listed during editing (the gallery was not rescanned) and vanished when Finish Order reloaded the folder.
  - *Keep* deleted nothing. *Erase* deleted only the backups recorded in the sidecar, as designed. So the edited photos were still on disk, hidden. After *Erase*, the originals in `Original/` were gone.
  - The `Original` folder is never shown in the PhotoX explorer, so from inside PhotoX nothing seemed left.
  - **Evidence:** the GitHub Actions run of 0.4.0 (run 40935422945, the first CI run after the owner created the remote) failed on `windows-latest` in `Erase_removes_only_known_originals_history_temps_and_cache`. The folder scan returned no photos after two saves. The UI Finish tests passed on Windows only because they checked the state of every photo in an (empty) list.
- **Fix:**
  - The temp file is no longer marked Hidden, and the Hidden attribute is cleared after the replace.
  - Opening a folder clears the Hidden attribute of photos listed in its sidecar. This repairs folders finished with *Keep* in 0.4.0. After *Erase*, the photos must be unhidden by hand (Explorer → View → Show → Hidden items, then Properties → uncheck Hidden).
  - *Erase* is stricter (DEC-043): it deletes a backup only when the sidecar names are plain file names inside `Original/`, the finished photo still exists, and the backup still has the recorded size and write time. Kept backups stay in the sidecar and are reported. No recursive deletion anywhere.
  - Separately fixed: a localization test switched the UI language off the UI thread and failed intermittently on Linux CI.
- **Verification (actually run, Linux):**
  - 81 core tests and 26 headless UI tests pass. Skipped by default: the Windows-only hidden-repair test and the opt-in benchmark.
  - New tests cover:
    - three photos (two edited, one reviewed) with the customer's own `Original/P1.jpg`, `Original/notes.txt`, `Original/Older/P2.jpg` and `Prints/P1.jpg`;
    - *Keep* changes no file;
    - *Erase* deletes exactly the two PhotoX backups and the sidecar, and every other file is byte-identical;
    - a backup is kept when its photo is missing or the backup was replaced;
    - a sidecar path pointing outside `Original/` is ignored;
    - saved photos are not hidden and stay listed;
    - the UI Finish tests now check that the gallery still lists all photos and that files are byte-identical with *Keep*.
  - Windows: see the CI result recorded for this commit in the next entry.
- **Windows CI result for 0.4.1:** run 40947076204 passed on `windows-latest` (79 core and 24 UI tests, including the Windows-only hidden-photo repair test) and on `ubuntu-latest`, and built the Windows package.

### 2026-10-29: Version 0.5.0, owner feedback (prompt_03)
- **Prompt:** [docs/prompts/prompt_03.md](prompts/prompt_03.md) §§2–5, 7–9.
- **Investigation (performance, §9):**
  - Measured with a new benchmark (`PerformanceTests.Interactive_latency`) and the existing large-order benchmark. Both were pinned to 4 cores (`taskset -c 0-3`) on the development PC (Ryzen 7 6800H) to approximate the studio PC.
  - Root causes:
    1. The bundled Magick.NET native library is single-threaded (`ResourceLimits.Thread = 1`, no OpenMP), so pinning to 4 cores barely changed anything.
    2. Each slider step re-ran the whole tone chain on the 1800-px proxy: linear-light conversion 230 ms, sigmoidal contrast 210 ms, levels 120 ms.
    3. Straightening re-ran an EWA `Distort` (350 ms) plus the tone chain.
    4. Saving a 12 MP photo spent 3.2 s in the tone chain when contrast was used.
    5. Returning to the gallery rebuilt the whole view (pages were re-created by a `ContentControl`).
    6. Changing filters cleared and refilled the gallery.
    7. Thumbnails were decoded on the UI thread.
    8. The ID preview resampled the 1800-px proxy with EWA.
- **Work done:**
  - **Tone (DEC-044):** Magick.NET computes the per-channel curves once on a gradient, and a parallel pass applies them with Magick's HCL saturation formula. This is used for saving (exact tables) and for previews.
  - **Editor preview (DEC-045):**
    - cached base pixels per quarter turn; straightening drawn as a canvas transform; crop and borders drawn by the canvas;
    - histogram computed off the UI thread; next photo preloaded with its pixels; previous photo kept;
    - preview proxy shrunk with box averaging.
  - **ID preview:** proxy, half and quarter copies; the one closest to screen scale is used with bilinear lookups.
  - **Editor UI:**
    - shared tone panel (`ToneAdjustmentsViewModel`/`View`) with gradient slider tracks;
    - `LevelsHistogram` with three triangle handles;
    - fixed full-width toolbar with icons and segmented buttons; Straighten after the rotate buttons;
    - resizable panel with a remembered width;
    - apply crop (double-click / button / after Save) with undo;
    - Close mode when the order is done.
  - **Browse:**
    - pages kept alive;
    - asynchronous folder scan; thumbnails decoded in the background;
    - gallery updated in place;
    - four collapsible recent orders;
    - tree click to open and expand, no arrow without subfolders, selection styles;
    - selected card highlighted (its look moved from local values to styles, which had blocked the selected style);
    - Finish Order as the main button when done.
  - **ID:**
    - same tone panel;
    - landscape sheets for 3×4 (8 copies, strip as row gap) and wherever more copies fit;
    - the 3×4 strip option replaces the cut-guide option (DEC-046, DEC-047).
  - **Window:** in-app logo and name removed (DEC-048).
- **Measurements:** median per step, Linux development PC limited to 4 cores, headless UI (software rendering). "Before" is v0.4.1 measured with the same benchmark in a separate checkout.

  | Operation | 0.4.1 | 0.5.0 |
  |---|---|---|
  | Exposure slider step (preview + histogram ready) | 327 ms | 45 ms |
  | Contrast slider step | 564 ms | 43 ms |
  | Saturation slider step | 631 ms | 55 ms |
  | Straighten slider step | 1073 ms (re-render) | 44 ms (change + one software-rendered frame) |
  | Crop drag frame (layout + render) | 25 ms | 25 ms |
  | Open photo in editor | 440 ms | 273 ms |
  | Next photo (preloaded) | 26 ms | 13 ms |
  | Save & Next, 12 MP (save + open next) | 1922 ms | 458 ms |
  | ID rotation step | 180 ms | 59 ms |
  | ID exposure step | 256 ms | 67 ms |
  | 300 thumbnails, cold / warm cache | 5.9 s / 0.75 s | 4.7 s / 0.50 s |
  | First 20 thumbnails, cold | 1.2 s | 0.76 s |

  - Memory: peak working set in the large-order benchmark rose from 743 MB to about 1.0 GB when it runs alone (1.4 GB when it runs after the interactive benchmark in the same process; that benchmark's ID portrait and editor stay referenced by test-only event subscriptions). Causes include the gallery staying alive, reusable preview buffers and the preloaded pixels. This is acceptable for the 16 GB reference PC and will be re-checked on Windows.
  - The interactive numbers include the headless test's dispatcher work. On Windows, Avalonia renders with the GPU, so drawing should be no slower.
- **Verification (actually run, Linux):**
  - 93 core tests and 33 headless UI tests pass. Two opt-in benchmarks are skipped by default.
  - New tests:
    - fast tone path equal to the Magick operations (7 recipes × 8/16 bit, alpha, greyscale, preview);
    - applied-crop result view compared with the saved file (mean difference < 3.5 %);
    - double-click apply does not save, undo, geometry returns to the frame;
    - Close mode and Finish Order from the main button;
    - four recent orders and collapsing; tree arrows and click-to-open;
    - gallery selection is shown and filters keep the cards;
    - landscape ID layouts and the strip option.
  - Screenshots at 1366×768, 1280×720 and 1920×1080 (EN/RO) were inspected; this found the hidden toolbar icons and the invisible gallery selection, both fixed.
- **Limitations / pending:**
  - No studio-PC measurement yet.
  - Straightened 12 MP saves still spend about 1.7 s in Magick's single-threaded EWA distort.
  - Background whitening is unchanged (waiting for samples).
  - Physical prints, the Romanian review and Windows trials remain with the owner.
- **Windows CI result for 0.5.0:** run 40954894289 passed on `windows-latest` (91 core and 31 UI tests; skipped: owner-sample tests and opt-in benchmarks) and on `ubuntu-latest`, and built the Windows package.

### 2026-10-29: Plan review and version 0.6.0
- **Prompt:** [docs/prompts/prompt_03.md](prompts/prompt_03.md) §11: review the plan against the implementation, then implement what remains and is feasible.
- **Review of PLAN.md:**

  | Status | Requirements |
  |---|---|
  | Implemented and covered by automated tests (Linux and Windows CI) | FR-001–FR-005, FR-008–FR-019, FR-021–FR-024, FR-026, FR-027, FR-030–FR-037, FR-039, FR-040, FR-042–FR-045, FR-047, FR-049, FR-051–FR-054, FR-061–FR-064, FR-066–FR-068, FR-072–FR-075, FR-077; NFR-001, NFR-004–NFR-006, NFR-009–NFR-011 |
  | Partially implemented before this version, finished now | FR-010/FR-011/FR-040: print-format dimensions were not a data file. FR-061: ID sizes were in code. FR-071: the ID scale correction was missing (the calibration sheets existed). |
  | In progress (needs the studio) | NFR-002 (studio use on Windows 11), NFR-003 (measurements on the studio PC), NFR-007 (real Windows scaling and resolutions), NFR-008 (Romanian review) |
  | Intentionally deferred (PLAN §9) | FR-041 multiple selection, FR-050 direct printing / LinkServer hot folders, HEIC, batch operations, monitor colour management, printer overscan beyond FR-071, touch, country templates |
  | Blocked by hardware, samples or owner input | FR-065 automatic background whitening (suitable sample photos); FR-071 physical calibration and all Citizen printing tests (printer); Romanian wording (owner review) |

- **Work done:**
  - `ConfigData` (Storage) creates and reads the two data files. `PrintFormats.Configure` updates only the three fixed presets; `IdSizes.Configure` uses the listed sizes. Both skip implausible values.
  - The ID scale correction is a setting, applied by `SheetRenderer` (photo size and positions about the page centre).
  - The cache text shows the configured limits; JSON is written with relaxed escaping.
- **Verification (actually run, Linux):**
  - 97 core tests and 34 headless UI tests pass. Two opt-in benchmarks are skipped.
  - New tests:
    - files created with defaults (no computed fields);
    - edited values applied, implausible and unknown entries ignored;
    - a damaged file left untouched with defaults used;
    - scale correction shrinks photos (354 → 347 px at 98 %) and keeps the layout centred;
    - an exported sheet with 98 % entered in Settings has 347-px photos.
  - The app started on the Linux desktop with empty settings folders, created both files and logged no errors (stopped after 12 s).
- **Not verified:** physical prints (no printer), Windows studio PC.
- **Windows CI result for 0.6.0:** run 40955807735 passed on `windows-latest` (95 core and 32 UI tests; skipped: owner-sample tests and opt-in benchmarks) and on `ubuntu-latest`. The `windows-package` job produced the `PhotoX-windows` artifact (Velopack setup and portable zip), which has not been installed or tested on a Windows machine.

### 2026-10-30: Version 0.7.0, prompt_04 (icon, Collages, shared explorer, refinements)
- **Prompt:** [docs/prompts/prompt_04.md](prompts/prompt_04.md), archived unchanged.
- **Work done:**
  - **Version and icon (§§1–3, DEC-050):**
    - flat icon drawn from scratch (SVG master, 16–24 px variant, PNGs 16–512, ICO 16–256) via `assets/logo/build_icons.py`;
    - used for the window, executable, installer (`--icon`), top bar, About and the Linux desktop entry (`packaging/linux`, X11 window class `PhotoX`);
    - title "PhotoX 0.7.0"; CI names the installer `PhotoX-<version>-win-Setup.exe` and the artifact `PhotoX-<version>-windows`;
    - new gear geometry. The old icon buttons stretched their 24-unit paths uniformly, which pins wide icons to the top of the box: icons are now drawn unstretched and scaled.
  - **Duplicate image (§4, FR-078):** `PhotoDuplicator` (Storage) copies with create-new semantics; the gallery's right-click menu also offers Edit and Use for ID photo.
  - **Collages (§5, FR-079, DEC-053):** templates and layout in Core, `CollageRenderer` in Imaging, `CollageViewModel`/`CollageView`/`CollageCanvas` in App. The preview draws bitmaps; Save renders at 300 ppi from the full-resolution photos.
  - **Shared explorer (§6, DEC-055):** `ExplorerViewModel`/`ExplorerView` extracted from the gallery; `PhotoPickerViewModel` + `PickerGrid` for ID Photos and Collages; `ThumbnailSource` shared by all thumbnail grids.
  - **Editor (§7):**
    - + / − keys; one tone-only Reset; largest crop on orientation switch;
    - custom size in cm on the smallest sheet (DEC-051, a 20×30 = 8×12 in sheet added to `print-formats.json`, OQ-030);
    - hold-to-compare button; corner-arrow reset icon; centred angle display; 24-px slider handles; no hint lines.
  - **Histogram (§7, DEC-054):** the review found that the histogram showed the finished preview (after levels, contrast and saturation) while the triangles act on the input to levels. Dragging a triangle therefore reshaped the data under it, and stretching levels left comb gaps. The square-root scale also exaggerated small populations. It now shows the levels input on a linear scale.
  - **ID (§8, DEC-052):**
    - Auto Adjust (the Auto Correct estimation on the unadjusted frame); built-in portrait picker; Space-only before/after; double-click resets zoom and rotation;
    - Continue; numbered step tabs; JPEG only; sizes in cm with removable custom sizes;
    - 3×4 head 70 %; 9×12 without guides or Auto Align; 2×2 in edge to edge (four fit, six possible);
    - upgrade of unedited 0.6.0 `id-sizes.json` entries.
    - **Bug found:** the frame on screen kept the previous size's width because `PreviewFrameWidth` was not notified when the size changed. This is why every size looked like 3.5×4.5. Fixed.
- **Verification (actually run, Linux):**
  - 117 core tests and 41 headless UI tests pass (the owner-sample tests ran on a temporary copy); skipped: the Windows-only hidden-photo test and the two opt-in benchmarks.
  - New tests:
    - custom sizes on sheets and the 9×13 render plan;
    - 2×2 in edge-to-edge positions;
    - the data-file upgrade (edited entries kept);
    - collage slots for every template in both orientations, slot crops, and a rendered collage with white gaps;
    - the levels histogram ignores levels and follows exposure;
    - collage names excluded from galleries; Duplicate never overwrites;
    - UI: version title, duplicate in the gallery, orientation switch, + / − keys, Reset with undo, the ID picker, ID sizes, Space, Auto Adjust and slider resets, and a collage saved through the UI (1200×1800, 300 ppi, white margin, photo content).
  - Screenshots of the editor, the ID picker, ID framing and Collages (1366×768) were inspected. This found the straighten group out of line and the icons pinned high; both were fixed.
  - The Linux desktop entry was installed into a scratch data folder and passed `desktop-file-validate`.
- **Measurements:**
  - Interactive benchmark, pinned to 4 cores (`taskset -c 8-11`). A Windows 11 virtual machine was running on the development PC, so all numbers are higher than those recorded for 0.5.0.
  - 0.6.0 was measured in a temporary checkout under the same conditions for comparison:

  | Operation (median) | 0.6.0 | 0.7.0 |
  |---|---|---|
  | Open photo in editor | 368 ms | 381–437 ms |
  | Exposure / contrast / saturation step | 78 / 77 / 179 ms | 78–91 / 75 / 174–195 ms |
  | Straighten step (change + frame) | 31 ms | 33–38 ms |
  | Crop drag frame | 25 ms | 25 ms |
  | Next photo (preloaded) | 52 ms | 52–55 ms |
  | ID rotation / exposure step | 65 / 84 ms | 68–70 / 86–89 ms |
  | Collage pan step (change + frame) | — | 25 ms |

  - Within run-to-run noise; the ID step adds one copy of the unadjusted frame for Auto Adjust and the histogram (a few ms).
- **Plan review (prompt_04 §10):** with 0.7.0 every Must requirement is Implemented. The remaining items are the same as at 0.6.0, and none can be done without the owner or new material:
  - studio use on Windows (NFR-002), studio-PC measurements (NFR-003) and Windows scaling (NFR-007);
  - the Romanian review (NFR-008), now including the new texts;
  - physical prints for FR-071, now including edge-to-edge 2×2 in sheets and the 20×30 size (OQ-030);
  - background whitening, which waits for sample photos (FR-065, DEC-041);
  - the deferred items of PLAN §9.

  Nothing further was implemented, to avoid adding unrequested features.
- **Not verified:** Windows (see the CI result once recorded), the studio PC, physical prints of collages, 2×2 in sheets and custom sizes, and the Romanian wording of the new texts.
- **Windows CI result for 0.7.0:**
  - Run 41039471662 passed on `windows-latest` (115 core and 39 UI tests; skipped: owner-sample tests and opt-in benchmarks) and on `ubuntu-latest`.
  - The `windows-package` job produced the artifact `PhotoX-0.7.0-windows` with `PhotoX-0.7.0-win-Setup.exe` and `PhotoX-0.7.0-win-Portable.zip`. All seven frames of the new `photox.ico` were found embedded in the packaged `PhotoX.exe`. Neither package has been installed or run on a Windows machine.
  - The run before (40956546045, a docs-only commit after 0.6.0) had failed on Windows in `Saved_crop_is_shown_as_the_result_and_matches_the_file`: 4.5 % against a 3.5 % limit, because the test photo was random. The fixture is now seeded and lightly blurred (1.3 %).

### 2026-10-30: Version 0.8.0, prompt_05 (direct printing, new Photos workflow)
- **Prompt:** [docs/prompts/prompt_05.md](prompts/prompt_05.md), archived unchanged. The owner also asked for a bigger PX in the logo during the work.
- **Work done:**
  - **Printing (§§2–4, 6; DEC-057, DEC-058):**
    - new project `PhotoX.Printing`: print items (unsaved edit, saved photo, original, ID sheet, collage), the queue (`PrintService`) and the Windows backend (spooler and GDI through P/Invoke; no new package);
    - `PrintLayout` (Core) and `PrintRenderer` (Imaging) prepare the page at device pixels;
    - `PrintHistory` (Storage) keeps the counters in the data folder.
    - App: print dialog, queue panel, counter badges.
  - **Photos workflow (§5, DEC-056):**
    - multiple selection; Print selected / Print all / Edit this photo / Use for ID photo / Save changes / Restore original (also in the right-click menu);
    - Finish Order and `OrderCleanup` removed; `OrderBackups` implements Save Changes and Restore per photo with strict identity checks.
  - **ID (§7):** compact size chips, the explorer and thumbnails under them (a click loads the photo), no source section or disclaimer, Save sheet and Print.
  - **Collages (§8, DEC-059):** 10×15 and 15×20 only; slot sizes in cm on the canvas; resizing by dragging gaps or typing the selected slot's size (at least 1 cm per cell); Save and Print share the composition.
  - **UI (§1, DEC-060):**
    - branding at the right of the top bar with the X in the lens colours; blue selected workspace;
    - explorer note removed; the details panel folds only the preview;
    - disabled buttons dim their text; larger PX in the icon.
- **Verification (actually run, Linux):**
  - 127 core tests and 45 headless UI tests pass. Skipped: the Windows-only tests (hidden photos, the PDF-printer job) and the two opt-in benchmarks.
  - New tests with a fake printer (TS-20):
    - layout and page geometry (matched paper, A4 centring, paper too small);
    - an unsaved edit printed equals the saved render and writes nothing;
    - an ID sheet printed pixel for pixel;
    - counters for accepted, cancelled before Windows, cancelled in Windows, removed outside PhotoX, refused, and a cancel that Windows refuses, plus persistence;
    - spooler flag mapping;
    - UI: dialog defaults, the plain 6x8 paper, PDF A4 centring, an ID sheet and a collage printed at 1200 × 1800, counters on thumbnails and after reopening, cancelling in the queue.
  - New Save Changes / Restore tests (TS-09) with the customer-file scenario of the old Finish Order tests, partial selections, kept backups and their reasons, path traversal, read-only photos, and UI flows with warning and cancel.
  - Screenshots inspected: gallery with multiple selection and actions, print dialog, print queue, ID explorer, collage sizes. Found and fixed: disabled button text not dimmed; a squeezed folder tree in ID and Collages.
  - The app started on the Linux desktop with empty settings folders (no printers: the dialog explains that direct printing is Windows-only) and logged no errors.
- **Not tested:**
  - the Citizen CY-02 and any physical print (no printer here);
  - the driver's settings dialog;
  - Windows runs outside CI.

  The Windows CI result for the virtual PDF printer is recorded below once the run has finished.
- **Plan review (prompt_05 §10):** every Must requirement is Implemented. Multiple selection (FR-041, formerly deferred) and printing (FR-050) are now Implemented too. What remains needs the owner or new material:
  - Citizen printing and calibration (OQ-031, FR-071);
  - studio use on Windows and studio-PC measurements (NFR-002/003/007);
  - the Romanian review (NFR-008, now with the printing texts);
  - background whitening, which waits for samples (FR-065);
  - the later optimisation of two 10×15 photos on one 15×20 sheet.

  Nothing else was added.
- **CI result for 0.8.0:** two runs of the same commit gave different results, so the failures were intermittent.
  - **Run 41049317024 (main) failed** in two UI tests:
    - **Linux:** a setup failure in `Romanian_plurals` inside Avalonia's per-test application setup ("a different thread owns it", `DefaultRenderLoop.Add`). The likely cause is background work from an earlier test reaching the dispatcher during that setup.
    - **Windows:** `Saved_crop_is_shown_as_the_result_and_matches_the_file` measured 3.8 % against a 3.5 % limit (1.3 % on Linux).
  - **Run 41049317604 (tag v0.8.0) passed** on `ubuntu-latest` and `windows-latest` (Windows: 126 core and 43 UI tests) and built the Windows package.
  - **Virtual printer:** in both Windows runs, `Windows_virtual_printer_accepts_a_photo_at_true_size` ran and passed (it is not among the skipped tests). A 10×15 photo went as a real job through the spooler and GDI to Microsoft Print to PDF: the job was accepted, followed until it left the queue, and the PDF had two pages for two copies.

### 2026-10-30: Version 0.8.1, test and dispatcher fixes after the 0.8.0 CI run
- **Work done:**
  - Background callbacks (print queue, print counters, thumbnails) post to the dispatcher captured on the UI thread.
  - The headless tests use one application per test run (`AvaloniaTestIsolationLevel.PerAssembly`).
  - The preview-versus-saved check now requires the aligned comparison to beat a 6 %-shifted one by a clear margin (and stay under 7 %), which still detects a misplaced crop.
- **Verification (Linux):** 127 core tests and 45 UI tests pass, the UI suite twice in a row. The Windows result is recorded below once CI has run.
- **Windows CI result for 0.8.1:**
  - Run 41050159676 passed on `windows-latest` (126 core and 43 UI tests, including the virtual-printer job) and on `ubuntu-latest` (124 and 43).
  - It built `PhotoX-0.8.1-win-Setup.exe` and `PhotoX-0.8.1-win-Portable.zip` (artifact `PhotoX-0.8.1-windows`).
  - Not installed or run on a Windows PC; nothing printed on the Citizen CY-02.
  - **Correction (recorded 2026-10-30 with 0.9.0):** the run of the tag `v0.8.1` (41050951200) **failed** on `windows-latest`. `Saved_crop_is_shown_as_the_result_and_matches_the_file` measured 3.8 % against 5.5 % for the 6 % shift, above the 0.6 ratio. That run was not looked at when the result above was written. The check is made robust in 0.9.0 (see below).

### 2026-10-30: Version 0.9.0, prompt_06 (consistent UI, light and dark mode, Printing workspace)
- **Prompt:** [docs/prompts/prompt_06.md](prompts/prompt_06.md), archived unchanged.
- **Work done:**
  - **Icon (§2, DEC-061):** redrawn as SVG from measurements of the owner's `icon example.png` (light tile, camera outline in exactly `#3B82F6`, dark PX, no gradient), with a 16–24 px variant; PNGs and ICO rebuilt. The X of the name uses the same blue.
  - **Light and dark mode (§3, DEC-061):** theme dictionaries and dynamic resources throughout, status colours as classes, a theme-aware histogram, quieter disabled buttons; chosen in Settings and remembered.
  - **Consistency (§1, DEC-062):**
    - workspace icons;
    - Auto Adjust in both workspaces;
    - one editing sidebar (Undo, Redo, Reset, histogram, Auto Adjust, sliders) shared by Photos and ID Photos;
    - resizable left and right panels in every workspace, widths remembered.
  - **Photos (§4):** no selection hint; Edit progress and the folder's printed copies; *Discard originals* (same checks); thumbnail-size slider (double-click resets, remembered).
  - **ID Photos (§5):**
    - toolbar with the steps, the format list, 90° turns, Auto Align, zoom, rotation, background brightness, reset framing and before/after;
    - explorer and photos on the left; the shared sidebar on the right;
    - 3 × 4 cm × 8 by default; the US visa size shown as 5 × 5 cm (still 50.8 mm); *Print* and *Save* labels;
    - 90° turns are part of the framing, so every output follows.
  - **Collages (§6, DEC-066):** drag and drop from the thumbnails or the file manager into a frame; the move handle on the hovered photo; a right-click menu with 90° rotation, fit and remove; the renderer turns photos.
  - **Printing (§7, DEC-063):**
    - a Printing workspace instead of the dialog and queue popup (preview, printer, size, paper, copies, Clear, Print, Back);
    - the session queue with progress;
    - the history of today, the week or the month with outcomes, and statistics by period and size, from a new `PrintLog`;
    - closing asks while jobs are not yet handed to Windows;
    - counters and history moved to the user data folder (outside the Velopack installation folder); the 0.8 counters are copied once.
  - **Installer (§8, DEC-065):** kept Velopack; a splash with icon, name and version (`packaging/windows/make_splash.py`); shortcut locations stated explicitly; the program folder and how to uninstall in Settings.
  - **Settings (§9, DEC-064):** JPEG quality 100 by default; the old default 95 of version-1 settings files becomes 100 once; theme selection.
  - **Fixed on the way:** in ID Photos, undoing a slider change kept the new value (the undo snapshot was taken after the change). The snapshot is now the state before the change, with a test.
- **Plan review (§11):** every Must requirement is Implemented. One feasible gap was closed: the calibration sheet could only be saved for printing through Citizen's software, so it can now be printed from the Printing workspace (*Calibration sheet…*, 10×15 or 15×20) through the same path as photos, which is what the pending calibration has to measure. What remains needs the owner: Citizen prints and calibration (OQ-031, FR-071), studio use on Windows and scaling (NFR-002/003/007), the Romanian review (NFR-008), samples for background whitening (FR-065), and the later two-up 15×20 printing. Nothing else was added.
- **Verification (actually run, Linux):**
  - 135 core tests and 51 headless UI tests pass. Skipped: the Windows-only tests (hidden photos, the PDF-printer job) and two opt-in benchmarks.
  - New tests (TS-21):
    - print history outcomes, statistics and week start, a damaged line, failed jobs, migration of the counters;
    - settings upgrade; 5 × 5 cm and 3 × 4 × 8;
    - ID quarter turns (framing, point mapping, rendered colours, undo and redo across a turn, Reset keeps the framing);
    - collage rotation in the renderer and the menu, dropping on a frame;
    - the Printing workspace from Photos and ID Photos, queue to history, statistics and the folder summary, history after a restart, the calibration sheet printed at 15 × 20 (1800 × 2400 px);
    - the thumbnail slider.
  - **Visual review (§10):** headless screenshots of Photos, the editor, ID framing and sheet, Collages, Printing and Settings in dark and light mode at 1440 × 900 were inspected.
    - Fixed: disabled buttons brighter than enabled ones in dark mode; the collage hint clipped at the bottom.
    - The ID toolbar fits at 1440 px and scrolls on narrower windows, like the editor's.
  - The real app was started on the Linux desktop with separate settings folders:
    - a version-1 settings file with quality 95 was upgraded to version 2 with quality 100;
    - light mode, the gallery, the details panel and the Printing workspace (no printers on Linux) looked as in the headless screenshots.
- **Not tested:**
  - the Citizen CY-02 and any physical print;
  - the installer on Windows: the splash, Installed apps entry, update in place and uninstall rely on Velopack's standard behaviour and were not run here;
  - drag and drop with a real mouse (the drop handling is tested through the view model; Avalonia's drag source needs a pointer);
  - Windows display scaling.

  The Windows CI result is recorded below once the run has finished.
- **CI result for 0.10.0:**
  - **Runs:** 41325195821 (main) and 41325198240 (tag v0.10.0), both on commit f3ea362, **passed**.
  - **windows-latest:** 146 core and 57 UI tests. That includes the 22 MP save-integrity tests (six variants, concurrently), the pixel-identity tests of the faster geometry, the full-size tone test and the virtual-printer job, so the black-image fix is also checked with the Windows Magick library.
  - **ubuntu-latest:** 144 core and 57 UI tests.
  - **Skipped on CI:** the owner's samples and examples, Auto Align samples, benchmarks and the mockup generator.
  - **Package:** `windows-package` built the artifact `PhotoX-0.10.0-windows` (`PhotoX-0.10.0-win-Setup.exe` and the portable zip).
  - **Not done:** installed or run on a Windows PC; nothing printed on the Citizen CY-02.
- **CI result for 0.9.0:**
  - **Run 41066925692 (main, commit 02474cd) passed:**
    - `ubuntu-latest`: 132 core and 49 UI tests;
    - `windows-latest`: 134 core and 49 UI tests, including the virtual-printer job (`Windows_virtual_printer_accepts_a_photo_at_true_size` ran and passed);
    - `windows-package` built the artifact `PhotoX-0.9.0-windows`: `PhotoX-0.9.0-win-Setup.exe` (with the version splash) and the portable zip.
  - **Run 41066928190 (tag v0.9.0, same commit) failed** on `ubuntu-latest`: the same crop-preview test measured 3.3 % against 4.6 % for the 6 % shift. The difference between screen and file varies between runs (1.3 % to 3.8 %), and a 6 % shift of the smooth test image is only 4.6 % to 5.5 %, so the margin was too small. The comparison now uses a 12 % shift: a misplaced crop is still detected, and normal rendering differences pass. This is a test-only change (no version change, DEC-034).
  - **Run 41068376600 (main, commit fd20e97, with the robust check) passed:** `ubuntu-latest` 132 core and 49 UI tests, `windows-latest` 134 and 49 (virtual printer included), and the Windows package.
  - Not installed or run on a Windows PC; nothing printed on the Citizen CY-02.

### 2026-11-01: Version 0.10.0, prompt_07 (black-image fix, faster saving, editor actions, printing refinements)
- **Prompt:** [docs/prompts/prompt_07.md](prompts/prompt_07.md), archived unchanged. The owner's example photos (`photos_example/`, 170 JPEGs) are kept out of Git (`.gitignore`) and were only read; every test that saves works on copies in a temporary folder and checks the originals' checksums.
- **Partly black photos (§1.6, DEC-067), investigated first:**
  - **Samples checked:** none of the 170 examples was already damaged; all are sRGB, so colour-profile conversion was ruled out.
  - **Reproduced on Linux:** a test saved 22 MP photos through the real pipeline in six variants. Only the variant without crop or turn (full frame, an exposure change) came out black from band 3 of 16 onwards; the same happened with copies of the owner's 5760 × 3840 Canon photos at the default 10×15 crop, whose full frame is exactly 2:3. Their thumbnail, preview and full size were black below about 1/8, as reported. PXL photos (3:4, so cropped) and turned photos were fine.
  - **Cause:** the tone pass wrote a clone in strips with queued (write-only) pixel access; ImageMagick gives a clone that still shares its pixels new memory without copying them for such a write, so every later strip read zeros. The editor showed the kept original, which is why it looked right there.
  - **Fix:** `ToneProcessor.OwnPixels` (a no-op composite) makes the image own its pixels first.
  - **Verification:**
    - the six-variant test, the full-size tone test and the owner-example test pass with the fix;
    - without it, the owner-example test fails exactly as reported (band differences 0.29–0.69 against the reference);
    - with it, the differences are 0.001–0.002 in all three read paths.
  - **Not Windows-specific:** it reproduced on Linux.
- **Saving after rotation (§1.4, DEC-068):**
  - **Profile:** straightening was the slow step (distorting the whole enlarged photo, then cropping: 3.1–4.1 s); a 90° turn cost 0.2–0.45 s.
  - **Now:** crop-then-turn without straightening (pixel-identical), and straightening rendered straight into the crop in parallel bands.
  - **Measured on the 5760 × 3840 example** (16-core development PC, Linux, warm, best of three):

    | Step | Before | After |
    |---|---|---|
    | turn only | 293 ms | 148 ms |
    | straighten 2° | 3144 ms | 994 ms |
    | turn + straighten | 3322 ms | 1130 ms |
    | turn + straighten −4.5° | 4107 ms | 1054 ms |

    A full save with a turn and straightening now takes about 2.0 s.
  - **Not measured:** the studio PC and Windows (where the Magick library is expected to use OpenMP).
- **Work done:**
  - Editor: 9×13 (DEC-069); Crop | Whole; Close, Print, Save, Save & Next; no Previous/Next buttons; Reset with straightening and crop (DEC-073).
  - ID Photos: background brightness removed with its code; steps above the photo with the format and Clear; framing tools below (DEC-074).
  - Printing:
    - paper by size with `PaperMatcher` (DEC-070); a message when the printer has no paper of the size;
    - Crop | Whole (DEC-071) with a matching preview;
    - calibration restores the prepared print; statistics folded on Print; all time "since";
    - Escape back to the origin with its state; tab at the right.
  - Collages: Save. Light by default (DEC-072).
  - The 10×15 calibration sheet shows the centred 9×13 frame.
  - **Mockups (§6):** four proposals and their use in ID Photos and Printing, drawn with PhotoX's styles by an opt-in generator test, in [docs/mockups/buttons](mockups/buttons/README.md). Not implemented.
- **Verification (actually run, Linux):**
  - **Totals:** 148 core and 59 UI tests pass (Release). Skipped: the Windows-only tests, two opt-in benchmarks and the mockup generator. The owner-example test runs only where `photos_example/` exists (skipped on CI).
  - **New tests:** TS-22.
  - **Screens inspected (headless):** the editor with 9×13 and the new actions, ID Photos before and after Clear, Printing with Whole (white sides in preview and print), the statistics toggle, the Printing tab on the right.
  - **Found and fixed during the work:**
    - the print preview ignored Whole;
    - the ID tool row was slightly too wide at 1440 px;
    - a test left the shared test application in dark mode.
- **Not tested:**
  - the Citizen CY-02 and its paper list (OQ-031), and any physical print;
  - Windows outside CI and the save timings there;
  - drag and drop with a real mouse.
- **Plan review (§8):** every Must requirement is Implemented. What remains needs the owner: Citizen prints and calibration (OQ-031, FR-071), studio use on Windows and scaling, the Romanian review, samples for background whitening (FR-065), the later two-up 15×20 printing, and the choice among the button mockups. Nothing else was added.

  The Windows CI result is recorded below once the run has finished.

### 2026-11-01: Version 0.10.1, icons beside button labels (owner's choice B)
- **Instruction (owner, in the conversation after 0.10.0):** implement proposal B (icon and text) of the button mockups, without moving any button, only where useful as in the examples, and raise only the last version number.
- **Work done (DEC-075):**
  - Six icons (Close, Save, Save & Next, Reset, Undo, Redo) were added on the 24-unit grid, with one style for icons in labels (text colour, white on blue, dimmed when disabled). The printer and back icons are the existing ones.
  - **Photos editor:** Close, Print, Save, Save & Next and Reset gained icons; Undo and Redo became icon buttons.
  - **ID Photos:** Clear, Continue, and the sheet step's Back, Save and Print gained icons.
  - **Collages:** Save and Print gained icons.
  - **Printing:** Clear and Print gained icons.
  - No button moved. The mockup README records the choice.
- **Verification (Linux):** 148 core and 60 UI tests pass. A new UI test checks the editor's action buttons for an icon beside their label. Headless screenshots of the editor, ID framing and sheet steps, Printing and Collages in light and dark mode were inspected.
- **CI result for 0.10.1:** runs 41328523416 (main) and 41328525438 (tag v0.10.1), both on commit 6eac4a8, passed: `windows-latest` 146 core and 58 UI tests, `ubuntu-latest` 144 and 58; `windows-package` built the artifact `PhotoX-0.10.1-windows`. Not installed or run on a Windows PC.
