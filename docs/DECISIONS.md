# PhotoX — Decision Log

Newest last. Decisions are never deleted. When one changes, a new entry supersedes it and the old entry's status says so.

**Status values:**
- **Accepted (owner):** the owner prescribed it.
- **Accepted:** the planner proposed it and the owner approved the plan.
- **Superseded by DEC-xxx.**
- **Pending (owner).**

| ID | Date | Decision | Rationale | Status | Related |
|---|---|---|---|---|---|
| DEC-001 | 2026-10-29 | C# on .NET 10 (LTS). | Owner preference; LTS to 2028-11. | Accepted (owner) | NFR-002 |
| DEC-002 | 2026-10-29 | Avalonia UI 12.x; TreeDataGrid and DataGrid not used. | Owner chose Avalonia; 12.x is the current stable line. | Accepted (confirmed by DEC-035) | FR-008 |
| DEC-003 | 2026-10-29 | MVVM; image, file and geometry logic outside the UI project. | Owner requirement; testability. | Accepted (owner) | NFR-009 |
| DEC-004 | 2026-10-29 | Magick.NET Q16-x64 for decode, encode, colour, metadata and geometry. | Owner preference. | Accepted (version refined by DEC-032) | FR-030–FR-035 |
| DEC-005 | 2026-10-29 | CommunityToolkit.Mvvm. | Avalonia template default, MIT, low ceremony. | Accepted | NFR-009 |
| DEC-006 | 2026-10-29 | Own managed LUT/SIMD tone pipeline shared by preview and export. | — | **Superseded by DEC-026** | — |
| DEC-007 | 2026-10-29 | Edits stored as parameters (`EditRecipe`); sidecar in `Original/`; re-edit from the preserved original. | No cumulative JPEG loss; cheap undo. | Accepted (lifecycle refined by DEC-028) | FR-026 |
| DEC-008 | 2026-10-29 | Journalled safe-save transaction with automatic recovery. | — | **Superseded by DEC-025** | — |
| DEC-009 | 2026-10-29 | Background save queue; advance before the save finishes. | — | **Superseded by DEC-027** | — |
| DEC-010 | 2026-10-29 | sRGB working and output space; convert other profiles on load. | Predictable printing; Avalonia has no colour management. | Accepted (owner, OQ-012) | FR-034 |
| DEC-011 | 2026-10-29 | ONNX Runtime + YuNet for face detection; MODNet only as an experiment. Rejected models with non-commercial licences. | Permissive licences, small models. | Accepted (confirmed by DEC-038) | FR-063, FR-065 |
| DEC-012 | 2026-10-29 | Serilog file logging behind Microsoft.Extensions.Logging abstractions. | MEL has no file provider. | Superseded by DEC-040 | FR-051 |
| DEC-013 | 2026-10-29 | xUnit v3 pinned to 3.2.2; Avalonia.Headless.XUnit; no FluentAssertions 8+. | xUnit 4 breaks Avalonia.Headless; licences. | Accepted | TEST_PLAN |
| DEC-014 | 2026-10-29 | `docs/PLAN.md` is the source of truth; prompts archived in `docs/prompts/`. | Owner requirement. | Accepted (owner) | — |
| DEC-015 | 2026-10-29 | SemVer-style 0.x versioning starting at 0.1.0, with a `-dev` suffix during milestones. | — | **Superseded by DEC-034** (no `-dev` suffix) | — |
| DEC-016 | 2026-10-29 | P-1 milestone map (0.1.0 … 0.8.0). | — | **Superseded by DEC-033** | — |
| DEC-017 | 2026-10-29 | Neutral dark-grey theme, one accent colour. | Neutral surroundings for photo judgement. | Accepted (owner, prompt_02 §14) | FR-039 |
| DEC-018 | 2026-10-29 | Top bar with workspace and language switch; Browse ⇄ Edit; ID Framing → Sheet; no menus or floating windows. | Owner UX principles. | Accepted | FR-008, FR-009 |
| DEC-019 | 2026-10-29 | Printing deferred. | Owner instruction. | Accepted (owner; refined by DEC-031) | FR-050 |
| DEC-020 | 2026-10-29 | The GitHub remote is not created by the agent. Local repository on `main`. | No credentials; the owner will create the remote. | Pending (owner) | OQ-026 |
| DEC-021 | 2026-10-29 | Mockups generated as SVG by `docs/mockups/tools/build_mockups.py` and rendered to PNG. | Editable and consistent. | Accepted | mockups |
| DEC-022 | 2026-10-29 | Self-contained folder publish rather than single-file. | Native libraries load without extraction. | Accepted (with Velopack, DEC-037) | — |
| DEC-023 | 2026-10-29 | Safe-area-based capacity layout engine for ID sheets. | — | **Superseded by DEC-030** | — |
| DEC-024 | 2026-10-29 | **Plan revision P-2** approved; implementation proceeds autonomously through the roadmap. | prompt_02 §§1, 20. | Accepted (owner) | ROADMAP |
| DEC-025 | 2026-10-29 | **Simplified saving:** temp file in the same folder, read-back check, *copy* the original to `Original/` on first save, atomic replace, then update the sidecar. No journal, recovery service or randomised crash campaign. | Proportionate to studio use on copies with independent backups (prompt_02 §§2, 8). Copy-then-replace means the photo path always holds a complete file. | Accepted (owner) | FR-005, FR-021–FR-023 |
| DEC-026 | 2026-10-29 | **Magick.NET performs all adjustments** (linear-light gains, `Level`, `SigmoidalContrast`, `Modulate`, `Rotate`, `Crop`, `Extent`). Custom code is limited to geometry, layouts, histogram bins, Auto Correct estimation and model integration. | prompt_02 §4. | Accepted (owner) | FR-013–FR-015, FR-043–FR-045 |
| DEC-027 | 2026-10-29 | **Save & Next is synchronous:** save with the current edits and exact crop, wait for success, then advance. On failure, stay with the edits kept. No background queue. | prompt_02 §5. | Accepted (owner) | FR-004, FR-018 |
| DEC-028 | 2026-10-29 | **Temporary originals per order:** `Original/` + `.photox-order.json` kept while an order is processed. **Finish Order** erases them by default (or keeps them), touching only known PhotoX files. | prompt_02 §§8, 9. | Accepted (owner) | FR-005, FR-026, FR-072 |
| DEC-029 | 2026-10-29 | **Print presets** 10×15 (default), 15×20, 13×18 (centred on a white 15×20 canvas), Custom. Composition *Crop to fill* (default) or *Whole photo with white borders*. HEIC unsupported with an explanation. | prompt_02 §§6, 7. | Accepted (owner) | FR-010, FR-011, FR-049, FR-073, FR-031 |
| DEC-030 | 2026-10-29 | **ID:** sizes 3×4, 3.5×4.5, 2×2 in, 4×6 cm, 5×7 cm, 9×12, Custom; simple uniform grid on a portrait 4×6 in page at 300 ppi; 3×4 with a 7 mm strip under each photo; remembered output folder; `ID_yyyyMMdd_HHmm[_n]` names, never overwrite. | prompt_02 §§10–12. | Accepted (owner) | FR-061, FR-066, FR-068, FR-074, FR-075 |
| DEC-031 | 2026-10-29 | **Printing:** via Citizen software for now; future integration through LinkServer hot folders; no queues, quantities or mixed-media management. | prompt_02 §13. | Accepted (owner) | FR-050 |
| DEC-032 | 2026-10-29 | **Magick.NET-Q16-x64 14.17.2** pinned (not 15.0.0). | 15.0.0 was released on 2026-10-10 with breaking API changes; 14.17.x is mature and supports .NET 10. No upgrade merely because it exists (prompt_02 §4). | Accepted | DEC-004 |
| DEC-033 | 2026-10-29 | **Roadmap P-2:** 0.1.0 vertical slice → 0.2.0 complete ordinary editing → 0.3.0 ID creator → 0.4.0 Auto Align and packaging → owner trials → 1.0.0. Projects: Core, Imaging, Storage, App plus two test projects. | Vertical workflow first (prompt_02 §20); modest structure. | Accepted | ROADMAP, ARCHITECTURE |
| DEC-034 | 2026-10-29 | **Versioning:** `Directory.Build.props` holds the version of the milestone being built (starting at 0.1.0). The version is finalised, logged in the CHANGELOG and tagged locally only when the milestone's tests pass. Patch for fixes; no bump per internal edit. | prompt_02 §19. | Accepted (owner) | DEVELOPMENT |
| DEC-035 | 2026-10-29 | **Avalonia 12.1.4** pinned. | Current stable; .NET 10 default. | Accepted | DEC-002 |
| DEC-036 | 2026-10-29 | **Thumbnail cache:** 1 GB, 30 days, Clear cache; Finish Order removes the order's entries. | OQ-018. | Accepted (owner) | FR-053 |
| DEC-037 | 2026-10-29 | **Distribution:** one studio. Velopack with manual updates; no code signing; dependencies must permit paid-studio use. | OQ-013, OQ-015, OQ-027, OQ-028. | Accepted (owner) | NFR-010 |
| DEC-038 | 2026-10-29 | **Auto Align** uses YuNet on ONNX Runtime, with no fallback chain; the operator picks when several faces are found. | prompt_02 §10 (small, documented integration). | Accepted (owner) | FR-063 |
| DEC-039 | 2026-10-29 | **UI strings in two embedded JSON files** (`Strings_en.json`, `Strings_ro.json`) instead of `.resx`. Each key is bound through a small observable object so a language switch updates the interface live. | Easier for the owner to review the Romanian wording. In Avalonia 12, bindings to an indexer did not update on change; per-key observables did (verified in a headless test). | Accepted | FR-009, NFR-008 |
| DEC-040 | 2026-10-29 | **Serilog used directly in the app**, without Microsoft.Extensions.Logging abstractions. Core, Imaging and Storage return typed results and do not log. | Nothing needed the abstraction (NFR-009). | Accepted | FR-051 |
