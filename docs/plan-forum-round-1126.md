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

**R5 (forum topic 204), done.** One resolver stays: `Joinery.ClearOpening(spec, lessThreshold)`, called by `OpeningMarkers.Sizes`, which markers, tag symbols, tag rules and the worked-out fields (box tags, labels, schedules, formulas) all read. A new drafting-standards option, `clearHeightLessThreshold` (`standards.options`, one undo step; **Manage → Drafting standards → Project → Door clear height less the threshold**, ticked by default), says whether a door's clear height takes its threshold off: unticked, the Czech rule, the void less the frame only (2050, frame 50 → 2000, whatever the threshold). Only false is stored, so older projects need no upgrade and the default is today's 780/2020 for 900 × 2100, frame 60, threshold 20; a standards file carries it with the options. A window's bottom member stays off either way. The threshold was already modelled (the frame's bottom member, `JoinerySpec.Threshold`, and the detailed threshold across the wall, `JoineryDetails.Threshold`) and sections already cut it; plans never drew it (it lies under the cut). A door type's new `thresholdInPlan` (configurator: **Threshold drawn in plan**) draws it in plan seen from above at LOD 200 and up (`JoinerySymbols.ThresholdPlan`: the detailed threshold across the wall, else the frame's bottom member), in project plans and the configurator's plan; null keeps today's plan. Tests: `ClearOpeningTests` (three new), `drawingOptions.test.ts`. Left: hiding the threshold in sections was not added (sections cut the body as modelled; a door without a threshold is the way to have none), nor a per-type override of the clear-height rule.
