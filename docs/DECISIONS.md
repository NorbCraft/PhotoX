# PhotoX — Decision Log

Architectural and product decisions, newest last. Requirements are in [PLAN.md](PLAN.md), and open items in [OPEN_QUESTIONS.md](OPEN_QUESTIONS.md).

**Status values:**
- **Accepted (owner):** prescribed in the owner's prompt and validated during planning.
- **Proposed:** the planner's recommendation, awaiting owner approval of the plan.
- **Approved:** accepted by the owner after review.
- **Superseded by DEC-xxx.**
- **Blocked.**

Decisions are never deleted. A change adds a new entry that supersedes the old one.

| ID | Date | Decision | Rationale | Status | Related |
|---|---|---|---|---|---|
| DEC-001 | 2026-10-29 | **C# on .NET 10 (LTS)** for all code. | Owner preference. LTS until 2028-11-14; .NET 11 is only STS. | Accepted (owner) | NFR-002, NFR-009 |
| DEC-002 | 2026-10-29 | **Avalonia UI 12.x** (12.1 current), not 11.x. Avoid TreeDataGrid (commercial) and DataGrid (deprecated). | Owner chose Avalonia. 12.x is the current stable line, defaults to .NET 10, and avoids a migration from 11 later. | Accepted (owner) for Avalonia; Proposed for 12.x | FR-008, NFR-002 |
| DEC-003 | 2026-10-29 | **MVVM** with view-model-first navigation and a ViewLocator. Image, file and layout logic outside the UI project. | Owner requirement. Testability of core logic without UI. | Accepted (owner) | NFR-009 |
| DEC-004 | 2026-10-29 | **Magick.NET 15.x, `Q16-x64` package** for decoding, encoding, ICC conversion, metadata and geometric resampling. | Owner preference. Q16 gives headroom for profile conversion and 16-bit PNG. The x64 package covers win-x64 and linux-x64. Q8 is the fallback if memory is tight. | Accepted (owner) for Magick.NET; Proposed for Q16 | FR-030–FR-035 |
| DEC-005 | 2026-10-29 | **CommunityToolkit.Mvvm** as the MVVM library. | Avalonia template default, MIT, source generators, low ceremony for agent-written code. ReactiveUI was not chosen. | Proposed | NFR-009 |
| DEC-006 | 2026-10-29 | **Own managed tone/colour pipeline** (LUT plus saturation), shared by preview and export. Magick.NET does not perform the adjustments. | Guarantees preview = export, is deterministic and unit-testable, and runs at interactive speed on proxies. | Proposed | FR-013, FR-014, FR-044, NFR-006 |
| DEC-007 | 2026-10-29 | **Edits are stored as parameters (`EditRecipe`)**, persisted in a hidden sidecar in `Original/`. Re-edits start from the pristine original. | No cumulative JPEG loss, cheap undo, crash recovery, and future batch operations. | Proposed (OQ-009) | FR-016, FR-026, FR-027, FR-046 |
| DEC-008 | 2026-10-29 | **Journalled safe-save transaction:** temp file, then verify, then *move* the original to `Original/`, then atomic rename. Automatic recovery on folder open. Backups are never overwritten. | Implements the owner's backup strategy with crash safety. Move keeps original timestamps and needs no copy. | Proposed | FR-005, FR-021–FR-023, NFR-005 |
| DEC-009 | 2026-10-29 | **Background save queue** (single worker). Save & Next shows the next photo immediately. | Makes Save & Next feel instant while keeping saves ordered and safe. | Proposed | FR-004, NFR-003 |
| DEC-010 | 2026-10-29 | **sRGB** working space for preview and processing. sRGB output with embedded profile. Sources with other profiles are converted on load. | Predictable printing through drivers or kiosks that ignore ICC. Avalonia has no colour management. | Proposed (OQ-012) | FR-034 |
| DEC-011 | 2026-10-29 | **ONNX Runtime** for ML features: **YuNet** face detection (MIT) and **MODNet** matting (Apache-2.0, experimental). Rejected: InsightFace, dlib 68-point, BRIA, FaceONNX bundled models, Emgu CV. | Permissive licences, small models, a single inference engine on Windows and Linux. | Proposed | FR-063, FR-065, NFR-010 |
| DEC-012 | 2026-10-29 | **Serilog** file logging behind `Microsoft.Extensions.Logging` abstractions. | MEL has no file provider; Serilog is mature and Apache-2.0. | Proposed | FR-051 |
| DEC-013 | 2026-10-29 | **xUnit v3 pinned to 3.2.2**, Avalonia.Headless.XUnit for UI tests, Shouldly or plain asserts, Magick-based image comparison. No FluentAssertions 8+, no ImageSharp. | Avalonia.Headless.XUnit 12.1 breaks with xUnit 4.x (issue #22072). Licence constraints. | Proposed | TEST_PLAN |
| DEC-014 | 2026-10-29 | **Documentation structure:** `docs/PLAN.md` is the source of truth; supporting docs reference requirement IDs; prompts are archived in `docs/prompts/`. | Owner requirement. | Accepted (owner) | — |
| DEC-015 | 2026-10-29 | **Versioning:** SemVer-style 0.x. The first implementation version is 0.1.0, milestones bump the minor version, fixes bump the patch, and 1.0.0 needs the release acceptance criteria. In-progress milestones carry a `-dev` suffix. | Owner policy. The `-dev` suffix is a proposal to avoid tagging unfinished milestones. | Accepted (owner) + Proposed (`-dev`) | [DEVELOPMENT.md](DEVELOPMENT.md#3-versioning) |
| DEC-016 | 2026-10-29 | **Milestone map** 0.1.0 Foundation → 0.2.0 Browsing → 0.3.0 Crop & safe save → 0.4.0 Basic adjustments (pilot) → 0.5.0 Advanced editing → 0.6.0 ID manual → 0.7.0 ID automation → 0.8.0 Stabilisation → 0.9.x RC → 1.0.0. | The owner's phases, with C split so the riskiest part (saving) lands early, and E split into manual and ML parts. | Proposed | [ROADMAP.md](ROADMAP.md) |
| DEC-017 | 2026-10-29 | **Neutral dark-grey UI theme** by default, one blue accent, status colours only for state. | Neutral surroundings avoid biasing colour judgement of photos (standard in photo tools). | Proposed | FR-039 |
| DEC-018 | 2026-10-29 | **Navigation:** persistent top bar (workspace switch, order context, EN/RO, help, settings). Photos workspace has Browse ⇄ Edit; ID workspace has Framing → Sheet. No menus, no floating windows. | Owner UX principles. See mockups. | Proposed | FR-008, FR-009, NFR-007 |
| DEC-019 | 2026-10-29 | **Printing deferred.** Only the `IPrintService` / `MediaDefinition` boundary is defined in 1.0. | Owner instruction. | Accepted (owner) | FR-050 |
| DEC-020 | 2026-10-29 | **Repository:** local Git repository `PhotoX`, branch `main`. The GitHub remote was **not** created: no `gh` CLI, no token, and github.com is not in SSH `known_hosts`. | The owner's prompt asks not to claim a remote that does not exist. | Blocked (OQ-026) | — |
| DEC-021 | 2026-10-29 | **Mockups** are generated as SVG by a small documentation script (`docs/mockups/tools/build_mockups.py`) and rendered to PNG with headless Chromium. English and Romanian variants. | Editable, consistent and quick to revise after review. Not application code. | Proposed | [mockups/README.md](mockups/README.md) |
| DEC-022 | 2026-10-29 | **Folder publish** (self-contained, win-x64) rather than single-file publish. | Native libraries (Magick, Skia, ONNX) would otherwise be extracted to `%TEMP%` on every start. | Proposed (OQ-015) | NFR-003 |
| DEC-023 | 2026-10-29 | **The layout engine works from the media safe area** and computes arrangements. No hard-coded counts per photo size. | Planning showed the example counts (8×3×4, 6×5×5) are wrong or unsafe for borderless media. | Proposed | FR-066 |
