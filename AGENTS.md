# Instructions for AI coding agents

## Current phase: PLANNING. Implementation is NOT approved.

Do not create application code, projects, solutions or dependencies until a prompt in `docs/prompts/` explicitly approves an implementation milestone. Planning documents may be edited when the owner asks.

## Read first

1. [docs/PLAN.md](docs/PLAN.md): the **source of truth** (requirements FR-/NFR-, scope, acceptance).
2. [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md): Git, versioning, coding, testing and documentation rules, and the agent rules in §7.
3. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/ROADMAP.md](docs/ROADMAP.md) for the milestone you are working on.
4. The latest entry in [docs/IMPLEMENTATION_LOG.md](docs/IMPLEMENTATION_LOG.md) and the newest prompt in [docs/prompts/](docs/prompts/README.md).

## Non-negotiables

- **Never** risk customer originals. All writes into order folders go through `PhotoX.Services`' safe-save transaction, and it must stay covered by the TS-08 fault-injection tests.
- **Never** commit real customer photos or personal images.
- **Never** edit or replace archived prompts in `docs/prompts/`.
- **Never** silently change scope, architecture or dependencies. Record the change in DECISIONS.md, and ask the owner when it is significant.
- **Never** claim a test, release, remote or result that did not happen.
- Keep PLAN.md statuses, DECISIONS.md, IMPLEMENTATION_LOG.md and CHANGELOG.md current in the same commits as the code.
- Reference requirement and test IDs in commit messages (`Refs: FR-005, TS-08`).
