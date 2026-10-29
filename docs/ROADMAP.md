# PhotoX — Roadmap

Plan revision **P-2** (2026-10-29). Implementation was approved by [prompt_02.md](prompts/prompt_02.md) and proceeds autonomously in this order. [prompt_03.md](prompts/prompt_03.md) added the owner's feedback from testing 0.4.0 (milestones 0.4.1–0.6.0); it takes precedence where it conflicts with older requirements. Requirement IDs refer to [PLAN.md](PLAN.md).

**Principles:**
- Deliver the vertical workflow first (open folder → open photo → adjust crop → basic correction → save → next photo), then widen it.
- A version is finished only when its scope is implemented, its tests pass and the docs are updated.
- Owner-dependent checks (Windows studio PC, physical prints, Romanian review) do not block other work. They are listed as pending.

| Version | Milestone | Scope (FR) | Done when | Status |
|---|---|---|---|---|
| **0.1.0** | **Vertical slice** | FR-001, 002, 003, 004, 005, 008, 009, 010 (10×15, 15×20), 011, 012, 013, 017, 018, 019, 021, 022, 023, 024, 026, 027, 030, 032, 033, 034, 035, 036, 039, 051, 053 (generation) | A folder can be browsed, a photo cropped and exposure-corrected, Save & Next writes the exact crop with the original preserved, and the next photo opens. EN/RO switch works. Tests are green. | Done 2026-10-29 (Linux tests) |
| **0.2.0** | **Complete ordinary editing** | FR-010 (13×18, Custom), 014 (incl. tint), 015, 016, 031 (message), 037, 040, 042, 043, 044, 045, 047, 049, 052, 053 (cache), 054, 072, 073 | All ordinary-photo requirements implemented, including Finish Order and the 13×18 canvas; tests green | Done 2026-10-29 (Linux tests) |
| **0.3.0** | **ID Photo Creator** | FR-007, 061, 062, 064, 066, 067, 068, 071, 074, 075 | Framing, adjustments, sheets (incl. the 7 mm strip), export with timestamp names, calibration sheet; tests green | Done 2026-10-29 (Linux tests; prints pending) |
| **0.4.0** | **Auto Align and packaging** | FR-063, 065 (bounded evaluation), 077; Velopack/folder build; measurements (NFR-003/004); UI scaling checks | Auto Align works on the sample portrait and synthetic tests; Windows build artifact produced; measurements recorded | Done 2026-10-29 (whitening not included, DEC-041) |
| **0.4.1** | **Finish Order data-loss fix** ([prompt_03](prompts/prompt_03.md) §6) | FR-005, FR-072, NFR-005 | Root cause found and fixed, regression tests for Keep and Erase with pre-existing files, Windows CI green | Done 2026-10-29 |
| **0.5.0** | **Owner feedback: speed, editing, browsing, ID consistency, window** (prompt_03 §§2–5, 7–9) | FR-001, 002, 012–015, 017, 039, 043, 044, 049, 052, 064, 066, 067, 074, 077; NFR-003, NFR-006 | Measured before/after; fast preview path; crop applied in preview; levels bar; visual sliders; fixed toolbar; resizable panels; Close / Finish Order at the end; conventional folder tree; ID uses the same controls; landscape sheets; single title | Done 2026-10-29 (Linux tests, Windows CI) |
| 0.5.x+ | Owner trials and fixes | — | Feedback from Windows use, prints and Romanian review incorporated | Waiting for owner feedback |
| **1.0.0** | Stable | — | [PLAN.md §10](PLAN.md#10-release-acceptance-100) acceptance met | — |

**Pending owner actions** (never blocking):
- Run Windows 11 trials on the studio PC (first trial of 0.4.0 done; feedback in prompt_03).
- Print physical test sheets through Citizen software (FR-071).
- Review the Romanian wording.

## Risks

| Risk | Mitigation |
|---|---|
| Avalonia 12 / headless-test toolchain issues | Pinned versions (xUnit 3.2.2); fall back to Avalonia 11.3 only if blocked (recorded as a decision) |
| Magick.NET performance on large photos | Proxy previews, JPEG size hints, memory check, size policy (FR-037) |
| Windows-only behaviour untested until the owner runs it | Platform-neutral code paths; folder build provided; issues fixed in 0.5.x |
| Printer enlargement or trimming changes physical sizes | Calibration sheet and a configurable ID scale (FR-071) |
| Auto Align accuracy from a single sample | Manual framing is always available; validation expands as samples arrive |
