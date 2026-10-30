# Instructions for AI coding agents

## Current phase: IMPLEMENTATION (approved by `docs/prompts/prompt_02.md`; later prompts `prompt_03.md` and `prompt_04.md` take precedence where they conflict)

Work autonomously through [docs/ROADMAP.md](docs/ROADMAP.md) under the rules in [docs/DEVELOPMENT.md §6](docs/DEVELOPMENT.md#6-agent-rules-autonomous-implementation-prompt_02-20). Ask the owner only for genuine contradictions, infeasible major requirements, unsuitable dependency terms, scope or user-data risks, or missing access.

## Read first

1. [docs/PLAN.md](docs/PLAN.md): the **source of truth** (requirements, scope, domain rules).
2. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/ROADMAP.md](docs/ROADMAP.md).
3. [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md): Git, versioning, code and test conventions.
4. The latest entry in [docs/IMPLEMENTATION_LOG.md](docs/IMPLEMENTATION_LOG.md) and the newest prompt in [docs/prompts/](docs/prompts/README.md).

## Non-negotiables

- A failed save must never damage the existing photo or its preserved original. All writes go through `PhotoX.Storage`.
- **Enter / Save & Next saves exactly the current edits and the selected crop, waits for success, then advances.**
- Never commit real photos: not customer images, not `id_photo/IMG_6153.JPG`, not their derivatives. Write tests on real photos only on copies in a temp workspace.
- Never edit archived prompts in `docs/prompts/`.
- Never silently change scope or architecture. Record significant decisions in DECISIONS.md.
- Never claim Windows runs, CI results, physical prints, remotes or owner acceptance that did not happen.
- Use Magick.NET for image processing. Custom pixel code is a documented last resort.
