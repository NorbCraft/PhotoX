# PhotoX

PhotoX is a lightweight, offline photo-preparation desktop application for a small professional photo studio. It replaces a workflow built on ACDSee, Photoshop and older Mitsubishi kiosk software with one focused tool:

- **Photos:** open an order folder, select photos, edit if needed (crop to 10×15 / 15×20 / 13×18 or a custom size, exposure, colour and levels, **Enter = Save & Next**), then **Print** or **Save changes**. Originals are kept in an `Original` subfolder until Save changes, and Restore original can bring them back.
- **ID Photos:** choose the format, click a portrait in the built-in explorer, frame it (face guides, Auto Align), adjust it, then **Save sheet** (JPG) or **Print**.
- **Collages:** choose a 10×15 or 15×20 sheet and a template, add photos, adjust the layout, then **Save** (JPG) or **Print**.
- **Printing:** directly through the installed Windows printer driver (Citizen CY-02), with a print queue and a print counter on every thumbnail.

The UI is in Romanian and English. All processing is local and no customer photo ever leaves the computer.

## Current status

> **Version 0.8.0**: photo editing, ID photos, collages and **direct printing through the Windows driver** (prompt_05), with multiple selection, Save changes and Restore original instead of Finish Order, a print queue and print counters. Earlier: the owner's feedback (prompt_03) and the new icon, Collages and refinements (prompt_04). Verified by automated tests on Linux and in CI on Windows, including a print job through a virtual PDF printer. **Printing on the Citizen CY-02, studio use on Windows and the Romanian review are still pending the owner.** See [CHANGELOG](docs/CHANGELOG.md) and [IMPLEMENTATION_LOG](docs/IMPLEMENTATION_LOG.md).

| Item | State |
|---|---|
| Plan | revision P-2 (simplified; all open questions decided) |
| UI mockups | concept screens, EN and RO, in `docs/mockups/` |
| GitHub repository | <https://github.com/NorbCraft/PhotoX> (private); CI on Linux and Windows |

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
| Printing | directly from PhotoX through the installed Windows driver (Citizen CY-02); no LinkServer |

## Build and run

```bash
export PATH="$HOME/.dotnet:$PATH"      # .NET 10 SDK
dotnet test PhotoX.slnx                # run tests
dotnet run --project src/PhotoX.App    # start PhotoX
dotnet publish src/PhotoX.App -c Release -r win-x64 --self-contained -p:DebugType=None -o publish/win-x64   # Windows build
dotnet tool restore && dotnet vpk "[win]" pack --packId PhotoX --packVersion 0.8.0 --packDir publish/win-x64 \
  --mainExe PhotoX.exe --icon assets/logo/photox.ico --packTitle PhotoX --outputDir releases/win          # Windows installer
python3 assets/logo/build_icons.py     # re-render the icon PNGs and ICO after editing the SVG masters
```

**Installing on Windows:** every push builds the artifact `PhotoX-<version>-windows` in GitHub Actions. It contains `PhotoX-<version>-win-Setup.exe` (one-click install for the current user, with Start menu and desktop shortcuts) and `PhotoX-<version>-win-Portable.zip` (no install). Only the Setup file is needed to install; the other files are for a future update feed. The installer is not code-signed, so Windows SmartScreen may ask to confirm (*More info → Run anyway*).

**Linux menu entry:** publish a build (`dotnet publish src/PhotoX.App -c Release -r linux-x64 --self-contained -o ~/PhotoX`), then run `packaging/linux/install-desktop-entry.sh ~/PhotoX` to add PhotoX with its icon to the application menu.

## Repository hosting

The repository is hosted on GitHub at `NorbCraft/PhotoX`. GitHub Actions builds and tests every push on `ubuntu-latest` and `windows-latest` and produces the Windows package ([.github/workflows/ci.yml](.github/workflows/ci.yml)).

## Licence

Private software for a single studio, not distributed (OQ-027). Third-party licences are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
