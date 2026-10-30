# PhotoX — UI Consistency, Printing Workspace and Usability Improvements

Continue developing PhotoX based on the current repository, architecture and approved project plan.

This iteration focuses on improving UI consistency, simplifying the Photos and ID Photos workflows, expanding printing functionality, and refining the application appearance.

**Keep the application simple, modern, responsive and easy to use. Avoid overengineering.**

Implement the changes autonomously, test them, make meaningful Git commits, update version numbers and maintain the existing documentation.

After completing these changes, continue with any remaining approved and feasible functionality from the original project plan.

---

## 1. General UI consistency

### Workspace descriptions

In the Photos workspace, remove the explanatory text about using Ctrl + Click, Shift + Click and similar selection instructions.

These are standard desktop interactions and do not need to be permanently displayed.

### Workspace icons

Add a small, simple icon next to each workspace name:

- Photos
- ID Photos
- Collages
- Printing

Use consistent, modern icons that fit the existing interface.

Keep the workspace selector compact and clearly highlight the active workspace.

### Consistent editing terminology

Currently, the Photos workspace uses **Auto Correct**, while ID Photos uses **Auto Adjust** for essentially the same functionality.

Choose one name and use it consistently in both workspaces.

My preference is **Auto Adjust**.

Make the following controls consistent between Photos and ID Photos:

- Auto Adjust
- Reset
- Undo
- Exposure
- Contrast
- White balance
- Tint
- Saturation
- Histogram / Levels

Use the same icons, styling, button positions and behaviour.

### Shared editing sidebar

Make the right-side editing panel look essentially identical in Photos and ID Photos.

Reuse the same components where practical.

The ID Photo Creator has additional functions:

- Zoom
- Rotation
- Auto Align
- Background Brightness

Move these ID-specific controls into the **upper editing toolbar**, similar to how crop and orientation tools are arranged in the Photos workspace.

Keep the right sidebar primarily for common image adjustments.

Avoid duplicating similar controls in different locations.

### Resizable side panels

Make the left sidebar resizable in every workspace by dragging its separator.

The right editing sidebar already supports resizing in some places. Apply the same behaviour consistently wherever it is missing.

Requirements:

- Drag left/right to resize.
- Sensible minimum widths.
- No overlapping or disappearing controls.
- Main image preview automatically uses the remaining space.

Use standard Avalonia layout functionality where practical.

---

## 2. Application icon and branding

The new PhotoX icon reference is available locally at:

`/home/norbert/Documents/PhotoX/icon example.png`

**Open this file and use it as the exact visual reference for the application icon.**

The selected logo has:

- A minimalist blue camera outline.
- Dark **PX** lettering inside the camera.
- A clean, flat design.
- A light background.
- No unnecessary gradients, 3D effects or shadows.

Preserve this design as closely as possible. Do not redesign it or generate a different logo.

### Colour

The blue of the camera outline should be **exactly the same blue used for the application's primary blue buttons and selected controls**.

Read the primary colour from the existing application theme and apply it to the logo.

Do not introduce a slightly different blue.

### Implementation

- Recreate the icon as a clean SVG/vector asset, using the reference image.
- Keep the original proportions and appearance.
- Generate the required PNG and Windows ICO assets.
- Make sure the icon remains recognizable at small sizes.
- Ensure good visibility in both light and dark modes.
- Update the application, window, taskbar, executable and installer icons consistently.
- Keep the PhotoX branding in the existing top-right toolbar position.

The reference image is a design source. Do not simply use a low-resolution screenshot as the final icon.

If the reference file cannot be accessed, report that clearly rather than creating an unrelated replacement.

---

## 3. Light and dark mode

Add support for:

- Light mode
- Dark mode

The selection should be available in **Settings**.

Requirements:

- Apply the selected theme throughout the application.
- Remember the selection between sessions.
- Keep buttons, icons, backgrounds, text and selection indicators consistent.
- Ensure image previews and editing controls remain clear in both themes.
- Preserve the same layout and functionality.

Use Avalonia's existing theme capabilities rather than creating a complicated custom styling system.

---

## 4. Photos workspace

### Explorer and gallery

Remove the unnecessary permanent explanation about multi-selection shortcuts.

### Progress information

In the photo-folder overview, rename:

**Order Progress → Edit Progress**

Also display a simple summary of how many photographs have been printed from the currently opened folder.

For example:

- Edited: 12 / 30
- Printed: 18 copies

Make the distinction between edited photographs and printed copies clear.

