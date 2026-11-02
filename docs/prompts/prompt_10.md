# PhotoX — Keyboard Workflow, Navigation, Print Preview and Release Improvements

The update to **PhotoX v0.12.1** worked well.

I have some additional improvements and corrections to implement.

Continue using the existing architecture and project plan. Keep the application simple, fast, responsive and intuitive. Avoid unnecessary complexity or refactoring.

Implement the requested changes autonomously, test them, make meaningful commits, update documentation and increment the version number.

**Important: After successful implementation and testing, publish a new stable GitHub Release so I can update PhotoX using its built-in update function.**

---

## 1. Photos — Editing and keyboard navigation

### Bring back Previous and Next buttons

Reintroduce the **Previous** and **Next** buttons in the photo editor.

Keep the existing Save, Save & Next, Close and Print functionality.

### Improve keyboard navigation

Currently, both the left/right arrow keys and Page Up/Page Down navigate between photographs.

Change this behaviour.

| Key | Action |
|---|---|
| Page Up | Previous photograph |
| Page Down | Next photograph |
| ← | Move selected crop left |
| → | Move selected crop right |
| ↑ | Move selected crop up |
| ↓ | Move selected crop down |
| Enter | Apply crop, save and open next photograph |
| Escape | Close editor / return to gallery |

The objective is to make basic photo editing possible almost entirely using the keyboard.

### Crop movement with arrow keys

When the crop tool is active:

- Arrow keys should move the selected crop area.
- Preserve the selected crop dimensions and aspect ratio.
- Movement should be small and precise.
- Respect image boundaries.
- Move only in directions where movement is possible.

For example, a portrait photograph cropped to 10×15 may need only small horizontal corrections, while a landscape photograph may need vertical corrections.

The operator should be able to:

1. Open the photograph.
2. Adjust the crop position using arrow keys.
3. Press Enter.
4. Have the crop applied and the photograph saved.
5. Automatically continue to the next photograph.

This should be smooth and fast.

Keep Page Up/Page Down exclusively for photo navigation while in the editor.

Keyboard shortcuts must not interfere with text input, dialogs or other controls that legitimately need those keys.

---

## 2. Crop information tooltip

Currently, the hovering information displays:

- Physical size in cm.
- Pixel dimensions.
- PPI.

Simplify it.

Display only:

- **Size in cm**
- **PPI**

Remove the pixel dimensions.

Also make the tooltip background slightly more transparent so it is less visually intrusive over the photograph.

Keep the information readable in both light and dark modes.

---

## 3. Before / After button

The current Before/After function works correctly with the Space key.

However, when Space is pressed, the corresponding button does not visually show its active state.

Make it consistent:

- Holding Space displays the original photograph.
- The Before/After button should look pressed/active while Space is held.
- Releasing Space restores the edited preview and normal button appearance.
- Holding the button with the mouse should produce the same behaviour.

The keyboard and mouse interactions should always reflect the same state.

---

## 4. Delete confirmation and gallery behaviour

### Keyboard handling in the Delete dialog

When the user presses Delete in the thumbnail gallery, the confirmation dialog opens.

Improve keyboard handling:

- **Escape:** Cancel deletion and close the dialog.
- **Left/Right arrows:** Switch between Delete and Cancel.
- **Enter:** Activate the currently focused button.

Currently, the arrow keys still move the photograph selection behind the confirmation dialog.

Fix this. While the dialog is open, keyboard input must belong to the dialog, not the gallery.

Keep Cancel as the safe default.

### Preserve scroll position after deletion

Currently, after deleting a photograph, the gallery jumps back to the top of the folder.

This is inconvenient when working with hundreds of photographs.

Expected behaviour:

- Keep the current scroll position.
- Remove the deleted photograph from the gallery.
- Keep the surrounding photographs visible.
- Maintain a sensible selection on a nearby photograph.

Do not unnecessarily reload or reset the entire gallery.

---

## 5. Preserve the correct selected photograph after editing

There is another navigation issue.

Example:

1. Select photograph 20 in the gallery.
2. Open the editor.
3. Navigate to photographs 21, 22 and 23.
4. Press Escape or Back to return to the gallery.

Currently, the selection remains on photograph 20.

