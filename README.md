# PhotoX

PhotoX is a lightweight, offline photo-preparation desktop application for a small professional photo studio. It replaces a workflow built on ACDSee, Photoshop and older Mitsubishi kiosk software with one focused tool:

- **Photos:** open an order folder, crop to 10×15 / 15×20, correct exposure, colour and levels, then **Save & Next**. Originals are kept safely in an `Original` subfolder.
- **ID Photos:** frame a portrait with face guides and optional Auto Align, adjust it, and export an exact-size print sheet.

The UI is in Romanian and English. All processing is local and no customer photo ever leaves the computer.

## Current status

> **Planning phase. Nothing is implemented yet.** The plan (revision P-1, 2026-10-29) is waiting for the owner's review and explicit approval. The first implementation version will be **0.1.0** (Foundation). No software release or tag exists.

| Item | State |
|---|---|
| Requirements, architecture, roadmap | drafted, awaiting review |
| UI mockups | 4 screens × 2 languages (concepts) |
| Application code | none (by design) |
| GitHub repository | **not created yet**, see [Repository hosting](#repository-hosting) |

## Documentation

| Start here | |
|---|---|
| [docs/PLAN.md](docs/PLAN.md) | **Source of truth:** scope, requirements (FR/NFR IDs), assumptions, acceptance |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Technology choices, module structure, image pipeline, safe-save design, diagrams |
| [docs/ROADMAP.md](docs/ROADMAP.md) | Milestones 0.1.0 → 1.0.0, dependencies, risks |
| [docs/OPEN_QUESTIONS.md](docs/OPEN_QUESTIONS.md) | Decisions needed from the owner, with recommended defaults |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Decision log |
| [docs/TEST_PLAN.md](docs/TEST_PLAN.md) | Verification strategy and release acceptance |
| [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | Git, versioning, coding and AI-agent conventions |
| [docs/mockups/](docs/mockups/README.md) | Concept mockups and navigation structure |
| [docs/IMPLEMENTATION_LOG.md](docs/IMPLEMENTATION_LOG.md), [docs/CHANGELOG.md](docs/CHANGELOG.md) | Progress and release history |
| [docs/prompts/](docs/prompts/README.md) | Archive of the prompts that direct the project |
| [AGENTS.md](AGENTS.md) | Rules for AI coding agents working in this repository |

## Mockup preview (concept)

![Photo editor concept](docs/mockups/02-photo-editor-en.png)

![ID photo editor concept, Romanian](docs/mockups/03-id-editor-ro.png)

## Platforms and technology

| | |
|---|---|
| Production platform | **Windows 11 x64** |
| Development platform | Linux Mint (with a Windows 11 VM and physical Windows PCs for testing) |
| Stack (proposed / validated) | C# · .NET 10 LTS · Avalonia UI 12 · MVVM (CommunityToolkit.Mvvm) · Magick.NET 15 · ONNX Runtime (ID features) |
| CI (planned) | GitHub Actions on `ubuntu-latest` and `windows-latest` |
| Future printer | Citizen CY-02 (direct printing deferred beyond 1.0) |

## Repository hosting

The local Git repository is initialised on branch `main`. **The GitHub repository has not been created**, because the planning environment had no GitHub CLI, no access token, and no verified SSH host key for github.com. To finish, choose one option.

**Option A: GitHub CLI**

```bash
sudo apt install gh                  # Linux Mint / Ubuntu
gh auth login                        # choose GitHub.com, SSH, existing key ~/.ssh/id_ed25519.pub
cd ~/Documents/PhotoX
gh repo create PhotoX --private --source=. --remote=origin --push
```

**Option B: web UI + SSH**

1. Create an empty **private** repository named `PhotoX` at <https://github.com/new>, with no README, licence or .gitignore.
2. Make sure `~/.ssh/id_ed25519.pub` is added to your GitHub account (Settings → SSH keys).
3. Run `ssh -T git@github.com` and accept the host key **only after** checking its fingerprint against GitHub's published SSH fingerprints.
4. Push:

   ```bash
   git remote add origin git@github.com:<your-account>/PhotoX.git
   git push -u origin main
   ```

## Licence

Proprietary. All rights reserved until the owner decides otherwise (see OQ-027). Third-party licences will be listed in `THIRD-PARTY-NOTICES.md` from version 0.1.0.
