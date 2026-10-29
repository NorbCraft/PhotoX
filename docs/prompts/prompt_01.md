# PhotoX — Initial Project Planning Prompt

## Role and objective

You are the lead software architect, product designer and technical planning agent for a new desktop application called **PhotoX**.

PhotoX is a lightweight photo-editing and photo-preparation application intended primarily for a small professional photo studio.

The application will replace an unnecessarily complicated workflow involving ACDSee, Photoshop and older Mitsubishi photo-kiosk software.

The main user is a photo-studio operator who regularly processes large numbers of customer photographs and needs a straightforward, fast and reliable workflow.

The software should prioritize **simplicity, efficiency, photo quality and reliability** over feature count.

## CRITICAL: Planning only

**Do not implement the application in this phase.**

Your task is to:

1. Create and initialize the PhotoX Git repository.
2. Research and validate the proposed architecture and technology choices.
3. Produce detailed requirements, architecture and implementation plans.
4. Identify uncertainties, risks, missing decisions and alternative approaches.
5. Create actual visual UI mockup images for review.
6. Establish documentation, versioning, testing and development conventions.
7. Commit all planning deliverables to the repository.
8. Provide a summary of the completed plan and questions requiring user decisions.

Do not begin implementing application functionality, generating prototype application code, installing application dependencies or developing production components.

**Stop after the complete planning deliverables are committed. Wait for explicit approval before implementing anything.**

The plan will be reviewed and modified by the project owner before development begins.

---

# 1. Product vision

PhotoX should feel like a dedicated professional photo-studio tool rather than a simplified Photoshop clone.

Typical workflow:

1. Customer brings photographs on a phone or USB stick.
2. Operator transfers them into a local folder.
3. Operator opens PhotoX.
4. Operator navigates to the folder using the application's built-in file explorer.
5. The application displays photographs as thumbnails.
6. Operator opens a photograph for editing.
7. A crop tool automatically activates with the default 10×15 cm aspect ratio.
8. Operator adjusts crop, exposure, colours or histogram where necessary.
9. Operator presses **Save & Next**.
10. The photograph is saved and the next photograph opens automatically.
11. Continue until the entire order is finished.

The objective is to minimize mouse clicks, dialogs and repetitive manual operations.

The user should be able to process a folder containing 100 or more photos comfortably.

PhotoX also needs a dedicated ID photograph creator with alignment, basic editing and print-sheet layout functionality.

## Design principles

- Simple enough for someone without advanced computer knowledge.
- Modern, clean and visually attractive.
- Fast keyboard-and-mouse workflow.
- Large, clearly labelled controls.
- Avoid excessive settings and nested menus.
- Make frequently used functions immediately accessible.
- No mandatory cloud services, subscriptions or internet connection.
- All image processing should happen locally.
- No customer photographs should be sent to external services.
- Preserve photographic quality.
- Handle large photo orders without freezing.
- Reliability and safe saving are more important than advanced features.

---

# 2. Technology and platforms

## Preferred technology stack

- Language: C#
- Runtime: .NET 10
- UI framework: Avalonia UI
- Architectural pattern: MVVM, with clear separation of concerns
- Image processing: Magick.NET
- Image rendering: Avalonia's rendering infrastructure; consider additional libraries only when justified
- Source control: Git
- Repository hosting: GitHub
- Main development platform: Linux Mint
- Primary production platform: Windows 11 x64

These are preferred choices, not permission to blindly introduce unnecessary dependencies.

Validate their suitability, compatibility, licensing and maintainability.

If a significantly better technology or architecture exists, document it with a comparison and recommendation before implementation. Do not silently change the agreed stack.

## Platform expectations

Development and most testing should be possible natively on Linux.

The same application should build and run on Windows 11.

GitHub Actions should eventually provide automated builds and tests for Linux and Windows.

The developer has access to:

- A Linux Mint development computer.
- A Windows 11 virtual machine for compatibility testing.
- A separate physical Windows computer for final testing.
- A dual-boot Windows laptop if required.

Windows 10 support is not required.

Do not sacrifice Windows user experience just to achieve perfect Linux integration. Windows remains the primary production target.

The initial implementation will not support direct printer communication.

---

# 3. Main application structure

Plan the application around two main workspaces.

## A. Photo Editor

Used to prepare ordinary photographs individually or in batches.

## B. ID Photo Creator

Used to prepare identification photographs and arrange them onto printable sheets.

