# PhotoX — Direct Printing and Workflow Improvements

Continue developing PhotoX using the existing repository, architecture and approved project plan.

This iteration focuses on integrating direct printing through the Citizen CY-02, simplifying the photo workflow, improving the workspace navigation and extending the Collage Creator.

Keep the application simple, responsive and intuitive. Avoid overengineering or adding unnecessary functionality.

Implement the changes autonomously, test them, make meaningful commits, increment the version and update the relevant documentation. Afterward, continue with any remaining feasible functionality from the original approved plan.

## 1. General UI improvements

### Application logo and name

Move the PhotoX branding to the **right side of the upper toolbar, immediately to the right of the Settings button**.

The branding should contain:

- The existing PhotoX logo/icon, slightly larger than currently displayed.
- The text **PhotoX** directly beside the logo.
- Use clean, modern typography consistent with the application.
- The letters **Photo** should use the normal text colour.
- The letter **X** should use the blue/blue-purple colours of the lens in the PhotoX logo.
- Keep the icon and text vertically aligned and visually balanced.

Do not display the logo on the left side of the workspace selector anymore.

The workspace navigation should remain on the left, while the Settings button and PhotoX branding are positioned on the right.

### Workspace selector

Improve the visual appearance of the workspace selector.

- Make the currently selected workspace more obvious.
- Use blue highlighting, consistent with the application's other selected buttons.
- Keep the workspaces clearly distinguishable:
  - Photos
  - ID Photos
  - Collages
- Maintain the modern, simple appearance.

### File explorer text

Remove the unnecessary explanatory text in the file explorer that says something like:

**"Originals are kept until..."**

This information does not need to be permanently displayed in the explorer.

Only remove the text. Do not change the existing original-file backup behaviour because of this UI change.

## 2. Direct printing — Citizen CY-02

**Add direct printing from PhotoX without requiring Citizen LinkServer.**

The Citizen CY-02 has already been tested with Windows, and printing through the installed Windows driver works.

I do not currently have the printer available on my development computer, so actual hardware testing and calibration will be performed later.

### Implementation approach

The Citizen CY-02 uses a standard Windows printer driver. PhotoX should use the normal Windows printing system to send print jobs through that driver.

- Use the existing Citizen Windows driver.
- Do not require LinkServer to be running.
- Do not reverse-engineer the USB printer protocol.
- Keep printing separate from the cross-platform image-editing functionality.
- Windows 11 is the primary printing platform.
- Photo editing and exporting should continue working on Linux.
- Use a PDF or virtual printer for development testing where helpful.

The printer driver handles the physical printing process. PhotoX should handle image preparation, layouts, quantities and the user-facing print workflow.

### Printing capabilities

Initially support:

- 10×15 cm photographs.
- 15×20 cm photographs.
- Printing individual photos.
- Printing multiple photos.
- Printing ID photo sheets.
- Printing collages.
- Selecting the installed printer.
- Selecting the print size.
- Selecting the number of copies.
- Previewing the output before printing.

The driver supports additional settings such as printing quality, glossy/matte finish, colour management and cutting options.

Do not implement an unnecessarily complicated interface for these yet. Investigate what can be reliably controlled through the standard Windows driver.

The driver also supports placing two 10×15 photos on one 15×20 sheet. Keep this capability in mind for future optimization, but it does not need to complicate the first printing implementation.

Use consistent colour management. Avoid unintended double corrections from both PhotoX and the printer driver.

Keep printer-specific options and physical calibration as open points until the Citizen can be tested directly.

## 3. Printing interface

When the user selects Print, open a dedicated printing interface.

### Print settings

Initially offer only:

- **10×15 cm**
- **15×20 cm**

Include:

- Printer selection.
- Print size.
- Copies.
- Print preview.
- Print confirmation.

The paper/media settings are not fully decided yet.

For now, use sensible defaults and allow the Citizen driver to manage its supported media settings where appropriate.

Do not assume the installed media size can be detected automatically.

If PhotoX cannot reliably determine the loaded media, require the user to select or confirm the correct size.

Keep the interface minimal and suitable for everyday studio use.

### Printing without saving

PhotoX must allow printing:

1. Original photographs that have not been edited.
2. Photographs that have already been edited and saved.
3. Photographs with unsaved editing changes.

