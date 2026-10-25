# Views, sets and the joinery's details: plan

Asked on 25.10.2026 (pending decisions 125–127). Three pieces, in the order they are built:

1. **Duplicating** levels, floor plans, sections, elevations, 3D views and sheets, and a new level of the project browser: **sets** that group the drawings by their parameters.
2. The **joinery configurator** shows and configures the plan, the section and the opening symbols (the makers' convention, DIN 1356), and the model's plans and elevations draw them.
3. **Reveals, sills and surrounds**: the openings finish the wall around them; windows and doors get their interior sill, exterior sill, surround and threshold.

The user's answers (25.10.2026): a level duplicates with everything on it (an empty variant too); the browser groups by sets with a filter on parameters; 3D views are saved with their camera; a 2D view duplicates with or without its detailing.

## 1. Duplicating and the browser's sets

### Engine

- `view.duplicate {id, withDetailing}`: a new `LapinView` with the source's attributes (kind, storey, cut height, range, scale, direction, line, depth, hidden lines, graphics or graphics set, hidden elements, linework edits, discipline), named "<name> copy" ("copy 2"…). The implicit plan of a storey (`plan:<storeyId>`) duplicates as a real plan view. With detailing, every `IfcAnnotation` of the source view (dimensions, texts, tags, drafted shapes) is copied with `viewId` set to the new view.
- `sheet.duplicate {id}`: paper, orientation and title block copied, the next free number, no viewports (a view sits on one sheet).
- `storey.duplicate {id, withContents = true}`: a new storey **inserted right above the source**, with its height; the storeys above move up by that height (their elements follow, their heights being relative). With contents, every element contained in the source is copied (`Placement.Copy`, the identity transform), with what it hosts and aggregates, and contained in the new storey; the joins between copied walls are made again from the originals' joins. GlobalIds are new.
- **3D views** become views of kind `3d`: `camera` (azimuth, pitch, distance, target, ortho), hidden elements, graphics or graphics set. `{3D}` stays as it is (its settings on the project, addressed as "3d"); `view.create {kind: "3d"}` and `view.duplicate` of "3d" make saved ones; `view.setCamera {id, camera}` stores the camera.
- **Discipline**: an attribute of views and sheets (`discipline`: Architecture, Structure, MEP, or any text), editable in the properties panel; new views and sheets take the discipline of the set they are made in, else Architecture.
- **Sets**: on the project, `browserSets`: `[{id, name, filter}]`, where the filter is `{disciplines?, kinds?, storeys?, scales?, phases?, nameContains?, params?: {name: value}}` (every given field must match; a set without a filter takes everything). Commands `browser.setSet` (add or change), `browser.removeSet`, `browser.moveSet`. A project without sets reads as the three defaults (Architecture, Structure, MEP by discipline). Views and sheets no set takes go under "Other". Saved in `.lapin` with the project entity.

### Interface

- The project browser: Levels first, as now; then one group per set, each with Floor plans, Sections, Elevations, 3D views, Sheets and Schedules inside, counted; "Other" last when needed. A set's "+" buttons make views in its discipline. Collapsed groups are remembered per project in the browser's storage (a per-viewer convenience).
- Duplicate buttons beside the delete one: a view (two: settings, with detailing), a sheet, a level (with contents; alt: empty), a 3D view ("save the current view as").
- "Organize…" at the top of the browser: the sets dialog (name, filter fields, order, remove).
- A saved 3D view opens with its camera; "Update camera" writes the current one.

### Tests

Engine: duplicates carry their attributes and (with detailing) their annotations; the implicit plan duplicates as a real plan; a sheet's copy has the next number and no viewports; a storey duplicated with contents has the same elements, joined alike, on the new level, the storeys above moved up; undo takes it all back; a 3D view keeps its camera; sets and disciplines round-trip through the package. Interface: the set filter (vitest).

## 2. The joinery's drawings

The engine already has the old Lapin's symbol engine (`JoinerySymbols`: plan, elevation from inside or outside with the makers' or DIN 1356 convention, section; `docs/simboluri-tamplarie.md`), used by the schedule's cards only.

