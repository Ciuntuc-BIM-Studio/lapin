# Forum and reports round of 26.11.2026 (decision 879)

Sources: forum topics 204, 258, 260, 274, 275, 278–280, in-app reports #194, #195, the user's roof screenshot (a beam turned 90°) and Fabio Emmolo's safety-family message. Russian is GitHub issue #135, not planned here.

Rules that apply to every stage: defaults reproduce today's output; nothing stored changes unless an `IProjectUpgrade` says so; new interface strings go in every language of `ui/src/i18n`; each command is one undo step; the guide page and scene change with what users see.

## Order

| # | Stage | What | Where |
|---|---|---|---|
| 1 | R1 | Roof beams "Lay flat": wall plate, ridge beam, purlin and collar tie sections turned 90° (145x45 instead of 45x145), a checkbox per role | Roofs (engine + dialog) |
| 2 | R2 | #195: the update dialog fits any screen and its buttons stay reachable (scrolls, or the window can move) | Tauri/UI |
| 3 | R3 | #194: the section box works in a 3D view opened in its own floating window | viewport |
| 4 | R4 | Forum 275: Hatch Patterns dialog: edit, copy an existing pattern as the start of a new one, delete custom patterns | UI (+ engine if needed) |
| 5 | R5 | Forum 204: a clear opening height that does not subtract the threshold (Czech rule), set in Drafting standards beside the existing tokens; the threshold drawn or hidden per door | Doors, tags |
| 6 | R6 | Fabio 1+2: a placed part's values as element fields (schedules, filters, formulas) and a part parameter marked as a measure (Length, Count, Area) that cost articles can use | Studio parts, cost |
| 7 | R7 | Fabio 3: time-based cost articles ("× duration": months, working days, hours from the 4D schedule) | Cost, planning |

Left for later, by the user's choice of what to plan next: Fabio 4 (a watch on a type or class), 5 (estimate PDF wrapping and superscripts), 6 (calendar holidays by country, Gantt headers translated on sheets); forum 260 (angle lock on skirting and every line-based element), 273 (coordination discipline in view settings), 278 (crosshair offset, zoom limit), 279 (Trim deletes an element nothing crosses), 280 (force one snap while moving or copying), 178 (sill tag types and window dimension styles); the IFC importer recognising framework as beams (274).

## Stage status

Each stage records here what was done, with its decision's source.

**R4 (forum 275), done on `feat/hatch-patterns-edit`.** The cause: the dialog's editor (with its fields, Save and Delete) opens on `hatch.get`, which counts the places naming the pattern by walking every entity's attributes and read each `hatch`, `cutHatch` or `surfaceHatch` value as text; a view style keeps `surfaceHatch` as a switch (`true`), so in a project with a stored view style every `hatch.get` threw, the query's failure was silent and the editor stayed on "Choose a pattern" for every pattern, built-in or the project's (the forum screenshot: YTONG-200X600 highlighted, nothing on the right). Edit and Delete existed but could not be reached. Fixed in `HatchPatternCommands.Walk` (only text values are names; `hatch.usage`, a rename's repointing and a forced delete went through the same code). A second cause, in the dialog, gave the same empty editor in any project: clicking the pattern already selected (the one just made, or the one the dialog opened on) cleared the editor, and since the selection had not changed nothing read it back; the pattern shown clicked again now stays open (`keepsOpen`). The guide's scene met it at once. The dialog now: Duplicate on every pattern with lines or a tile (built in or the project's), opening an unsaved new pattern started from it (NAME-COPY) instead of saving a copy at once; New pattern → Start from (Blank or any pattern); a bin beside each project pattern in the list besides Delete in the editor; a built-in pattern's fields greyed out with a note that it is fixed; a project pattern's note that it is edited in place; "The pattern could not be read." when `hatch.get` fails instead of the empty prompt. A delete in use still asks, falls back as before (materials, standards, sets and rows lose the hatch, drafted hatches take LINES45), keeps every entity (GlobalIds) and is one undo step. No new command (catalogue unchanged). Tests: `PatternEditorTests.A_pattern_is_read_renamed_and_deleted_beside_switches_of_the_same_name` (xUnit), `hatchPatternModel.test.ts` (startFrom, canCopy, canEdit). Guide: `material-graphics.md` (EN, RO) and the scene `matgfx-patterns` (new picture `hatch-patterns-start-from`). Left: the forum reply.