### Discard Originals

Rename the existing **Save Changes** action to:

**Discard Originals**

This is clearer because the action removes the backed-up originals while keeping the edited photographs.

Preserve the existing confirmation warning and file-safety checks.

Expected behaviour:

- Edited photographs remain.
- Only the corresponding original backups created by PhotoX are removed.
- Unrelated files are never deleted.
- The user receives a clear warning before deletion.

Do not reintroduce the previous Finish Order workflow.

### Thumbnail size adjustment

Add a slider to change the size of photograph thumbnails in the file explorer/gallery.

- Slide left for smaller thumbnails.
- Slide right for larger thumbnails.
- The current thumbnail size should remain the default.
- Double-clicking the slider should reset it to the default value.
- The gallery should adapt smoothly without unnecessary delays.

Keep this control compact and easily accessible.

---

## 5. ID Photos workspace

### Editing sidebar

Use the same editing sidebar as the Photos workspace.

Keep common editing controls in the same order and with the same appearance.

Move the ID-specific Zoom, Rotation, Auto Align and Background Brightness controls to the upper toolbar.

### 90-degree rotation

Add a simple **90° rotation** function for ID photos.

This is needed when the imported photograph has the wrong orientation.

Support rotation left and right, similar to the normal Photos workspace.

The crop and alignment should remain valid after rotation.

### Simplify the left sidebar

The left side of ID Photos currently contains:

- ID format selection.
- Folder location.
- Small thumbnail viewer.

This feels too crowded.

Improve the arrangement.

My suggested solution:

- Move **ID format selection to the upper toolbar**.
- Keep the file explorer and thumbnail viewer together on the left.
- Keep the photograph preview in the main area.
- Keep the shared editing settings on the right.

You may choose a slightly different arrangement if it is cleaner and more practical.

The objective is to reduce clutter and make choosing a photo and format easier.

### US Visa photograph size

Currently, the US Visa format is displayed as **2×2 inches**.

Change the main displayed name to:

**5×5 cm**

However, preserve the existing exact physical dimensions of **50.8×50.8 mm**.

This is only a user-friendly naming change. Do not accidentally change the actual image size to 50×50 mm.

### Default ID format

Make **3×4 cm** the default selected ID photo format.

### Default 3×4 layout

Set the default number of 3×4 photographs per sheet to **8** instead of the current 6.

Keep the existing option for the 7 mm white strip.

### Simplify button text

In ID Photos:

- Change **Save Sheet** to **Save**.
- Change the second workflow step **Print Sheet** to **Print**.

Keep the functions unchanged, only simplify the labels.

---

## 6. Collages workspace

### Drag and drop

Improve collage editing with drag-and-drop functionality.

The user should be able to:

- Drag photographs from the explorer into collage slots.
- Drag a photograph from one slot to another.
- Replace a photograph in a slot.
- Reposition the photograph inside its frame.

Keep the interactions intuitive and responsive.

### Right-click image menu

After adding a photograph to the collage, provide a right-click context menu.

Include at least:

- Rotate left 90°.
- Rotate right 90°.

Ensure that rotation does not distort the photograph or break the layout.

Keep the existing collage templates and sizing functionality.

---

## 7. Printing — new workspace

The current printing functionality should be expanded and better integrated into PhotoX.

### Printing interface

Instead of opening printing options in a popup window, make **Printing** a dedicated workspace.

It should use the same application window and general design as Photos, ID Photos and Collages.

When the user presses Print from another workspace:

1. Open the Printing workspace.
2. Show the selected photographs or prepared print sheet.
3. Display the printing options.
4. Allow confirming and submitting the print job.

Keep the workflow straightforward.

The actual printing should continue to use the installed Windows printer driver, without requiring Citizen LinkServer.

### Print queue

Display a useful print queue inside the Printing workspace.

Show:

- Photograph/job name.
- Print size.
- Number of copies.
- Job status.
- Printing progress or available status information.
- Pending, printing, completed, canceled and failed jobs.

Allow the user to cancel a job if it has not yet printed.

If printing is already in progress, attempt cancellation through the Windows printing system where supported.

Do not assume cancellation is guaranteed once the printer has begun processing a job.

Avoid automatically resubmitting uncertain jobs.

### Printing history

Keep a history of photographs printed through PhotoX.

During the current application session, all submitted print jobs should remain visible in the history, even after completion or cancellation.

Also preserve useful historical data between sessions so that summaries for previous days, weeks and months are possible.

