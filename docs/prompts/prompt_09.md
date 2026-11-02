# PhotoX — Print Quantity Management and UI Improvements

Continue developing PhotoX using the existing repository, architecture and approved project plan.

I want to improve the Photos workspace by adding the possibility to specify how many copies of each photograph should be printed.

This should integrate naturally into the existing thumbnail gallery, right sidebar and printing workflow.

Additionally, there are several small UI improvements and fixes needed in the ID Photos, Collages and workspace navigation.

Keep the implementation simple, intuitive and visually consistent with the current PhotoX interface. Avoid unnecessary complexity.

Implement the changes autonomously, test them, make meaningful commits and update the existing documentation.

---

## 1. Print quantity for each photograph

Each photograph should have an individual setting for the number of copies to print.

- Default quantity: **1**
- Minimum quantity: **0**
- Allow increasing the quantity to 2, 3, 4 or more.
- Setting the quantity to **0 excludes the photograph from printing**.
- Quantities should be remembered when closing and reopening PhotoX or returning to the same folder.

Use the existing local order/photo metadata where possible.

Do not modify the original image files to store this information.

---

## 2. Thumbnail gallery — Print quantity indicator

Currently, each thumbnail already displays:

- Editing status in the upper-right corner.
- Number of previously printed copies in the lower-right corner.

Keep these existing indicators.

Add a new indicator in the **lower-left corner** showing the requested number of copies.

Examples:

- `×0` — Excluded from printing.
- `×1` — One copy requested (default).
- `×2` — Two copies requested.
- `×5` — Five copies requested.

### Visual design

Use a small, rounded badge consistent with the existing thumbnail indicators.

Choose a suitable colour scheme that clearly distinguishes:

1. Requested copies.
2. Already printed copies.
3. Editing status.

Choose colours that fit PhotoX's existing blue theme and work in both light and dark modes.

The indicators should remain easy to read without covering too much of the photograph.

---

## 3. Right sidebar — Improve information and layout

The right sidebar currently displays:

- Selected photograph preview.
- Filename and resolution.
- Editing/printing status.
- Edit progress.
- Number of previously printed copies.
- Action buttons.

Keep this structure, but improve the presentation and add print quantity management.

### Suggested layout

**Selected Photo**

Keep the existing image preview, filename, dimensions and editing status.

**Edit Progress**

Edited: 45 / 245

Keep the existing progress bar.

**Printing Information**

Selected for print: 245

Printed: 1 copy

Copies: `[ − ]   1   [ + ]`

### Meaning of these values

**Edited: X / Y**

- Number of edited photographs compared with the total photographs in the current folder.

**Selected for print: Y**

- Number of photographs in the current folder with a requested quantity greater than 0.
- For a folder containing 245 photographs, the default is 245 because each photograph initially has one requested copy.
- If 20 photographs are changed to quantity 0, this becomes 225.
- This value represents the number of photographs selected for printing, not the total number of copies.

**Printed: X copies**

- Total copies previously recorded as printed from the current folder, using the existing print-history tracking.
- Keep this consistent with the printed counters on individual thumbnails.
- Do not claim physical printing was confirmed if Windows only confirmed job submission.

**Copies**

- Requested print quantity for the currently selected photograph.
- Use a compact control:

`[ − ]     1     [ + ]`

- The minus button decreases the quantity, minimum 0.
- The plus button increases the quantity.
- Make the value clearly visible.
- Update the thumbnail indicator and folder summary immediately.

### Layout and visual consistency

Keep all progress and printing information grouped logically.

Use consistent spacing, typography, icons and alignment.

The requested copies control should be clearly visible without dominating the sidebar.

Avoid excessive borders, large cards or unnecessary descriptions.

Keep the action buttons at the bottom as they currently are:

- Print selected
- Print all
- Edit this photo
- Use for ID photo
- Discard originals
- Restore original

The result should feel like a refinement of the existing UI rather than a redesign.

---

## 4. Changing quantities for multiple photographs

