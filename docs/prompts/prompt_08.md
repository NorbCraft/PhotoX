# PhotoX — File Explorer, Workspace Consistency and Automatic Updates

Continue developing PhotoX using the current repository, existing architecture and approved project plan.

This iteration focuses on improving the file explorer across all three workspaces, simplifying navigation, adding safe photo deletion and implementing a GitHub-based update mechanism.

Keep PhotoX simple, modern, responsive and practical for everyday photo-studio use.

Avoid overengineering. Reuse existing components where possible rather than implementing separate solutions for each workspace.

Implement the changes autonomously, test them, make meaningful commits, update version numbers and maintain the relevant documentation.

---

## 1. Improve the Photos file explorer

The current Photos explorer displays the folder tree on the left and photographs as thumbnails.

I want to make it a more useful file explorer, while keeping it focused on photographs.

### Display folders in the thumbnail gallery

- Show subfolders as items in the thumbnail gallery, alongside the photographs.
- Use recognizable folder icons or suitable folder previews.
- Double-clicking a folder should open it and display its contents.
- Allow navigating back to the parent folder.
- Keep the current folder tree on the left.
- Keep the existing photograph thumbnails, selection, editing indicators and print counters.
- Maintain the existing All / To Do / Done filters for photographs.
- Folder items should remain clearly distinguishable from photographs.

The goal is familiar desktop file-explorer behaviour without adding unnecessary file-management features.

Navigation should remain fast, even in folders containing many photographs and subfolders.

---

## 2. ID Photos — Improve layout and navigation

Slightly redesign the ID Photos workspace to make navigation consistent with Photos.

### Step 1 — Select Photo

The first step should use the same file-navigation structure as the Photos workspace:

- Shared folder tree on the left.
- Folder contents and photograph thumbnails displayed to its right.
- Show subfolders as well as photographs.
- Allow normal folder navigation.
- Select a photograph from the thumbnail gallery to continue.

The folder tree and thumbnail gallery should look and behave the same as in Photos.

### Preserve the three-step workflow

Keep the existing workflow:

1. **Select Photo**
2. **Framing & Adjustments**
3. **Print**

Step 1 is mainly a file-selection view.

After selecting a photograph, the user continues to Step 2, where the current ID editing interface is displayed.

Step 3 remains the existing print-sheet preparation, saving and printing workflow.

Except for the improved Step 1 layout and navigation, preserve all existing ID Photo functionality.

Do not unnecessarily modify the framing, automatic alignment, format selection, editing controls or print-sheet generation.

---

## 3. Collages — Improve explorer and layout

Make the Collages workspace use the same folder-navigation component.

### Left sidebar

The left sidebar should contain only Folder tree.

Use the same component and behaviour as Photos and ID Photos.

### Photograph thumbnails

Move the photograph thumbnail gallery into its own panel.

The layout should conceptually be:

**Folder Tree | Photo Thumbnails | Collage Preview  |  Collage settings**

The thumbnail panel should:

- Display photographs from the currently selected folder.
- Allow selecting and dragging photographs into collage slots.
- Preserve the existing drag-and-drop functionality.
- Be resizable where practical.
- Be collapsible to the right to give the collage preview more space.
- Be expandable again using a clearly visible button.

The collage settings pannel should be able to hide, to get more place for the thumbnails and preview.&#x20;

---

## 4. Shared folder tree — Show / Hide

Make the folder tree collapsible in all three workspaces.

### Shared toggle button

Add one consistent button in the top application toolbar, immediately **to the left of the Photos / ID Photos / Collages workspace selector**.

This button should show or hide the folder tree.

Requirements:

- Use a recognizable sidebar/folder-navigation icon.
- The same button and behaviour must work in all three workspaces.
- When hidden, the main content should expand into the available space.
- When shown again, restore the previous folder-tree width where possible.
- Keep the current folder selection when hiding or showing the tree.

### Workspace-specific locations

Each workspace should remember its own current folder/location.

For example:

- Photos may be opened in Folder A.
- ID Photos may be opened in Folder B.
- Collages may be opened in Folder C.

Switching between workspaces should not unexpectedly change their selected folders.

However, the visual design and behaviour of the folder tree should be identical everywhere.

Reuse one shared explorer component rather than maintaining three separate implementations.

Keep the show tree pannel or hide for each workspace alone. For example: if I hide for collage tree view, in the photos, it should remain open.&#x20;

---

## 5. Photos — Safe photo deletion and restoration

Add the possibility to delete photographs directly from the Photos thumbnail gallery.

This must be implemented safely.

### Delete photograph

- Allow deleting a selected photograph using a right-click context menu.
- Support the Delete keyboard key where appropriate.
- Allow multiple selected photographs to be deleted.
- Always show a confirmation dialog before deleting.
- Clearly display how many photographs will be deleted.

### Soft deletion

Do not permanently delete the photograph immediately.

Instead, move it into the existing `Original` backup area and mark it as deleted.

The photograph should disappear from the normal gallery, but remain recoverable.

Important:

- Do not overwrite an existing original backup with the same filename.
- Preserve the original photograph and any existing backup safely.
- Keep enough information to restore the correct file to its previous location.
- Handle filename conflicts safely.
- Do not accidentally modify unrelated files already present in `Original`.

Use the existing file-tracking mechanism where possible. Introduce only the additional tracking necessary for safe deletion and restoration.

### New Deleted filter

The Photos gallery currently has:

- All
- To Do
- Done

Add:

- **Deleted**

The Deleted view should show photographs marked as deleted.

From this view, allow the user to:

- See deleted photographs as thumbnails.
- Select one or several photographs.
- Restore the selected photographs.

