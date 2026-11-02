# PhotoX — Citizen Print Dimensions, Cropping Accuracy and Keyboard Improvements

Continue developing PhotoX from the current repository and approved project plan.

I found an important issue with the paper dimensions used for printing, along with some keyboard and navigation improvements.

Keep the implementation simple, reliable and consistent with the existing application. Avoid unnecessary refactoring.

Implement the changes autonomously, test them, update the documentation, make meaningful commits and publish a new stable GitHub Release after successful verification so I can update PhotoX directly.

---

## 1. Important — Correct Citizen CY-02 print dimensions

The paper dimensions we previously assumed do not match the dimensions reported by the installed Citizen Windows printer driver.

Currently, when printing a photograph, there is a very small unprinted strip (less than approximately 1 mm) along one side.

This suggests that the image sizing, placement or borderless printing logic may not be entirely correct.

### Actual paper sizes reported by the Citizen driver

I inspected the Windows printer paper-size selection and obtained the following values from the driver:

| Driver paper format | Dimensions |
|---|---|
| PR (3.5×5) | 92.1 × 131 mm |
| PR (3.5×5) × 2 | 92.1 × 131 mm |
| PR (4×6) | 104.9 × 156.1 mm |
| PR (4×6) × 2 | 104.9 × 156.1 mm |
| (5×3.5) | 131 × 92.1 mm |
| (6×4) | 156.1 × 104.9 mm |
| (5×7) | 131 × 181 mm |
| (6×8) | 156.1 × 206.2 mm |
| (6×4) × 2 | 156.1 × 104.9 mm |
| (5×3.5) × 2 | 131 × 92.1 mm |
| (5×5) | 131 × 130.3 mm |
| (6×6) | 156.1 × 155.4 mm |

These are the dimensions displayed in the actual Windows Citizen printer driver.

The driver also lists landscape alternatives and special ×2 printing modes.

### Investigate the actual meaning of these dimensions

Do not simply assume that the driver dimensions are the physical dimensions of the final trimmed photograph.

Investigate the distinction between:

- Nominal photograph size.
- Physical finished paper size.
- Driver-reported page dimensions.
- Printable image area.
- Bleed/overscan required for borderless printing.
- Physical trimming and cutting.

Check the official Citizen CY-02 driver documentation where possible.

Use the actual dimensions and capabilities reported by the installed driver.

The objective is to have PhotoX produce correctly sized prints without unintended white borders.

### Fix the printing pipeline

Review and correct the way PhotoX:

1. Selects the corresponding Windows driver paper size.
2. Calculates the final print layout.
3. Scales and positions the photograph.
4. Handles portrait and landscape orientation.
5. Applies Crop or Whole printing modes.
6. Sends the prepared image to the Windows printing system.

Pay particular attention to incorrect margins, rounding and image scaling.

### Crop versus Whole

The current print settings offer:

**Crop**
- The photograph should completely fill the selected print area.
- Preserve aspect ratio.
- Crop any excess image content.
- Avoid stretching or distortion.
- No unintended white bands should appear.

**Whole**
- Show the entire photograph.
- Preserve aspect ratio.
- Add white borders only where necessary.

I had Crop selected when I observed the small white strip.

This makes me suspect that the current Crop implementation may not be fully correct.

Investigate whether Crop is actually filling the driver's required image area or whether Windows or the printer driver is adding or retaining a margin.

Fix this consistently.

However, note that a border could also originate from printer-specific scaling or cutting behaviour. Do not assume PhotoX is the only possible cause without verification.

### Apply the correction consistently

Review the sizing and rendering logic used by all PhotoX workspaces:

**Photos**
- Regular 10×15 and 15×20 crops.
- Custom crops.
- Crop and Whole printing.
- Printing saved and unsaved edits.

**ID Photos**
- 10×15 and 15×20 print sheets where applicable.
- Exact ID photograph dimensions.
- Correct photo spacing and sheet layout.
- No unintended scaling of individual ID photographs.

**Collages**
- 10×15 and 15×20 output.
- Correct template proportions.
- Accurate placement and physical dimensions.
- No unintended borders or cropping.

Reuse the same corrected print-size and rendering logic wherever appropriate.

**Important:** Do not change the intended physical sizes of ID photos or custom photographs simply because the driver reports a larger page area. Preserve their exact dimensions while handling any necessary outer-page bleed or scaling separately.

For example, a 9×13 cm photo centered on a 10×15 cm sheet should retain its intentional white borders. Those are not the printing defect we are trying to fix.

### Testing

Add automated tests for:

- Correct paper-size selection.
- Correct portrait and landscape orientation.
- Crop filling the intended print area.
- Whole preserving the complete image.
- Accurate ID photograph sizes.
- Correct collage dimensions.
- Printing without unintended margins introduced by PhotoX.

Use virtual/PDF printing where useful.

Physical Citizen calibration remains necessary to confirm the final result. Clearly distinguish software verification from physical printing verification.

---

## 2. Settings — Escape to close

The Settings window should close when pressing **Escape**.

This should behave the same as closing the Settings window normally.

Preserve any already saved settings.

Do not unexpectedly discard or apply pending changes.

---

