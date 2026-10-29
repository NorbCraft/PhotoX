# PhotoX — Open Questions

Status: plan revision P-1, 2026-10-29. Requirements are in [PLAN.md](PLAN.md). Decisions, once taken, are moved to [DECISIONS.md](DECISIONS.md) and the question is marked **Decided → DEC-xxx** (questions are never deleted).

Each question lists why it matters, the options, the **recommended default** (what implementation will assume if the owner accepts it), the impact, and **when** a decision is needed. Priority: **H** = blocks or reshapes work; **M** = matters for quality or effort; **L** = can be settled late.

## Summary: decisions needed from the owner

| ID | Question | Priority | Decide before | Recommended default |
|---|---|---|---|---|
| OQ-024 | How will exported files be printed until PhotoX prints directly? | **H** | 0.6.0 | Citizen LinkServer hot folder or other 1:1 printing; export printer-exact canvas for ID sheets |
| OQ-001 | Exact physical media sizes and overscan of the CY-02 | **H** | 0.3.0 (presets), 0.6.0 (sheets) | 4×6 in / 6×8 in, overscan ≈ 1.8 mm/side; confirm with a calibration print |
| OQ-006 | What does saving an *unchanged* photo do? | **H** | 0.3.0 | Write nothing, mark “Reviewed” |
| OQ-007 | Backup collision and retention policy | **H** | 0.3.0 | Never overwrite; unknown existing backup is treated as the backup; no automatic deletion |
| OQ-009 | May PhotoX keep a hidden sidecar file inside `Original/`? | **H** | 0.3.0 | Yes: `Original/.photox-order.json` |
| OQ-029 | Is “fit whole photo with white borders” (no crop) needed? | **H** | 0.3.0 | Yes, as a second crop mode in 0.4.0 |
| OQ-016 | Studio PC specs, performance targets, max image size | **H** | 0.2.0 | Targets in NFR-003; ≤ 64 MP full support, ≤ 210 MP slower, larger refused |
| OQ-014 | Is Enter = Save & Next acceptable? | M | 0.3.0 | Yes, with guards (FR-018) |
| OQ-005 | JPEG quality / chroma / DPI metadata policy | M | 0.3.0 | q95, 4:4:4, DPI = effective ppi |
| OQ-012 | Output colour space (sRGB vs keep source profile) | M | 0.3.0 | Convert to sRGB, embed sRGB profile |
| OQ-020 | Keep GPS and personal metadata in saved photos? | M | 0.3.0 | Keep all (unchanged behaviour); option to strip GPS |
| OQ-003 | Mixed print sizes in one order / on one sheet | M | 0.4.0 | Per-photo format choice, shown in gallery; no mixed sizes per ID sheet in 1.0 |
| OQ-019 | May the operator edit directly on a USB stick? | M | 0.2.0 | Allowed with warning |
| OQ-027 | Who uses / owns PhotoX (one studio vs. product)? | M | 0.8.0 (affects licensing early) | Single-studio commercial use; proprietary |
| OQ-013 | Dependency licence review (LGPL in Magick native, model weights) | M | 1.0.0 (start at 0.6.0) | Short legal check; ship notices |
| OQ-015 | Installer and update mechanism | M | 0.8.0 | Velopack, manual updates (no auto-check) |
| OQ-028 | Windows code-signing certificate | M | 0.8.0 | Buy one if distributed beyond one PC; otherwise accept SmartScreen prompt |
| OQ-010 | Quality bar for automatic background whitening | M | 0.7.0 | Ship as *Experimental* only if TS-13 rubric passes |
| OQ-011 | Country-specific ID templates (Romania, visas…) | M | 0.6.0 | Geometric presets only; owner supplies rules if wanted |
| OQ-008 | Batch Auto Correct / batch apply: when? | M | 0.5.0 | After 1.0 (1.1.0) |
| OQ-002 | Future printer integration approach | M | before printing milestone | Spike: Win32 driver printing vs LinkServer hot folder |
| OQ-004 | HEIC/HEIF support | L | post-1.0 | Not in 1.0; operator converts on the phone or on transfer |
| OQ-018 | Thumbnail cache retention (privacy) | L | 0.2.0 | 1 GB, 30 days, clearable |
| OQ-021 | Extra adjustments (tint, sharpening, red-eye)? | L | 0.5.0 | Not in 1.0 |
| OQ-022 | Who reviews the Romanian translation? | L | 0.8.0 | Owner or a native-speaking studio employee |
| OQ-023 | Monitor colour calibration / display profile | L | post-1.0 | sRGB preview; recommend an sRGB-calibrated monitor |
| OQ-017 | Touchscreen requirements | L | 0.8.0 | Not required; avoid hover-only UI |
| OQ-025 | Where do ID sheets and single ID photos get saved? | L | 0.6.0 | Next to the source portrait, new filename |
| OQ-026 | GitHub gooaccount/organisation for the repository | **H (admin)** | now | Owner's private repository `PhotoX` |