Expected behaviour:

- The gallery should select the photograph that was last open in the editor.
- In this example, photograph 23 should be selected.
- The gallery should scroll to that photograph if necessary.
- Preserve the gallery's general scroll position where practical.

This should work whether navigating using Page Up/Page Down or the Previous/Next buttons.

---

## 6. Printing — Show all selected photographs

Currently, when selecting many photographs for printing, the Print workspace displays only the first 20 photographs as previews.

The remaining photographs appear only as small text entries.

Change this.

### Show all photographs

- Display all photographs included in the print selection as thumbnails.
- Make the thumbnail area scrollable.
- Do not limit visual previews to 20 photographs.
- Keep the interface responsive for large selections containing hundreds of photos.
- Use the existing thumbnail loading and virtualization mechanisms where practical.

### Adjust quantities in Print Preview

Allow changing the requested number of copies directly in the printing interface, even after entering it from the Photos workspace.

Requirements:

- Select a photograph in the print preview.
- See its requested number of copies.
- Increase or decrease the quantity.
- Allow quantity 0 to exclude the photograph.
- Update the total number of copies immediately.
- Keep the print preview and actual print queue synchronized.

### Multiple selection

Also support:

- Selecting multiple photographs.
- Ctrl/Shift selection where appropriate.
- Select All.
- Changing the requested quantity for all selected photographs simultaneously.

Reuse the existing quantity controls and logic instead of implementing a separate system.

Changes made in the printing interface should remain consistent with the quantities in the Photos workspace.

Do not accidentally multiply individual quantities through an additional global copies setting.

Keep the print preview simple and practical.

---

## 7. Photos gallery — Keyboard navigation

Currently, the thumbnail gallery allows keyboard navigation using the left and right arrow keys.

Extend this to all four arrow keys:

- Left: Previous thumbnail in the row.
- Right: Next thumbnail in the row.
- Up: Thumbnail in the row above.
- Down: Thumbnail in the row below.

Navigation should follow the visible thumbnail grid.

When the number of columns changes because of window resizing or thumbnail scaling, adapt the navigation accordingly.

Keep the selected photograph visible by scrolling when necessary.

Do not interfere with keyboard handling in confirmation dialogs.

---

## 8. Thumbnail size controls

The current thumbnail-size control could be improved.

### Icons

Replace or refine the thumbnail-size icons so their function is easier to understand.

Use simple, clear icons consistent with PhotoX.

### Clickable size controls

In addition to the existing slider, make the increase/decrease controls clickable.

**Increase (+):**

- Increase thumbnail size.
- Ideally adjust the size so that approximately one fewer column fits in the currently available gallery width.

**Decrease (−):**

- Decrease thumbnail size.
- Ideally adjust the size so that approximately one additional column fits in the gallery.

For example, if the gallery currently displays four columns:

- Clicking + should enlarge the thumbnails to approximately three columns.
- Clicking − should reduce them to approximately five columns.

Calculate the sizes based on the actual available gallery width, spacing and current window dimensions.

Keep reasonable minimum and maximum thumbnail sizes.

Preserve the existing slider functionality.

Double-clicking the slider should still restore the default thumbnail size.

Ensure that selection, scrolling and thumbnail loading remain smooth when changing size.

---

## 9. ID Photos — Escape clears the current photograph

In the ID Photos workspace, when a photograph is loaded, pressing Escape should perform the same action as the existing **Clear** button.

Expected behaviour:

- Remove the currently loaded photograph from the ID Photo workspace.
- Clear its framing and adjustment state.
- Return to the empty photo-selection view.
- Do not delete or modify the source photograph.

Reuse the existing Clear functionality.

If a confirmation dialog is open, Escape should close that dialog first rather than clearing the photograph.

---

## 10. Simplify Windows build artifacts

The current GitHub Actions build produces a relatively large artifact archive, approximately **200 MB**.

It also includes multiple distribution formats, such as the installer and portable version.

I no longer need all these alternatives.

### Preferred distribution

Produce **one Windows installer `.exe`** as the main downloadable application.

Requirements:

- Keep the existing working Windows installer.
- Include the current PhotoX version number in its filename.
- Do not generate a separate portable distribution unless technically necessary.
- Avoid uploading duplicate Windows application packages.
- Prefer making the installer `.exe` directly available as a GitHub Release asset.
- If GitHub Actions requires an artifact ZIP, that is acceptable, but it should contain only the necessary installer, not several duplicate distribution formats.

### Reduce artifact size where practical

Investigate whether the build can be made smaller without compromising functionality.

Remove unnecessary duplicated distribution files, symbols or development-only output from the release package.

Do not remove runtime dependencies required by PhotoX.

Do not sacrifice application reliability, image processing, face detection or installation compatibility just to reduce file size.

A single installer containing all necessary dependencies is preferable to several smaller packages.

Keep the Windows installation and upgrade process working as it does now.

---

## 11. Publish a GitHub Release after implementation

**This is an important requirement for this iteration.**

After all changes are implemented and the appropriate automated tests and build checks pass:

1. Increment the PhotoX version using the existing versioning rules.
2. Update the changelog and relevant documentation.
3. Commit the completed implementation.
4. Push the commits to GitHub.
5. Create and push the corresponding version tag.
6. Build the Windows installer.
7. Publish a new stable GitHub Release.
8. Attach the Windows installer `.exe` to that release.
9. Include a short summary of changes in the release notes.

The GitHub Release should be compatible with the existing PhotoX **Check for Updates** functionality.

The installed application should detect this new stable version, offer to download it and allow updating without manually downloading Actions artifacts.

Do not publish an incomplete or untested implementation as a stable release.

If the release process fails, clearly report the problem rather than claiming it was published.

Continue keeping development builds in GitHub Actions if useful, but the stable installer should be distributed through GitHub Releases.

The installer remains unsigned for now; do not attempt to bypass Windows SmartScreen.

---

## 12. Implementation and testing

Continue implementation autonomously.

Keep the existing architecture and reuse working components where possible.

Pay particular attention to:

- Correct keyboard navigation.
- Crop movement and boundaries.
- Save & Next.
- Delete-dialog keyboard focus.
- Gallery selection and scroll restoration.
- All selected photos appearing in print preview.
- Correct per-photo and bulk printing quantities.
- Responsive thumbnail resizing.
- ID Photo Escape behaviour.
- Windows installer compatibility.
- GitHub Releases and updater compatibility.

Add or update appropriate automated tests.

Run the full relevant test suite.

Test on Linux and Windows where possible, clearly identifying anything not verified on Windows.

Avoid introducing unrelated functionality or unnecessary refactoring.

---

## 13. Documentation and development workflow

Follow the existing PhotoX conventions:

- Archive this prompt unchanged under `docs/prompts/`.
- Keep `docs/PLAN.md` as the source of truth.
- Update the roadmap, implementation log and changelog where relevant.
- Make meaningful Git commits.
- Increment the version correctly.
- Push changes to GitHub.
- Publish the stable Release after successful completion.

After completing these improvements, review the original approved plan and continue with any remaining feasible, approved functionality.

Do not introduce new features outside the approved scope.

---

## 14. Final report

When finished, provide:

1. Summary of the implemented changes.
2. Keyboard shortcuts and navigation behaviour.
3. Improvements to printing previews and quantity editing.
4. Thumbnail gallery improvements.
5. Automated test results.
6. Linux and Windows build status.
7. Performance or responsiveness improvements.
8. New PhotoX version number.
9. Important commit hashes.
10. GitHub Release link and publication status.
11. Windows installer filename and approximate size.
12. Remaining limitations or open points.
13. A short Windows 11 manual testing checklist.

Only report functionality as verified when it has actually been tested.

---

## Overall objective

PhotoX should support a fast, keyboard-friendly photo-editing workflow suitable for processing hundreds of photographs.

The main workflow should be:

**Open photo → Adjust crop with arrow keys → Enter to Save & Next → Repeat.**

The gallery should preserve its position and selection during navigation and deletion.

The printing interface should display every selected photograph and allow adjusting quantities before printing.

Finally, distributing new PhotoX versions should become simple:

**Complete implementation → Test → Publish GitHub Release → Update directly from PhotoX.**

Keep everything practical, responsive and reliable without unnecessary complexity.