For photographs with unsaved changes, **print the currently edited version displayed in PhotoX**, including crop and adjustments, without forcing the user to save the file.

Printing must not automatically modify or overwrite the photograph.

Use the same full-quality image-processing pipeline for printing as for saving, so the print matches the edited preview.

## 4. Print queue

Add a simple **Print Queue** view.

It may be implemented as another workspace or an accessible section of the printing interface. Choose whichever integrates better with the existing UI.

The print queue should display:

- Photograph or job name.
- Print size.
- Number of copies.
- Job status.
- Pending and completed jobs.
- Failed jobs where the Windows printing system reports an error.

Allow canceling pending jobs where supported.

Avoid building a complicated printer-management application.

Use the Windows printing system for job status. Clearly distinguish between a job submitted to Windows and a photograph physically printed, since the standard print queue may not reliably confirm physical output.

Do not automatically resubmit uncertain jobs, as this could cause duplicate prints.

## 5. Photos workspace — new workflow

With direct printing available, the Photos workspace should become simpler.

### Multiple photo selection

Allow selecting multiple photographs in the gallery.

Support familiar Windows-style selection:

- Ctrl + click to select individual photos.
- Shift + click to select a range.
- Select All where useful.

Keep the selected photographs clearly highlighted.

### Remove Finish Order

The previous Finish Order functionality is no longer needed as a separate workflow.

Remove it from the normal user interface.

Instead, provide these actions:

**Print All**
- Print all photographs in the current folder/order.
- Include photographs that have not been edited.
- Use the latest edited version for photographs with unsaved changes, when those changes are available in the current editing state.

**Print Selected**
- Print only the selected photographs.
- Allow selecting multiple photographs.

**Edit This Photo**
- Open the selected photograph in the normal editor.
- Preserve the existing editing functionality.

**Use for ID Photo**
- Open the selected photograph in the ID Photo Creator.

**Save Changes**
- Save the current editing changes.
- Finalize the saved versions.
- Remove the original backups created by PhotoX after displaying a clear confirmation warning.
- The warning must explain that the originals will be permanently deleted.
- Never delete the edited photographs.
- Never delete unrelated files.

**Restore Original**
- Restore the original photograph from the `Original` backup.
- Clearly warn when current edits will be discarded.
- Preserve the ability to recover the original safely.

Keep these actions organized and intuitive. Actions intended for a single photo should be available when one photo is selected; bulk actions should work with multiple selections where appropriate.

### Important: file safety

The previous Finish Order functionality caused file-loss problems.

Do not reuse unsafe deletion logic.

When saving or deleting backups:

- Only delete files that PhotoX can reliably identify as its own backups.
- Never delete the entire customer folder.
- Never remove completed edited photos.
- Verify saves before deleting backups.
- Add automated tests for the new Save Changes and Restore Original actions.

Prioritize data safety over convenience.

## 6. Printing status in the photo gallery

Currently, the thumbnail gallery already displays whether a photograph has been edited or contains unsaved changes.

Keep these existing indicators in the upper-right corner.

Add another indicator in the **lower-right corner** of each thumbnail showing its printing history.

### Design suggestion

Use a small circle with a number inside.

- `0` or a dash: Not printed.
- `1`: Printed once.
- `2`: Printed twice.
- etc.

The circle could use different background colours to make printed and unprinted photographs easy to distinguish.

Keep it subtle and visually consistent with the existing thumbnail indicators.

The counter should represent the total number of copies submitted through PhotoX, with the meaning clearly documented. Do not claim physical print confirmation if only Windows job submission is available.

Update the counter only after the print operation has been successfully accepted by the printing system, not merely when the user presses Print.

Keep the information available when reopening the same order, without creating unnecessary extra files in the customer photo folder.

If jobs fail or are canceled, handle the counter consistently.

## 7. ID Photos workspace

### Remove unnecessary text

Remove the descriptive text:

**"Geometric sizes..."**

It is not needed in the ID photo interface.

### Remove Source Photo section

Remove the separate **Source Photo** section from the interface.

The source image should instead be selected through the common file explorer.

### File explorer

Make the existing explorer and folder viewer directly accessible in ID Photos.

- Display it underneath the format selector.
- Allow navigating folders and selecting photographs directly.
- Selecting a photograph should load it into the ID Photo Creator.
- Reuse the existing explorer instead of implementing a separate browser.