The workspaces should share common image-processing components and maintain a consistent user interface.

The application should provide English and Romanian translations.

Language selection must be available directly from the main screen, not buried in settings.

Romanian should be supported as a primary user language.

---

# 4. File explorer and photo gallery

Implement a built-in file explorer rather than relying exclusively on an operating-system folder-selection dialog.

Requirements:

- Navigate local drives and folders.
- Select a folder containing photographs.
- Display the photographs as thumbnails.
- Show filename and basic image information.
- Open photographs for editing.
- Remember the last opened folder.
- Allow navigation between photographs.
- Show progress within an order, such as 23 / 85 photographs.
- Clearly distinguish processed and unprocessed photographs.
- Avoid loading all full-resolution images into memory simultaneously.
- Generate and cache thumbnails where appropriate.
- Preload adjacent photographs to make Next/Previous navigation responsive.

Consider support for multiple selection and simple operations on selected photographs.

Avoid building a general-purpose Windows Explorer replacement. Focus on the functions required for photo orders.

The `Original` backup folder must not appear as an ordinary part of the current photo order.

---

# 5. Ordinary photo editing

## Cropping

Cropping is one of the most important functions.

When a photograph opens in the editor, the crop tool should automatically become active.

The default crop preset is:

**4×6 inches / nominal 10×15 cm, portrait or landscape as appropriate.**

Do not assume that nominal centimetre measurements match the printer's actual media dimensions exactly.

Separate:

- Aspect ratio
- Physical print dimensions
- Pixel resolution
- Output DPI/PPI

The plan must address this distinction and leave exact printer-specific physical dimensions configurable.

## Required crop presets

- 10×15 cm — default
- 15×20 cm
- Custom crop ratio / dimensions

The larger formats must already exist as crop options in the first application version.

The final printer format and exact physical media measurements remain open points.

Cropping requirements:

- Adjustable crop frame.
- Move and resize crop area.
- Preserve the selected aspect ratio.
- Portrait and landscape orientation.
- Crop preview.
- Reset crop.
- Fit crop to photograph.
- Avoid unnecessary image upscaling.
- Preserve maximum practical image quality.

Automatic activation of the crop tool must not modify the source file until the user explicitly saves.

## Basic editing

The first implementation should include:

- Exposure/brightness adjustment.
- Plus and minus buttons for quick exposure adjustment.
- Exposure slider.
- Contrast adjustment.
- Saturation adjustment.
- White-balance / colour-temperature adjustment.
- 90-degree rotation.
- Basic straightening.
- Undo and redo.
- Reset adjustments.
- Before/after comparison.

Keep these controls visually simple.

## Histogram and levels

Provide a useful live histogram.

Requirements:

- Luminance histogram.
- RGB channel information.
- Black-point adjustment.
- White-point adjustment.
- Midtone/gamma adjustment.
- Live image preview.
- Reset levels.

Evaluate how to implement automatic correction using the histogram.

Avoid overly aggressive histogram equalization and colour correction, particularly on skin tones.

## Automatic correction

Provide an Auto Correct button.

Automatic correction should attempt to improve exposure and tonal range while preserving natural-looking photographs.

The result must remain manually adjustable.

A future bulk Auto Correct operation should be considered and documented.

The plan should clarify whether it belongs in the initial implementation or a later milestone.

---

# 6. Batch editing workflow

This is a core PhotoX feature.

The editor should support a fast sequential workflow.

### Save & Next

The operator should be able to:

1. Open a photograph.
2. Adjust crop or image settings.
3. Press Save & Next.
4. Have the edited image saved automatically.
5. Immediately continue with the next photograph.

The next photograph should already be preloaded whenever practical.

Provide:

- Save & Next.
- Previous.
- Next / Skip.
- Save without leaving.
- Reset current photo.
- Progress indicator.

Plan convenient keyboard shortcuts, such as Enter, arrow keys and other suitable combinations.

Shortcuts must not create accidental overwrites.

The interface should clearly indicate whether a photograph has unsaved modifications.

Consider optional batch operations such as applying the same tonal correction to multiple selected photographs.

The design must remain efficient even when handling hundreds of photographs.

---

# 7. File saving and original backups

Unlike a fully non-destructive editor, PhotoX should save ordinary edited photographs directly into the customer's original folder.

However, a backup mechanism is required.

## Initial backup strategy

When an image is saved for the first time:

