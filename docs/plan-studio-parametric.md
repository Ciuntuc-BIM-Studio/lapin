# Studio: parametric parts, sketch constraints, STEP and FreeCAD

Decision 517 (`docs/decisions-pending.md`), proposed and decided on 13.11.2026 (the user's answers in §8). It follows a tester's request for sketch constraints "similar to Inventor" (with a video of Lasteno Build, https://build.lasteno.com/, a browser BIM tool: rooms first, walls following). It extends `docs/plan-studio-editor.md` (decisions 196–202), `docs/plan-studio-sketch.md` (374–377), `docs/plan-studio-thick.md` (189) and §5.6 of `docs/plan-user-extensibility.md` (the "light family").

## 1. The user's direction (13.11.2026)

> „Sunt mai mare fan cum rezolvă Inventor sau FreeCAD — piese parametrice din schițe, editabile în proiect —, așa că extinde mai departe în modulul Studio, cu constrângeri într-un mod de editare, ca în Blender, unde vedem părțile componentei; în modul model/world folosim doar panoul de proprietăți, configurat în modul de editare, pentru parametri adaptivi sau conduși de formule. Interoperabilitatea Lapin–FreeCAD nu e deloc o idee rea.” Then: „la nivel de STEP sau de obiecte parametrice FreeCAD.”

> "I'm a bigger fan of how Inventor or FreeCAD solve it — parametric parts from sketches, editable in the project — so extend this further in the Studio module, with constraints in an edit mode, like Blender, where we see the component parts; in model/world mode use only the properties panel, set up in edit mode, for adaptive or formula-driven parameters. Lapin–FreeCAD interoperability is not a bad idea at all." Then: "at the level of STEP or FreeCAD parametric objects."

So: Studio becomes the place where parametric parts are made (Inventor's part, FreeCAD's PartDesign Body, Revit's family: one idea); constraints live in an edit mode; the project sees a part through its published parameters; parts go out and come in as STEP solids (any CAD) and as FreeCAD parametric bodies.

## 2. What exists (read in the code on 13.11.2026)

- **Sketches** are drafted shapes (`IfcAnnotation` DRAFT, `Drafts.On(m, viewId)`, `Lapin.Drafting/Cad/Drafts.cs`) on a view's plane (`Sketches.PlaneOf`: a plan's level, a section, an elevation's fixed plane, a reference plane; faces through `studio.onFace`). Shapes are Line, Polyline, Circle, Arc with plain coordinates in `attrs.shape`; profiles may be closed chains (`Sketches.Closed`), faces split into regions (`SketchRegions`).
- **Solids** keep their recipe in their own attributes: `operation`, `profile`, `guide`, `rail`, `features` (steps redone in order, `Features.Apply`). The body is rebuilt from the sketch on every build, keeping the GlobalId. The recipe is written to IFC in `Lapin_Intent` (decision 189). `Pset_LapinSolid.Spec` in `docs/spec-studio.md` describes the old Lapin; it is not in the code.
- **Edit sketch** (`ui/src/modules/studio/sketch.ts`, `editSketch`) opens the sketch's view (plan or drawing) and selects its shapes. There is no edit mode in 3D, no part tree.
- **Nothing is constrained.** The only driving behaviour is a polyline side typed a new length (`ui/src/viewport/cad/sides.ts`); `docs/plan-studio-editor.md` §6 says a solver is not part of it. Drafting dimensions (`Lapin.Drafting/Dimensions.cs`) are associative (their `refs` found again on every draw) but only measure.
- **Types**: Studio has `LapinSolidType` (exported as the class's IfcTypeProduct). Type parameters marked `instance` are copied onto elements by `TypeDefaults.Settle` (decision 506). Shared bodies are written once as `IfcRepresentationMap` and placed by `IfcMappedItem` (`IfcExport.Mapped`).
- **Formulas**: `Expr` (`Lapin.Platform/Expressions.cs`), declarative, bounded, never code; parameter formulas `ParamFormulas`, never stored (decision 515).
- **STEP**: `Lapin.Ifc/Step.cs` writes any ISO 10303-21 file (the schema is a parameter); `StepFile.cs` reads one (no complex entity instances `#1=(A()B());` today). `IfcAdvanced.cs` tessellates IFC4 advanced B-reps (planes, cylinders, cones, spheres, tori, surfaces of revolution, B-spline surfaces and curves).
- **Kernel**: `Lapin.Geometry.Solid` is a closed polyhedron with planar faces; curved surfaces arrive tessellated (`Chords`). There are no exact analytic or B-spline surfaces in the kernel.

## 3. The concept

### 3.1 A Studio part is a parametric part

A **part** is a type (`LapinSolidType` grown, or a `LapinPart` type class) from day one: its definition is the tree below; every placed part is an instance of it, sharing its body per parameter set. Instance parameters follow decision 506. The "family editor" and Studio are one thing: there is no second editor.

### 3.2 Edit mode (Blender's Tab)

- Entered by **Tab** with a part selected, or a double-click on it; left by Tab, Esc or *Finish part*. One undo step per command inside, as everywhere; leaving is not a step.
- The rest of the model is **dimmed** and stays a snap reference (the 3D snapping core `snap3d.ts`, the sketch inference); it cannot be selected.
- The **part's tree** in a side panel: origin and reference planes, sketches, steps (extrude, revolve, sweep, loft, push/pull, fillet, shell…, the existing `features`), parameters. A step can be switched off, reordered, edited; a *rollback bar* shows the body up to a step (a later stage).
- **Sketches edited in place** on their plane in 3D (the direct and CAD ways of `docs/plan-studio-sketch.md` on the sketch's work plane), with the constraint glyphs, DOF colouring (free: blue, fully constrained: ink, conflict: red), a "N degrees of freedom" chip, dimensions edited in place. *Open on its view* (today's Edit sketch) stays as an option.
- The **parameter table**: name, unit kind, value or formula (`Expr`), type or instance, **published** flag, group, description. **Flex**: values changed live with the engine's preview, then put back.

### 3.3 Model mode

The part is one element. Properties shows **only the published parameters**, each a value, a formula, or a **link to the model**: a distance between two picked points or faces, a level's height, another element's property (`{wall.thickness}`), so a part can be **adaptive**. Links and formulas are derived on every ask, never stored (the standing principle); a link to something gone is reported under Problems and the last value kept.

## 4. Constraints

### 4.1 The kinds

The set follows FreeCAD Sketcher's (https://wiki.freecad.org/Sketcher_Workbench), so a constrained sketch round-trips with FreeCAD (§5.2):

| Lapin | FreeCAD | Meaning |
|---|---|---|
| coincident | Coincident | two points one |
| pointOn | PointOnObject | a point on a line, circle or arc |
| horizontal, vertical | Horizontal, Vertical | a line or two points |
| parallel, perpendicular | Parallel, Perpendicular | two lines |
| tangent | Tangent | line–arc, arc–arc (end to end or along) |
| equal | Equal | lengths or radii |
| symmetric | Symmetric | two points about a line or a point |
| fix | Block (and Lock: DistanceX + DistanceY) | an entity held where it is |
| distanceX, distanceY, distance | DistanceX, DistanceY, Distance | dimensions |
| radius, diameter | Radius, Diameter | dimensions |
| angle | Angle | dimension |
| midpoint | (Symmetric about the line's ends) | a point at a line's middle |

Left out: Snell's law, B-spline alignment and weights, ellipses (Lapin's sketches have none).

A dimension is **driving** (its value, or an `Expr` formula of the part's parameters) or **driven** (a reference, shown in parentheses, as FreeCAD's "reference" and Inventor's driven dimension). A driven one may become driving only while it would not over-constrain.

### 4.2 Storage

Constraints belong to the sketch: a list on the sketch's carrier (the reference plane or the part), each `{kind, refs: [{shape, part: start|end|center|edge:i}], value?, formula?, driving}`. Kept in `.lapin`, never written to IFC (IFC has no sketch constraints; the solved geometry goes as it does today). A command that replaces shapes (TRIM, JOIN, EXTEND, FILLET, OFFSET, a region split) maps the refs onto what replaced them, or drops the constraint and says so.

### 4.3 The solver: Lapin's own, MIT

No planegcs (LGPL-2.1+), no SolveSpace (GPL-3.0+), no JSketcher (restrictive dual licence), no D-Cubed (proprietary). Written after the method these programs describe publicly:

- unknowns: points' x, y, radii (and angles of arc ends); one residual per constraint, analytic Jacobian;
- a damped least-squares step (Levenberg–Marquardt, or a dogleg trust region) from the **current** geometry, the step of least norm, so what moves moves least; a dragged point is a soft goal of high weight;
- QR with column pivoting gives the rank: the degrees of freedom left, which unknowns are still free (the colouring), and redundant or conflicting constraints (a row whose removal leaves the rank unchanged);
- the system split into connected clusters; dense algebra (sketches stay under about 2,000 unknowns);
- in `Lapin.Geometry` (`Constraints/`), so parts, walls and rooms can share it.

The solve runs **in the engine** inside the command that changed the sketch: the shapes are written back, the solids rebuild as they already do, in one undo step.

### 4.4 Constraints while drawing

The inference of `inference.ts` (endpoint, midpoint, on edge, axes, parallel, perpendicular) records what it inferred when a point is clicked in edit mode (coincident, pointOn, horizontal, vertical, parallel, perpendicular, tangent); a value typed (`vcb.ts`) becomes a driving dimension. A setting turns it off; Ctrl held skips it for a point.

## 5. Interop at the level of objects

STEP carries **geometry** to and from any CAD (Inventor, SolidWorks, Fusion, FreeCAD); FCStd carries **parameters** with FreeCAD; IFC stays the BIM exchange. All three are written after their public specifications, with no dependency and no FreeCAD or Open CASCADE code (both LGPL); clean-room.

### 5.1 STEP (ISO 10303-21, AP214 and AP242)

- **Export** (X1): a part, or the selection of Studio solids, as `MANIFOLD_SOLID_BREP` with `ADVANCED_FACE`s on `PLANE`s (the kernel's faces are planar; a circle's facets are written as planes, by `Chords`), in AP214 (`AUTOMOTIVE_DESIGN`) and AP242 (`AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF`) with product, shape representation and units; colours by `STYLED_ITEM`. Where exact surfaces are known from the recipe (an extruded circle: a `CYLINDRICAL_SURFACE`) they may be written exact later. AP242's tessellated shapes (`TESSELLATED_SOLID`) as the fallback for bodies (meshes, imported references). The writer is `Step` with the schema given.
- **Import** (X2): `MANIFOLD_SOLID_BREP`, `BREP_WITH_VOIDS`, `SHELL_BASED_SURFACE_MODEL`, assemblies (`NEXT_ASSEMBLY_USAGE_OCCURRENCE` with their placements) as Studio solids of `operation: body`. Planar faces native; analytic and B-spline faces tessellated by the same sampling as `IfcAdvanced.cs` (to be shared: the entities are the same without the `IFC` prefix), finer by `Chords`. `StepFile` learns complex entity instances (rational B-splines are written that way). The fidelity rule of IFC applies: a body whose volume or extents differ from the file's by more than 1 mm or 0.5 % stays a reference (`RecognitionFidelity`). Units read from the file. Never written back to the file read.

### 5.2 FreeCAD (`.FCStd`)

Format (https://wiki.freecad.org/File_Format_FCStd): a zip holding `Document.xml` (objects, their properties and links), `GuiDocument.xml` (visibility, colours), and shapes as Open CASCADE BRep files (`*.brp`, text, https://dev.opencascade.org/doc/overview/html/specification__brep_format.html). Target FreeCAD 1.0 and later.

- **Export** (X3): a part as a **PartDesign Body**: its sketches as `Sketcher::SketchObject` (geometry and the constraints of §4.1, placed on the body's origin planes or by `AttachmentSupport`), its steps as `PartDesign::Pad`, `Pocket`, `Revolution`, `Groove`, `AdditiveLoft`, `AdditivePipe`, `Fillet`, `Chamfer`, `Thickness` where they map, and the published parameters as a `Spreadsheet::Sheet` with aliases bound by expressions; a step with no counterpart (Lapin's push/pull on a face, `upTo` another element) is written as a `Part::Feature` with its shape (a BREP of the solved body), so FreeCAD opens the part parametric as far as it can and whole always. Every object also carries its shape (BRep), so files open without recompute.
- **Import** (X4): recognised Bodies (sketches with the constraint kinds of §4.1, Pad, Pocket, Revolution…) rebuilt as native parts (sketches, constraints, steps, parameters from the spreadsheet); anything else as a fixed shape read from its BREP (planar faces native, others tessellated), a reference. The fidelity rule as for IFC: a rebuilt part that differs from the file's own shape by more than 1 mm or 0.5 % stays the file's shape.
- Risks: the format changes between versions (the 1.0 topological naming adds element maps; property names moved); attachment modes are many. Tests on files saved by FreeCAD 1.0 and the current release, made by the user in FreeCAD (Lapin ships no FreeCAD).

## 6. Stages

Each stage is a commit (or a few) with its tests first, the user guide page in every language (`docs/user/studio.md` and a new `docs/user/studio-parts.md`), its scene in `scripts/user-docs/scenes.mjs`, the strings in every table of `ui/src/i18n`, the catalogue (`node scripts/command-catalogue.mjs --check`), a row in `docs/decisions-pending.md` and a paragraph in `docs/history.md`. Engine work on Opus, interface on Sonnet.

| Stage | What | Effort | Tests |
|---|---|---|---|
| **P0** | Edit mode shell: Tab / double-click, model dimmed but snapped to, the part's tree (planes, sketches, steps, parameters), sketches edited in place on their plane in 3D, Finish part; the part as a type (`LapinPart` or `LapinSolidType` grown) with an upgrade for existing solids | 2 weeks | `PartTreeTests`; `editMode.test.ts`; scene `studio-part` |
| **C0** | Constraint storage on sketches, ref mapping under TRIM/JOIN/EXTEND/FILLET/OFFSET/regions | 1 week | `SketchConstraintStoreTests` |
| **C1** | The solver (`Lapin.Geometry/Constraints`): LM/dogleg, QR rank, DOF, redundancy, conflicts, clusters; `sketch.solve` in the commands | 2 weeks | a rectangle's DOF after each constraint, tangent arcs, symmetric, conflict found, a bad start converging, determinism, 500-entity time |
| **C2** | Constraints recorded while drawing, typed values as dimensions | 1 week | `inference.test.ts` grown |
| **C3** | Interface: glyphs, DOF colouring, dimensions edited in place, driven in parentheses, Constraints group | 2 weeks | UI tests; scene `sketch-constraints` |
| **P1** | Parameters, formulas (`Expr`), published flag, flex | 1–2 weeks | `PartParameterTests` (cycles refused, units) |
| **P2** | Model links: distances, levels, other elements' properties; adaptive parts | 2 weeks | a part following a level and a wall's thickness |
| **P3** | Part as type everywhere: instances share a body per parameter set (`docs/plan-instancing.md`), instance parameters (506), IFC `IfcTypeProduct` (the class's type) with `RepresentationMaps`, parameters in Lapin's own set (never `Pset_`), `Lapin_Intent` with the part id, GlobalIds stable on regeneration | 2 weeks | `PartTypeIfcTests`, round trip through Lapin's IFC import |
| **X1** | STEP export (AP214, AP242; tessellated fallback) | 1–2 weeks | a box and a cylinder part read back by Lapin's reader; checked by the user in FreeCAD and Inventor |
| **X2** | STEP import (B-reps, assemblies, complex instances, fidelity) | 2–3 weeks | public sample files (with their licences checked), volumes and extents |
| **X3** | FCStd export as a PartDesign Body | 2–3 weeks | the zip's XML against files saved by FreeCAD 1.0; opened by the user |
| **X4** | FCStd import: Bodies rebuilt, the rest as BREP references | 3–4 weeks | files made by the user in FreeCAD 1.0 and the current release |
| **P4** | Nesting: a part placed inside a part, parameters passed down, ids derived from the path | 2 weeks | nested flex, ids stable |
| **C5** | Optional: walls and rooms from constrained outlines (the Lasteno idea: a room's dimension drives its walls) | 2 weeks | after the user's answer |

Order: P0, C0–C3, P1, P3, X1, P2, X3, X2, X4, P4, C5. About 6–8 weeks to constrained sketches in an edit mode; 20–28 weeks for everything.

## 7. Risks

1. **Refs under topological edits** (TRIM, JOIN, regions, a step's face lost). The hardest problem. Map by geometry where a shape is split or joined; otherwise drop with a message, never a silent wrong solve.
2. **Solution flipping**: a solve that jumps to the mirror solution. Steps from the current state, signed angles and orientations, refuse rather than jump.
3. **Performance per type**: solve per type and parameter set, cache the body; instances never solve.
4. **No exact curved surfaces in the kernel**: STEP and BREP curved faces come in tessellated; a STEP export writes facets unless the recipe knows the surface. Adding exact surfaces to `Lapin.Geometry` is a separate decision.
5. **FreeCAD format drift** (1.0's topological naming, renamed properties, attachment modes): target 1.0+, test on real files from each release, fall back to shapes.
6. **Licences**: clean-room from public documents only; reviewers check that no FreeCAD, OCCT, planegcs or SolveSpace code is read while writing.

## 8. The user's answers (13.11.2026: "as recommended")

1. Edit mode opens on **Tab** and on a double-click (and a button).
2. **`LapinSolidType` grows**: every Studio solid becomes a one-step part; no second class of solids.
3. Constraints added while drawing are **on by default**, as in Inventor and FreeCAD; Ctrl held places one point without them.
4. Model links (P2) come **after** STEP export (X1).
5. A step with no FreeCAD counterpart is exported as a **fixed shape**, never refused.
6. C5 (walls and rooms driven by constrained outlines) **waits** for another request.
7. A **`.lapinpart`** file for the part library, as `.lapintypes`, so parts can go to the store too.

## 9. Families become parts (the user's answers, 13.11.2026)

The light families of decision 515 (`LapinFamily` / `LapinFamilyInstance`, `Lapin.Studio/Families.cs`, `.lapinfamily`, `docs/user/families.md`) were made the same day as this plan. They overlap, so the user decided (all as recommended):

1. **Families become parts.** One class: a family is a part (`LapinSolidType` grown). An `IProjectUpgrade` turns saved families and their objects into parts and their instances (GlobalIds kept); a `.lapinfamily` file is read as a `.lapinpart`; the `studio.createFamily`, `placeFamily`, `setFamilyValues`, `familyFromSketch`, `exportFamily`, `importFamily` commands stay as shortcuts onto the part commands (extend, never break).
2. **A formula corner is a constraint.** A family's corner written as formulas (`["W", "T"]`) becomes a fixed point with driving `distanceX` / `distanceY` from the sketch's origin, carrying those formulas; one sketch may mix such points with points the solver moves. The upgrade converts families with no loss: the same bodies, the same volumes.
   - *Where it hooks in (C0):* until constraints exist the corners stay formulas in the part's `def.bodies[].points` (`Parts.Bodies` evaluates them). C0 turns each body's outline into a sketch on its origin plane (the part tree already lists them as `body:<n>` sketches on `xy`/`xz`/`yz` with their `points`), each formula corner a `fix` point with driving `distanceX`/`distanceY` carrying the formula; an `IProjectUpgrade` then converts saved parts the way `FamilyPartUpgrade` converts families.
3. **Parts take over from families:**
   - `when`: a body or a step suppressed by a boolean formula of the parameters (Inventor's and FreeCAD's suppress by expression);
   - `exportAs`: the IFC class a part is written as, with its type;
   - **part from a sketch** with W, H, D stretching it (`familyFromSketch`), its dimensions placed automatically;
   - the **ghost** under the cursor while placing (the point gesture's `ghost`).
4. **When: in P0**, with the edit mode and `LapinSolidType` grown, so the part type and the upgrade are made once; the part's tree shows former families too. `FamilyTests` stay and pass through the shortcuts; the upgrade gets its own test (a project saved with families opens as parts with the same bodies and ids).

**P0 engine: done (13.11.2026, decision 519).** `LapinSolidType` carries an optional part definition (`def`, the families' schema; parameters may also hold `unit`, `instance`, `published`, `group`, `description`). A placed part is an ordinary Studio solid: an `IfcBuildingElementProxy` with `operation: "part"`, `exportAs` the definition's class, `at`, `z`, `rotation`, `mirror`, `values`, Studio's `features` after its bodies, typed by its part through `IfcRelDefinesByType` (`Lapin.Studio/Parts.cs`). `FamilyPartUpgrade` turns saved `LapinFamily` / `LapinFamilyInstance` into parts with the same ids. New: `studio.createPart`, `updatePart`, `deletePart`, `placePart`, `setPartValues`, `partFromSketch`, `importPart` (reads `.lapinpart` and `.lapinfamily`), queries `studio.parts`, `partPreview`, `exportPart` (`.lapinpart`, `{format: "lapinpart", version: 1, name, def}`) and `partTree`; the family commands are shortcuts onto them. Tests `PartTreeTests` (5); `FamilyTests` unchanged. Left for P0's interface half: the edit mode, the tree panel, `editMode.test.ts`, the `studio-part` scene and the guide pages.
