# PhotoX — Next Implementation Iteration: Editing, Printing and UI Improvements

Continue developing PhotoX using the existing repository, approved plan and current implementation.

This prompt contains bug fixes, workflow improvements, printing adjustments and UI refinements.

Keep the application simple, modern, responsive and reliable. Avoid overengineering and unnecessary architectural changes.

Implement the requested changes autonomously, test them, make meaningful commits, update the version number and maintain the relevant documentation.

After completing these changes, review the original approved plan and continue with any remaining feasible functionality.

---

## 1. Photos — Editing

### 1.1 New crop format: 9×13 cm

Add **9×13 cm** to the available crop presets.

- Crop the photograph to the correct 9:13 aspect ratio.
- Place the resulting 9×13 cm image centered on a 10×15 cm white print canvas.
- Preserve the exact intended physical dimensions.
- The final output should be suitable for printing on 10×15 cm paper.

Reuse the existing custom-size-on-print-sheet functionality where practical.

### 1.2 Rename crop options

Simplify the names:

| Current         | English | Romanian |
| --------------- | ------- | -------- |
| Crop to Fill    | Crop    | Decupare |
| Whole + Borders | Whole   | Întreagă |

Keep the existing functionality.

### 1.3 Simplify navigation buttons

Remove the separate **Previous** and **Next** buttons from the photo editor.

Keep **Save & Next** as the main action for sequential editing.

### 1.4 Improve saving performance after rotation

Currently, cropping and saving a photograph after it has been rotated takes too long.

Investigate the performance bottleneck and optimize it.

- Avoid unnecessary repeated transformations.
- Avoid full-resolution processing until required for saving.
- Preserve output quality.
- Keep the UI responsive while saving.
- Measure the saving time before and after optimization.

### 1.5 Editing action buttons

Reorganize the editing actions:

- **Save & Next** — keep this as the primary blue button.
- **Save** — save changes without automatically moving to the next photograph.
- **Close** — close the editor and return to the gallery, equivalent to Escape or the Back button.
- **Print** — print the currently edited photograph.
- **Reset** - will also reset the crop setings. 

Keep these actions visually organized without overcrowding the interface.

Close must handle unsaved changes consistently with the existing Escape/Back behaviour.

When no further photographs remain, preserve the existing sensible Close behaviour.

Remove unnecessary descriptive text under the buttons.

### 1.6 Critical bug — Images partially black after editing

I discovered an intermittent issue after editing photographs.

**Observed behaviour:**

- A photograph is edited and saved.
- In the thumbnail gallery, only approximately 1/8 of the photograph is visible.
- The rest of the thumbnail is black.
- If I reopen the photograph in the normal Photo Editor, the whole image appears correctly.
- However, when using the same photograph in **ID Photos** or **Collages**, it appears mostly black, just like the thumbnail. Also when opening the print, the black picture is shown.
- It was observed just on Windows, here in linux after trying multiple pictures, no issue was observed, but on windows it came multiple times.
- to test this issue, and if needed for any other purposes, there are some example pictured that you can take: /home/norbert/Documents/PhotoX/photos_example

It does not happen with every photograph, apparently only sporadically.

I have created a folder containing example photographs to help reproduce the issue. Locate and inspect these samples where available.

Please investigate the root cause, including:

- Saved image dimensions and pixel data.
- Rotation and crop transformations.
- Thumbnail decoding.
- Image loading differences between workspaces.
- Possible image stride, buffer, bitmap or rendering problems.
- Differences between the Photos editor pipeline and the ID/Collage image loaders.

Do not merely fix the thumbnail display if the underlying image handling is incorrect.

Make sure the same saved photograph appears correctly in all three workspaces and in external image viewers.

Use copies of the test photographs. Do not modify or delete the original samples.

Add regression tests covering the actual cause once identified.

**This bug is a high priority.**

---

## 2. ID Photos

### 2.1 Remove Background Brightness

Remove the **Background Brightness** function entirely from the ID Photo editing interface.

I no longer intend to use it.

Keep the other image corrections and Auto Adjust functionality.

Remove unused code only where it is safe and straightforward.

### 2.2 Workflow step buttons

Move **Framing & Adjustments** and **Print** back to their previous position above the image toolbar.

They should not stretch across the full application width as they currently do.

Keep them compact, clearly visible and aligned with the photograph editing area.

### 2.3 Clear current photograph

The Collages workspace already has a **Clear All** button.

Add a similar **Clear** button to ID Photos.

It should:

- Remove the currently loaded photograph from the ID Photo workspace.
- Clear its current framing and editing state.
- Return to an empty state ready for a new photograph.
- Not delete or modify the original file.

Place the button somewhere intuitive without cluttering the interface.

---

