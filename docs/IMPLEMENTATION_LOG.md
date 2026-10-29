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
