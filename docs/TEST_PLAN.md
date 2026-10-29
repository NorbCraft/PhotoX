# PhotoX — Test Plan

Status: **proposal** (plan revision P-1, 2026-10-29). Requirements and IDs are in [PLAN.md](PLAN.md). The milestone mapping is in [ROADMAP.md](ROADMAP.md#4-testing-expectations-per-milestone).

## 1. Verification strategy

PhotoX is used commercially on customers' irreplaceable photos. Testing priorities follow the product priorities: **(1) no data loss, (2) image fidelity, (3) workflow correctness, (4) performance, (5) UI polish.**

| Level | Tooling | Runs | Purpose |
|---|---|---|---|
| Unit | xUnit v3 (3.2.2), Shouldly, FsCheck (property-based) | every push, both OSes | geometry, layout, LUT math, Auto Correct estimation, state derivation |
| Integration | xUnit + real Magick.NET + temp folders | every push, both OSes | decode/encode/metadata/ICC, save transaction on a real file system |
| Fault injection | xUnit + `IFileSystem` fake that can fail or “crash” at step *n* | every push | crash-safety invariant of the save transaction |
| UI (headless) | Avalonia.Headless.XUnit | every push | view-model and view behaviour, shortcuts, navigation, localization |
| Visual regression | headless `CaptureRenderedFrame()` and Magick `Compare`, per-OS baselines | every push | catches unintended UI changes |
| Image-quality golden tests | Magick `Compare` (PSNR, ΔE00 via Lab) against reference renders | every push | processing correctness, preview/export parity |
| Performance (smoke) | BenchmarkDotNet for micro-benchmarks; timed scenarios with generous CI thresholds | nightly / on demand | regressions; real targets measured on the reference PC |
| Manual | scripted checklists (MT-xx) | each milestone; release | real hardware, real orders, real prints |

**Rules:**
- A requirement is “done” only when its listed suites pass on both Linux and Windows CI.
- Every bug fix adds a regression test where technically possible.
- Tests never touch real customer photos inside the repository (§3).

## 2. Test suites

Each suite lists representative cases. The full case lists grow with implementation and are kept in the test code, named `TSxx_…`.

### TS-01: Crop geometry and print formats (FR-003, FR-010–FR-012, FR-047–FR-049)
- The default crop is the largest centred rectangle of the preset ratio for landscape, portrait and square sources and for extreme ratios (panorama 4:1, 1:3).
- Ratio lock holds under every handle drag; the frame never leaves the image; there is never upscaling.
- Pixel output: for each preset and orientation, the output width/height ratio equals the preset ratio within ±0.5 px per edge rounding (property test over random sizes 100 px to 16 000 px).
- Effective ppi is correct for 4×6 and 6×8 media definitions; low-resolution thresholds trigger at 200 and 150 ppi.
- Custom ratios, including the 13×18 cm vs 5×7 in mismatch example.
- The safe-area guide position matches the configured overscan.
- Whole photo with border (if approved): canvas ratio exact, photo centred, no resampling of photo pixels.

### TS-02: Orientation (FR-032, FR-015)
- EXIF orientations 1–8 on JPEG fixtures display upright. Saved files are physically upright with Orientation = 1.
- Quarter turns are lossless (pixel-exact compared with the reference transpose).
- Straighten ±10°: the crop is always inside the valid rotated area (no transparent or empty pixels in output).
- The EXIF orientation combines correctly with quarter turns and straighten.

### TS-03: Histogram and levels (FR-043, FR-044)
- Bin counts on synthetic images with known distributions (flat, gradients, two-level) match the expected values exactly.
- The histogram reflects the edited output inside the crop only.
- Levels formula: endpoints, gamma midpoint and monotonicity (property test).
- Reset restores identity.

### TS-04: Adjustments, Auto Correct and preview/export parity (FR-013, FR-014, FR-016, FR-042, FR-045, FR-064)
- Each operation at neutral is the identity (bit-exact for 8-bit round trips).
- Monotonicity and bounds: exposure ±, contrast ±, saturation ±, temperature ±.
- **Parity:** render the proxy vs render full resolution then downscale the same way. Max ΔE00 < 0.5, mean < 0.1.
- Undo/redo sequences restore exact recipes; slider drags make one history step.
- Auto Correct:
  - Clamps respected: |EV| ≤ 1, black ≤ 0.06, white ≥ 0.88, temperature ≤ ±25.
  - No change on already well-exposed references.
  - Skin-tone fixtures: hue shift in skin regions < 3°.
- Golden images: a set of synthetic scenes with stored expected outputs, compared at PSNR ≥ 45 dB.

### TS-05: Colour management (FR-034)
- Fixtures: sRGB, Display P3, Adobe RGB, ProPhoto (16-bit PNG), untagged, CMYK JPEG with profile, and PNG with gAMA/cHRM.
- Colour-checker patches after load to sRGB match reference Lab values within ΔE00 < 1.0. Out-of-gamut handling uses relative colorimetric intent with black-point compensation.
- Output is tagged sRGB, and round trip source → output → re-read is ΔE00 < 1.0 for in-gamut patches.
- Preview pixels equal output pixels for the same recipe (shares TS-04 parity).

### TS-06: Export quality and metadata (FR-026, FR-033, FR-035)
- No resampling: output pixel grid equals the crop of the source grid (pixel-exact for PNG; PSNR ≥ 45 dB for JPEG q95).
- JPEG settings: quality 95, sampling 4:4:4 (read back from the file).
- Generation loss: save → re-edit → save produces output still exactly one generation from the original (compare against direct single render: PSNR ≥ 50 dB).
- Metadata:
  - EXIF date, camera, GPS kept (per OQ-020 default); Orientation = 1; dimensions updated; embedded thumbnail removed.
  - IPTC and XMP kept; DPI equals effective ppi.
- 16-bit PNG stays 16-bit; alpha preserved.

### TS-07: Save & Next workflow (FR-003, FR-004, FR-017–FR-019, FR-024, FR-027)
- State-machine tests: sequences of Save & Next, Skip, Previous, Save, Reset and Undo across 1–300 photos lead to the expected state per photo and correct progress counts.
- 200 consecutive Save & Next operations with background saves complete in order. All outputs are valid, and none is lost or duplicated.
- No-op photo (FR-024): nothing written, state Reviewed.
- Unsaved edits persist across navigation. The close-with-unsaved prompt lists the right photos. The session-recovery file restores pending recipes after a simulated crash.
- Shortcut guards: auto-repeat Enter ignored; Enter within 300 ms of opening ignored; Enter in text field ignored.
- Save failure keeps edits, marks Failed and allows retry.

### TS-08: Backup, safe save and recovery (FR-005, FR-021–FR-025, NFR-005), the most important suite
- **Invariant check under fault injection:**
  - For each step S1–S7 of the first-save and replace transactions, inject (a) an exception and (b) a simulated crash (process state discarded) before and after the step.
  - Then run recovery and assert:
    1. the original's bytes exist completely, at `X` or `Original/X`;
    2. after recovery `X` exists and is either the old or the new valid file;
    3. `Original/X` equals the original bytes;
    4. no temp files or journal remain;
    5. the sidecar is consistent.
  - Plus ≥ 1000 randomised fault schedules per CI run.
- Real file-system integration (Linux and Windows): first save, multiple re-saves, revert, collision cases from [ARCHITECTURE.md §9.4](ARCHITECTURE.md#94-collisions-and-special-cases) (existing unknown backup, case-only names, `Original` as a file).
- Pre-checks:
  - Read-only file, read-only folder, insufficient space (simulated via `IFileSystem` free-space and write failure), file changed externally, file deleted externally.
- Windows-specific: sharing violation during rename (a file held open by another process) leads to retry, then failure with no data loss.
- **An existing backup is never modified** (hash before and after every scenario).

### TS-09: Corrupt and unusual inputs (FR-030, FR-036)
- Zero-byte, truncated JPEG (several cut points), truncated PNG, random bytes with a `.jpg` name, PNG content named `.jpg` (and the reverse), CMYK JPEG, progressive JPEG, 16-bit and palette PNGs, PNG with alpha, a decompression bomb (huge declared dimensions), and extreme sizes (1×1, 30 000×100).
- Expected: typed error or correct decode, never a crash or hang (timeout 5 s per file); the gallery shows an error badge; the rest of the folder still works.

### TS-10: Filenames and paths (FR-001, FR-021, FR-023)
- Romanian diacritics (ș ț ă î â), Hungarian, emoji, spaces, very long names (~240 chars), long paths (> 260 chars on Windows with `longPathAware`), names with dots and no extension, leading/trailing spaces (Windows-invalid), NFC vs NFD forms, case-only differences (Linux).
- Explorer and gallery list them correctly; saving, backup and recovery work; collision naming works.

### TS-11: Large folders and performance (FR-052, FR-053, NFR-003, NFR-012)
- Synthetic folders of 100, 500 and 1000 photos (12 MP and 48 MP) measure:
  - time to first screen;
  - full thumbnail time cold and warm;
  - open time cold and preloaded;
  - slider fps;
  - save time.
- **CI** runs smoke versions with generous limits to catch large regressions. **Reference-PC** runs (manual, scripted) record the NFR-003 numbers in IMPLEMENTATION_LOG at 0.2.0, 0.3.0, 0.5.0 and 0.8.0.
- UI-thread stall detector: fails if any UI dispatcher job exceeds 100 ms during scenarios.
- Thumbnail cache respects the size cap and retention.

### TS-12: Memory (NFR-004, FR-037)
- Working set during a 500-photo browse plus 100 Save & Next at 24 MP stays < 2 GB, with no growth trend over 300 photos (leak test).
- 100 MP and 200 MP photos save successfully within the documented limits; > 210 MP is refused cleanly.

### TS-13: Face alignment and background (FR-063–FR-065)
- YuNet decoder unit tests against reference outputs (same boxes and landmarks as the Python/OpenCV reference within 1 px at 640 px).
- **Alignment accuracy** on a labelled set of ≥ 50 consented portraits (owner-provided, stored outside the repository) plus synthetic renders. Metrics: eye line, head height and centre error in mm at template scale. Target: ≥ 90 % within ±1.0 mm eye line and ±1.5 mm head height.
- No-face, two-faces, profile-face and low-light cases produce the correct message; no crash.
- **Background whitening rubric** (OQ-010), scored by the owner on ≥ 30 portraits:
  - hair edge quality, clothing preservation, halo or shadow artefacts, colour spill;
  - each scored 1–3;
  - ships as Experimental only if no portrait scores 1 on clothing and ≥ 80 % score 3 on hair.

### TS-14: ID template geometry (FR-061, FR-062, FR-069)
- Template JSON validation.
- Output pixel sizes, for example 35×45 mm at 300 ppi gives exactly 413×531 px.
- Guide positions in mm match the template.
- Framing render equals the expected affine transform (synthetic grid image, landmark positions within 0.5 px).

### TS-15: Sheet layout and exact dimensions (FR-066–FR-068, FR-070, FR-071)
- Reproduces the validated table in [ARCHITECTURE.md §11.3](ARCHITECTURE.md#113-sheet-layout-engine-and-validated-example-layouts-fr-066) exactly (counts, orientation, margins ±0.05 mm).
- Property tests: no overlap, all photos inside the safe area, spacing ≥ the configured value, centred.
- Pixel analysis of exported sheets:
  - every photo rectangle is exactly round(w·ppi/25.4) × round(h·ppi/25.4) px;
  - cut guides lie only in gaps or margins;
  - DPI metadata is correct;
  - canvas is 1200×1800 (nominal) or 1240×1844 (printer-exact).
- **Physical print test (manual MT-05):** print the calibration page and one sheet per size on the CY-02 and measure with a caliper. Each photo must be within ±0.5 mm and the overscan measured and recorded.

### TS-16: Localization (FR-009, NFR-008)
- Key parity between `Strings.resx` and `Strings.ro.resx`; no unused keys; no empty values.
- Analyzer test: no string literals assigned to UI text properties in XAML or view models.
- Runtime switch updates all visible text without restart (headless UI test walks every page in both languages and checks no resource key is shown raw).
- Romanian formatting: decimal comma, date formats, comma-below ș/ț preserved (no cedilla ş/ţ in resources, checked by test).
- Pseudo-localization (+40 % length) screenshots reviewed for clipping at 0.8.0.

### TS-17: Platform, packaging and offline (NFR-001, NFR-002, NFR-011)
- CI build and tests on `ubuntu-latest` and `windows-latest`.
- A per-milestone published Windows build is smoke-tested in a **clean Windows 11 VM**: app starts, opens a folder, saves a photo.
- An offline test runs a full workflow with the network disabled (Windows firewall rule or `unshare -n` on Linux) and asserts no socket is opened (event tracing).
- Paths for settings, logs and cache are correct per OS. The log contains no image data.

### TS-18: UI behaviour, headless (FR-001, FR-002, FR-008, FR-012, FR-016–FR-019, FR-039, FR-041, FR-042, FR-007, FR-061, FR-062, FR-067)
- Navigation (state diagram in [ARCHITECTURE.md §6.2](ARCHITECTURE.md#62-navigation-structure)).
- Workspace switching keeps sessions.
- Keyboard-only workflow possible.
- Focus order; minimum control sizes (NFR-007).
- Visual baselines of key screens per OS (compared with tolerance; reviewed when intentionally changed).

### TS-19: Manual acceptance scripts (studio)
| ID | Script | When |
|---|---|---|
| MT-01 | Process a real order of 100+ photos (copy of a customer order) with W-1; verify outputs and backups by spot checks | 0.4.0 pilot, 0.9.x, release |
| MT-02 | Kill the process (Task Manager) during saves 10 times at random; restart; verify recovery and no loss | 0.3.0, release |
| MT-03 | Pull the power or reset the VM during a save; verify recovery | 0.3.0, release |
| MT-04 | Re-edit and Revert on 10 photos; verify one-generation quality and restored originals | 0.4.0, release |
| MT-05 | ID calibration page and sheets printed and measured (TS-15) | 0.6.0, release |
| MT-06 | Language switch on every screen; native-speaker read-through | 0.8.0 |
| MT-07 | Display scaling 100 %, 125 %, 150 %, 200 % at 1366×768, 1920×1080, 2560×1440 and 3840×2160 | 0.8.0 |
| MT-08 | Orders in an antivirus-scanned folder and a OneDrive-synced folder; verify retries and warnings | 0.3.0, 0.8.0 |
| MT-09 | Auto Correct rating session on the 50-photo reference set | 0.5.0 |
| MT-10 | ID Auto Align and background rubric sessions | 0.7.0 |
| MT-11 | Print 10×15 and 15×20 test photos from exported files through the chosen print path (OQ-024); owner approves colour and framing | 0.4.0, 0.8.0 |

## 3. Test data and fixtures

| Kind | Source | Stored |
|---|---|---|
| Synthetic images (gradients, colour checker, grids, faces rendered from vector art, EXIF and ICC variants, corrupt files, unicode names, large folders) | generated by `PhotoX.TestKit` at test time | not committed (generated); generator code committed |
| Small binary fixtures that cannot be generated (camera-specific EXIF samples, CMYK JPEG, real ICC profiles) | public-domain or self-made; licence recorded in `tests/fixtures/README.md` | committed (< 5 MB total) |
| Real customer samples and consented portraits | provided by the owner during implementation | **never committed.** Kept outside the repository; tests read the path from `PHOTOX_SAMPLES` and are skipped when it is unset. |
| Reference outputs (golden images) | generated once from reviewed builds | committed only for synthetic inputs |

`.gitignore` blocks JPEG/PNG/HEIC files everywhere except whitelisted documentation and fixture folders, as a guard against committing customer photos.

## 4. Image-quality validation

- **Metrics:** ΔE00 (via Lab with D50 adaptation, in Magick), PSNR, SSIM where useful, plus exact pixel-dimension checks.
- **Thresholds:** see TS-04/05/06. Any change to thresholds is a documented decision.
- **Interpolation filter choice** (Lanczos vs Mitchell for straighten and ID resampling) is selected at 0.5.0 by comparing sharpness and ringing on a resolution chart and portraits. The result is recorded in DECISIONS.
- **Print validation:** owner-approved test prints (MT-05, MT-11) at 0.4.0, 0.6.0 and release.

## 5. Platform testing

| Environment | Purpose | Frequency |
|---|---|---|
| Linux Mint (development) | daily development, all automated tests | continuous |
| GitHub Actions `ubuntu-latest`, `windows-latest` | automated gate | every push / PR |
| Windows 11 VM (clean snapshot) | published-build smoke, installer, offline test | each milestone |
| Physical Windows PC | performance, antivirus, real display scaling | 0.3.0+ milestones |
| Studio PC (production) | pilot and acceptance, real prints | 0.4.0 pilot, 0.6.0, RC, release |

## 6. Continuous integration (planned for 0.1.0)

- Workflow `ci.yml`, matrix `[ubuntu-latest, windows-latest]`:
  1. restore (locked);
  2. build (warnings as errors);
  3. test with coverage;
  4. upload test results and, on failure, visual diff artifacts.
- `windows-latest` additionally runs `dotnet publish -r win-x64 --self-contained` and uploads the folder as an artifact (pilot builds come from these).
- Coverage targets: Core ≥ 90 % lines, Services save/recovery ≥ 95 % branches, Imaging ≥ 80 %. Coverage is not enforced for App.

## 7. Defect severity

| Severity | Definition | Release rule |
|---|---|---|
| Critical | Possible loss or corruption of an original or saved photo; crash that loses work | must be fixed |
| High | Wrong output (size, colour, crop, metadata); workflow blocked; data shown wrong | must be fixed |
| Medium | Workaround exists; noticeable but not harmful | owner decides |
| Low | Cosmetic | may ship |

## 8. Release acceptance

Version **1.0.0** requires all of:

1. Every **Must** requirement in [PLAN.md](PLAN.md) is implemented, and its suites pass on Linux and Windows CI.
2. TS-08 fault injection passes 100 %, including ≥ 10 000 randomised schedules in a release run. MT-02 and MT-03 are completed without loss.
3. NFR-003/NFR-004 targets are met on the studio PC (or the owner accepts documented deviations).
4. MT-01 is completed on **three real orders** (one ≥ 200 photos) with no Critical or High defects.
5. MT-05 and MT-11 test prints are approved by the owner.
6. Romanian review is complete (MT-06).
7. `THIRD-PARTY-NOTICES.md` is complete and the licence review signed off (OQ-013).
8. Docs are current: PLAN statuses, CHANGELOG, IMPLEMENTATION_LOG, and the user quick guide (EN/RO).
9. No open Critical or High defects.

Milestone releases (0.x) require their own completion criteria from [ROADMAP.md](ROADMAP.md) and green CI.