## 3. Photos editor — Keyboard shortcuts

I want to improve keyboard-only editing.

### Move Before/After to the # key

Currently, holding Space shows the original photograph for comparison.

Change this shortcut.

Use the **# key on a German keyboard** for the Before/After comparison.

For English keyboard layouts, use the equivalent key in the same physical keyboard position, where technically possible.

Prefer identifying the physical key rather than relying only on the character produced by the current keyboard layout.

The comparison should remain a press-and-hold action:

- Hold the key → show the original photograph.
- Release the key → return to the edited photograph.

Keep the existing Before/After button synchronized with this behaviour.

### Space and Backspace navigation

Use:

| Key | Function |
|---|---|
| Space | Next photograph |
| Backspace | Previous photograph |
| Enter | Save & Next |
| Escape | Close editor |
| Arrow keys | Move selected crop |
| # / corresponding physical key | Hold for Before/After |

Keep the previously implemented Page Up/Page Down photo navigation if it remains useful and does not conflict.

The arrow keys should continue moving the crop area, not navigating between photographs.

Make sure keyboard shortcuts do not interfere with text input, dialogs or other controls that need these keys.

### Intended workflow

For a typical order requiring only cropping:

1. Open a photograph.
2. Use the arrow keys to position the crop.
3. Press Enter to save and open the next photograph.
4. Use Space to skip forward when necessary.
5. Use Backspace to return to the previous photograph.
6. Hold the Before/After key to compare when needed.

Keep the workflow responsive and predictable.

---

## 4. Thumbnail gallery — Backspace navigation

In the Photos thumbnail gallery, pressing Backspace already navigates one folder up.

This is good, but there is a focus problem.

### Current behaviour

After pressing Backspace, the application navigates to the parent folder, but the keyboard focus moves to the folder tree.

Subsequently, pressing arrow keys navigates through the folder tree instead of the thumbnail gallery.

### Expected behaviour

- Backspace navigates one folder up.
- Keyboard focus remains in the thumbnail gallery.
- Arrow keys continue navigating thumbnails and folder items.
- Ideally, the folder just exited should be selected in the parent folder's thumbnail view.
- The folder tree should only receive keyboard focus when the user explicitly interacts with it.

Keep the existing folder navigation functionality.

---

## 5. Thumbnail gallery — Selection highlight bug

The current blue selection highlight looks good, but behaves inconsistently with keyboard navigation.

### Current behaviour

- Left/Right arrow navigation preserves the nice rounded blue selection border.
- Up/Down arrow navigation sometimes produces a black rectangular border with sharp corners.

### Expected behaviour

Use the same rounded blue selection border for all keyboard and mouse selection methods.

- Left → Blue border.
- Right → Blue border.
- Up → Blue border.
- Down → Blue border.
- Mouse click → Blue border.

The selected photograph should always have the same visual appearance.

Investigate whether Avalonia's default focus indicator is being displayed instead of or in addition to the custom selection border.

Fix this without removing keyboard accessibility or breaking focus handling.

Make sure the selected item remains visible while navigating the gallery.

The highlight should also work consistently in light and dark modes.

---

## 6. Development and verification

Implement these changes using the existing codebase.

Suggested priority:

1. Investigate the Citizen page dimensions and the white-border issue.
2. Correct the print layout and paper-size handling.
3. Verify Photos, ID Photos and Collages use consistent dimensions.
4. Update keyboard shortcuts in the Photo Editor.
5. Fix thumbnail navigation and selection highlighting.
6. Add Escape handling in Settings.
7. Run regression tests.

Avoid unnecessary architectural changes.

Preserve existing functionality and image quality.

Use copies of photographs for any tests that modify files.

### Documentation and release

- Archive this prompt unchanged under `docs/prompts/`.
- Update `docs/PLAN.md` where requirements change.
- Update the changelog and implementation log.
- Create meaningful commits.
- Increment the version according to the existing conventions.
- Run the relevant automated tests.
- Build the Windows installer.
- Push the commits and version tag.
- **Publish a new stable GitHub Release with the installer**, provided the implementation passes the relevant tests.
- Ensure the release is detectable by PhotoX's built-in update mechanism.

Do not claim the white-border issue has been physically verified if no Citizen printer test was performed.

---

## 7. Final report

After implementing the changes, provide:

1. Summary of what was changed.
2. Explanation of the difference between Citizen driver page dimensions and nominal photograph dimensions.
3. Root cause of the unwanted white border, if identified.
4. How Crop and Whole printing now work.
5. Verification of ID photo and collage dimensions.
6. Updated keyboard shortcuts.
7. Gallery navigation and selection fixes.
8. Automated test results.
9. New version number and commit hashes.
10. GitHub Release link and installer status.
11. Remaining limitations and any physical calibration measurements still needed.

Provide a short Windows 11 manual-testing checklist, particularly for printing and keyboard navigation.

## Overall objective

PhotoX should produce correctly sized photographs without unintended white borders, while preserving accurate ID-photo and collage dimensions.

The editing workflow should be fast and convenient using the keyboard, and folder navigation should behave consistently.

**Implement these changes, verify them carefully, and publish the updated version when ready.**