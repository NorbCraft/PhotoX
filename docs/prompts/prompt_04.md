# PhotoX — Next Implementation Prompt

Please continue implementing PhotoX based on the current repository, existing plan, and all approved functionality so far.

This prompt contains new requirements, fixes, UI refinements and one new workspace.

Implement them in a practical way, without overengineering. Reuse the current architecture where possible.

Work autonomously, make meaningful commits, run tests, update documentation where needed, and increment the version according to the existing versioning rules.

## General instructions

- Continue from the current implementation and repository state.
- Keep the application simple, practical, modern and easy to use.
- Do not overengineer or redesign the whole app unless truly necessary.
- Keep direct Citizen printer integration deferred for now.
- If something below conflicts with an older requirement, this prompt takes precedence.
- After implementing these changes, continue with any remaining approved implementation from the original plan that is still missing and feasible.

---

## 1. Version visibility and installer

- Always include the **version number in the installer**.
- The **version number should also be visible in the Windows title bar** of the app.
- Keep versioning consistent with the existing rules.

---

## 2. New icon / logo

Replace the current PhotoX icon/logo.

### Create the new icon from scratch

- Flat icon style only.
- No 3D effects, glossy effects or unnecessary shadows.
- Modern, clean and simple.
- Dark navy rounded-square background.
- White camera outline, simple and clean.
- Blue / blue-purple circular lens ring inside the camera.
- Bold white **PX** centered inside the lens/camera area.
- Subtle dual-tone blue accents are acceptable.
- No red recording/status point.
- Keep the geometry balanced, with clean and consistent line thickness.
- Ensure the icon remains recognizable at small sizes.

Recreate the agreed dual-tone camera/lens concept as a clean vector icon.

### Use the icon in:

- Application icon.
- Window title bar.
- Windows taskbar and executable.
- Windows installer.
- Linux desktop icon.
- Application toolbar/workspace area, where **Photos** and **ID Photos** are shown.

Create and retain an SVG master, PNG exports and a multi-resolution Windows ICO file.

---

## 3. Toolbar and small UI fixes

- Add the new PhotoX icon to the top toolbar/workspace area.
- The **Settings icon currently has a display issue**. Correct it so the icon is clean and sharp.
- Remove the descriptive text under **Start Editing** that says something like "opens the first photo to do".
- Change **Finish Order...** to simply **Finish**.

---

## 4. Duplicate image

Add a **Duplicate Image** function to the right-click context menu.

- Duplicate the selected photograph in the current folder.
- Use a sensible filename, such as `filename_copy.jpg` or `filename_1.jpg`.
- Never overwrite an existing file.
- Refresh the gallery to display the duplicated photograph.

---

## 5. New workspace — Collages

Add a third workspace named **Collages**.

The objective is to create simple photo collages and save them as print-ready images.

### Supported sheet sizes

- 10×15 cm
- 15×20 cm
- 20×30 cm

### Layouts

Use a **template-based workflow with adjustments**, not a fully free-form design editor.

Provide several common layouts for 10×15 cm and 15×20 cm, and basic templates for 20×30 cm.

Examples include:

- Two photographs side by side.
- Two photographs stacked.
- Three-photo arrangements.
- Four-photo grids.
- One large photograph with two or three smaller photographs.
- Mixed portrait and landscape arrangements.

The templates should use as much of the available sheet area as practical.

Different photo sizes and orientations should be possible on the same collage.

### Editing

Allow the user to:

- Choose a template.
- Add or replace photographs.
- Rearrange photographs between template slots.
- Adjust the crop and positioning of each photograph inside its slot.
- Preview the entire collage.
- Save the finished collage as a high-quality print-ready image.

Keep the workflow simple and intuitive.

---

## 6. Shared file explorer

Make the existing file explorer useful for all three workspaces:

- Photos
- ID Photos
- Collages

In particular, **ID Photos should use the built-in explorer to open a picture**, instead of requiring a separate workflow.

Reuse the explorer rather than implementing three different file browsers.

---

## 7. Normal photo editor

### Keyboard shortcuts

- Keyboard `+` and `-` should adjust exposure.

### Reset

- Provide only one button named **Reset** for image adjustments.
- Reset should clear exposure, contrast, saturation, white balance, tint, histogram/levels and other colour adjustments.
- **Reset must not reset the crop.**

### Toolbar icons

Slightly enlarge:

- Portrait orientation icon.
- Landscape orientation icon.
- Back button.

### Crop orientation

When switching between portrait and landscape:

- Reset the crop to the largest valid crop for the newly selected orientation.
- Do not retain a smaller crop area inherited from the previous orientation.
- Switching back should also restore the largest valid crop for that orientation.

### Reset crop icon

Replace the current crop-reset icon with a clearer icon showing **four arrows pointing toward the corners**.

### Angle display

- Correct the placement of the angle display.
- Center it properly and make the layout more visually balanced.

### Exposure slider

- Remove the `+` and `−` buttons beside the exposure slider.
- Keep keyboard shortcuts for exposure adjustments.

### Slider handles

- Slightly enlarge the slider handles so they are easier to grab and drag.

### Save & Next

- Remove the descriptive text underneath the Save & Next button.
- Keep the button clean and simple.