At minimum, today's printing history should remain available.

### Printing statistics

Add simple, useful statistics.

For example:

- Photographs printed today.
- Photographs printed this week.
- Photographs printed this month.
- Total copies submitted.
- Optionally, a breakdown by 10×15 and 15×20 cm.

Keep the statistics clear and compact.

Use the existing local data storage or a simple local persistence mechanism.

Do not introduce a cloud service or an unnecessarily complicated database system.

### Important distinction

Windows may confirm that a job was submitted or processed by the spooler without confirming that the photograph physically came out of the printer.

Therefore:

- Track print-job status as accurately as the Windows printing system allows.
- Clearly distinguish submitted, completed, failed and canceled jobs.
- Do not present unverified physical print completion as guaranteed.
- Keep canceled and failed jobs in the history, but distinguish them from successful jobs in the statistics.
- Use consistent rules for the printing counters displayed in the Photos gallery.

---

## 8. Windows installer

Improve the Windows installer slightly.

Currently, PhotoX installs successfully, but it is not obvious where the application was installed or how it can be managed afterward.

I would prefer a simple, conventional Windows installation experience.

If straightforward, implement:

- A recognizable installer with the PhotoX name and version.
- A conventional installation location.
- A Start Menu shortcut.
- A visible entry in Windows Installed Apps.
- A normal uninstall option.
- A straightforward way to install a new version over the existing one.

The installer should show its version number.

Avoid complicated custom installation interfaces.

**If improving the installer requires disproportionate effort or introduces reliability risks, keep the existing installer.**

Do not make this task a higher priority than the application's actual functionality.

---

## 9. Settings

### JPEG quality

The application currently allows selecting JPEG quality in Settings.

Change the default value to:

**100**

Keep the option adjustable.

Existing user-selected quality settings should remain respected unless the user changes them.

### Theme selection

Add the Light/Dark mode selection described earlier.

Keep Settings simple and organized.

---

## 10. Final UI consistency review

After all functionality above has been implemented, perform a final visual and usability review of the entire application.

Check all workspaces:

- Photos
- ID Photos
- Collages
- Printing
- Settings

Pay particular attention to:

- Consistent button appearance.
- Consistent control sizes.
- Correct alignment.
- Logical spacing.
- Readable text.
- No duplicated controls or unnecessary descriptions.
- No clipped text.
- No overlapping elements.
- Consistent icons.
- Clear active selections.
- Consistent editing terminology.
- Correct light/dark theme appearance.
- Proper layout when sidebars are resized.
- Usability at common Windows display resolutions and scaling factors.

Check that controls remain properly arranged when the window is resized.

If something looks incorrect or inconsistent, correct it.

Do not completely redesign the interface. Refine the existing design and make it coherent.

Where possible, launch the actual application and inspect the resulting UI rather than relying only on successful compilation.

---

## 11. Development instructions

Continue implementation autonomously.

- Inspect the existing architecture before making changes.
- Reuse existing components wherever practical.
- Avoid unnecessary refactoring.
- Keep the application responsive.
- Add or update tests where necessary.
- Preserve existing functionality.
- Run automated tests and relevant build checks.
- Update documentation.
- Make meaningful Git commits.
- Increment the version according to the existing rules.
- Push commits and tags to GitHub when available.
- Archive this prompt unchanged in the next numbered file under `docs/prompts/`.

After implementing this feedback, review the original approved project plan and continue with any remaining feasible functionality.

Do not begin unrelated features or expand the scope unnecessarily.

Physical Citizen printer calibration remains an open point until the hardware is available.

---

## 12. Final report

When finished, provide a concise summary including:

1. Implemented changes.
2. UI improvements and shared editing components.
3. Printing workspace, queue and history functionality.
4. Light/dark mode implementation.
5. Installer improvements, if implemented.
6. Automated test results.
7. Linux and Windows build/test status.
8. New version number and commit hashes.
9. Remaining open points.
10. A short manual testing checklist for Windows 11.

Only report functionality as verified when it has actually been tested.

---

## Overall objective

PhotoX should become a polished, practical desktop application for everyday photo-studio use.

The main priorities are:

1. Simple and consistent operation.
2. Reliable editing and saving.
3. Practical direct printing.
4. Responsive interface.
5. Clean, modern appearance.
6. Maintainable implementation without unnecessary complexity.

**Implement the requested improvements, verify them, review the overall UI consistency, and continue autonomously with the remaining approved project work.**