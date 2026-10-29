# PhotoX — Planning Revision and Autonomous Implementation

## 1. Authorization and objective

Read this entire prompt before starting.

This prompt approves revising the initial PhotoX plan and then implementing the application. It supersedes conflicting requirements, recommendations and approval rules in the existing planning documents.

First update and commit the planning documents. Then continue implementation in dependency order, as far as the available environment permits, without stopping for approval after each milestone.

Do not produce another large planning exercise. Resolve the decisions below, simplify the existing plan, and proceed to working software.

The owner wants to intervene only when a serious issue requires a decision. Routine implementation choices, reversible fixes, dependency compatibility adjustments and ordinary debugging should be handled autonomously.

## 2. Product context and engineering scope

PhotoX is primarily for the owner's mother, operating a small photo studio.

It is not currently intended to be sold, published as a product or distributed to other studios. Possible future sharing must not introduce requirements or infrastructure now.

The priorities are:

1. A simple, reliable daily workflow.
2. Good photographic output.
3. Straightforward implementation and maintenance.
4. Responsive operation on typical studio hardware.
5. Minimal unnecessary complexity.

The operator works on copies of customer photographs. Independent backups exist outside PhotoX.

Keep essential safeguards against incomplete saves, but remove the disproportionate journal, crash-recovery and transaction infrastructure proposed in the initial plan.

Do not build a general-purpose photo editor, file manager, printing platform or extensible framework.

## 3. Required first action: revise the existing plan

Read the existing repository instructions, archived prompt and relevant planning documents.

Update these documents consistently:

- `README.md`
- `AGENTS.md` and any equivalent agent instructions
- `docs/PLAN.md`
- `docs/ARCHITECTURE.md`
- `docs/ROADMAP.md`
- `docs/OPEN_QUESTIONS.md`
- `docs/DECISIONS.md`
- `docs/TEST_PLAN.md`
- `docs/DEVELOPMENT.md`
- `docs/IMPLEMENTATION_LOG.md`
- `docs/CHANGELOG.md`
- Mockups and their documentation
- The prompt archive index

Preserve stable requirement and decision IDs. Add IDs where needed rather than renumbering existing requirements.

Keep provenance/approval status separate from implementation progress. For example, “Owner requirement” and “Implemented” describe different things and should not occupy the same status field.

Mark answered open questions as decided. Remove superseded behavior from active requirements rather than leaving contradictory instructions.

Do not edit `prompt_01.md` or this archived prompt. Record later corrections separately.

Commit the planning revision, then begin implementation immediately. No further planning approval is required.

## 4. Technology and platforms

Retain the agreed stack:

- C#
- .NET 10
- Avalonia UI
- MVVM
- Magick.NET
- Git
- GitHub when the owner provides the remote repository

Develop primarily on Linux Mint. Windows 11 x64 is the production target. Windows 10, macOS and ARM are not required.

Validate and pin compatible package versions at implementation time. Do not adopt a framework upgrade merely because it exists.

Use Magick.NET directly for decoding, encoding, colour management, adjustments and geometric transformations wherever practical.

Remove the mandatory custom managed image-processing pipeline, SIMD implementation, float-buffer infrastructure and LUT optimization work from the initial architecture.

Custom image-processing code is a last resort when a required function cannot reasonably be achieved with the existing library. Document the limitation and choose the smallest necessary solution.

Ordinary crop geometry, sheet-placement calculations and model integration code are still expected where necessary.

Keep a modest separation between UI, image processing, file handling and pure geometry. Do not add abstractions without a concrete use.

## 5. Ordinary-photo workflow and exact Enter behavior

The application provides a built-in folder explorer and thumbnail gallery.

The operator opens a photograph, edits it, adjusts the crop and presses **Enter**.

**Enter in the ordinary-photo editor means Save & Next:**

1. Save the current photograph using its current edits and its currently selected crop.
2. Wait for the save to succeed.
3. Open the next photograph.

Saving must never reset, recenter or enlarge the crop. It must use exactly the crop selected by the operator.

When a new, previously unedited photograph opens:

- Activate the crop tool automatically.
- Select the default 10×15 preset.
- Choose portrait or landscape orientation to match the photograph.
- Start with the largest centered rectangle of the selected ratio that fits inside the photograph.
- Keep the crop ratio locked.
- If the photograph already matches the ratio, initially include the whole image.

Restore the saved crop and adjustments when reopening a photograph with retained edit history. Do not replace those settings with the default crop.

