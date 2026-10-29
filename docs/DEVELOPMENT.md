# PhotoX — Development Conventions

Status: **proposal** (plan revision P-1, 2026-10-29). These conventions apply from milestone 0.1.0 onward. They complement [PLAN.md](PLAN.md), which stays authoritative for *what* is built.

## 1. Workflow overview

1. The owner approves the plan or a milestone. Approval is recorded as a prompt in `docs/prompts/` and a note in [IMPLEMENTATION_LOG.md](IMPLEMENTATION_LOG.md).
2. An agent (or developer) implements the milestone in **dependency order**, in small, coherent commits, with tests.
3. At milestone end: all completion criteria from [ROADMAP.md](ROADMAP.md) pass, the docs are updated, the version is finalised, and a tag is created.
4. The owner reviews (and pilots, where planned) and approves the next milestone.

## 2. Git

- **Host:** GitHub, private repository `PhotoX` (pending, see [README.md](../README.md#repository-hosting)).
- **Branches:**
  - `main` is always green: builds, tests pass, docs consistent.
  - Work happens on `feature/<milestone>-<topic>` (for example `feature/0.3-safe-save`) and merges into `main` by fast-forward or squash after CI passes.
  - A solo developer with agents may commit small doc fixes directly to `main`.
- **Commits:**
  - One logical change per commit: code, its tests and the doc updates together.
  - Do not mix unrelated functionality, and do not commit trivial intermediate states.
  - Message format (Conventional Commits style):

    ```text
    <type>(<scope>): <imperative summary, ≤ 72 chars>

    <why / what, wrapped at 72>

    Refs: FR-005, FR-022, TS-08
    ```

    - `type`: `feat`, `fix`, `test`, `docs`, `refactor`, `perf`, `build`, `ci`, `chore`.
    - `scope`: `core`, `imaging`, `services`, `vision`, `app`, `editor`, `id`, `save`, `l10n`, `docs`, …
    - **Refs:** requirement and test IDs where applicable. This gives traceability from code to the plan.
- **Tags:** `v0.N.M` only for finished versions that meet their criteria. Never tag planning work or unfinished milestones.
- **Never commit:**
  - customer photos or any real personal images;
  - secrets;
  - build outputs;
  - large binaries other than approved model files and small fixtures.

  `.gitignore` guards against image files outside whitelisted folders.

## 3. Versioning

The owner's policy, with one proposal (the `-dev` suffix):

| Change | Version effect | Example |
|---|---|---|
| Milestone or significant functional addition completed | **minor** +1, patch reset | 0.1.5 → 0.2.0 |
| Bug fix or small change after a release | **patch** +1 | 0.2.0 → 0.2.1 |
| Work in progress towards the next milestone | target version with `-dev` suffix | `0.3.0-dev` |
| Stable release meeting [release acceptance](TEST_PLAN.md#8-release-acceptance) | **1.0.0** | 0.9.3 → 1.0.0 |

- The single source of the version is `<Version>` in `Directory.Build.props`. It drives the assembly version, the About box and installer metadata.
- 0.1.0 is the **first implementation milestone** (Foundation). No version exists before it and nothing has been tagged during planning.
- Every version change gets a [CHANGELOG.md](CHANGELOG.md) entry.

## 4. Code conventions

- **Language:** C# 14 / .NET 10, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, .NET analyzers at `Recommended` level, `.editorconfig` committed in 0.1.0.
- **Style:** file-scoped namespaces; one public type per file; `PascalCase` for types and members, `_camelCase` for private fields; `Async` suffix on async methods; `CancellationToken` as the last parameter on long operations.
- **Architecture rules** ([ARCHITECTURE.md §4.1](ARCHITECTURE.md#41-dependency-rules)):
  - Core has no I/O or UI.
  - Only `PhotoX.Services` writes into order folders.
  - View models have no image math or file I/O.
  - An architecture test enforces these rules.
- **Immutability:** domain models (`EditRecipe`, templates, layouts) are immutable `record`s.
- **Threading:** never block the UI thread. Heavy work runs through services that return immutable results, and UI updates are marshalled by the view model.
- **Errors:** expected failures return typed results (`Result<T, PhotoXError>`). Exceptions are reserved for bugs and unexpected I/O failures. Every user-facing message is a localization key.
- **Localization:** no literal UI strings in code or XAML. Add the key to *both* `.resx` files in the same commit.
- **Comments:** explain *why*, not *what*. Public APIs in Core and Services get XML doc comments.
- **Dependencies:** a new NuGet package needs a DECISIONS entry (licence checked against NFR-010) and a central version pin.

## 5. Testing obligations

- New logic comes with unit tests. Bug fixes come with a regression test.
- Changes to save, backup or recovery require the TS-08 suite to be extended for the new path *before* merging.
- Run `dotnet test` locally before committing. CI must be green before merging to `main`.
- Visual baseline changes must be reviewed (inspect the diff images) and committed deliberately.

## 6. Documentation obligations

| When | Update |
|---|---|
| A requirement is implemented or changes status | PLAN.md (Status/MS columns) |
| A design decision is made or changed | DECISIONS.md (new entry; supersede, never edit history) |
| An open question is answered | OPEN_QUESTIONS.md (mark *Decided → DEC-xxx*) and PLAN.md |
| A significant change is completed | IMPLEMENTATION_LOG.md (what, validation results, commits, issues) |
| A version changes | CHANGELOG.md |
| Architecture deviates from the document | ARCHITECTURE.md *in the same commit* |

Conflicts between documents are **reported** (OPEN_QUESTIONS) and never silently resolved.

## 7. AI coding agent rules (effective after plan approval)

These rules implement the owner's “Autonomous implementation — future instruction” ([prompt_01.md §15](prompts/prompt_01.md)).

**At the start of every task, the agent:**
1. Reads [AGENTS.md](../AGENTS.md), this file, and the relevant sections of PLAN.md and ARCHITECTURE.md.
2. Checks the latest IMPLEMENTATION_LOG entry and the current milestone in ROADMAP.md.
3. Confirms the milestone is **approved** (a prompt in `docs/prompts/` says so). Without approval, the agent does not implement.

**While working, the agent:**
- Implements milestones in dependency order and stays within the approved scope.
- Runs the applicable tests, fixes the problems it finds, and commits meaningful completed steps (§2).
- Updates the version per §3 and keeps the documentation aligned (§6).
- Records design changes and results in DECISIONS and IMPLEMENTATION_LOG.
- Continues to the next approved milestone without asking for confirmation unnecessarily.

**The agent stops and asks the owner when:**
- requirements conflict;
- a fundamental decision is missing (an **H** open question without an accepted default);
- a change would significantly alter the approved architecture, functionality, dependencies or licences;
- a test cannot be made to pass without lowering an agreed threshold.

**The agent never:**
- silently redefines scope;
- commits customer photos;
- overwrites or modifies `docs/prompts/` records;
- claims a remote, release or test result that did not happen.

## 8. Prompt archive

- Prompts are stored in `docs/prompts/` with sequential names: `prompt_01.md`, `prompt_02.md`, …
- They are **never edited or replaced** after archiving. Corrections arrive as a new prompt.
- [docs/prompts/README.md](prompts/README.md) indexes each prompt with its date, purpose, resulting commits, decisions and milestone.

## 9. Local development setup (from 0.1.0)

- Linux Mint: install the .NET 10 SDK (version pinned by `global.json`), Git, and an IDE (VS Code with C# Dev Kit, or Rider). Magick.NET's native library needs `fontconfig` (present on Mint).
- Windows 11 VM: .NET 10 SDK for running published builds and Windows-only tests.
- Real sample photos live outside the repository. Set `PHOTOX_SAMPLES=/path/to/samples` to enable sample-based tests.