- The configurator shows the engine's drawings: elevation (inside, with the convention), plan and section, as the type is edited (the preview query returns their strokes).
- The convention (handle / hinge) and the hardware (hinges and handles drawn) are project settings (`joinerySymbols` on the project), set in the configurator, applied in the configurator, the plans, the elevations and the schedule.
- The model's plans draw the window and door symbols from `JoinerySymbols.Plan` (today a simpler symbol: leaves and arcs, frame and the double glass line); the elevations draw the opening marks over the joinery (seen from outside, mirrored, dashed where the sash opens away), as an annotation layer the view can switch off.
- Per panel, the operation list stays the configurator's (fixed, side-hung left/right, tilt-and-turn, bottom-hung, top-hung, sliding, folding; doors: single, double, sliding, folding, double-acting), as the makers name them.

## 3. Reveals, sills, surrounds, thresholds

New fields of the joinery type (the configurator, a "Details" group), each a part of the door's or window's geometry with its material, so it shows in 3D, is cut in plan and section, and is measured:

- **Reveal finish** (`reveal`): a skin on the opening's jambs and head (and the sill side of a door), thickness (15 mm) and material; **default: the wall's exterior finish**, the last layer of its type (the layer on the exterior face), or none when the wall has one layer. On the interior side the same, or its own material.
- **Interior sill** (`sillInside`, "glaf"): a board on the inside, projecting past the wall face (30 mm), thickness (20 mm), material.
- **Exterior sill** (`sillOutside`, "solbanc"): a board on the outside, projecting (40 mm), thickness, slope (5°), material; with a drip.
- **Surround** (`surround`, "ancadrament"): a band on the facade around the opening, width (80 mm), projection (20 mm), material; optionally only the jambs and the head.
- **Threshold** (`threshold`, doors): a strip in the opening at floor level, height, material.

Defaults are per type; each field can be off. The schedule's section symbol draws the sills. The parts carry their material and area in the quantities. IFC: parts of the door or window (`IfcBuildingElementPart` under the fill), with their materials.

## Order and effort

1 (duplicating, 3D views, sets): the engine first, with its tests, then the browser and the dialog. 2 (the joinery's drawings): the preview and the project settings, then the plans and elevations. 3 (the details): the spec and geometry, then the drawings and quantities.

## Progress (25.10.2026)

**1, done** (engine 463 tests, interface 94): `view.duplicate` (with detailing: the annotations of the view copied), `sheet.duplicate`, `storey.duplicate` (inserted above, the storeys above moved up; the contents copied with what they host, the joins made again), 3D views of kind `3d` with `view.setCamera` ({3D} duplicates into one), `discipline` on views and sheets, `browserSets` on the project with `browser.setSet/removeSet/moveSet` and the query `browser.sets` (the defaults when the project has none). The browser shows the sets with their kinds inside, "Other" for what none takes, duplicate buttons on views, sheets, levels and 3D views, "Organize…" for the sets. A saved 3D view opens with its camera; its hidden elements and graphics are its own (`view.hidden3d {id}`). An item sits in every set whose filter it matches.

**2, done:** the preview query returns the symbol engine's drawings (elevation from inside and outside, plan, section) and the configurator shows them, with the project's convention and hardware (`architecture.joinerySymbols`, kept on the project as `joinerySymbols`); the model's plans draw the engine's plan (the door leaf as a cut stroke, as before); the elevations mark the openings whose exterior faces the eye, dashed, through `IViewAnnotations` (layer `JOINERY`), skipping those something stands in front of (`ViewContext.Occluded`, over the elements' geometry) and those the view hides; the view's `openingSymbols` turns them off; the schedule's cards use the same engine and convention (the older `JoineryElevation` is no longer used). Not done: hardware on the elevations (configurator and schedule only); the phases' filter is not consulted for the marks.

**3:** next.