The visible **Save & Next** button performs the same action as Enter.

Also provide:

- Save without moving to the next photograph, with Ctrl+S.
- Previous.
- Next/Skip without saving.
- Undo/redo.
- Reset.
- Before/after.
- Clear unsaved-change indication.
- Order position and progress.

Do not wrap automatically from the last image to the first.

On a save failure, keep the current image and edits available and explain the problem. Do not advance as if saving succeeded.

Use background execution where necessary to keep the interface responsive, but do not introduce a background save queue or advance to the next image before saving finishes.

Ignore held-key auto-repeat. Editor-wide shortcuts must not interfere with dialogs, text fields, numeric inputs or controls using those keys.

Merely opening a photograph must not write it to disk. Skipping a photograph must not silently accept or save its default crop.

## 6. Crop sizes, borders and output resolution

Ordinary crop presets:

- 10×15 — default.
- 15×20.
- 13×18.
- Custom.

Do not include a named 15×23 preset. Its earlier mention was a misunderstanding associated with the previous Mitsubishi workflow.

Use these initial inch-based definitions:

- 10×15: 4×6 inches, 101.6×152.4 mm, ratio 2:3.
- 15×20: 6×8 inches, 152.4×203.2 mm, ratio 3:4.

Keep these physical dimensions in simple configuration data so testing can refine them.

Do not resize ordinary photographs to a fixed pixel resolution. Preserve the cropped photograph's native pixel resolution.

Allow interpolation required by straightening or other explicitly selected geometric transformations. “No resizing” does not prohibit the interpolation inherently needed by those operations.

Provide two clear composition modes:

- **Crop to fill** — default.
- **Whole photo with white borders** — retain the entire photograph and extend the canvas to the selected output ratio without unnecessarily resampling its pixels.

### Special 13×18 behavior

13×18 paper will not be used.

When 13×18 is selected:

1. Crop to the ratio of exactly 130×180 mm.
2. Center that photograph on the configured 15×20 output canvas.
3. Add white borders.
4. Preserve the cropped photograph's pixels wherever possible.

Use a consistent physical scale so the central photograph is intended to reproduce at 130×180 mm when the complete canvas is printed at the configured 15×20 size.

Support the corresponding landscape orientation.

## 7. Adjustments and image handling

Include:

- Exposure with slider and minus/plus buttons.
- Contrast.
- Saturation.
- White balance/temperature.
- **Tint**, for green–magenta correction.
- Luminance and RGB histogram.
- Levels: black point, white point and midtones.
- Conservative, single-photo Auto Correct.
- 90-degree rotation.
- Straightening.
- Undo/redo, reset and before/after.

Auto Correct must remain manually adjustable and undoable.

Do not implement batch apply or batch Auto Correct. Remove them from the committed implementation roadmap. Sequential editing with Save & Next remains essential.

Required image formats:

- JPEG.
- PNG.

HEIC/HEIF remains explicitly deferred and tracked as a future open point. Unsupported files should receive a useful explanation rather than silently disappearing.

Use:

- JPEG quality 95.
- 4:4:4 chroma.
- Embedded sRGB for edited outputs.
- Effective-PPI metadata where the selected physical dimensions define it.

Metadata alone must not be treated as a guarantee of printed dimensions.

Preserve appropriate source metadata in ordinary saved photographs, correcting orientation, dimensions and stale metadata as necessary.

Handle EXIF orientation correctly. Preserve PNG alpha and bit depth where supported.

Resolve contradictory support statements about CMYK JPEG and 16-bit PNG. State clearly which variants are supported and handle unsupported variants without crashing.

## 8. Saving, temporary originals and edit history

Ordinary photographs are saved in the opened folder under their existing filenames. A separate ordinary-photo export workflow is not required.

Keep the `Original` folder and edit history while an order is being processed. Their purpose is temporary re-editing and evaluation, not permanent archival backup.

On the first effective save:

- Preserve the untouched source in `Original`.
- Write the edited photograph at its original location and filename.
- Retain enough edit information to reopen from the preserved original without cumulative JPEG degradation.

On later saves, retain that original and re-render from it where available.

A simple sidecar such as `Original/.photox-order.json` is acceptable. Keep it small and versioned. No database or elaborate recovery framework is required.

Never overwrite an existing backup whose identity is uncertain. Use a fresh collision-safe name and record the association.

For an effectively unchanged photograph, mark it done/reviewed without unnecessarily re-encoding it. A selected crop that removes pixels is an effective change when explicitly saved.

