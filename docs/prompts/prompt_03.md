# PhotoX — Feedback, Improvements and Continued Implementation

I have tested PhotoX v0.4.0 on Linux and Windows 11. The application works, but I found several bugs, usability issues and performance problems.

**Your task is to address all my feedback below and then continue implementing any remaining functionality from the original approved project plan.**

This is not a new project or a replacement for the original plan. My feedback updates and improves the existing requirements.

Work autonomously, test your changes, make meaningful commits, update version numbers and keep the documentation up to date.

Keep the application simple, modern, responsive and reliable. Avoid overengineering.

## 1. Answers to your previous questions

1. **GitHub:** I have uploaded the repository to GitHub. Use the existing remote.
2. **Windows:** I installed and tested the application on Windows 11. It launches and works, but I found the issues described below.
3. **Citizen printer:** Postpone physical printer calibration and printing tests. I don't have access to the printer yet. Develop and test everything possible without it. Later I will provide the application to the studio and perform printing and calibration tests. Keep direct printing and printer-dependent validation as open points.
4. **Romanian wording:** I will review the translations later.
5. **Additional photographs:** I will provide more samples later. Background whitening is not good enough yet. Keep further improvements open until suitable images are available.

## 2. Home screen

### Recent orders
- Display only the four most recent orders.
- Make the Recent Orders section collapsible.

### File browser
The current browser feels unusual. Make it behave more like a conventional desktop file explorer.

- Clicking a folder itself should expand/select it, not only clicking `>`.
- Display subfolders nested underneath their parent folders.
- Do not show an expansion arrow when a folder has no subfolders.
- Make navigation and selection visually clear.

### Photograph selection
- When a folder containing photos is opened, clearly highlight the selected photograph in the thumbnail gallery.

## 3. Normal photo editor

### Histogram
- Replace the current levels controls with **one horizontal bar containing three draggable triangle handles** for black point, midtones and white point.
- No separate RGB histograms are needed.
- Keep the histogram compact and intuitive.
- Update the preview smoothly.

### Exposure
- The `+` and `−` buttons should adjust exposure directly.
- Synchronize them with the exposure slider.

### Visual sliders
Make slider tracks visually explain their effects:

- Exposure: Dark to bright.
- Contrast: Low to high contrast, using white/black tonal variation.
- Saturation: Grayscale to colourful.
- White balance: Blue to red/orange.
- Tint: Magenta to green.

Use similar visual cues for other adjustments where useful.

Move **Saturation below Tint**.

### Straighten
- Move Straighten to the upper toolbar, immediately after the rotation buttons.
- Straightening is currently very sluggish.
- The rotation control in the ID Photo Creator works more smoothly. Investigate and reuse the better approach if appropriate.
- Ensure smooth preview updates while dragging.

### Portrait / Landscape
- Make these options more visual, with recognizable portrait and landscape rectangle icons.
- Clearly indicate the selected orientation.

### Crop modes
- Display **Crop to Fill** and **Whole + Borders** as one segmented button, split into two selectable parts.
- Clearly highlight the active mode.

### Toolbar
- Maintain a consistent, fixed order of controls.
- Prevent the toolbar from wrapping unpredictably and moving buttons around.
- Use a compact arrangement or simple overflow solution where needed.

### Settings panel
- Make the editing settings panel horizontally resizable by dragging its separator.
- The image viewer should use the remaining space.
- Apply the same principle to ID Photo Creator where applicable.

## 4. Crop behaviour

Currently, when I crop an image and save, the file is saved correctly, but the preview continues displaying the original image with the crop selection.

I expect the crop to be visually applied.

Required changes:

- After applying a crop, display the actual cropped result.
- Automatically fit the cropped image to the viewer.
- Remove the old crop overlay once the crop is applied.
- Allow continued editing and undo.
- Allow **double-clicking inside the crop selection** to apply the crop to the preview.
- Double-clicking should not immediately save the file; actual saving remains a separate action.

