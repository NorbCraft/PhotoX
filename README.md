# PhotoX

PhotoX is a lightweight, offline photo-preparation desktop application for a small professional photo studio. It replaces a workflow built on ACDSee, Photoshop and older Mitsubishi kiosk software with one focused tool:

- **Photos:** open an order folder, crop to 10×15 / 15×20 / 13×18 (centred on 15×20), correct exposure, colour and levels, then **Enter = Save & Next**. Originals are kept temporarily in an `Original` subfolder for re-editing until **Finish Order**.
- **ID Photos:** frame a portrait with face guides and optional Auto Align, adjust it, and export a 10×15 sheet with timestamped filenames to a chosen folder.

The UI is in Romanian and English. All processing is local and no customer photo ever leaves the computer.

## Current status

> **Version 0.4.0**: ordinary-photo editing, Finish Order, ID Photo Creator with Auto Align, and a Windows installer. Everything is verified by automated tests on Linux. **Windows use, physical Citizen prints and the Romanian review are still pending the owner.** See [CHANGELOG](docs/CHANGELOG.md) and [IMPLEMENTATION_LOG](docs/IMPLEMENTATION_LOG.md).

| Item | State |
|---|---|
| Plan | revision P-2 (simplified; all open questions decided) |
| UI mockups | concept screens, EN and RO, in `docs/mockups/` |
| GitHub repository | **to be created by the owner**, see [Repository hosting](#repository-hosting) |

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
| Stack | C# · .NET 10 LTS · Avalonia UI 12.1 · CommunityToolkit.Mvvm · Magick.NET 14.17 · ONNX Runtime (Auto Align) |
| CI | GitHub Actions on `ubuntu-latest` and `windows-latest` once the remote exists |
| Printing | via Citizen software (CY-02); future: LinkServer hot folders |

## Build and run

```bash
export PATH="$HOME/.dotnet:$PATH"      # .NET 10 SDK
dotnet test PhotoX.slnx                # run tests
dotnet run --project src/PhotoX.App    # start PhotoX
dotnet publish src/PhotoX.App -c Release -r win-x64 --self-contained -p:DebugType=None -o publish/win-x64   # Windows build
dotnet tool restore && dotnet vpk "[win]" pack --packId PhotoX --packVersion 0.4.0 --packDir publish/win-x64 \
  --mainExe PhotoX.exe --icon assets/logo/photox.ico --packTitle PhotoX --outputDir releases/win          # Windows installer
```

## Repository hosting

The local Git repository is on branch `main`. **The GitHub repository will be created by the owner.** The development environment has no GitHub CLI, token or verified SSH host key, and the agent does not change credentials. Once the remote exists, push with one of the options below; CI is then added.

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

Private software for a single studio, not distributed (OQ-027). Third-party licences are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