Use a temporary output file and replace the destination only after writing succeeds. Handle ordinary failures such as insufficient disk space, read-only locations and files changed externally.

Remove the mandatory multi-step journal, automatic transaction recovery service and large randomized crash-recovery infrastructure from the initial plan.

Editing directly on removable media is allowed, with a clear warning recommending a local working copy.

## 9. Finish Order and cleanup

Add a **Finish Order** action.

It must offer:

- **Erase originals and edit history — default.**
- **Keep originals and edit history.**

Complete all required saves before cleanup. Do not clean up after a failed save.

If there are unresolved manual edits, let the operator decide whether to save them or return to editing. Do not silently discard them.

When erasing:

- Delete only originals and history known to belong to PhotoX's current order.
- Remove associated PhotoX temporary/recovery data and cached order thumbnails where applicable.
- Preserve the finished photographs.
- Preserve exported ID images.
- Preserve unrelated files, including unfamiliar content in an existing `Original` folder.
- Remove the `Original` directory only when it is empty.

Make the selected keep/erase behavior clear. One concise confirmation before erasing is sufficient; avoid repeated dialogs.

If cleanup fails, retain the finished output and report what remains.

After history has been erased, later editing starts from the remaining saved photograph. Do not imply that the earlier original or adjustments are still recoverable.

This temporary-per-order lifecycle supersedes the previous proposals for retaining originals indefinitely or removing the backup feature entirely from the final application.

## 10. ID Photo Creator

Keep the separate ID workspace and the two-step workflow:

1. Framing and adjustments.
2. Sheet preview and save.

Support these initial geometric photo sizes:

- 3×4 cm.
- 3.5×4.5 cm.
- 2×2 inches: exactly 50.8×50.8 mm, displayed as approximately 5.1×5.1 cm.
- 4×6 cm.
- 5×7 cm.
- 9×12 cm.
- Custom dimensions.

The 4×6 and 5×7 sizes in this ID list are **centimetres**, not inches.

Do not add country-specific document templates or conduct a broad biometric-compliance project. These are geometric presets.

Provide:

- Face-positioning oval and useful framing guides.
- Manual movement, zoom and rotation.
- Exposure, contrast, white balance and tint.
- Basic manual background-brightness improvement.
- Undo/reset and comparison where useful.
- **Auto Align**, running only when the operator presses its button.
- Manual correction after Auto Align.

Do not silently choose a person when several faces are detected. Let the operator choose or retain manual framing.

Evaluate a suitable offline face detector. Use a small, documented integration rather than an unnecessary collection of model fallbacks.

Automatic background whitening is **experimental only**. Investigate with bounded effort. Include it only if its results are useful; otherwise keep manual background adjustment and record the limitation.

Do not let experimental whitening delay the rest of the application.

The imported portrait must never be overwritten by the ID workflow.

## 11. ID sheets and the additional 7 mm strip

Default sheet: nominal 10×15 / 4×6 inches.

Initially render at 300 PPI. A nominal 4×6-inch sheet is 1200×1800 pixels.

Provide:

- Sheet preview.
- Simple uniform-grid arrangements calculated from dimensions.
- Selectable copy count.
- Spacing and margins.
- Optional cutting guides.
- JPEG and PNG output.

Do not build a sophisticated maximum-capacity packing algorithm. Offer sensible layouts, and preserve familiar lower copy counts even when more photographs could fit.

Do not shrink the selected ID dimensions merely to fit more copies.

### Additional requirement for the existing 3×4 template

For the 3×4 layout, ensure that at least the lower row can be cut out with a **7 mm white strip below each photograph**.

- The image area remains exactly 30×40 mm.
- Reserve at least 7 mm of white space immediately underneath.
- A lower-row photo can therefore be cut as a 30×47 mm piece, including the strip.
- Cutting guides must permit keeping that strip.
- Reduce the copy count if necessary.

Implement this within the existing 3×4 sheet behavior. Do not create another document/template category.

Treat it as the owner's requested studio layout, without claiming official compliance.

## 12. ID output location and filenames

Export is required for ID outputs.

Let the operator choose a fixed output folder and remember that choice.

Use sortable timestamp filenames, for example:

`ID_20261029_1845.jpg`

Use local date/time with a Windows-safe format. Minute precision is sufficient for the expected workflow.

If a filename already exists, append a suffix after the timestamp:

- `ID_20261029_1845_1.jpg`
- `ID_20261029_1845_2.jpg`

Never overwrite an existing output.