### Print Sheet

In the Print Sheet section, offer two actions:

**Save Sheet**
- Keep the current JPG export functionality.

**Print**
- Open the new PhotoX printing interface.
- Print the prepared sheet directly through the Windows printer driver.
- Maintain the exact ID photograph dimensions.

Do not change the existing alignment, cropping or sheet-generation functionality unnecessarily.

## 8. Collages workspace

### Template dimensions

Currently, it is not clear which physical sizes the smaller photographs have inside each collage template.

Improve this.

- Display the dimensions of individual photo slots in centimetres.
- Make the slot dimensions visible when selecting or editing a template.
- Allow different image sizes and orientations within a collage.

### Custom sizing

Allow custom adjustments to collage templates.

The user should be able to:

- Modify the sizes of existing photo slots.
- Add a custom size where appropriate.
- Adjust the template layout.
- Reposition photographs inside their slots.
- Keep the output within the selected print-sheet dimensions.

Prefer a simple template-based editor with adjustable slots rather than a fully free-form design application.

Provide useful default layouts and maintain the existing simple workflow.

### Supported sheet sizes

Keep only:

- 10×15 cm
- 15×20 cm

**Remove the 20×30 cm option.**

### Save and Print

Like ID Photos, provide:

**Save**
- Save the finished collage as a high-quality JPG.

**Print**
- Open the PhotoX printing interface.
- Print the collage directly with the selected dimensions.

Both actions should use the same final image composition.

## 9. Performance and usability

Keep PhotoX responsive during printing-related operations.

- Do not block the UI while preparing large print jobs.
- Keep image previews responsive.
- Avoid unnecessarily reprocessing photographs.
- Preserve full image quality when printing.
- Keep printing, saving and editing separate so one operation does not unexpectedly modify another.

Reuse the existing image-processing components where practical.

Avoid additional complexity unless it is necessary for correctness or reliability.

## 10. Development and testing

Continue working autonomously.

1. Inspect the existing code and architecture.
2. Implement the new printing functionality.
3. Update the Photos workflow.
4. Integrate printing into ID Photos and Collages.
5. Implement the UI improvements, including the updated branding placement and explorer text removal.
6. Add appropriate automated tests.
7. Build and test on Linux and Windows where possible.
8. Update the existing documentation.
9. Create meaningful commits and version increments.
10. Continue with any remaining approved, feasible functionality from the original roadmap.

Do not stop after simply planning these changes.

### Testing without the Citizen printer

Since the printer is not available on the development machine:

- Test printer discovery where possible.
- Test print-job preparation and submission through Windows using a virtual/PDF printer.
- Verify dimensions and layout calculations.
- Verify that unsaved edits are included in printed output.
- Verify print counters and queue behaviour.
- Verify that printing does not modify source files.
- Test error and cancellation handling where possible.

Clearly document what has been tested and what still requires the physical Citizen printer.

Do not claim physical printing has been verified through PhotoX until that test is actually performed.

## 11. Documentation and versioning

- Continue using the existing GitHub repository.
- Archive this prompt unchanged under `docs/prompts/`.
- Update `docs/PLAN.md` as the source of truth.
- Update the architecture, roadmap, implementation log and changelog where relevant.
- Follow the existing versioning policy.
- Make meaningful commits.
- Push commits and tags where access is available.

Keep documentation concise and accurate.

## 12. Final report

When finished, report:

1. Implemented changes.
2. How Windows printing was integrated.
3. Which functions can be tested without the Citizen printer.
4. Print queue and print counter behaviour.
5. Updated Photos, ID Photos and Collages workflows.
6. Automated tests and build results.
7. New version number and commit hashes.
8. Remaining open points.
9. A short Windows 11 manual-testing checklist.

## Overall objective

PhotoX should become a complete, simple photo-studio application that handles editing, ID photos, collages and direct printing without requiring LinkServer.

The expected workflows are:

**Photos:** Open folder → Select photos → Edit if needed → Print or Save Changes.

**ID Photos:** Choose format → Select photo in explorer → Align/Edit → Save Sheet or Print.

**Collages:** Select template → Add photos → Adjust layout → Save or Print.

Keep everything intuitive, safe and efficient for a small photo studio.

**Implement these changes and continue autonomously with the remaining approved project work.**