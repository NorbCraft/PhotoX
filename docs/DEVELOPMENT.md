# PhotoX — Development Conventions

Plan revision **P-2** (2026-10-29). [PLAN.md](PLAN.md) is authoritative for *what* is built; this file covers *how we work*.

## 1. Git

- Work on `main` in small, coherent commits (code + tests + needed doc updates together). Feature branches are optional for larger experiments.
- Commit message format:

  ```text
  <type>(<scope>): <imperative summary, ≤ 72 chars>

  <why / what>

  Refs: FR-004, TS-07
  ```

  - `type`: feat, fix, test, docs, refactor, perf, build, chore.
  - `scope`: core, imaging, storage, app, editor, id, l10n, docs, …
- The remote is created by the owner. Until then, commits stay local. Never alter credentials or claim a remote exists.
- Never commit customer photos, the owner's sample (`id_photo/`), derivatives of it, secrets or build output. `.gitignore` blocks image files outside whitelisted folders.

## 2. Versioning

| Change | Effect |
|---|---|
| Milestone completed (tests pass, docs updated) | minor bump, e.g. 0.1.0 → 0.2.0; CHANGELOG entry; local tag `v0.N.0` |
| Fix or small change after a release | patch bump, e.g. 0.2.0 → 0.2.1 |
| Internal edits | no bump |
| Stable release (PLAN §10) | 1.0.0 |

The version lives in `Directory.Build.props`. Implementation starts at **0.1.0**. Unfinished or unverified milestones are never tagged.

## 3. Code

- .NET 10, C# latest, nullable enabled, warnings as errors, file-scoped namespaces, one public type per file (small related records may share a file).
- **Separation** ([ARCHITECTURE.md §3](ARCHITECTURE.md#3-projects)):
  - Core is pure.
  - Imaging wraps Magick.NET.
  - Storage owns all writes.
  - App holds views and view models with no image math or file I/O.
- Prefer Magick.NET operations over custom pixel code (DEC-026). Add custom image code only when the library cannot reasonably do the job, and document why.
- No abstractions without a concrete use. Interfaces only where tests or two implementations need them.
- Never block the UI thread with decoding, rendering or saving.
- All UI text goes through `src/PhotoX.App/Localization/Strings_en.json` and `Strings_ro.json`, and a new key is added to both files in the same commit (a test checks parity). Use a consistent form of address in Romanian (polite plural imperative, e.g. “Salvați”, or neutral infinitive/noun labels such as “Salvare”), and keep terminology consistent.
- New NuGet packages are pinned in `Directory.Packages.props`, and their licence is checked against NFR-010.

## 4. Tests

- Run `dotnet test` before committing. Fix failures rather than weakening meaningful checks.
- Tests use generated fixtures. Tests that need the owner's sample copy it to a temp workspace and skip if it is missing.
- See [TEST_PLAN.md](TEST_PLAN.md) for the suites.

## 5. Documentation (proportionate)

| When | Update |
|---|---|
| A requirement's behaviour or progress changes | PLAN.md (Progress column) |
| A significant decision or deviation | DECISIONS.md |
| A milestone is completed | IMPLEMENTATION_LOG.md (what works, what was verified, limitations), CHANGELOG.md |
| A new genuinely open issue | OPEN_QUESTIONS.md |

Do not repeat long narratives across documents for small commits.

## 6. Agent rules (autonomous implementation, prompt_02 §20)

- Proceed through [ROADMAP.md](ROADMAP.md) without routine approval requests. Fix defects, resolve compatibility issues, refactor within the architecture and continue to the next milestone.
- Ask the owner only when:
  - a contradiction materially changes behaviour;
  - a major requirement cannot be implemented reasonably;
  - a dependency imposes cost or unsuitable usage terms;
  - a solution would materially expand scope or risk unrelated user data;
  - required access or a decision cannot be inferred.
- If a feature is blocked, isolate it, record the limitation and continue.
- At significant checkpoints, report briefly: what works, what was verified, version and commits, limitations, and owner actions needed.
- Never edit archived prompts in `docs/prompts/`. Never claim results that did not happen.

## 7. Prompt archive

Prompts are stored as `docs/prompts/prompt_NN.md`, sequential and never edited. [docs/prompts/README.md](prompts/README.md) indexes them.

## 8. Local setup

- The .NET 10 SDK is installed user-locally at `~/.dotnet` on the development machine (`export PATH="$HOME/.dotnet:$PATH"`).
- Build: `dotnet build PhotoX.slnx`. Test: `dotnet test PhotoX.slnx`. Run: `dotnet run --project src/PhotoX.App`.
- Windows trial build: `dotnet publish src/PhotoX.App -c Release -r win-x64 --self-contained -o publish/win-x64`.