Do not require a separate incrementing photo-number system.

Exclude generated ID sheets from ordinary-photo processing by default.

## 13. Citizen printing boundary and research

Direct printing remains outside the current implementation scope.

The operator will print saved files using Citizen's software.

The preferred future integration is **Citizen LinkServer hot folders**, rather than a new direct Win32 printing implementation.

Do not implement automatic hot-folder submission now.

Mixed printing is not required. Do not build print grouping, queues, quantities or mixed-media management into PhotoX.

Relevant research:

- Citizen lists CY-02 4×6 and 6×8 media:
  https://www.citizen-systems.com/en/products/printer/photo/cy-02
  https://www.citizen-systems.com/en/products/printer/photo/photo-media
- LinkServer supports hot folders:
  https://www.citizen-systems.com/en/products/printer/photo/linkserver-for-photo
- Citizen's older CY driver manual lists larger driver image areas, including 1240×1844 and 1844×2436 pixels at 300 DPI:
  https://www.mitsubishi-photo.cz/citizen/citizen_drivers/cy_printerdriverinstruction_for7,8,10_v1.12_english.pdf

The older manual is reference evidence, not proof of the installed Windows 11/LinkServer behavior.

Keep ordinary crop ratios independent of printer overscan.

Do not make speculative printer-canvas or overscan assumptions mandatory. Keep any needed ID export calibration simple and configurable.

Provide a simple calibration image or test sheet. A physical print through the actual Citizen software must eventually verify:

- ID photo dimensions.
- The 7 mm strip.
- The centered 13×18 output.
- Any adjustment needed for driver enlargement or trimming.

Physical printing tests may wait for the owner without blocking unrelated implementation.

## 14. Interface, mockups and logo

Keep:

- Modern, simple, neutral dark UI.
- Large, clear primary actions.
- Built-in folder navigation.
- Compact editor filmstrip.
- Collapsible browse details where appropriate.
- Visible Save & Next.
- A clear Back to folder action.
- Separate Photos and ID Photos workspaces.

Mouse and keyboard are required. Touchscreen optimization is not required.

English and Romanian must be selectable directly from the main screen, with the selection remembered.

The owner will review Romanian wording. Keep terminology and forms of address consistent.

Update the generated mockups from their editable sources. The owner will provide written feedback rather than editing images.

Correct the mockup where the To do filter displays already-edited photographs.

Update mockups to reflect:

- Current crop presets.
- Tint.
- Revised ID sizes.
- The 7 mm strip.
- ID output-folder selection.
- Finish Order with keep/erase, defaulting to erase.
- Simplified saving behavior.

Add a simple, original **PhotoX logo and application icon** suitable for the main window, About screen and Windows installation. Reuse an editable asset where practical. Do not turn this into a branding project or delay functionality over logo alternatives.

Ensure the actual interface remains usable at common Windows resolutions and scaling. A large mockup alone is not sufficient verification.

## 15. Performance and caching

Retain the proposed reference machine as a provisional baseline:

- Four-core CPU.
- 16 GB RAM.
- SSD.
- 1080p-class display.

Use measurements rather than invented performance claims.

Retain the initial image-size policy for evaluation:

- Through 64 MP: normal supported range.
- 64–210 MP: slower operation permitted.
- Above 210 MP: refuse clearly.

Check memory before attempting expensive operations. If the proposed upper range proves unsafe or impractical, implement a conservative limit, document the evidence and continue with mainstream images. Do not build a custom streaming image engine merely to reach 210 MP.

Use prioritized thumbnail generation and bounded caching. Preload adjacent images only as useful and within a memory budget.

Persistent thumbnail cache defaults:

- 1 GB maximum.
- 30-day retention.
- Clear-cache action.

Finish Order cleanup should remove associated order cache entries where practical, without clearing unrelated orders.

For preview colour, use an explicitly documented sRGB-display assumption initially. Do not implement full monitor-profile management now. Record visible display discrepancies discovered during testing.

## 16. Sample photograph and test-data handling

The owner supplies this real reference:

`/home/norbert/Documents/PhotoX/id_photo/IMG_6153.JPG`

It is a 3456×2304 JPEG and is suitable for framing, alignment and background-adjustment trials.

Before any operation that could write an image:

1. Create a separate working copy in a dedicated test workspace.
2. Perform edits and cleanup tests only on those working copies.
3. Leave the supplied reference unchanged.

Never commit this image, its derivatives, real customer photographs or identifying test artifacts to Git.