This is particularly important for large customer orders.

When multiple thumbnails are selected, allow changing the requested quantity for all selected photographs simultaneously.

For example:

1. Select 15 photographs using Ctrl or Shift.
2. Set Copies to 2 in the right sidebar.
3. All 15 photographs now have a requested quantity of 2.
4. Their thumbnail indicators update to `×2`.

Also allow setting the quantity to 0 for multiple selected photographs.

This makes it possible to select all photographs, set them to 0, then select only the desired photographs and set their quantities to 1 or more.

### Multiple-selection sidebar

When several photographs are selected:

- Clearly indicate how many photographs are selected.
- Show the shared quantity if all selected photographs have the same value.
- If they have different quantities, indicate that the values are mixed.
- Changing the quantity should apply the new value to all selected photographs.

Keep the interaction simple and intuitive.

---

## 5. Printing behaviour

Update the existing Print Selected and Print All functions to respect individual requested quantities.

### Print All

Print all photographs in the current folder that have a requested quantity greater than 0.

Example:

| Photograph | Requested copies |
|---|---:|
| IMG_001.jpg | 1 |
| IMG_002.jpg | 3 |
| IMG_003.jpg | 0 |
| IMG_004.jpg | 2 |
| IMG_005.jpg | 1 |

Result:

- 4 photographs included.
- 7 total copies.
- IMG_003 is excluded.

### Print Selected

Print only the photographs currently selected in the thumbnail gallery.

Respect their individual quantities.

Photographs with quantity 0 should be excluded.

### Printing workspace

When opening the Printing workspace, carry over the requested quantities.

Display a clear summary, for example:

**4 photographs · 7 copies**

The print preview and queue must reflect the quantities accurately.

Avoid accidentally multiplying quantities through an additional global copies setting. The individual photo quantities should determine the number of copies submitted.

The existing ability to print original, saved and unsaved edited photographs must remain unchanged.

### Individual quantities in the print preview

In the **first printing screen**, which opens after pressing Print and where the user selects the paper size, also display the requested number of copies for each photograph.

For example:

- IMG_001.jpg — ×1
- IMG_002.jpg — ×3
- IMG_004.jpg — ×2
- IMG_005.jpg — ×1

The quantity should be visible directly on or near each photograph's preview.

Use a small badge similar to the new quantity indicator in the Photos thumbnail gallery.

Requirements:

- Display the correct requested quantity for each photograph.
- Photographs with quantity 0 should not appear in the prepared print selection.
- Keep the total photograph and copy counts clearly visible.
- Preserve the quantities when changing paper size or returning to the Photos workspace.
- Ensure the actual print queue uses these quantities.

The goal is to let the operator verify **which photographs will be printed and how many copies of each** before submitting the job.

Keep the interface compact and avoid unnecessary additional controls.

---

## 6. Print history and quantities

Keep two separate values for each photograph:

**Requested copies**

- How many copies should be printed in the next print operation.
- Editable by the user.

**Previously printed copies**

- How many copies have already been recorded through PhotoX.
- Updated through the existing printing mechanism.

Do not automatically reset requested quantities after printing.

For example, a photograph may show:

- Requested: 2
- Previously printed: 3

This means the user currently wants another two copies, while three copies have already been recorded.

Keep the existing print-history and queue logic consistent with these quantities.

A canceled or failed print job should not incorrectly increase the printed counter. Preserve the current distinction between submission and verified physical output.

---

## 7. ID Photos — Simplify photo selection

### Remove unnecessary helper text

In the ID Photos workspace, remove the explanatory text:

**"Click a photo to frame it. Double click to open it."**

This instruction does not need to be permanently displayed.

### Change photo-opening behaviour

Currently, a single click can open a photograph for framing.

Change this behaviour:

- **Single click:** Select and highlight the photograph in the thumbnail gallery.
- **Double click:** Open the photograph in the Framing & Adjustments step.

This should behave more like a conventional desktop file explorer.

