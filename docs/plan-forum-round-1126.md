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

**R2 (report #195, 0.1.1667, 1493×802).** The update window is the system dialog of Tauri's dialog plugin, sized by its text; a build's release notes made it taller than the screen, and a system dialog cannot be moved or scrolled by the page. `shortNotes` (`ui/src/app/updates.ts`) now shows at most 10 lines and 700 characters of the notes (a line cut at 160), with an ellipsis when more follows, so the window and its buttons always fit. The Install and Later buttons are unchanged. Test: `updates.test.ts`.

**R3 (report #194, Windows, 0.1.2058).** A view in a window of its own is the same interface in a separate webview (`?pane=`, no ribbon). The section box lives in a zustand store per webview and `syncWindows` shared only the `SHARED` keys of `useUi`, so the box set in the main window never reached the floating view, and the floating window had no ribbon to set one from. `mirrorSectionBox` (`ui/src/app/windows.ts`) now carries the box and its hidden flag between the windows as a `section-box` event (a new window asks the main one at start, as for the other state), and a floating 3D view shows a Section box chip button when it has no box (the existing phrase, no new string). Test: `windows.test.ts`. Not run on a real second window (Tauri events cannot be driven here): check on desktop.