Use generated or appropriately licensed synthetic fixtures for committed automated tests.

Do not claim robust face/background validation from this single portrait. Use it as an initial real example and expand testing when more samples are available.

## 17. Proportionate verification

Implement and run meaningful tests for:

- Default largest-centered crop and ratio locking.
- Saving the operator's selected crop exactly.
- Crop presets, orientations, borders and 13×18-on-15×20 placement.
- EXIF orientation.
- Adjustments and levels.
- Colour profiles and metadata.
- JPEG/PNG output.
- Save & Next success and failure.
- Temporary original creation and re-editing.
- Backup filename collisions.
- Finish Order keep and erase behavior.
- Cleanup preserving finished output and unrelated files.
- ID dimensions, layouts and the 7 mm strip.
- Timestamp collisions.
- Corrupt inputs and resource limits.
- Localization.
- Large-folder responsiveness and memory.
- Linux and Windows compatibility.

Do not retain universal, mathematically invalid preview/export equality thresholds.

Nonlinear adjustments before versus after resizing need not produce identical pixels. Test processing equivalence at matching resolution, geometry separately, and representative preview fidelity with appropriate tolerances.

Do not treat every JPEG quality-95 image as mathematically lossless or require one universal PSNR threshold for all photographs.

Use focused save-failure tests instead of an elaborate transaction-recovery campaign.

Avoid rigid coverage percentages, excessive screenshot baselines and duplicated tests that merely restate the implementation.

Test important behavior, inspect actual outputs, fix failures and continue.

Run Windows/Linux CI once the remote is available. Do not claim Windows execution, physical printing, CI results or owner acceptance that did not happen.

## 18. Dependencies, installation and repository

Use free dependencies whose licences permit the actual intended use. Commercial-use permission is preferred.

Do not impose requirements for selling or distributing a proprietary product.

However, use in a paid photo studio must not automatically be treated as “personal use” under a dependency's licence. Check the applicable terms. If a dependency is unsuitable, prefer an available compatible alternative rather than creating a broad legal-review project.

Retain necessary notices.

Velopack is accepted for installation. A self-contained folder build is acceptable for early trials.

No code-signing certificate is required. Do not promise that unsigned installation bypasses Windows warnings or local policy.

The owner will create the GitHub repository after resolving account access. Do not make missing remote access a blocker for local implementation, tests and commits. Do not alter credentials or claim a remote exists.

When a remote becomes available, add the planned CI and push through the authorized workflow.

## 19. Versioning, commits and documentation

Start implementation at **0.1.0**.

- Small changes and fixes increment the patch.
- Significant additions or milestones increment the minor version.
- Reserve 1.0.0 for a sufficiently stable release.
- Do not bump versions for every internal edit.
- Do not tag unfinished or unverified milestones.

Use small, meaningful commits containing coherent changes and appropriate verification.

Keep documentation current, but proportionate:

- Update requirements when behavior or implementation status changes.
- Record significant decisions and deviations.
- Record completed milestones, verification and limitations.
- Update the changelog for meaningful version changes.

Do not repeat a long narrative across several documents for every small commit.

## 20. Autonomous execution rules

Proceed through the revised roadmap without routine approval requests.

You are authorized to:

- Revise the planning documents and mockups as directed.
- Create application projects and code.
- Install necessary project dependencies and development tooling within the available permissions.
- Research documentation and resolve compatibility issues.
- Build, run, inspect and test the application.
- Fix discovered defects.
- Refactor within the agreed architecture.
- Create development builds and meaningful local commits.
- Continue to subsequent implementation milestones.

Prioritize a working vertical workflow early:

**Open folder → open photo → adjust crop → apply a basic correction → save → next photo.**

Then expand adjustments, ID workflows and stabilization.

Do not wait for physical-printer tests, Romanian review or unavailable Windows hardware when independent implementation work remains.

If one feature is blocked, isolate it, record the limitation and continue with other authorized work.

Ask the owner only when:

- A genuine contradiction materially changes expected behavior.
- A major requirement cannot be implemented reasonably.
- A dependency introduces an unavoidable cost or unsuitable usage restriction.
- A proposed solution would materially expand scope or risk unrelated user data.
- Progress requires access or a decision that cannot reasonably be inferred.

Routine choices and ordinary bugs are not reasons to stop.

At significant checkpoints, report briefly:

- What now works.
- What was actually verified.
- Current version and relevant commits.
- Remaining limitations.
- Any specific owner action required later.

