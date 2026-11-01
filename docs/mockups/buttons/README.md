# Button mockups (prompt_07 §6, for review, not implemented)

Proposals for the action buttons of the Photos editor, and for the same buttons in ID Photos and Printing. They are
drawn with PhotoX's own styles (light mode) by [ButtonMockups.cs](../../../tests/PhotoX.App.Tests/ButtonMockups.cs).
The photo is a placeholder, and so is the adjustments area.

| Image | Proposal | Difference |
|---|---|---|
| [A-text.png](A-text.png) | **A · Text** (as in 0.10.0) | Words only. Clearest for new staff; the shortcuts are in the tooltips and the Enter chip. |
| [B-icon-text.png](B-icon-text.png), [dark](B-icon-text-dark.png) | **B · Icon and text** | Every action keeps its word and gains an icon (✕ Close, printer, disk, arrow for Save & Next, circular arrow for Reset). Easiest to recognise at a glance; the buttons stay the same size. |
| [C-icons.png](C-icons.png) | **C · Compact icons** | Close, Print, Save, Reset, Undo and Redo become icon buttons with tooltips; only Save & Next keeps its words. The most space for the adjustments; meanings must be learned (tooltips help). |
| [D-toolbar.png](D-toolbar.png) | **D · Actions in the toolbar** | Close, Print and Save move to the right end of the top toolbar, as icon and text; the side panel keeps only Save & Next. Separates "leave or print this photo" from "adjust it". |
| [E-other-workspaces.png](E-other-workspaces.png) | A, B and C applied to ID Photos (sheet step) and Printing | Shows that the chosen style carries over: Back, Save and Print, or Back, Clear and Print. |

Notes for the choice:

- Whatever the choice, **Save & Next** stays the large blue button with its words, and **Print** in ID Photos and Printing stays blue.
- B changes the least and keeps the interface self-explanatory; C saves space but relies on tooltips; D can be combined with B.
- Regenerate after changing the proposals: `PHOTOX_MOCKUPS=1 dotnet test tests/PhotoX.App.Tests --filter ButtonMockups`.
