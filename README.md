# PhotoX

PhotoX is a lightweight, offline photo-preparation desktop application for a small professional photo studio. It replaces a workflow built on ACDSee, Photoshop and older Mitsubishi kiosk software with one focused tool:

- **Photos:** open an order folder (folders and photos side by side, like a file explorer), select photos, edit if needed (crop to 10×15 / 15×20 / 9×13 / 13×18 or a custom size, exposure, colour and levels, **Enter = Save & Next**), set how many **copies** each photo needs (×1 by default, ×0 to leave it out, for one or many photos at once), then **Print** or **Discard originals**. Originals are kept in an `Original` subfolder until Discard originals, and Restore original can bring them back. Deleted photos wait in `Original` too and can be restored from the Deleted filter.
- **ID Photos:** choose the format, double-click a portrait among the folder's thumbnails (step 1), frame it (face guides, Auto Align, 90° rotation), adjust it, then **Save** (JPG) or **Print**.
- **Collages:** choose a 10×15 or 15×20 sheet and a template, drag photos from the thumbnail panel into the frames, adjust the layout, then **Save** (JPG) or **Print**.
- **Printing:** a workspace that prints directly through the installed Windows printer driver (Citizen CY-02), with the queue, the print history and statistics, the requested and the printed copies on every thumbnail, and each photo's copies on the print preview.

Light and dark mode are available in Settings.

The UI is in Romanian and English. All processing is local and no customer photo ever leaves the computer.

## Current status

> **Version 0.12.0**: **copies per photo** (a ×N badge on each thumbnail, *Copies* in the sidebar for one or many photos, *Selected for print* and *Printed* for the folder; Print all and Print selected print each photo's copies, shown on the print preview), ID Photos opens a photo with a double-click, fewer hints, the open workspace stays highlighted, and a quieter tree button (prompt_09). Earlier: one file explorer in all workspaces, safe photo deletion, Discard originals for the whole folder, Check for updates from GitHub Releases and a new About (prompt_08); the fix for partly black saved photos, faster saving, Close / Print / Save in the editor and printing refinements (prompt_07); light and dark mode, the Printing workspace with queue, history and statistics, one editing sidebar and collage drag and drop (prompt_06); direct printing through the Windows driver (prompt_05), the new icon, Collages and refinements (prompt_04), the owner's feedback (prompt_03). Verified by automated tests on Linux and in CI on Windows, including a print job through a virtual PDF printer. **Printing on the Citizen CY-02, studio use on Windows and the Romanian review are still pending the owner.** See [CHANGELOG](docs/CHANGELOG.md) and [IMPLEMENTATION_LOG](docs/IMPLEMENTATION_LOG.md).

| Item | State |
|---|---|
| Plan | revision P-2 (simplified; all open questions decided) |
| UI mockups | concept screens, EN and RO, in `docs/mockups/` |
| GitHub repository | <https://github.com/NorbCraft/PhotoX> (public since prompt_08); CI on Linux and Windows; approved versions on [Releases](https://github.com/NorbCraft/PhotoX/releases) |

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
python3 packaging/windows/make_splash.py 0.12.0 releases/splash.png                                     # installer splash with the version
dotnet tool restore && dotnet vpk "[win]" pack --packId PhotoX --packVersion 0.12.0 --packDir publish/win-x64 \
  --mainExe PhotoX.exe --icon assets/logo/photox.ico --packTitle PhotoX --packAuthors PhotoX \
  --splashImage releases/splash.png --shortcuts StartMenuRoot,Desktop --outputDir releases/win            # Windows installer
python3 assets/logo/build_icons.py     # re-render the icon PNGs and ICO after editing the SVG masters
```

**Installing on Windows:** approved versions are on the [Releases page](https://github.com/NorbCraft/PhotoX/releases): download `PhotoX-<version>-win-Setup.exe` and run it. Every push also builds the artifact `PhotoX-<version>-windows` in GitHub Actions (for testing), with the same Setup file, its `.sha256` checksum and `PhotoX-<version>-win-Portable.zip` (no install). The installer is not code-signed, so Windows SmartScreen may ask to confirm (*More info → Run anyway*).

- **Install:** run the Setup file. It shows PhotoX and its version while it installs for the current user (no administrator rights) into `%LOCALAPPDATA%\PhotoX`, adds PhotoX to the Start menu and the desktop, and starts it.
- **Find and manage it:** *Settings → Apps → Installed apps → PhotoX* (version, uninstall). PhotoX's Settings → About also shows the program folder.
- **Update:** *Settings → Updates → Check for updates* (from 0.11.0). When a newer published version exists, *Download and install* downloads its Setup file, checks it against the published checksum, closes PhotoX and starts it; the setup asks to update the older version. Running the newer Setup file by hand does the same. Settings, print counters, the print history and the logs (in `%APPDATA%\PhotoX`) stay.
- **Uninstall:** from Installed apps. The program folder is removed; the settings folder `%APPDATA%\PhotoX` stays unless deleted by hand.

**Linux menu entry:** publish a build (`dotnet publish src/PhotoX.App -c Release -r linux-x64 --self-contained -o ~/PhotoX`), then run `packaging/linux/install-desktop-entry.sh ~/PhotoX` to add PhotoX with its icon to the application menu.

## Repository hosting

The repository is hosted on GitHub at `NorbCraft/PhotoX`. GitHub Actions builds and tests every push on `ubuntu-latest` and `windows-latest` and produces the Windows package ([.github/workflows/ci.yml](.github/workflows/ci.yml)). A version tag `vX.Y.Z` also prepares a **draft** release with the installer and its checksum; publishing the draft approves the version, and only then does Check for updates offer it ([DEVELOPMENT.md §2](docs/DEVELOPMENT.md#2-versioning)).

## Licence

Private software for a single studio, not distributed (OQ-027). Third-party licences are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