### Custom crop sizes

A custom crop must preserve the exact requested physical dimensions and be placed on the smallest supported print canvas that fits.

Examples:

- **9×13 cm** photograph centered on a **10×15 cm** canvas.
- **14×17 cm** photograph centered on a **15×20 cm** canvas.

The image should not be enlarged unnecessarily to fill the sheet.

Use a white background around the photograph.

The saved result should have the physical dimensions and aspect ratio of the selected print sheet.

### Before / After

The spacebar behaviour is already correct: the original appears only while Space is held down.

Make the Before/After button behave the same way:

- Show the original only while the button is pressed.
- Release the button to return to the edited preview.
- Do not make it a toggle.

### Histogram

Review the current histogram implementation.

The image adjustments appear to work correctly, but the displayed histogram looks slightly inaccurate.

Check whether the histogram is calculated and displayed correctly, including the relationship between the image data, histogram and adjustment handles.

Correct any issues found without unnecessarily redesigning the control.

### Close button

When all photographs are edited:

- Keep only **Close** as the button label.
- Preserve Enter as the keyboard shortcut.
- Remove unnecessary descriptive text.

---

## 8. ID Photos workspace

### Auto Adjust

Add an **Auto Adjust** button, using the existing image-correction functionality where practical.

### Romanian 3×4 alignment

Adjust the Romanian 3×4 ID photo framing:

- The person/head should appear slightly smaller than in the current template.
- Keep the upper alignment line at its current position.
- The head does not need to be perfectly vertically centered.
- The objective is a more visually balanced composition.

Preserve manual adjustment after automatic alignment.

### Rotation and zoom

- Double-clicking the rotation slider should reset rotation.
- Double-clicking the zoom slider should reset zoom.

### Before / After

For ID Photos, use only the **Space** keyboard shortcut for Before/After.

Remove the additional button.

### Continue button

Change **Continue to sheet** to **Continue**.

### Workflow steps

- Make **Framing & Adjustment** and **Print Sheet** slightly larger and easier to recognize.
- Remove the unnecessary dash between them.

### Save format

- Save ID photo sheets as **JPG only**.
- Remove the PNG export option.

### Crop ratio

The crop selection must reflect the selected ID photograph dimensions.

Currently the crop appears to have the same proportions for different formats.

For example:

- 3×4 cm should display a 3:4 crop.
- 3.5×4.5 cm should display its correct ratio.
- **2×2 inches must display a square crop.**

The 2×2-inch preset is intended for US visa photographs.

### 9×12 police photograph

- Keep the 9×12 cm preset.
- Remove the face/body alignment grids for this format.
- Allow normal manual positioning and cropping.

### 2×2-inch layout

Check whether four 2×2-inch photographs can fit on one nominal 10×15 cm Citizen print sheet.

Use the actual physical dimensions rather than approximate proportions.

If four photographs fit correctly, implement that layout.

Do not stretch or change the individual photo dimensions to force an arrangement.

### Custom ID sizes

- Allow users to add custom ID photo dimensions using a **+ New Size** button.
- Allow previously added custom sizes to be removed.
- Display dimensions in centimetres.
- Do not show pixel dimensions in the size-selection interface.
- Keep built-in presets separate from removable custom presets.

---

## 9. Performance and consistency

Keep the application responsive on both Linux and Windows.

Reuse existing UI components and image-processing functionality where practical.

Pay attention to preview performance when changing crop positions, moving collage photographs or adjusting sliders.

Do not reduce final image quality to improve preview responsiveness.

Make sure the new Collages workspace follows the same design language as Photos and ID Photos.

Avoid unnecessary settings, dialogs or complicated controls.

---

## 10. Implementation workflow

Implement the requested changes autonomously.

- Inspect the current implementation before changing it.
- Prefer targeted improvements.
- Preserve existing working functionality.
- Add or update relevant automated tests.
- Run the test suite.
- Update documentation where necessary.
- Make meaningful Git commits.
- Increment versions using the existing rules.
- Push changes and tags to GitHub where access permits.
- Archive this prompt unchanged as the next numbered Markdown file under `docs/prompts/`.

Keep `docs/PLAN.md` as the authoritative source of truth.

After completing this feedback, review the original approved roadmap and continue with any unfinished functionality that is feasible without further input.

Keep Citizen printer integration and physical calibration deferred.

Do not add unrelated features or overengineer the application.

---

## 11. Final report

When finished, provide a concise report containing:

1. Implemented features and UI improvements.
2. Current version number.
3. Important commit hashes.
4. Automated test results.
5. Linux and Windows build/test status.
6. Remaining limitations and open points.
7. A short Windows 11 manual-testing checklist.

Only report something as verified if it was actually tested.

---

## Overall objective

PhotoX should remain a **simple, modern, fast and reliable photo-editing application** for a small professional photo studio.

The three workflows should be:

**Photos:** Open folder → Crop/Edit → Save & Next → Finish.

**ID Photos:** Select portrait → Align/Edit → Choose layout → Save JPG.

**Collages:** Select photos → Choose template → Arrange → Save JPG.

Prioritize ease of use, accurate image processing, reliable saving and a consistent interface.

Implement these changes and continue autonomously with the remaining approved project work.