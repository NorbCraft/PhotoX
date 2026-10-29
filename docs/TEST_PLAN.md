# PhotoX — Test Plan

Plan revision **P-2** (2026-10-29). The aim is proportionate verification: test important behaviour, inspect actual outputs, fix failures and continue (prompt_02 §17). Requirement IDs refer to [PLAN.md](PLAN.md).

## 1. Approach

| Level | Tooling | Where |
|---|---|---|
| Unit tests (geometry, layouts, names, recipe, Auto Correct estimation) | xUnit v3 3.2.2 | `tests/PhotoX.Tests` |
| Integration tests with real Magick.NET and real temp folders (load, render, save, Finish Order, cache) | xUnit v3 | `tests/PhotoX.Tests` |
| UI behaviour (view models, keyboard handling, localization, screen rendering) | Avalonia.Headless.XUnit | `tests/PhotoX.App.Tests` |
| Output inspection | rendered PNG/JPEG files and headless screenshots, checked by eye during development | local only |
| Manual and physical checks | Windows 11 studio PC, Citizen prints, Romanian review | owner; recorded in IMPLEMENTATION_LOG when done |

**Rules:**
- Fixtures are **generated** at test time (gradients, patches, EXIF orientations, ICC variants, corrupt files). Committed binary fixtures stay tiny and synthetic.
- Real photos, including the owner's sample `id_photo/IMG_6153.JPG`, are **never committed**. Write tests on the sample use a copy in a temporary workspace, and the original's checksum is verified unchanged.
- No rigid coverage percentages, no universal PSNR/ΔE threshold for all images, and no large screenshot-baseline suites.
- Preview and export equivalence is tested **at matching resolution**. Geometry is tested separately. Preview fidelity is checked with tolerances on representative images only.
- Never claim Windows runs, CI results, physical prints or owner acceptance that did not happen.

## 2. Suites

| Suite | Covers | Key checks |
|---|---|---|
| TS-01 Crop & composition | FR-003, FR-010–FR-012, FR-047, FR-049, FR-073 | largest centred default crop for landscape/portrait/square/extreme ratios; ratio lock under handle drags; crop stays inside the image and the straighten-valid area; **saved output equals the selected crop** (pixel size and position); presets and orientations; 13×18 canvas size and centring; whole-photo borders without resampling; effective ppi and warnings |
| TS-02 Orientation | FR-015, FR-032 | EXIF orientations 1–8 load upright; output Orientation = 1; quarter turns exact; straighten output has no empty corners |
| TS-03 Histogram & levels | FR-043, FR-044 | bin counts on synthetic images; histogram limited to the crop; levels endpoints and midtone |
| TS-04 Adjustments | FR-013, FR-014, FR-016, FR-045, FR-064 | neutral settings leave pixels unchanged (within encode tolerance); each slider moves output in the expected direction; tint green↔magenta; undo/redo restores recipes; Auto Correct respects clamps and leaves well-exposed images nearly unchanged; same recipe at the same resolution gives identical preview and save pixels |
| TS-05 Colour | FR-034 | Display P3 / Adobe RGB / CMYK sources converted to sRGB (patch values within a stated tolerance); output carries an sRGB profile |
| TS-06 Output & metadata | FR-030, FR-033, FR-035 | JPEG q95 4:4:4 read back; no resize of the crop; PNG 16-bit and alpha preserved; EXIF kept, orientation and dimensions updated, embedded preview removed, density = effective ppi; re-edit renders from the original (not from the edited file) |
| TS-07 Save & Next workflow | FR-003, FR-004, FR-017–FR-019, FR-024, FR-027 | Enter saves **exactly** the current crop and advances only after success; failure keeps the photo and edits; no wrap at the end; skip never saves; manual edits kept as Unsaved; held-key repeat ignored; shortcuts inactive in text inputs and dialogs; no-op photo marked Reviewed without writing |
| TS-08 Saving & backups | FR-005, FR-021–FR-023, FR-026 | first save copies the original to `Original/` and writes the edit; later saves keep the original; collision with an unknown file gives a unique name and records the mapping; read-only file or folder, file changed externally, missing file and simulated write failure leave the existing photo untouched; stray temp files are cleaned |
| TS-09 Finish Order & bad input | FR-031, FR-036, FR-072 | erase deletes only known backups, sidecar, temp files and order cache; keeps finished photos, ID exports and unknown files; removes `Original/` only when empty; keep mode deletes nothing; corrupt, zero-byte and truncated files, HEIC and decompression bombs give explanations, never crashes |
| TS-10 Names & paths | FR-001, FR-002, FR-021 | diacritics, spaces, long names, case-only differences; natural sort; ID outputs excluded from the gallery |
| TS-11 Large folders & responsiveness | FR-052, FR-053, NFR-003 | 500-photo synthetic folder: time to first thumbnails, cache hits, preload; **measured** numbers recorded |
| TS-12 Memory & size limits | FR-037, NFR-004 | > 210 MP refused from the header only; memory check; working set during a long Save & Next run (measured) |
| TS-13 Face detection | FR-063, FR-065 | YuNet decode on a synthetic face and the owner's sample (copy); several faces lead to operator choice; no face gives a message; whitening evaluated on the sample only and the result recorded |
| TS-14 ID framing | FR-007, FR-061, FR-062, FR-064 | sizes in mm to px at 300 ppi; framing transform maps a known grid correctly; the portrait source is never modified |
| TS-15 ID sheets & export | FR-066–FR-068, FR-071, FR-074, FR-075 | capacities per size; each photo exactly round(mm·300/25.4) px; **3×4: 7 mm white strip under each photo** and cut guides not crossing the strip; copy counts; 1200×1800 px canvas; timestamp name with `_1`, `_2` suffixes; never overwrite; calibration sheet geometry |
| TS-16 Localization | FR-009, NFR-008 | EN/RO key parity; no raw keys on screen after switching; RO number formatting; comma-below ș/ț only |
| TS-17 Platform | NFR-002, NFR-011, FR-051 | Linux test run each commit; Windows via CI once the remote exists; settings/log/cache paths per OS |
| TS-18 UI | FR-001, FR-002, FR-008, FR-012, FR-039, FR-042, FR-054, FR-067 | headless rendering of key screens at 1366×768 and 1920×1080 (layout fits, nothing clipped); filters show the right photos; Back to folder; removable warning |

## 3. Manual and physical checks (owner, pending)

| Check | When |
|---|---|
| Windows 11 studio PC: install or unzip build, process a copied order, Finish Order | after 0.2.0 / 0.4.0 builds |
| Citizen prints of the calibration sheet: ID sizes, 7 mm strip, centred 13×18 on 15×20, any enlargement/trim | after 0.3.0 |
| Romanian wording review | any time |
| Display check on the studio monitor (sRGB assumption) | first Windows trial |

Results are recorded in [IMPLEMENTATION_LOG.md](IMPLEMENTATION_LOG.md) when they actually happen.