## 3. Printing Workspace

### 3.1 Hide statistics during printing

When Print is pressed from Photos, ID Photos or Collages, PhotoX opens the Printing workspace.

In this situation, **hide the statistics section**.

Customers may occasionally see this screen, and they do not need to see the studio's printing statistics.

The printing options and print queue should remain visible.

The print history may remain accessible, as it is not a major concern.

Keep statistics accessible separately when the operator wants to review them, for example through a dedicated collapsible section or another simple interface.

### 3.2 All-time statistics

In the All Time statistics, show the starting date.

For example:

**All time — since 01.11.2026**

Use the actual date of the first recorded print job, not a hardcoded date.

### 3.3 Automatic paper-size selection

I cannot test the Citizen printer directly on my development computer.

My only available printer is currently **Microsoft Print to PDF**, which does not offer the expected Citizen paper sizes.

Current behaviour:

- Selecting 10×15 cm appears to choose the North American 4×6 format.
- Selecting 15×20 cm does not select a corresponding paper size.

Please investigate and improve this.

**Expected behaviour:**

When I select 10×15 or 15×20 in PhotoX, the application should automatically select the corresponding supported paper size in the Windows printer driver.

For the Citizen CY-02:

- 10×15 cm corresponds approximately to 101×152 mm / 4×6 inches.
- 15×20 cm corresponds approximately to 152×203 mm / 6×8 inches.

Use the actual paper sizes supported by the installed printer driver rather than assuming every printer uses the same names or paper identifiers.

Important:

- Determine the paper-size options reported by the selected printer.
- Match the selected PhotoX format to the correct driver paper size.
- Support different driver names for equivalent sizes.
- Do not silently use the wrong paper size.
- If the printer does not support the selected format, display a clear message.
- Avoid unnecessary manual configuration when the correct size can be selected automatically.

Microsoft Print to PDF does not need to expose the Citizen's custom paper sizes. Do not add complicated workarounds solely for this virtual printer.

Keep physical printer calibration as an open point until the Citizen is available for testing.

### 3.4 Calibration sheet — return to previous print selection

The calibration sheet is useful, but currently pressing it can replace the photographs that were prepared for printing.

Add a simple way to return.

Expected behaviour:

1. I have photographs ready to print.
2. I open the calibration sheet.
3. I can press **Back** or **Cancel Calibration**.
4. The previous photographs and print settings are restored.

Opening the calibration sheet should not permanently discard the existing print selection.

Keep this functionality simple.

### 3.5 Crop or Whole when printing ordinary photographs

Add a printing option for photographs coming from the Photos workspace:

**[ Crop | Whole ]**

This is important when photographs have not yet been edited or cropped for the selected print size.

#### Crop — default

- Fill the selected print format.
- Preserve the photograph's proportions.
- Crop excess areas as necessary.
- Center the crop by default.
- Avoid stretching or distortion.

#### Whole

- Display the complete photograph.
- Preserve its proportions.
- Add white borders where necessary.
- Do not crop away image content.

**Crop must be the default every time a new printing operation is started from Photos.**

Whole should remain available as an alternative.

For ID Photos and Collages, the print sheet size is already determined by the template. Do not apply an additional automatic crop that could change the carefully prepared layout or physical dimensions.

### 3.6 Printing photographs with unsaved edits

Clarify and implement consistent behaviour for printing a photograph that has an active crop or other editing adjustments but has not yet been saved.

**Expected behaviour: print the current edited version, even if it has not been saved.**

For example:

1. I open a photograph.
2. I adjust exposure and select a crop.
3. I press Print without saving.
4. PhotoX prints the edited photograph with the selected crop and adjustments.
5. The original photograph on disk remains unchanged.

The print preview must show exactly what will be printed.

Important details:

- Use the current in-memory editing state.
- Include unsaved crop, rotation and colour adjustments.
- Do not automatically save or overwrite the photograph when printing.
- Do not use an older saved version if a newer unsaved edit is active.
- If a manual crop is already selected, preserve that crop when preparing the print instead of silently replacing it with a new automatic crop.
- Keep the user able to choose Crop or Whole for photographs without an explicit manual crop.
- If printing fails or is canceled, preserve the current editing state.

Use the same high-quality rendering logic as saving, where practical.

### 3.7 Escape returns to previous workspace

When Print is pressed, PhotoX opens the Printing workspace.

Pressing **Escape** should return to the workspace from which the user started printing.

Examples:

- Photos → Print → Escape → Photos
- ID Photos → Print → Escape → ID Photos
- Collages → Print → Escape → Collages

Preserve the previous workspace state when returning, including current photo selection or editing state.

Escape should not delete the prepared print job or editing work unexpectedly.

### 3.8 Move Printing out of the normal workspace group