---

## Printing and print formats

### OQ-001: Exact physical media sizes and overscan of the Citizen CY-02 (H)
- **Why it matters:** crop presets, effective-ppi calculations, ID sheet layout (how many photos fit) and exact millimetre sizes all depend on it.
- **Research so far (2026-10-29):** Citizen lists 4×6 in (≈101×152 mm) and 6×8 in (≈152×203 mm) media, 300 dpi (600×300 dpi high-quality mode). The 4×6 media can also be cut into 2×(2×6 in). The printer appears to share its engine with the DNP DS-RX1. Gutenprint's tables give a 4×6 printable canvas of **1240×1844 px at 300 dpi** (≈105.0×156.1 mm), which implies about **1.7–1.9 mm overscan per side** on borderless prints. *Not confirmed for the Windows driver.*
- **Options:** (a) use nominal 101.6×152.4 / 152.4×203.2 mm, with overscan as configured; (b) measure with a calibration print (FR-071) and store the measured values per media.
- **Recommended default:** (a) now, (b) as soon as 0.6.0 provides the calibration page. All values live in `print-formats.json`, never in code.
- **Impact:** low for ordinary photos (only the ratio matters), high for ID sheets.
- **Owner input needed:** which media are actually used (4×6 only? 6×8? 2×6 cut?), and access to the printer for one calibration print.

### OQ-024: How are exported files printed until PhotoX prints directly? (H)
- **Why it matters:** a 1200×1800 px page printed with “fit/fill to page” on a borderless printer is enlarged by about 3 %, so a 35 mm ID photo prints at about 36 mm. Exactness depends on the printing software.
- **Options:** (a) Citizen **LinkServer for Photo** hot folder: files printed at native size; (b) Windows Photos / other viewers: usually fit or fill, so not exact; (c) the old Mitsubishi kiosk software; (d) wait for PhotoX printing.
- **Recommended default:** (a), with ID sheets exported on the **printer-exact canvas** (FR-070, 1240×1844 px) when a printer media definition exists, and the nominal canvas otherwise.
- **Impact:** decides the default sheet canvas and what TS-15/TS-19 print tests use.
- **Decide before:** 0.6.0.