When the next photograph opens, the default crop behaviour should activate again as usual.

Keep this simple and intuitive.

## 5. Navigation and completing orders

- When all photos are saved, change **Save & Next** to **Close**.
- Close should return to the gallery.
- Replace Back to Folder with a recognizable folder/back icon.
- When all photos in the gallery are finished, change the main blue **Start Editing** button to **Finish Order**.

## 6. CRITICAL — Finish Order deletes photographs

I encountered this problem twice.

**Test 1:** Finished editing, chose Finish Order → Keep Originals. Both originals and edited photographs disappeared.

**Test 2:** Finished editing another folder, chose Finish Order → Delete Originals. Again, the entire photo content disappeared.

Investigate the root cause and fix it before continuing with other functionality.

### Expected behaviour

**Keep Originals:**
- Edited photographs remain in the main folder.
- `Original/` remains with the backed-up originals.
- No photographs are deleted.

**Delete Originals:**
- Edited photographs remain in the main folder.
- Only original backups created by PhotoX are deleted.
- No edited photographs, unrelated files or customer folders are removed.

Review the file paths, backup handling, order state and deletion logic.

Add automated tests covering both operations, including multiple photos and pre-existing files in `Original/`.

Do not recursively delete customer folders. Only delete files that PhotoX can safely identify as its own backups.

If deletion cannot be verified as safe, disable that option until it is fixed.

**This is the highest-priority issue.**

## 7. ID Photo Creator

### Editing controls
Make the ID Photo Creator consistent with the normal editor:

- Same slider appearance and order.
- Same exposure plus/minus controls.
- Same compact histogram with three triangle handles.
- Same common adjustments where applicable.

Reuse components where practical.

### Cut guides
- Remove the general Cut Guides option.
- For the **3×4 cm format**, provide only the specific option to include the **7 mm white strip below each photo**.
- Other formats do not need that option.

### Layout
- Arrange 3×4 ID photographs on a **landscape-oriented 10×15 cm sheet**.
- Other layouts should also use landscape orientation when it makes better use of the sheet.
- Maintain correct photograph dimensions and orientation.
- Arrange photos efficiently without stretching.

### Auto Align and background whitening
- Keep Auto Align working as currently designed.
- The background whitening needs improvement, but I will provide more suitable samples later.
- Fix obvious issues if possible, but do not spend excessive effort optimizing it against insufficient test data.

## 8. Duplicate PhotoX title and icon

On Windows, the application icon appears twice: once in the Windows title bar and again below it.

The PhotoX text also appears twice.

Choose a clean solution that displays the application title and icon only once.

Prefer simple native Windows window handling.

Preserve minimize, maximize, close and window resizing.

## 9. IMPORTANT — Performance optimization

PhotoX feels somewhat sluggish on **both Linux and Windows**.

Improving responsiveness is a major priority.

Investigate actual bottlenecks rather than making only cosmetic changes.

Pay particular attention to:

- Opening folders.
- Loading and selecting thumbnails.
- Opening photographs.
- Crop dragging and resizing.
- Exposure and other adjustment sliders.
- Histogram updates.
- Straightening and rotation.
- Moving between photographs.
- Save & Next.
- ID Photo Creator interactions.

Potential improvements:

- Use lower-resolution previews during interactive editing.
- Preserve full resolution and quality for saving.
- Avoid full-resolution processing for every slider movement.
- Reuse decoded images and cache previews when useful.
- Preload adjacent photographs.
- Avoid blocking the UI thread.
- Reduce unnecessary rendering and repeated calculations.

Make straightening and crop operations noticeably smoother.

Measure important operations before and after optimization and report the results.

Focus on practical, measurable improvements without unnecessary complexity.

The goal is comfortable operation on a typical four-core Windows studio computer.

## 10. Development priorities

Implement the feedback in a sensible order:

1. Fix the critical Finish Order data-loss issue.
2. Investigate and improve overall responsiveness.
3. Improve cropping and normal-photo editing.
4. Improve gallery, folder browsing and navigation.
5. Make ID Photo Creator consistent with the normal editor.
6. Fix remaining visual and layout issues.