Currently, Printing appears together with Photos, ID Photos and Collages.

Change the upper navigation layout.

On the left, keep the main editing workspaces:

- Photos
- ID Photos
- Collages

Move **Printing** to the right side of the toolbar, immediately to the left of the EN/RO language selector.

Printing should remain accessible at any time, but visually separate from the three creation/editing workspaces.

Keep the active navigation state clear.

---

## 4. Collages

### 4.1 Save button text

Change:

**Save JPG → Save**

Keep the existing JPG saving functionality.

No other Collage changes are required in this iteration unless necessary to fix shared functionality or bugs.

---

## 5. General Appearance and Settings

### 5.1 Default theme

Make **Light Mode** the default theme for new installations or when no theme preference has been saved.

Keep Dark Mode available in Settings.

Respect an existing user-selected theme when upgrading, rather than unexpectedly resetting it.

### 5.2 UI consistency

Keep controls consistent across workspaces.

Do not introduce new styles unnecessarily.

Make sure the printing and editing changes fit into the existing PhotoX interface.

---

## 6. UI Mockup Task — Do Not Implement Yet

As a separate design task, evaluate whether some text buttons could be replaced with icons or a combination of icon and text.

Examples include:

- Save
- Save & Next
- Close
- Back
- Print
- Reset
- Other frequently used controls

The goal is a cleaner, more intuitive interface without making functionality harder to understand.

### Required deliverable

Create several **actual visual mockups** showing possible alternatives.

For example:

- Current text-based buttons.
- Icon + text combinations.
- Minimal icon-only controls where the meaning is sufficiently clear.

Focus on the Photos editing toolbar and action buttons, but consider other workspaces where useful.

Keep the proposals modern, consistent and practical.

**Do not implement these button/icon redesigns yet.**

This task is only to generate design examples for my review.

Save the mockup images in the repository documentation and briefly explain the differences.

I will select the preferred design or provide further feedback in a future prompt.

---

## 7. Development and Testing

Continue implementation autonomously.

Suggested priorities:

1. Investigate and fix the partially black photograph bug.
2. Optimize rotated-image cropping and saving.
3. Improve the Photos editor actions and crop presets.
4. Simplify ID Photos.
5. Improve printing behaviour, paper-size selection and workspace navigation.
6. Apply remaining UI adjustments.
7. Create the button/icon mockups without implementing their proposed redesigns.
8. Perform regression testing and final verification.

### Testing

Pay particular attention to:

- Image decoding and saving across workspaces.
- Rotated and cropped photographs.
- Thumbnail generation.
- Unsaved edits being printed correctly.
- Printing without modifying source files.
- Crop versus Whole printing.
- Correct print format selection.
- Restoring the previous print selection after calibration.
- Escape navigation.
- ID Photo clearing.
- Correct saving and backup behaviour.

Run existing automated tests and add targeted regression tests.

Use disposable copies of photographs for any tests that modify files.

Test on Linux and Windows where possible, but do not claim physical Citizen printing has been verified until the hardware is available.

---

## 8. Documentation, Versioning and Commits

Follow the existing project conventions:

- Continue using the current GitHub repository.
- Archive this prompt unchanged in the next numbered Markdown file under `docs/prompts/`.
- Update `docs/PLAN.md` where requirements change.
- Update the roadmap, implementation log, architecture and changelog where relevant.
- Make meaningful Git commits.
- Increment the version according to the existing rules.
- Push commits and tags where access is available.
- Keep documentation concise and consistent.

After implementing these changes, continue with remaining approved and feasible functionality from the original plan.

Do not add unrelated features or overengineer the project.

---

## 9. Final Report

When finished, provide a concise summary covering:

1. Changes implemented.
2. Root cause and fix for the partially black image bug.
3. Performance improvements for rotated-image saving, with measurements.
4. Changes to printing and paper-size handling.
5. How unsaved edits are handled when printing.
6. Automated test results.
7. Linux and Windows build status.
8. Version number and relevant commits.
9. Remaining open points, particularly physical Citizen calibration.
10. Location of the new UI button/icon mockup images.
11. A short Windows 11 manual-testing checklist.

Only describe functionality as verified when it has actually been tested.

---

## Overall Objective

PhotoX should remain a simple, fast and reliable application for a small photo studio.

The most important improvements in this iteration are:

- Correct image rendering across every workspace.
- Faster and smoother photo editing.
- More intuitive Save, Close and Print actions.
- Correct printing of unsaved edits.
- Reliable print sizing through the Windows driver.
- Clear navigation between editing and printing.
- Consistent UI without unnecessary controls.

**Implement the requested changes, test them thoroughly, create the UI mockups for later review, and continue autonomously with the remaining approved project work.**