### OQ-002: Future printer integration approach (M)
- **Options:** (a) Win32 GDI/XPS printing through the Citizen driver (full control: paper, copies, spooler status); (b) LinkServer hot folder (simple, robust, little status feedback); (c) both.
- **Recommended default:** a 2–3 day spike before the printing milestone, comparing (a) and (b) on the real printer. The boundary in [ARCHITECTURE.md §13](ARCHITECTURE.md#13-future-printing-boundary-fr-050) supports either.
- **Decide before:** the post-1.0 printing milestone. No impact on 1.0.

### OQ-003: Mixed print sizes (M)
- **Why it matters:** an order may contain photos for 10×15 and others for 15×20. Today the crop preset is per photo, but nothing records “this photo is for 15×20” for the person printing.
- **Options:** (a) the preset chosen per photo is stored in the sidecar and shown as a badge in the gallery (e.g. “15×20”); (b) separate sub-folders per size; (c) filename suffix.
- **Recommended default:** (a) in 0.4.0. The future print module can then group by size. No mixed photo sizes on one ID sheet in 1.0.
- **Decide before:** 0.4.0.

### OQ-012: Output colour space and printer colour profiles (M)
- **Why it matters:** iPhones and many cameras embed Display P3 or Adobe RGB. Software that ignores ICC profiles (common in print drivers and kiosks) shows these desaturated.
- **Options:** (a) convert everything to sRGB and embed sRGB; (b) keep the source profile; (c) convert to a printer ICC profile.
- **Recommended default:** (a). It is safe with any print path. (c) becomes an option with printing, if Citizen or the owner provides profiles (none are public).
- **Impact:** small, but it must be fixed before 0.3.0 because it defines “correct output” for the quality tests.

### OQ-005: Export resolution, DPI and JPEG quality policy (M)
- **Options and defaults:**
  - Ordinary photos are **never resampled** (owner requirement).
  - JPEG **quality 95, 4:4:4 chroma** (visually lossless, about 30 % larger than 4:2:0). The alternative is quality 92 at 4:2:0 for smaller files.
  - **DPI metadata = effective ppi** for the chosen format, so other software prints at the intended size. Alternatives: keep the source DPI, or a fixed 300.
  - ID sheets render at **300 ppi**, the printer's native resolution. 600 ppi is possible but the printer's 600×300 mode is non-square, so it brings no clear benefit.
- **Decide before:** 0.3.0 (defaults can change later in Settings).

---

## Saving and files

### OQ-006: Policy for saving unchanged photos (H)
- **Why it matters:** the owner's strategy moves the original into `Original/` on first save. For an untouched photo that creates a needless backup and a lossy re-encode.
- **Options:** (a) write nothing, mark the photo *Reviewed*; (b) save anyway (re-encode, back up); (c) copy bytes unchanged and back up.
- **Recommended default:** (a). Note that the **default 10×15 crop counts as a change** whenever it removes pixels, which is almost always (a 4:3 photo loses 11 %). So “unchanged” really means the photo already had the exact ratio and nothing was touched.
- **Decide before:** 0.3.0.

### OQ-007: Backup collisions and retention (H)
- **Collision default:** never overwrite. If `Original/X` already exists and PhotoX has no record of it, assume it is X's original from an earlier session and keep it. Unrelated or case-colliding names get `X (2).ext` and a sidecar mapping ([ARCHITECTURE.md §9.4](ARCHITECTURE.md#94-collisions-and-special-cases)).
- **Retention:** when may `Original/` folders be deleted? Options: never automatically (owner deletes the order folder); or offer “Clean up originals” per order after N days.
- **Recommended default:** never delete automatically in 1.0.
- **Decide before:** 0.3.0.

### OQ-009: Hidden sidecar file for re-editing (H)
- **Why it matters:** without stored edit settings, re-editing a saved photo must start from the already-compressed edited file (generation loss), and progress states like “Reviewed” cannot survive a restart.
- **Options:** (a) hidden `Original/.photox-order.json` (travels with the order, invisible in the gallery); (b) a database in the app-data folder keyed by folder path (lost if the folder is moved, keeps customer folders clean); (c) no persistence.
- **Recommended default:** (a). Contents: file names, edit parameters and timestamps, no image data.
- **Decide before:** 0.3.0.

### OQ-019: Editing directly on removable media (M)
- **Why it matters:** pulling a USB stick mid-save can corrupt files. The atomic design limits the damage, but the medium itself may still be corrupted.
- **Options:** (a) allow with a warning; (b) offer “Copy to local order folder” (a mini import); (c) block.
- **Recommended default:** (a) in 0.2.0. (b) could become a nice post-1.0 feature (import from card or phone into a dated order folder).

### OQ-029: Fit whole photo with borders instead of cropping (H)
- **Why it matters:** the prompt lists “Fit crop to photograph”. In studio practice there are two meanings: (1) make the crop frame as large as possible (implemented as *Fit*), and (2) **print the whole photo without cropping, with white borders** to reach 10×15. Customers often ask for (2), for example with panoramas or group photos.
- **Options:** (a) only (1); (b) add (2) as a crop mode (“Whole photo + border”), with border colour white and centring.
- **Recommended default:** (b) in 0.4.0. It needs canvas extension in the renderer: small, but it must be planned.
- **Decide before:** 0.3.0, because it affects the recipe model.

### OQ-020: Metadata privacy (M)
- **Options:** keep all metadata (EXIF/IPTC/XMP incl. GPS) — the default and least surprising; or strip GPS and device serials from edited copies.
- **Recommended default:** keep all metadata; make “Remove location data” a Settings option (off by default).

### OQ-018: Thumbnail cache retention (L)
- Customer images persist as thumbnails in the local cache.
- **Recommended default:** 1 GB cap, purge entries older than 30 days, and *Clear cache* in Settings.

### OQ-025: Location of ID outputs (L)
- **Recommended default:** next to the source portrait as a new file (`<name>_ID_35x45_6.jpg`). Alternative: a fixed “ID photos” folder with dated subfolders.

---

## Image processing

### OQ-004: HEIC/HEIF (L)
- Magick.NET can **decode** HEIC (libheif + libde265 bundled) but **cannot encode** it. HEVC patent-pool implications are unclear.
- **Options:**
  - (a) Not supported in 1.0. The operator transfers as JPEG; iPhone “Most Compatible” or automatic conversion on transfer.
  - (b) Read HEIC and save the edited result as JPEG with a `.jpg` name, keeping the original in `Original/`.
  - (c) Windows HEIF extension via WIC (Windows-only).
- **Recommended default:** (a) for 1.0. Reconsider (b) after the licence review.

### OQ-008: Batch Auto Correct and batch apply (M)
- **Recommended default:** post-1.0 (1.1.0). The recipe architecture already supports it ([ARCHITECTURE.md §7.6](ARCHITECTURE.md#76-auto-correct)).
- **Proposed behaviour:** each selected photo gets its own computed recipe (or the copied tonal settings), marked *Unsaved*, and the operator confirms each with Save & Next. Nothing is ever written unseen.
- **Decide before:** 0.5.0, in case the owner wants it in 1.0.

### OQ-021: Additional adjustments (L)
- A tint (green–magenta) slider, sharpening, red-eye removal and noise reduction are *not* in the owner's list.
- **Recommended default:** not in 1.0. Tint is the cheapest to add if colour casts from fluorescent light prove common.

### OQ-016: Performance targets, studio PC and image sizes (H)
- **Needed from owner:** CPU, RAM, disk (SSD?), display resolution and scaling of the studio PC; typical and largest photo size; typical and largest order size.
- **Recommended defaults:** NFR-003/NFR-004 targets on a 4-core / 16 GB / SSD / 1080p reference. Images up to 64 MP fully supported, 64–210 MP slower, larger refused.
- **Decide before:** 0.2.0 (sets the performance test baseline).

### OQ-023: Display colour management (L)
- Avalonia does no colour management, so previews assume an sRGB monitor.
- **Recommended default:** sRGB previews in 1.0; recommend an sRGB-calibrated monitor at the studio. Monitor-profile conversion is a post-1.0 option.

---

## ID photos

### OQ-010: Automatic background whitening quality bar (M)
- **Recommended default:** ship only as *Experimental*, off by default, and only if the TS-13 rubric passes on at least 30 consented test portraits (hair edges, light clothing, glasses, children). Otherwise ship only the manual background-brightness slider.

### OQ-011: Country-specific templates (M)
- Research suggests Romanian electronic passports and ID cards capture the photo at the counter (unconfirmed), so studio ID photos are mostly for visas, foreign documents, permits and other cards.
- **Recommended default:** geometric presets with ISO-style guide defaults and a clear “no compliance claim” label. The owner names the documents customers most often need, and templates are added as data.

---

## UX

### OQ-014: Enter = Save & Next (M)
- **Recommended default:** yes, with the FR-018 guards (no auto-repeat, 300 ms dead time after opening a photo, inactive in text fields) plus Revert. Alternative: Ctrl+Enter.

### OQ-017: Touchscreen (L)
- **Recommended default:** not required in 1.0. Avoid hover-only UI and keep 32 px+ hit targets, so touch works reasonably later.

### OQ-022: Romanian translation review (L)
- Planner-written Romanian strings (see mockups) need native review before 1.0. Who reviews them?

---

## Project, licensing and distribution

### OQ-026: GitHub repository (H, administrative)
- The local repository is initialised. The **GitHub repository was not created**: the GitHub CLI is not installed, no token is available, and github.com is not in SSH `known_hosts` on the development machine.
- **Remaining action for the owner:** see [README.md](../README.md#repository-hosting). Confirm which account or organisation should own the private `PhotoX` repository.

### OQ-027: Distribution model (M)
- Is PhotoX an in-house tool for one studio, or will it be sold or given to other studios? This affects licence obligations (distribution triggers LGPL/notice duties), code signing, installer, update channel and support.
- **Recommended default:** single-studio commercial use, proprietary, no public distribution.

### OQ-013: Commercial licensing of dependencies (M)
- All proposed dependencies are permissive (MIT/Apache/BSD). Items for a legal check:
  - The LGPL components statically linked inside Magick.NET's native library.
  - The ImageMagick licence notice.
  - Model weights (YuNet MIT, MODNet Apache-2.0).
  - HEVC patents, if HEIC is ever enabled.
- Rejected for licence reasons: FluentAssertions 8+, ImageSharp, Emgu CV, InsightFace models, dlib 68-point, BRIA RMBG, Avalonia paid add-ons (not needed).

### OQ-015: Installer and updates (M)
- **Options:**
  - **Velopack** (MIT): installer plus delta updates from a local or network share or GitHub Releases.
  - **Inno Setup**: classic installer, manual updates.
  - **MSIX**: needs signing; Store-style.
- **Recommended default:** Velopack with updates **only on explicit operator action** (no background network checks, NFR-001).

### OQ-028: Code signing (M)
- Unsigned installers trigger Windows SmartScreen warnings.
- **Recommended default:** acceptable for a single PC; buy an OV/EV certificate if PhotoX is distributed further.