1. Create a subfolder named `Original` inside the currently opened photo folder if it does not already exist.
2. Move the original photograph into `Original`.
3. Save the edited photograph under the original filename and location.
4. Preserve the original backup during subsequent edits.

Example:

Before editing:

    CustomerOrder/
      IMG_001.jpg
      IMG_002.jpg

After editing IMG_001:

    CustomerOrder/
      IMG_001.jpg
      IMG_002.jpg
      Original/
        IMG_001.jpg

This is the initial strategy for evaluation. It may be changed before the final release.

### Reliability requirements

Plan protection against:

- Application crashes while saving.
- Interrupted or failed writes.
- Insufficient disk space.
- Read-only files and folders.
- Backup filename collisions.
- Multiple edits of the same photograph.
- Invalid or corrupted images.
- Incorrect file extensions.
- Unintentional image quality degradation.
- Accidental loss of original metadata.

Use safe temporary-file operations and replacement strategies as appropriate.

Clearly document how recovery works if an error occurs halfway through saving.

An existing original backup must never be silently overwritten.

The exact policy for saving unchanged photographs should be documented as an open decision if not obvious from the requirements.

---

# 8. Supported image formats and quality

Required initial formats:

- JPEG
- PNG

HEIC/HEIF remains an open point for future versions.

Other formats, including TIFF and RAW, are out of initial scope unless a strong reason is identified during planning.

Quality requirements:

- Correct EXIF orientation.
- Preserve appropriate metadata where practical.
- Correct handling of embedded colour profiles.
- Reliable conversion to suitable output colour spaces.
- High-quality interpolation for crop and resize.
- Avoid cumulative JPEG degradation where possible.
- Configurable JPEG export quality if needed.
- Prevent unexpected colour shifts.
- Avoid unintentional resizing when editing ordinary photos.

Describe the complete image-processing pipeline in the architecture.

Image operations must be testable independently of the UI.

---

# 9. ID Photo Creator

Implement a separate ID photo workflow.

The objective is to prepare a portrait and create a printable photo sheet.

## Basic workflow

1. Open an existing portrait.
2. Choose an ID photo dimension.
3. Display a crop and face-positioning interface.
4. Show visual framing guides.
5. Apply optional automatic face alignment.
6. Allow manual correction of the alignment.
7. Adjust brightness, contrast and background where necessary.
8. Preview the resulting ID photographs on a 10×15 cm page.
9. Choose layout and number of copies.
10. Save a new print-ready image.

The imported portrait should not be overwritten by the ID photo creator.

## Face alignment

Display an oval or head-positioning guide that helps the operator position the face correctly.

Provide an **Auto Align** button.

Automatic alignment should only run when explicitly requested.

It should attempt to identify the face and position it according to the selected template.

The result must remain manually adjustable.

Evaluate suitable offline face-detection approaches and their technical and licensing implications.

Automatic face detection must never be treated as a guarantee of correct official biometric positioning.

## Background adjustment

The first implementation should include basic manual adjustment:

- Brightness/exposure.
- Contrast.
- White balance.
- Background brightness improvement.

Also investigate an optional automatic background correction or whitening feature.

Automatic background correction should be treated as an experimental or separately validated feature if the output cannot be made sufficiently reliable.

Document limitations involving hair, clothing, shadows and official document-photo requirements.

## Initial ID formats

Support configurable dimensions, initially including:

- 3×4 cm
- 3.5×4.5 cm
- 5×5 cm
- 9×12 cm
- Custom dimensions

These are geometric print presets, not claims of compliance with particular national identification documents.

Templates may later include country-specific requirements.

## Sheet layout

Use 10×15 cm as the initial output sheet size.

Provide:

- Page preview.
- Layout selection.
- Automatically arranged photographs.
- Adjustable number of copies.
- Appropriate spacing.
- Optional cut guides.
- Correct physical dimensions.
- Reliable export resolution.

Example layouts to evaluate:

- Eight 3×4 cm photographs.
- Six 3.5×4.5 cm photographs.
- Up to six 5×5 cm photographs.
- One 9×12 cm photograph.
- Other layouts that efficiently use a 10×15 cm page.

Do not hardcode these counts without validating geometry, spacing, orientation and printable-area constraints.

Plan an adaptable sheet-layout engine rather than creating a separate hardcoded implementation for every size.

The ID creator should save the completed layout as a new print-ready JPEG or PNG.

---

# 10. Printing — explicitly deferred

Do not implement direct printing in the first version.

The anticipated printer is:

