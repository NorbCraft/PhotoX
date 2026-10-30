# PhotoX — Open Questions

Plan revision **P-2** (2026-10-29). All questions raised in P-1 were **decided by the owner** in [prompt_02.md §21](prompts/prompt_02.md). Decisions are recorded in [DECISIONS.md](DECISIONS.md), and the resulting behaviour is in [PLAN.md](PLAN.md). New questions are added only when implementation reveals a genuinely unresolved issue with material consequences.

## Open

Non-blocking: a default is applied and can be changed without a new version.

| ID | Question | Default applied | Where |
|---|---|---|---|
| OQ-030 | Physical size of the 20×30 sheet (prompt_04 collages and custom sizes) | 8×12 in = 203.2 × 304.8 mm, the usual size of "20×30" photo prints and consistent with the inch-based 10×15 and 15×20. Editable as `20x30` in `print-formats.json`. The owner confirms with the printer or lab that prints 20×30 (the Citizen CY-02 is not assumed to). | FR-010, FR-079, DEC-051 |

## Decided

| ID | Question | Owner decision (prompt_02) | Where applied |
|---|---|---|---|
| OQ-001 | Media dimensions | Use the researched initial dimensions (10×15 = 4×6 in 101.6×152.4 mm; 15×20 = 6×8 in 152.4×203.2 mm). Configurable; physical output verified later through Citizen software. | FR-011, FR-071, DEC-029 |
| OQ-002 | Future printing | Citizen LinkServer hot folders; integration deferred. | FR-050, DEC-031 |
| OQ-003 | Mixed printing | Not required; no mixed-printing management. | FR-050, DEC-031 |
| OQ-004 | HEIC/HEIF | Unsupported initially; deferred future point. Unsupported files are explained. | FR-031, DEC-029 |
| OQ-005 | Output quality | JPEG q95, 4:4:4; native crop resolution; interpolation only where geometry requires it; effective-ppi metadata where defined; ID sheets at 300 ppi. | FR-033, FR-035, FR-068 |
| OQ-006 | Unchanged photos | Mark done/reviewed without re-encoding. Enter otherwise saves current edits and the selected crop, then advances. Originals are temporary until Finish Order. | FR-004, FR-024, DEC-027 |
| OQ-007 | Backup collisions/retention | Never overwrite an uncertain backup; use a unique name. Retention is controlled by Finish Order. | FR-021, FR-072, DEC-028 |
| OQ-008 | Bulk corrections | No batch apply or batch Auto Correct. | §7 of PLAN (FR-046 removed) |
| OQ-009 | Edit persistence | Keep originals and edit history while processing. Finish Order offers Keep or **Erase (default)**. | FR-026, FR-072, DEC-028 |
| OQ-010 | Automatic whitening | Experimental only; must not block other work. | FR-065 |
| OQ-011 | ID templates | Only the geometric sizes of prompt_02 §10 plus Custom; 7 mm strip within the 3×4 sheet; no country templates. | FR-061, FR-074, DEC-030 |
| OQ-012 | Output colour | Convert edited outputs to sRGB and embed the profile. | FR-034 |
| OQ-013 | Dependency licences | Free dependencies whose terms permit use in a paid studio; no product-sale requirements. | NFR-010, DEC-037 |
| OQ-014 | Enter shortcut | Enter = Save & Next with the current edits and the selected crop, never resetting the crop. | FR-004, FR-018, DEC-027 |
| OQ-015 | Installer/updates | Velopack; manual updates. | DEC-037 |
| OQ-016 | Performance/image limits | Reference hardware and initial image-size targets accepted, subject to measured feasibility. | NFR-003, FR-037 |
| OQ-017 | Touchscreen | Not required. | NFR-007 |
| OQ-018 | Cache retention | 1 GB, 30 days, clearable; order cache cleaned at Finish Order where practical. | FR-053, DEC-036 |
| OQ-019 | Removable media | Allowed with a warning recommending a local working copy. | FR-054 |
| OQ-020 | Metadata | Keep appropriate source metadata; update edited fields; remove stale previews. | FR-033 |
| OQ-021 | Additional adjustments | Add tint; nothing else for now. | FR-014 |
| OQ-022 | Romanian review | The owner reviews the wording. | NFR-008 |
| OQ-023 | Display colour | sRGB-display assumption; monitor profiles deferred; record discrepancies found in testing. | FR-034 |
| OQ-024 | Interim printing | Print through Citizen software: ordinary photos via Save, ID outputs via the remembered export folder and timestamp names. | FR-050, FR-068, FR-075 |
| OQ-025 | ID output location/names | User-selected, remembered folder; `ID_[date]_[time]`, with `_1`, `_2` … on conflict; never overwrite. | FR-068, FR-075, DEC-030 |
| OQ-026 | GitHub repository | The owner creates the remote. Local implementation and commits continue meanwhile. | DEC-020 |
| OQ-027 | Distribution | One studio. Selling or sharing must not drive engineering. | DEC-037 |
| OQ-028 | Code signing | Not required. | DEC-037 |
| OQ-029 | Whole-photo borders | Include *Whole photo with white borders* alongside the default *Crop to fill*. | FR-049, DEC-029 |