When the current environment prevents further useful progress, provide a runnable build where possible and an honest handoff. Do not equate unavailable manual acceptance with completed testing.

## 21. Explicit resolution of the original open questions

Apply these owner decisions to `OPEN_QUESTIONS.md`. Mark each question decided and link it to the appropriate updated requirement or decision record. The detailed instructions earlier in this prompt explain implementation behavior.

Do not reopen these questions or substitute their original recommended defaults.

| Question | Owner decision |
|---|---|
| **OQ-001 — Media dimensions** | Use the researched initial dimensions in §§6 and 13. Keep them configurable and verify physical output later through Citizen software. |
| **OQ-002 — Future printing** | Option B: Citizen LinkServer hot folders. Integration remains deferred. |
| **OQ-003 — Mixed printing** | Not required. Do not implement mixed-printing management. |
| **OQ-004 — HEIC/HEIF** | Option A: unsupported initially; retain as an explicitly deferred future point. |
| **OQ-005 — Output quality** | JPEG quality 95, 4:4:4. Preserve native crop resolution; interpolate only where geometry requires it. Effective-PPI metadata where defined. ID sheets initially 300 PPI. |
| **OQ-006 — Unchanged photographs** | Mark done/reviewed without unnecessary re-encoding. Enter otherwise saves the current edits and selected crop, then advances. Originals remain temporary until Finish Order. |
| **OQ-007 — Backup collisions/retention** | Keep handling simple. Never overwrite an uncertain existing backup; use a unique name. Retention is controlled by Finish Order. |
| **OQ-008 — Bulk corrections** | No batch apply or batch Auto Correct. Remove them from the implementation roadmap. |
| **OQ-009 — Edit persistence** | Keep originals and edit history while processing the order. Finish Order offers Keep or Erase; **Erase is selected by default**. |
| **OQ-010 — Automatic whitening** | Experimental only. It must not block the rest of implementation. |
| **OQ-011 — ID templates** | Only the geometric sizes specified in §10, plus Custom. Include the 7 mm lower strip within the existing 3×4 sheet behavior. No additional country-specific templates. |
| **OQ-012 — Output colour** | Option A: convert edited outputs to sRGB and embed the profile. |
| **OQ-013 — Dependency licences** | Prefer free commercial-use dependencies. No product-sale or closed-source-distribution requirement. Verify that licences permit the actual studio use; do not assume personal-use-only permission covers it. |
| **OQ-014 — Enter shortcut** | Enter in the editor is Save & Next, using the **current edits and currently selected crop**, without resetting the crop. |
| **OQ-015 — Installer/updates** | Velopack accepted. Manual updates initially. |
| **OQ-016 — Performance/image limits** | Accept the proposed reference hardware and initial image-size targets, subject to measured feasibility as described in §15. |
| **OQ-017 — Touchscreen** | Not required. Mouse and keyboard first. |
| **OQ-018 — Cache retention** | Accept the original recommendation: 1 GB maximum, 30 days, manually clearable. Also clean associated order cache during Finish Order where practical. |
| **OQ-019 — Removable media** | Option A: allow editing with a warning recommending a local working copy. |
| **OQ-020 — Metadata** | Keep appropriate source metadata, updating fields affected by editing and removing stale embedded previews. |
| **OQ-021 — Additional adjustments** | Add tint. Other extra adjustments remain outside current scope. |
| **OQ-022 — Romanian review** | The owner will review the Romanian wording. |
| **OQ-023 — Display colour** | Initial sRGB-display assumption. Full monitor-profile management is deferred; investigate visible discrepancies found during testing. |
| **OQ-024 — Interim printing** | Print through Citizen software. Ordinary photographs use Save; ID outputs use the remembered export folder and timestamp filenames. |
| **OQ-025 — ID output location/names** | User-selected, remembered folder. `ID_[date]_[time]`; on conflict append `_1`, `_2`, etc. Never overwrite. |
| **OQ-026 — GitHub repository** | The owner will create the remote. Continue local implementation and commits meanwhile. |
| **OQ-027 — Distribution** | One studio. Selling or sharing is outside current scope and must not drive additional engineering. |
| **OQ-028 — Code signing** | Not required. |
| **OQ-029 — Whole-photo borders** | Option B: include Whole photo with white borders alongside the default Crop to fill mode. |

Only raise a new question if implementation reveals a genuinely unresolved issue with material consequences. Routine technical choices remain delegated to the agent.


**Begin by revising and committing the plan, then continue directly into implementation.**