Restoring should return the photographs to their original folder and make them available in the normal gallery again.

Handle filename conflicts safely when restoring.

Deleted photographs should not appear in normal editing and printing selections until restored.

### Discard Originals — Entire folder

The existing **Discard Originals** action should apply to the **entire currently opened folder/order**, not only the selected photographs.

It should:

- Remove the original backups created by PhotoX for that folder.
- Keep all edited photographs in the main folder.
- Never delete unrelated files.
- Clearly warn the user before permanently deleting backups.

Since deleted photographs are being stored temporarily in the backup area, explain in the confirmation dialog that **Discard Originals will also permanently remove photographs currently marked as Deleted**, unless they are restored first.

The confirmation should be clear and difficult to misunderstand.

### File safety

PhotoX previously had a serious bug where finishing an order caused photographs to disappear.

Do not reintroduce similar behaviour.

Add automated tests for:

- Deleting one photograph.
- Deleting multiple photographs.
- Restoring deleted photographs.
- Existing original backups.
- Filename conflicts.
- Discard Originals for an entire folder.
- Preserving edited photographs.
- Preserving unrelated files.
- Reopening a folder with deleted photographs.

Use temporary test directories and copied images only.

**Data safety is more important than implementation simplicity or speed.**

---

## 6. GitHub — Check for Updates

The PhotoX GitHub repository is now **public**.

Implement a simple update mechanism in the application's Settings.

### Check for Updates button

Add a section showing:

- Current installed version.
- **Check for Updates** button.
- New version information when available.

When the button is pressed:

1. Check the latest stable PhotoX version published on GitHub Releases.
2. Compare it with the installed version.
3. If no update is available, show a simple confirmation.
4. If a newer version is available, display the version number and ask whether the user wants to update.
5. If confirmed, download the new Windows installer.
6. Start the installer and close PhotoX safely so the update can proceed.
7. Allow the installer to upgrade the existing installation.

The user should not need to manually open GitHub, download ZIP files or extract the installer.

### GitHub Releases

Currently, Windows installers are produced by GitHub Actions and downloaded from the Actions Artifacts section.

Keep the existing CI builds for development and testing.

In addition, use **GitHub Releases** for distributing approved versions.

Configure the existing GitHub Actions workflow so that approved version tags can produce a Release containing the Windows installer.

Use a consistent filename, including the version number.

Do not automatically publish every development commit as a stable release.

The updater should check stable releases, not unfinished development builds.

### Installation and safety

- Use the public GitHub Releases API without embedding authentication tokens.
- Download only the expected PhotoX installer asset from the configured repository.
- Verify the downloaded file's integrity where practical, preferably using a published checksum.
- Do not install incomplete or failed downloads.
- Do not attempt to replace the running executable directly.
- Preserve application settings, print history, preferences and customer photographs.
- Do not interrupt an active save or print operation without warning.
- Handle missing internet connections and download errors gracefully.
- Display download progress where practical.
- Keep the update process simple.

The installer is currently unsigned, so Windows may still display its SmartScreen warning. This is acceptable for now.

Do not attempt to bypass Windows security protections.

### Linux

Windows 11 remains the primary installation platform.

The automatic installer-based update mechanism only needs to work on Windows.

On Linux, a simple version check with a link to the GitHub Release is sufficient if that is easier.

Do not implement a complicated cross-platform updater.

---

## 7. General UI and performance

Keep the three main workspaces visually consistent:

- Photos
- ID Photos
- Collages

Use the same folder-tree styling, selection behaviour, folder icons and navigation conventions.

Keep existing editing, saving and printing functionality unchanged unless necessary for these improvements.

Pay attention to:

- Responsive folder navigation.
- Fast thumbnail loading.
- Correct selection highlighting.
- Smooth panel resizing.
- Proper layout when panels are collapsed.
- Consistent appearance in light and dark themes.
- No unnecessary duplication of controls.
- No overlapping or clipped UI elements.

Avoid unnecessary refactoring.

---

## 8. Implementation instructions

Continue working autonomously.

Suggested order:

1. Improve the shared file explorer.
2. Apply it to Photos, ID Photos and Collages.
3. Implement collapsible sidebars.
4. Add safe photo deletion, Deleted view and restoration.
5. Adjust Discard Originals to operate on the entire folder safely.
6. Implement GitHub Releases and Check for Updates.
7. Run regression tests and review UI consistency.

Use meaningful commits and increment the version according to the existing conventions.

Update the relevant documentation, including `docs/PLAN.md`, roadmap, changelog and implementation log.

Archive this prompt unchanged in the next numbered Markdown file under `docs/prompts/`.

Push commits and tags to GitHub where access is available.

After completing this feedback, continue with any remaining approved, feasible implementation from the original project plan.

Do not add unrelated features.

---

## 9. Final report

When finished, provide:

1. Summary of implemented changes.
2. Changes to the shared explorer and workspace layouts.
3. How photo deletion, restoration and Discard Originals work.
4. How GitHub Releases and the updater are implemented.
5. Automated test results.
6. Linux and Windows build/test results.
7. New version number and commit hashes.
8. Remaining limitations or open points.
9. A short Windows 11 manual testing checklist.

Only describe functionality as verified when it has actually been tested.

---

## Overall objective

PhotoX should provide a familiar and efficient way to navigate customer photo folders in all three workspaces.

The Photos workspace should support safe photo management, including deletion and restoration.

ID Photos and Collages should benefit from the same explorer without losing their existing specialized workflows.

The GitHub updater should make installing approved new versions as easy as possible for the studio operator.

**Implement these improvements, keep the application simple and responsive, test carefully, and continue autonomously with the remaining approved project work.**