**Citizen CY-02 professional dye-sublimation printer.**

The printer supports different media sizes, including approximately 10×15 and 15×20 cm. 

The architecture must leave room for future printing without requiring a major rewrite.

Plan future support for:

- Windows printer discovery.
- Citizen Windows printer driver.
- Paper size selection.
- Media compatibility.
- Print quantities.
- Print queue and status.
- Print-job error handling.
- Correct colour management.
- Accurate physical print dimensions.
- Individual and batch printing.
- ID photo sheets.

Identify interfaces and architectural boundaries that would make future printing straightforward.

**Do not implement or prototype printer integration now.**

The initial application should prepare and save photographs correctly.

---

# 11. User interface and UX

The UI should be:

- Modern.
- Minimalistic.
- Intuitive.
- Professional.
- Consistent.
- Comfortable for prolonged daily use.

Avoid large numbers of nested menus, floating tool windows and Photoshop-like complexity.

The operator should reach the most frequent functions directly.

Suggested layout elements:

- Built-in file explorer.
- Photo thumbnail gallery.
- Large image preview.
- Crop handles and image guides.
- Simple editing controls.
- Histogram panel.
- Save & Next button.
- Progress display.
- Language selector.
- Separate ID photo workspace.

Support both English and Romanian.

The language selector should be easily accessible from the main screen.

Consider light and dark themes, but prioritize usability and image-preview accuracy.

Mouse and keyboard operation should be first-class. Touchscreen compatibility should be considered without making it an initial hard requirement.

Use sensible scaling and readable controls at common Windows display resolutions.

---

# 12. UI mockups — mandatory deliverable

Create actual image mockups illustrating proposed PhotoX interfaces.

Do not provide only written descriptions.

Produce at least these mockups:

### Mockup 1 — Main screen and file explorer
Show the folder explorer, thumbnail gallery, language selection, workspace navigation and a clear way to begin editing.

### Mockup 2 — Ordinary photo editor
Show a large photograph, default 10×15 crop overlay, exposure and contrast controls, histogram, navigation and Save & Next.

### Mockup 3 — ID photo editor
Show a portrait, face-positioning oval, Auto Align control, manual adjustment controls and background correction options.

### Mockup 4 — ID photo print-sheet preview
Show a 10×15 page with multiple ID photographs, size selection, layout selection, copy count and Save Layout.

Additional mockups or alternative designs are welcome if they help resolve design decisions.

Requirements:

- Generate actual image files, preferably PNG, with editable SVG sources where practical.
- Store them under `docs/mockups/`.
- Use realistic desktop window proportions.
- Include Romanian UI examples where useful.
- Keep the design clean and implementation-feasible.
- Use synthetic or placeholder photographs, not actual customer data.
- Clearly label the mockups as concepts rather than implemented interfaces.

Document the proposed navigation structure and reasoning behind the designs.

Mockups are intended for feedback and may be changed before implementation.

---

# 13. Software architecture

Create a detailed technical architecture.

The architecture must cover:

- Application entry point and composition.
- UI and view models.
- Navigation.
- Folder and file browsing.
- Thumbnail generation and caching.
- Image decoding and encoding.
- Image editing pipeline.
- Image preview rendering.
- Crop calculations.
- Histogram and levels processing.
- Automatic corrections.
- Face detection and alignment.
- ID photo layout generation.
- File saving and backup strategy.
- Undo and redo.
- Localization.
- Configuration management.
- Logging and error handling.
- Future printing integration.
- Automated testing.

Prefer modular components with clear interfaces.

Separate image-processing logic from UI logic so that image algorithms can be tested independently and reused.

Avoid unnecessary abstraction, microservices or excessive complexity.

The result should be maintainable by a developer working with AI coding agents.

Provide:

- Proposed repository structure.
- Component responsibilities.
- Data flow.
- Dependency relationships.
- Important interfaces.
- Image processing and saving sequence.
- Application-state management.
- Performance considerations.
- Architecture diagrams, preferably Mermaid diagrams embedded in Markdown.

Identify which architectural decisions are finalized and which are proposals awaiting approval.

---

# 14. Reliability, performance and testing

The application is intended for actual commercial use.

Plan tests for:

