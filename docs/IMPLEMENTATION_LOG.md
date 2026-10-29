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