Single-click selection should not automatically switch to the framing workspace.

Keep the existing ID photo functionality unchanged otherwise.

---

## 8. Collages — Remove unnecessary helper text

In the Collages workspace, remove the explanatory text:

**"Drag photo into a frame..."**

The drag-and-drop functionality is already intuitive enough and does not require a permanent instruction.

Only remove the unnecessary text.

Keep the existing drag-and-drop behaviour and collage functionality unchanged.

---

## 9. Workspace selection — Fix active highlighting

Currently, the selected workspace is highlighted in blue, which is good.

However, there is a small bug:

If I click the already active workspace again, the blue highlight disappears.

### Expected behaviour

- The currently open workspace must always remain highlighted.
- Clicking the active workspace again must not deselect it.
- Only switching to another workspace should move the blue highlight.
- The visual state must always match the workspace actually displayed.

Apply this consistently to Photos, ID Photos and Collages.

Keep the existing blue selection style.

---

## 10. Folder tree toggle — Improve inactive appearance

The Show/Hide Folder Tree button works correctly.

Only its appearance needs a small adjustment.

Currently, when the folder tree is hidden, the button becomes black, which does not fit the rest of the interface.

### Expected behaviour

When the folder tree is hidden:

- Use a subtle grey icon colour.
- Use the same neutral grey background style as an inactive workspace button.
- Avoid black or overly strong highlighting.

When the folder tree is visible:

- Keep the existing active appearance, consistent with PhotoX's blue selection style.

Do not change the functionality. Only improve the visual appearance.

Make sure it also looks appropriate in dark mode.

---

## 11. Implementation and testing

Implement these changes using the existing architecture and components where practical.

Pay attention to:

- Updating quantities without noticeable UI delays.
- Correct behaviour with hundreds of photographs.
- Multiple-selection handling.
- Persistent quantities between sessions.
- Correct printing totals.
- Proper handling of quantity 0.
- Individual quantities displayed correctly in the print preview.
- Consistency between thumbnails, sidebar, print preview and print queue.
- Correct ID Photo single-click and double-click behaviour.
- Workspace highlighting always matching the active workspace.
- Consistent folder-tree toggle appearance.
- Light and dark theme compatibility.

Add appropriate tests for quantity changes, bulk updates, persistence, printing calculations and the corrected UI behaviour.

Do not modify the existing image-processing or backup functionality unnecessarily.

---

## 12. Development workflow

Continue working autonomously.

- Implement the requested functionality.
- Test the changes.
- Keep the existing UI style.
- Make meaningful commits.
- Increment the version according to the current rules.
- Update relevant documentation.
- Archive this prompt in the next numbered Markdown file under `docs/prompts/`.
- Push changes to GitHub where possible.

After completing these changes, continue with any remaining approved and feasible functionality from the original project plan.

Avoid unrelated features or unnecessary refactoring.

---

## 13. Final report

When finished, provide:

1. Summary of the implemented functionality.
2. How individual and multiple-photo quantities work.
3. How the thumbnail indicators and right sidebar were updated.
4. How Print All and Print Selected handle quantities.
5. How individual quantities are displayed in the print preview.
6. Changes to ID Photo selection behaviour and removed helper text.
7. Workspace highlighting and folder-tree toggle fixes.
8. Automated test results.
9. New version and commit hashes.
10. Any remaining limitations.
11. A short Windows 11 manual-testing checklist.

Only report functionality as verified if it has actually been tested.

---

## Overall objective

The user should be able to open a folder containing hundreds of photographs and easily decide:

- Which photographs should be printed.
- How many copies of each photograph are required.
- Which photographs have already been printed.

The information should be immediately visible in the thumbnail gallery, easy to modify through the right sidebar and clearly presented again in the print preview.

The ID Photos and Collages workspaces should remain clean and intuitive, without unnecessary instructions.

Workspace navigation and folder-tree controls should behave consistently and always reflect their actual state.

**Keep the workflow fast, clear and suitable for everyday photo-studio use.**