- Correct crop ratios and dimensions.
- Portrait/landscape orientation.
- EXIF rotation.
- Histogram calculation.
- Image adjustments.
- Colour profiles.
- Image quality after export.
- Multiple Save & Next operations.
- Backup creation and recovery.
- Corrupt files.
- Long filenames and unusual characters.
- Large folders with hundreds of photographs.
- Memory consumption and responsiveness.
- Face alignment.
- ID template geometry.
- Exact sheet dimensions.
- Localization.
- Windows and Linux compatibility.

Prefer automated unit and integration tests for important image and filesystem operations.

Include plans for representative image fixtures and visual regression tests where useful.

Real customer photo samples will be provided separately during implementation.

Do not create or commit real customer photographs.

Plan continuous integration through GitHub Actions for Windows and Linux.

The final product will be validated on a physical Windows 11 computer.

---

# 15. Development workflow and versioning

This project will be implemented with AI coding agents after planning approval.

The development process must be structured so agents can work independently for meaningful periods while following approved requirements.

## Versioning

Use semantic-style versioning with the initial development version:

**0.1.0**

Conventions:

- Small changes and bug fixes: increment patch version.
  - Example: 0.1.0 → 0.1.1.
- Larger functional additions or significant milestones: increment minor version.
  - Example: 0.1.5 → 0.2.0.
- Major version 1.0.0 is reserved for a sufficiently stable release.

Document the release criteria and versioning policy.

During planning, identify 0.1.0 as the intended initial implementation version. Do not publish a software release or tag a nonexistent application during the planning phase.

## Git

Use GitHub as the repository host.

Create a repository named `PhotoX`.

Prefer a private repository initially unless otherwise instructed.

If GitHub authentication or repository creation is unavailable, initialize the local repository and document the exact remaining action. Do not falsely claim the remote repository was created.

Use meaningful commit messages.

Each future implementation milestone should comprise coherent, reviewable changes, accompanied by appropriate tests and documentation.

Avoid large commits combining unrelated functionality.

Do not create unnecessary commits for trivial intermediate states.

## Autonomous implementation — future instruction

After explicit approval of the final plan, future development agents should:

- Follow the approved architecture.
- Implement milestones in dependency order.
- Work autonomously within agreed scope.
- Run applicable tests.
- Fix problems discovered during implementation.
- Commit meaningful completed steps.
- Update version numbers appropriately.
- Keep documentation aligned.
- Record design changes and implementation results.
- Continue to the next approved milestone without unnecessary requests for confirmation.

Ask for clarification when requirements conflict, when a fundamental design decision is missing, or when a change would significantly alter the approved architecture or functionality.

Do not silently redefine the project's scope.

**These autonomous implementation rules are for future phases only. They do not authorize implementation in the current planning task.**

---

# 16. Documentation and source of truth

Documentation must be maintained throughout the project.

The main authoritative document is:

`docs/PLAN.md`

This is the project's **source of truth**.

It should clearly identify approved requirements, proposed approaches, deferred features, unresolved issues, milestones and acceptance criteria.

Use stable requirement IDs, for example:

- FR-001 — Built-in folder explorer.
- FR-002 — Photo thumbnail gallery.
- FR-003 — Default crop behaviour.
- FR-004 — Save & Next workflow.
- FR-005 — Original-file backup.
- FR-006 — Exposure and histogram correction.
- FR-007 — ID photo creator.

These are examples, not a complete list.

Requirements should be traceable to implementation milestones and verification tests.

## Required documentation structure

Create the following documents:

`README.md`
- Project overview.
- Current status.
- Documentation entry points.
- Development platform and expected production platform.

`docs/PLAN.md`
- Authoritative requirements and scope.
- Approved decisions.
- Assumptions.
- Functional and non-functional requirements.
- Version 1 goals.
- Deferred functionality.
- Acceptance criteria.
- References to supporting documents.

`docs/ARCHITECTURE.md`
- Proposed technical architecture.
- Technology choices and rationale.
- Module structure.
- Data flows.
- Interfaces.
- Diagrams.
- Dependencies.
- Performance and reliability considerations.

`docs/ROADMAP.md`
- Implementation phases.
- Dependencies between milestones.
- Proposed version increments.
- Completion criteria.
- Testing expectations.
- Risks and blockers.

`docs/OPEN_QUESTIONS.md`
- Unresolved questions.
- Options and trade-offs.
- Proposed defaults.
- Priority and impact.
- Decisions required from the project owner.

`docs/DECISIONS.md`
- Architectural and product decision log.
- Date.
- Decision.
- Rationale.
- Status.
- Related requirement IDs.