Group related changes into meaningful commits.

Run appropriate automated tests after each significant change.

Preserve existing functionality and avoid unnecessary architectural rewrites.

## 11. IMPORTANT — Continue the original implementation plan

**After implementing and verifying all feedback above, do not stop.**

Return to the original approved project documentation, especially:

- `docs/PLAN.md`
- `docs/ROADMAP.md`
- `docs/OPEN_QUESTIONS.md`
- `docs/ARCHITECTURE.md`

Review the implementation status against the original requirements and roadmap.

Determine:

1. What has already been implemented and verified.
2. What is partially implemented.
3. What is still missing.
4. What remains intentionally deferred.
5. What is blocked by unavailable hardware, example photographs or decisions from me.

Then **continue autonomously implementing all remaining approved functionality that is feasible without additional input**.

Do not redo functionality that is already complete.

Do not treat this feedback prompt as limiting the project to only these corrections.

My feedback takes precedence over older requirements wherever they conflict. Otherwise, the original approved plan remains valid.

### Scope and limitations

- Continue toward completing the originally agreed PhotoX functionality.
- Keep direct Citizen printer integration deferred.
- Keep physical print calibration deferred.
- Keep final Romanian wording review deferred.
- Keep sample-dependent automatic background-whitening improvements open until additional pictures are available.
- Do not invent new features or expand the scope beyond the approved plan.
- Do not implement open points that explicitly require my decision.
- If an item is blocked, document it and continue with independent tasks.

If all feasible approved requirements are already complete, do not invent additional work. Instead, perform a final integration and regression-test pass and report what remains deferred.

### Autonomous execution

I want you to continue working independently for as long as meaningful, approved implementation work remains.

For each milestone:

1. Implement the functionality.
2. Test it.
3. Fix discovered issues.
4. Update relevant documentation.
5. Make meaningful Git commits.
6. Increment the version according to the existing conventions.
7. Continue to the next remaining milestone.

Do not stop after completing just one milestone unless a genuine blocker prevents further progress.

Avoid repeatedly asking for confirmation on decisions that are already covered by the plan.

## 12. Documentation and repository

- Continue using the existing GitHub repository.
- Archive this prompt unchanged as the next numbered Markdown prompt under `docs/prompts/`.
- Maintain `docs/PLAN.md` as the source of truth.
- Update the roadmap, architecture, decision log, implementation log and changelog where relevant.
- Keep documentation consistent with the actual implementation.
- Use meaningful Git commits.
- Continue versioning from **0.4.0**, following the established version rules.
- Push commits and version tags to GitHub when possible.

Do not unnecessarily generate additional documents or introduce complicated development processes.

## 13. Final report

When all feedback and remaining feasible tasks from the original plan are completed, provide:

1. **Implemented changes:** Summary of corrections and improvements.
2. **Finish Order bug:** Root cause, fix and verification.
3. **Performance:** Main optimizations and measured before/after results.
4. **Original roadmap:** Which requirements were already complete, which were finished during this iteration and which remain open.
5. **Tests:** Number of tests passed, Linux and Windows build/test status.
6. **Versions and commits:** Version numbers, important commit hashes and GitHub push status.
7. **Remaining open points:** Especially Citizen printer testing, physical calibration, translations and additional ID photo samples.
8. **Manual testing:** A short checklist for what I should test on Windows 11.

Only claim tests or functionality are verified when they have actually been tested.

## Overall objective

PhotoX should be a **simple, modern, reliable and responsive photo-editing application** for a small professional photo studio.

The central workflow remains:

**Open folder → Select photo → Crop/edit → Save & Next → Finish Order.**

Prioritize data safety, speed, intuitive controls and correct image processing.

**First incorporate and implement my feedback. Then continue completing the remaining approved functionality from the original PhotoX plan, autonomously, until everything feasible is implemented and tested.**