`docs/TEST_PLAN.md`
- Verification strategy.
- Automated tests.
- Manual testing.
- Platform testing.
- Image-quality validation.
- Release acceptance requirements.

`docs/IMPLEMENTATION_LOG.md`
- Initially document its future purpose.
- Once implementation begins, track significant completed changes, validation results, issues and related commits.

`docs/CHANGELOG.md`
- Track releases and meaningful changes once software development begins.

`docs/mockups/`
- Actual generated interface mockup images.
- Optional editable source assets.
- Descriptions or index connecting mockups to proposed workflows.

`docs/prompts/`
- I will save here the promps and I will provide you the file for the next prompt.
- Maintain a chronological archive of prompts used to direct agents.
- Future prompts should use sequential filenames.
- Do not replace or silently modify earlier prompt records.
- Where possible, associate later prompts with commits, decisions and implementation milestones.

Avoid duplicating authoritative requirements in several documents.

Supporting documents should reference `docs/PLAN.md`, and conflicting instructions must be identified rather than silently resolved.

Any substantial design change during implementation must be reflected in the source of truth.

---

# 17. Open points and uncertainties

The plan must explicitly track uncertainties, including:

- Exact physical dimensions of Citizen printer media.
- Future Citizen printer integration.
- Handling of mixed print sizes.
- HEIC/HEIF support.
- Image export resolution and DPI policies.
- Policy for saving unchanged photographs.
- Original backup collision and retention policies.
- Batch automatic-correction behaviour.
- Automatic background whitening quality.
- Offline face detection approach.
- Country-specific biometric photograph templates.
- Printer colour profiles and colour management.
- Touchscreen requirements.
- Commercial licensing of dependencies.
- Future application installer and update mechanism.
- Exact performance targets and supported image sizes.

Add any additional unresolved matters discovered during planning.

For each open point, document:

- Why it matters.
- Possible options.
- Recommended default.
- Impact on development.
- Whether a decision is needed before implementation or can safely be postponed.

Do not invent finalized requirements where an important decision remains unresolved.

---

# 18. Implementation roadmap

Divide development into independently testable milestones.

A suggested sequence is:

**Phase A — Foundation**
- Application architecture.
- Basic UI shell.
- Localization.
- Settings and logging.

**Phase B — Photo browsing**
- Folder explorer.
- Gallery and thumbnails.
- Image loading.
- Navigation.

**Phase C — Basic editing**
- Crop presets.
- Rotation.
- Exposure and contrast.
- Save & Next.
- Original backup handling.

**Phase D — Advanced basic editing**
- Histogram and levels.
- White balance and saturation.
- Auto Correct.
- Improved batch workflow.

**Phase E — ID photo creator**
- ID templates.
- Face alignment.
- Background correction.
- Sheet-layout generator.
- Export.

**Phase F — Stabilization**
- Automated and manual tests.
- Performance improvements.
- Windows 11 verification.
- Installer and release preparation.

These phases are proposals.

Evaluate and improve the sequence, split phases when useful and ensure each milestone provides a meaningful, testable result.

Define proposed version boundaries starting with 0.1.0.

Do not equate task completion with successful implementation unless its acceptance criteria and tests pass.

---

# 19. Planning deliverables and completion criteria

The planning task is complete only when:

1. The PhotoX repository exists locally and on GitHub if access is available.
2. The required planning documents have been created.
3. The requirements are clearly organized and traceable.
4. The architecture is sufficiently detailed to guide implementation.
5. Platform and dependency choices have been evaluated.
6. At least four actual UI mockup images exist.
7. Important risks and unresolved questions are recorded.
8. The implementation roadmap includes testing and acceptance criteria.
9. The initial prompt has been archived.
10. Planning files have been committed with meaningful commit messages.
11. No application functionality has been implemented.

## Final response expected from the agent

After completing planning, provide:

### A. Repository status
Repository name, location, GitHub URL if created, and commit hash.

### B. Planning summary
Brief summary of architecture, proposed functionality and implementation phases.

### C. UI mockups
Paths or links to the generated mockup images and the design choices they illustrate.

### D. Important open decisions
Prioritized list of decisions that require the project owner's input.

### E. Potential improvements
Any recommendations that could meaningfully improve simplicity, speed, image quality or reliability.

Clearly separate recommendations from approved requirements.

### F. Next step
Explain what the project owner should review before authorizing implementation.

**Do not start implementation automatically. The project owner must explicitly approve the reviewed plan first.**