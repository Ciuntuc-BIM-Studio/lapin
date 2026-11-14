# Drawings of an element or an assembly (shop, fabrication and detail drawings)

Forum topic 93 (Victor1, 14.11.2026): "Create automatic detailing views/drawings on sheet for a single part and/or all
single parts in a project. Detailing steel parts and assemblies is very tedious. Revit still can't do this; Advance Steel
can (AutoCAD-based, complicated, many commands). Please implement it in an easy intuitive way like WandererCAD for
Blender. Maybe an interactive window with a generic sketch of a part and checkboxes for views (top, bottom, left, right,
front, back, sections parallel to faces) to place on sheet."

The user (Ionuț) wants it **general, not only for steel**: any element or assembly of any module gets its own drawing,
one at a time or all at once. This plan is that general system. Status: **approved** (decision 579): the user accepted
every recommended answer of §16 on 14.11.2026 ("toate recomandate"); stages S0–S3 in progress.

## 1. In one paragraph

Every element gets **its own frame** (a module says how its elements' axes run; anything else gets one from its body).
An **element view** is an ordinary view (plan, section, elevation, reference plane, 3D) whose plane is read from that
frame on every ask, and which shows only its **subject** (the element or the assembly). A **drawing rule** (one per kind
of element, coded built-ins, the user's own beside them) says which element views to make, at what scale, with which
dimension rules, tags and bill of materials, and how they are laid out on a sheet. **One command** makes or updates the
sheet and its views in one undo step, for one element, an assembly, a selection or every mark in the project. Identical
elements share a **mark**, so a mark gets one drawing with its quantity. The drawings are drawn by the engine like every
other view, so they are always live; what gets flagged is a drawing that **changed since it was issued**.

## 2. What exists (read in the code on 14.11.2026)

The pieces are mostly there; what is missing is the element's frame, the rule that ties them, and marks in general.

### 2.1 Views and drawings

- `Views.cs`: kinds `plan`, `section`, `elevation`, `3d`, `plane` (`RefPlanes`: a plane anywhere, by origin, `bearing`,
  `tilt`, `spin`, drawn as a section on it with a `depth`). `Generator.Draw` and the hidden-line removal draw them all;
  sheets, PDF and DXF print the same pens (`Sheets.Compose`, `Sheets.Pdf`).
- **Element-anchored views already exist three times**, each its own way:
  - `AcrossSections.cs` (decision 210): a section across an element at a station, the view keeps `across` (the element)
    and reads its plane from the element as it is now, a window with a margin round its section. This is the pattern.
  - `wood.frameView` (decision 240), CLT panel elevation (decision 246): a section along a wall's core, keyed
    `FrameViewKey`, with the frame's dimensions added by `IViewAnnotations`.
  - `steel.frameView` and `steel.anchorPlan` (`SteelDrawings.cs`, decision 276): views keyed to a hall and a frame, with
    steel's annotations and the B1, B2… marks of the column bases.
- `View3dDrawings.cs` (decision 574): a saved 3D view drawn by the engine, orthographic (axonometric) or perspective, with
  its section box. An isometric of a part comes from here.
- `Insets.cs` (decision 576): any 2D view drawn inside another at its own scale, cropped, its annotations mapped and
  edited at the source. Details at a scale of their own on a part drawing come from here.
- `ViewCrops.cs` (forum topic 68): crop region and annotation crop, applied in `Views.Render`.
- `ViewHiding.cs` (forum topic 73): elements hidden in a view, kept (`hiddenElements`); isolate exists only as the
  interface's temporary state (`temp.isolate`), never kept.
- `VisibilityGraphics` with `ViewFilters` (rules over fields and parameters), graphics sets, view styles
  (`docs/view-styles.md`), the level of development (`ViewDetail.cs`, decision 404).
- `ViewBoundaries.cs` (decision 236): named boundaries a sheet's viewport binds to.
- `TypePreviews.cs` (decision 529): a type built in a scratch project with the module's own commands, so a configurator's
  preview is the engine's. The dialog's preview (§6) uses the same idea on the real element.

### 2.2 Sheets, title blocks, schedules

- `Sheets.cs`: `LapinSheet` (A0–A4, custom paper), viewports at their view's scale (`sheet.addViewport`,
  `placeAtScale`, `cropViewport`, pinned), `ISheetContent` for what is not a view (schedules, Gantt, cost curve).
- `TitleBlocks/` (decision 340): the user's own title blocks, live text `{project.*}`, `{sheet.*}`, `{param.*}`, the
  revision table; CDE issuing (`docs/plan-cde.md`) prints an issue's revision and status.
- Schedules (`ScheduleEntity`, `IScheduleSource`): steel members, steel parts, timber, boards (cut list), CLT panels,
  rebar; placed on sheets.

### 2.3 Dimensions, tags, marks

- `Lapin.Drafting/Dimensions.cs`: associative dimensions whose ends hold on edges **in the element's own frame**
  (`Dimensions.Frame`: its axis, or its placement and rotation), grid axes, axes, openings.
- `DimensionRules.cs` and `ExteriorDimensions.cs` (forum topic 78): dimensions made at once by the user's rules (which
  strings, in what order, offsets, style, what they measure to), kept on the project and carried by `.lapinstandards`;
  `RoomDimensions.cs`. These are the model for a part's dimension rules.
- `Tags.cs`, `TagText.cs`: tag types with `{field}` templates; `LabelParts.cs`: a label's parts moved on screen.
- Marks today, each module its own:
  - steel members schedule: a mark per kind with the same profile, length (mm) and ends, numbered heaviest and
    longest first (decision 276); column bases B1, B2… by type;
  - wood: a mark per prefabricated panel with "a shop elevation" foreseen (`docs/plan-wood.md`, stage 4), CLT panels;
  - rebar: `mark` on bars and runs, typed by the user;
  - `Architecture/Numbering.cs`: the next number of a new element's name (not a mark).
- Identical geometry is already detected for other ends: `IfcOptimize` (the same mesh, moved, written once as a map),
  `BosWriter.Signature`, Studio's shared bodies per part and values (`Parts.BuildShared`), components (identical by
  construction, `docs/components.md`).

### 2.4 Elements with parts

| Module | What it has | Assembly today |
|---|---|---|
| `steel` | `LapinSteelMember` and steel beams/columns: axis, profile, rotation, ends (`SteelEnd`: cuts, notches), holes (`MemberHoles`, in the member's frame); nodes with plates, bolts, anchors, welds | a node is an `IfcElementAssembly`; a haunched rafter is one (rafter + haunch) |
| `wood` | frame members (`Framing`, in the wall's own elevation `u`, `z`), CLT panels with router cutters, prefabricated panels, joists | a prefabricated panel is an `IfcElementAssembly` |
| `rebar` | bar shapes with nominal lengths, views of a bar (front, top, side, section), bending schedules | — |
| `studio` | parts (`LapinSolidType` with a definition, its own sketches on `xy`, `xz`, `yz`, parameters), placed with `at`, `z`, `rotation`, `mirror` | a part's bodies |
| `furniture` | casework: boards with role, grain axis, sizes (`BoardRow`), the cut list | a piece of casework (its boards) |
| `architecture` | doors and windows (`JoinerySymbols`: elevation inside/outside, plan, section, used by the schedule's cards), stairs (also precast flights, `docs/spec-stairs.md`), walls, floors | a door/window assembly (`docs/plan-door-window-assemblies.md`) |
| `structure` | beams, columns, footings, walls, floors in concrete (precast is a material choice, not a module) | — |
| core | groups, components, `IfcRelAggregates` chains (`Assemblies.cs`, `core.assemblyPath`) | an IFC file's `IfcElementAssembly`, a group, a component instance |

`docs/plan-steel.md` put "shop drawings per part and per assembly, shop numbering" and DSTV/NC in the premium
`steel-pro`. The user's answer to question 1 (§16) moved shop drawings and marks to the open side; only DSTV/NC, BTLx,
workshop numbering series by phase, ISO 2553 weld symbols and EC3/EC5 checks stay premium (§11).

### 2.5 What others do (the good ideas taken)

- **Tekla Structures**: numbering before drawings (a part mark and an assembly mark per identical set; "modified
  numbering" keeps the marks of what did not change); single-part, assembly and cast-unit drawings; automatic
  dimensions set per view by dimensioning rules; a part's views in its own coordinate system; Document manager flags a
  drawing whose parts changed, were deleted, or whose quantity changed, and updates it; drawing properties files and
  wizards per filter (a template per kind).
- **Advance Steel**: drawing styles (which views, dimensions, labels) and drawing processes (a style per kind of part,
  run on the whole model), identical-part search with prefixes per role, a document manager with "update/revision".
- **Blender add-ons** (the user cited WandererCAD's video; the add-on itself could not be looked up): a sheet per
  distinct part with front, top, side and iso views, a section with hatching, overall dimensions, identical parts
  grouped and counted, one click. The simplicity is what Victor1 asks for: one window, a sketch of the part, checkboxes.

What Lapin keeps from them: marks and identical-part grouping, the part's own axes, automatic dimensions by rules,
sections at features, a BOM on the sheet, templates per kind, batch generation, and status flags. What it leaves: the
many commands and the frozen drawing that must be "updated" by hand (Lapin's drawings are live).

## 3. The element's frame

`IElementFrame` (a platform service, each module declares it for its classes, as `IElementFaces` does):

```
ElementFrame? FrameOf(IModelView m, Entity e)
  → { Origin, X, Y, Z (unit, right-handed), Length, Width, Height (the box in the frame), Source }
```

- **X** runs along the element's main extent (its length), **Z** is "up" as the element is drawn in the shop, **Y**
  completes it. The **front** view looks along +Y (seeing X to the right, Z up); top looks down −Z; left, right, back,
  bottom follow by the projection method (§5.3).
- Read from the element's attributes on every ask, never stored, so it follows a move, a turn or a new profile (as
  `Dimensions.Frame` and the face roles do).

| Kind | Origin | X | Z (up in the shop) |
|---|---|---|---|
| steel member, steel beam/column | the axis start (`MemberHoles.Origin`) | along the axis | the section's local y (web up) after the member's rotation |
| plate (node plate, base plate, `IfcPlate`) | the outline's first corner | its longest edge | the plate's normal (thickness) |
| bolt, anchor | its head | along the shank | — (a symbol, not drawn alone by default) |
| timber member (stud, plate, rafter, joist) | its start | along it | the section's height |
| wall, timber-frame or CLT or precast panel | the axis start at the base | along the axis | up |
| floor, roof slab, CLT floor panel | the outline's first corner | its longest edge | up |
| column, footing | the base centre | the section's x | up |
| Studio part | the part's origin (`at`, `z`) | the part's x (after `rotation`, `mirror`) | the part's z |
| casework piece | the front-left-bottom corner | width | up; front looks at its fronts |
| casework board | its corner | its grain axis | its thickness |
| door, window | the type's frame (width × height) | width, seen from inside | up |
| stair | the first flight's start | along the run | up |
| rebar bar | the shape's origin | the shape's plane (`a`) | the shape's plane (`b`) |
| assembly | its main part's frame (§7.1), or its own placement | as the main part | as the main part |
| anything else (IFC references, proxies, MEP) | the body's box | the longest principal axis of the body (principal-axes / minimal box), snapped to level and vertical within 1° | the axis nearest to world up |

- A user may **turn the frame** of a mark (which side is the front, which way is up), kept by the mark (§8); never on the
  element, so an IFC reference is never written to.

## 4. Element views

An element view is a view of an existing kind with `of: {element, role, station?, margin?}`:

- `role`: `front`, `back`, `top`, `bottom`, `left`, `right` (orthographic, from the frame); `section:x|y|z` at a
  `station` (a plane parallel to a frame face, the user's "sections parallel to faces"); `section:feature:<key>` (at a
  feature, §9); `iso` (a 3D view, `View3dDrawings`, from the frame's south-west, orthographic); `detail:<key>` (an inset
  of one of the above at a larger scale, `Insets.cs`).
- The plane and window are worked out from the frame on every ask, exactly as `AcrossSections` reads its plane from the
  element; the orthographic views are reference-plane views (`plane` kind: origin, bearing, tilt, spin from the frame)
  seeing through the whole box, sections cut it.
- **Subject.** `subject: [ids]` (the element, or the assembly's members) is a kept isolate: the view draws only them,
  with `context: none | faded` (the neighbours greyed, as a drawing of a node often shows the members it joins). It goes
  into the one resolver, `VisibilityGraphics` (a new first rule: not in the subject → hidden or faded), so sheets, PDF,
  DXF, 3D and mesh exports read it the same way. This also gives the interface a saved isolate it lacked.
- `AcrossSections`, `wood.frameView`, the CLT elevation and `steel.frameView` keep their keys and behaviour (extend,
  never break); each is registered as an element view role of its module, so a rule can ask for "the frame elevation"
  of a timber wall.

## 5. The drawing rule (the template)

### 5.1 The data

`LapinDrawingRule` (a project resource, undone, saved, carried by `.lapinstandards` and the library):

```
{ name, appliesTo: {classes, kinds, filter (a ViewFilters rule set)}, subject: part | assembly,
  views: [{role, station?, scale? | "auto", style?, graphicsSet?, detail (LOD)?, dimensions: <rule id>, tags: [...],
           title: "{role.name} {scale}", crop?}],
  layout: {projection: "iso-e" | "third-angle", papers: ["A4", "A3", …], titleBlock?, drawn?: <layout id>,
           bom: {schedule columns} | null, notes: "text with {tokens}", spacing},
  sheet: {number: "{mark}", name: "{kind} {mark}"},
  alsoParts: false }   // an assembly's drawing also makes its unique parts' drawings
```

- Live text everywhere: `{mark}`, `{qty}`, `{assembly.mark}`, `{profile}`, `{material}`, `{length}`, `{weight}`,
  `{area}`, `{finish}`, `{param.*}`, and formulas `{= …}` (`Expr`), e.g. a scale chosen from the length.
- **Built-in rules, coded**, one per kind (§10): the user's rule of the same kind overrides it; the built-in stays the
  default with the same output.

### 5.2 Dimension rules for a part

`PartDimensionRules`, the same shape as `DimensionRules` (strings in order, offsets in paper mm, style, what they
measure to), per view role:

- **overall**: length, width, height in the frame;
- **features**: a chain or **ordinate** string (from the frame's origin) through holes, notches, cut ends, openings,
  studs, slots; hole **groups** written "4×80 = 320" with the gauge; diameters and slots as tags;
- **ends**: cut angles (as the steel members schedule reports them), notch sizes, bevels;
- **assemblies**: each attached part's position from the main part's ends and its work line;
- what a string measures to: faces, axes, hole centres.

Dimensions are annotations derived on every draw (`IViewAnnotations`), held by refs in the element's frame, so they
follow the part; a string the user moves is kept by its key (as `LabelParts`); a dimension the user adds by hand is an
ordinary drafting dimension in the view and stays.

### 5.3 The layout

- **Projection**: ISO E (first angle, Romanian and European practice: the top view under the front, the left view on its
  right) or third angle, from the project's drafting standard, overridable per rule.
- **Automatic arrangement**: the front view as the anchor, the others in projection, sections in a row with their
  markers on the front view (`SectionMarkers`), details beside, the iso and the BOM top-right above the title block,
  notes over it. The smallest paper of the rule's list that fits at the chosen scale; else the next scale down the
  standard series (1:5, 1:10, 1:15, 1:20, 1:25, 1:50) — the rule says which wins.
- **Drawn layouts** (the standing rule of `docs/plan-user-extensibility.md`): a user draws a sheet with **slots** (a
  rectangle named `front`, `top`, `section:x`, `bom`, `iso`, `notes`) and saves it as a layout; a rule names it. The
  views fill the slots at the best scale that fits; a slot without a view stays empty. The coded arrangement stays the
  default.

## 6. The dialog (Victor1's sketch with checkboxes)

**Make drawing…** (right-click on an element, Annotate → Shop drawings, the properties panel's section of a mark):

- On the left, **the element itself**, the engine's iso of it (as the type previews, decision 529), with its frame drawn
  (X red, Y green, Z blue) and **six checkboxes on its six faces** (front, back, top, bottom, left, right) plus **iso**.
  A click on a face's checkbox ticks it; *Turn frame* turns which side is the front.
- Under it, **sections**: per axis a slider along the element with its station (sections parallel to faces), *add* to
  stack more; *at features* ticks a section at each feature the module reports (a notch, an opening, a node).
- On the right: **rule** (the kind's, or another), **scale** (auto or typed), **paper** (auto or chosen), *dimensions*
  (overall, features, none), *BOM*, *and its parts* (for an assembly), and the **live preview of the sheet** composed by
  the engine as the boxes change (the same `Sheets.Compose`).
- **Create** makes it in one step; **Save as rule** keeps the choices as the kind's rule (the user's).
- The **scope** tab (batch, §7.3): this element, its mark, the selection, all of a kind, every mark in the project, a
  storey or a phase; *one sheet per mark* (default) or per element.

Defaults are the rule's, so most users press Create without touching anything.

## 7. Single part, assembly, batch

### 7.1 Single part

The subject is one element; the views of the rule; the title block with `{mark}`, `{qty}`, material, profile, weight,
finish; a small BOM of one line when the rule asks.

### 7.2 Assembly

What is an assembly: an `IfcElementAssembly` (a steel node, a haunched rafter, a prefabricated timber panel, one from an
IFC file), a component instance, a casework piece (its boards), a timber-frame wall (its members), a Studio part with
several bodies, a door/window assembly; a group only when asked.

- **The main part**: the module says it (a node's main member, the rafter of a haunch, the panel's frame); otherwise the
  heaviest (largest volume) member. Its frame is the assembly's.
- Views of the whole (subject: all members), **part marks as balloons** on each part (a tag type `{mark}`), the
  **BOM**: mark, quantity, profile or section, length, material, weight, total (an `IScheduleSource` over the subject);
  welds and bolts listed from the module's parts.
- *And its parts*: each unique part of the assembly gets its own part drawing (once per mark, not per assembly).

### 7.3 Batch

`drawing.create {scope, rule?, perMark: true}`: one command, one undo step, every sheet and view made with stable ids.
A **Drawing list** in the project browser (a set of sheets, `docs/plan-views-and-joinery.md` §1) lists them by kind and
mark with their status (§8.3), with *Update* and *Remove*.

## 8. Marks and identical parts

### 8.1 The signature

Two elements are identical when their **signature** in their own frame is the same:

- the module's own when it has one (a steel member: profile, length to the mm, its ends' cuts and notches, its holes;
  a timber member: section, grade, length, cuts; a board: sizes, thickness, grain, material; a Studio part: the part and
  its values, as `Parts.BuildShared` keys them; a component's member: its `componentKey`);
- otherwise geometric: the body's vertices in the frame, quantized (0.5 mm by default), its materials and finish, hashed
  (the same idea as `IfcOptimize`'s identical bodies, but in the element's frame, so a turned copy is identical);
- **mirrored** parts are not identical; they get their own mark, noted "mirror of P12" (Tekla's and the shops' habit).
- An assembly's signature: its main part's mark and the set of (part mark, placement relative to the main part).

### 8.2 The mark register

Grouping is derived on every ask (the standing principle), but a shop needs **marks that do not move**: once P12 is on
a sheet it must stay P12. So the project keeps a **register** (`marks` on the project: prefix per kind, signature →
mark, frozen after an issue), as GlobalIds are kept:

- a new signature takes the next number of its prefix (P for steel parts, A for steel assemblies, T for timber, B for
  boards…, the user's own per rule);
- a signature that changes keeps its mark when it was the only element of it (a part edited stays P12), else the edited
  ones get a new mark (Tekla's "modified numbering");
- a mark with no element left stays in the register as free until renumbered on request (`marks.renumber`, one step).
- `{mark}` is a field for tags, schedules and filters; the existing steel members marks and wood panel marks become the
  register's marks of those kinds (the same numbers, an `IProjectUpgrade` when needed).

### 8.3 Live drawings and the stale flags

Drawings are views: they are drawn from the model as it is, so they are never out of date. What a shop needs to know is
different, and the Drawing list flags it:

- **changed since issued**: the subject's signature (or the rule) differs from the one kept when the sheet was last
  issued (CDE issuing, `docs/plan-cde.md`); revision clouds round the views that changed (a later stage);
- **quantity changed**: the mark's count differs from the issued one;
- **subject gone**: the mark has no element left (the sheet is kept, marked, until removed);
- **does not fit**: the views no longer fit the paper at the rule's scale (the part grew);
- **mark split**: an element of the mark was edited and took a new mark.

## 9. Features (what dimensions and sections hold on to)

`IElementFeatures` (a platform service declared by modules): holes (centre, diameter or slot), hole groups, notches,
cut ends with their planes, openings, studs and their positions, cut-outs, bends; each with a stable key (from the
element's attributes, as face roles are).

- **steel**: `SteelEnd` cuts and notches, `MemberHoles`, the node's bolts through plates and members;
- **wood**: `Framing` members in the wall's elevation, CLT router cutters, opening rough sizes;
- **studio**: the part's own sketches and steps (holes from a cut extrusion of a circle);
- **furniture**: boards' holes for hinges and runners when modelled; else none;
- **fallback**: from the body in the frame: circular loops through the body (holes), steps of the outline (notches).

## 10. Built-in rules, by kind

| Rule | Views | Dimensions | BOM |
|---|---|---|---|
| Steel member | front, top, end sections, sections at notches, iso | overall, ordinate of holes from the start, end cuts | — |
| Steel plate | face view, edge view | overall, ordinate of holes, chamfers | — |
| Steel assembly (node, haunched rafter) | front, top, end view, iso; parts' drawings | parts from the main part's ends, hole groups | parts, bolts, welds |
| Timber member | front, top, end section | overall, cuts, notches | — |
| Prefabricated timber panel, timber-frame wall | the frame elevation (existing), section across | the frame's chains (existing) | members, boards |
| CLT panel | face view (existing panel elevation), section | overall, openings, cutters | — |
| Precast concrete (wall, slab, beam, column, flight) | front, top, sections, iso | overall, openings, recesses | the rebar schedule of its bars |
| Casework piece | front, plan, side section | overall, carcasses | the cut list (existing) |
| Board | face view, edge | overall, holes | — |
| Door, window type | elevation inside and outside, plan, section (`JoinerySymbols`) | overall, cells | — |
| Studio part | front, top, right, iso | overall, its driving dimensions shown | — |
| Generic (anything) | front, top, right, iso | overall | — |

## 11. Open and premium (decided 14.11.2026, question 1)

The open repo holds hooks only for premium; open modules never depend on it.

- **Open** (core and the modules' own rules): the element frame and features services, element views, the kept
  subject in `VisibilityGraphics`, drawing rules and the dialog, single part, assembly and batch drawings, marks and the
  register, the BOM, the Drawing list with its flags, drawn layouts, the built-in rules of the open modules. This is
  what Victor1 asked for and what Revit lacks; it fits Lapin's helpware stance.
- **Premium** (`steel-pro`, and later `wood-pro`/`rebar-pro`, private repos, by hooks): DSTV/NC and BTLx files
  from the same features; workshop numbering conventions (series per phase, locked numbering across phases, mark
  comparison options by finish and camber), weld symbols after ISO 2553 on the drawings, EC3/EC5 checks.
- This changed `docs/plan-steel.md`'s boundary (shop drawings and numbering were premium there); updated there too.

## 12. IFC and exports

- Drawings are sheets: PDF and DXF as today; sheets are still not written to IFC.
- Marks go into IFC in Lapin's own set `Lapin_Fabrication` (`Mark`, `AssemblyMark`, `MarkPrefix`; never a `Pset_`
  name), on each element at export; an IFC file's own marks (an assembly's `Tag` or a vendor set) are read as the
  register's marks of those references when they are consistent, never written back (an existing IFC file is never
  written to).
- Optionally, each element gets an `IfcDocumentReference` to its drawing's sheet number (`IfcRelAssociatesDocument`),
  so a viewer finds the drawing from the part (question 9).
- IFC references and recognised elements get drawings like any other (geometry and the fallback frame); nothing of
  Lapin's own is added to them.

## 13. The rules this keeps

- **No parallel pipeline**: element views are views, drawn by `Views.Render`, one `VisibilityGraphics` resolver; the
  sheet is `Sheets.Compose`; dimensions are `IViewAnnotations` and drafting dimensions.
- **GlobalIds stable**: a drawing's sheet and views are keyed by (rule, subject or mark, role); making them again keeps
  their ids (as the hall's roles do), and the user's own additions on them stay.
- **One undo step** per command: `drawing.create`, `drawing.update`, `drawing.remove`, `marks.renumber`,
  `marks.setFrame`, `drawingRule.create/update/remove`. Which a shared session's guest may run is listed in
  `EngineDispatcher.AllowedRemote` (proposed: create and update; renumber not).
- **Derived, not stored**: frames, features, signatures, grouping, quantities, the BOM, the flags; stored only the
  rules, the register's names and the views' and sheets' own edits.
- **Curves**: sections and hidden lines use the parts themselves (`Chords`), never the screen's copies.
- **Extensibility**: rules as data and drawn layouts beside the coded built-ins (`docs/plan-user-extensibility.md`).
- The commands go into the command catalogue (`scripts/command-catalogue.mjs`), so Python, MCP and visual graphs get
  them; new strings in every language of `ui/src/i18n`; each stage its guide page (`docs/user`, `docs/user/ro`) and
  its scene.

## 14. Stages

Each stage: engine tests first, its guide page in both languages and its scene, merged on its own.

- **S0 — The frame and element views.** *Done 14.11.2026* (decision 580; frames for steel members, beams and columns, Studio parts, path elements, columns and footings, `IfcPlate` and the body fallback; timber members inside a framed wall and casework boards are not elements of their own yet and take theirs in S7). `IElementFrame` with the fallback from the body; frames for steel members,
  plates, timber members, walls, floors, Studio parts, casework; `of` on views (orthographic roles and sections at a
  station, read from the frame on every ask); `subject` and `context` in `VisibilityGraphics`; the existing four
  element-anchored views registered as roles. *Tests*: a member turned 37° and tilted gives the same front drawing as
  one along X (same pens after the frame's map); a moved element's view follows; the fallback frame of a boxed proxy is
  its box's; subject hides everything else in plan, section, sheet, PDF and DXF; old projects draw the same.
- **S1 — A single part on a sheet.** *Done 14.11.2026* (decision 581; the dialog's *Turn frame*, *at features*, *Save as rule* and the scope tab come with S2, S3, S6 and S5). `LapinDrawingRule` with the generic built-in; `drawing.create` for one element
  (sheet, views, automatic layout in ISO E and third angle, paper choice, title block tokens); overall dimensions; the
  dialog with the face checkboxes, the section sliders and the engine's sheet preview. *Tests*: the generic rule on a
  box makes front, top, right and iso in first-angle positions on A4; made again keeps every id; one undo step removes
  it all; the layout picks A3 when A4 does not fit and the scale series when asked.
- **S2 — Features, part dimensions, sections at features.** *Done 14.11.2026 as a correct slice* (decision 582): steel members' holes, copes and cut ends, holes from a body (plates, Studio parts), runs "n×p = total", ordinates from the start, rows across, hole tags, notch length and depth, cut angles, sections through notches, moved strings kept. *Left*: details (insets) at features, section markers on the front view, wood and Studio features from their sketches, plates' chamfers, mitred and fitted ends' angles, gauge strings of flange holes, dragging a derived string on screen. `IElementFeatures` for steel (cuts, notches, holes, bolts),
  wood, Studio, the fallback; `PartDimensionRules` (overall, chain, ordinate, hole groups, end angles); sections and
  details (insets) at features. *Tests*: a member with two hole groups gets "4×80 = 320" and ordinates from its start
  equal to the holes' stations; a notch gets its length and depth and a section through it; dimensions follow a hole
  moved; a moved string keeps its place.
- **S3 — Marks.** *Done 14.11.2026* (decision 583; marks are unique in the project, so where the steel schedule gave two different parts one mark, one per hall, or a mark to mirror images, the second takes a new number on the first numbering; numbering runs on demand and with each drawing, not on every edit; the register is on the IfcProject, never exported). Signatures (module ones and geometric), the register (prefixes, next numbers, modified numbering,
  frozen after issue), `{mark}` and `{qty}` fields, `marks.renumber`, `marks.setFrame`; the steel members marks and the
  wood panel marks brought into it with the same numbers. *Tests*: 20 copies turned and moved give one mark, qty 20; a
  mirrored copy a mark of its own; editing one of the 20 gives it a new mark and keeps the others; editing the only one
  keeps its mark; quantization at 0.5 mm; old projects' steel marks unchanged.
- **S4 — Assemblies.** Assembly detection (IfcElementAssembly, components, casework, timber walls, Studio parts, door
  assemblies; groups on request), the main part, balloons, the BOM schedule on the sheet, *and its parts*. *Tests*: a
  base node's drawing lists its plate, anchors and welds with weights summing to the node's; identical nodes share an
  assembly mark; a part shared by two assemblies gets one part drawing.
- **S5 — Batch and the Drawing list.** Scopes, one sheet per mark, the Drawing list set, the flags (changed since
  issued, quantity changed, subject gone, does not fit, mark split), *Update* and *Remove*. *Tests*: a hall of 6 frames
  makes one drawing per mark with the right quantities in one step; issuing then editing one rafter flags its sheet
  only; deleting the last element of a mark flags "subject gone"; the batch run twice changes nothing.
- **S6 — The user's rules and drawn layouts.** The rules editor, rules in `.lapinstandards` and the library, drawn
  layouts with slots, formulas in scales and titles, *Save as rule* from the dialog. *Tests*: a user's rule overrides
  the built-in for its kind only; a drawn layout's slots get their views at the best fitting scale; a rule carried to
  another project by `.lapinstandards` makes the same sheet.
- **S7 — The built-in rules of every kind** (§10): steel member, plate and assembly, timber member and panels, CLT,
  precast, casework and boards, doors and windows, Studio parts, stairs; IFC `Lapin_Fabrication` and the optional
  document references. *Tests*: per kind one sample drawn and compared with its expected views, dimensions and BOM;
  IFC export → import keeps the marks; an IFC reference gets a drawing and is not written to.
- **S8 — Premium hooks** (if the user so decides): the hooks for `steel-pro` (DSTV/NC from the same features, workshop
  numbering series, weld symbols), tested in the private repo; the open side tests that a missing premium module
  changes nothing.

Order of value: S0–S3 give Victor1 a steel part's drawing with its marks; S4–S5 give assemblies and "all parts in the
project"; S6–S7 make it general for every module.

## 15. Risks

- **Automatic dimensions crowd**: on a dense part they overlap. Mitigation: rules order strings, the collision pass of
  `ExteriorDimensions`' offsets, and moved strings kept by key.
- **The fallback frame** of an odd IFC body may pick the wrong front: the user turns it once per mark (kept on the
  register).
- **Large batches** (thousands of parts): drawing every sheet in one step is heavy. The command makes the entities only;
  the drawings are drawn lazily when shown or printed (as every view today); measured on a 1,000-part hall in S5.
- **Signature churn**: tiny model changes must not renumber the shop; quantization and the frozen register guard it.

## 16. Questions for the user (answered 14.11.2026: "toate recomandate")

The user accepted every recommended answer below. In short: (1) the general system is open for every module; premium
keeps DSTV/NC, BTLx, workshop numbering series by phase, ISO 2553 welds, EC3/EC5 checks; (2) ISO E by default, from the
project's drawing standard, third angle per rule; (3) the marks register, frozen after issue; (4) identity at 0.5 mm +
material + finish, a mirrored part has its own mark; (5) live drawings, the Drawing list flags, issued PDFs in the CDE;
(6) sheet number = mark, name "{kind} {mark}", a "Shop drawings" set; (7) assemblies = IfcElementAssembly, component
instances, furniture, framed walls, multi-body Studio parts, groups on request; (8) the part's frame turned per mark, in
the register; (9) IFC: `Lapin_Fabrication` marks, `IfcDocumentReference` only if ticked; (10) start with steel bars and
plates plus the generic rule.


1. **Ce e liber și ce e premium?** `docs/plan-steel.md` punea desenele de execuție pe piesă și pe ansamblu și numerotarea
   pentru atelier în `steel-pro`. *Recomandat*: sistemul general (cadrul elementului, vederile elementului, regulile,
   dialogul, piesa, ansamblul, toate piesele deodată, mărcile, BOM, lista desenelor) **liber**, pentru toate modulele;
   în premium rămân DSTV/NC și BTLx, convențiile de numerotare pentru atelier (serii pe faze, numerotare blocată între
   faze), simbolurile de sudură ISO 2553 și verificările EC3/EC5.
2. **Metoda de proiecție implicită?** *Recomandat*: ISO E (metoda europeană, vederea de sus sub cea din față), luată din
   standardul de desen al proiectului, cu metoda americană la alegere pe regulă.
3. **Mărcile se păstrează sau se refac de fiecare dată?** *Recomandat*: gruparea pieselor identice se calculează mereu,
   dar numele mărcilor se țin într-un registru al proiectului, ca GlobalId-urile, și se îngheață după emitere; o piesă
   editată ia o marcă nouă doar dacă mai are identice.
4. **Când sunt două piese identice?** *Recomandat*: aceeași formă în cadrul propriu la 0,5 mm, același material și
   aceeași finisare; piesele în oglindă primesc marcă proprie, notată „oglinda lui P12”.
5. **Desenele „vii” sau înghețate?** *Recomandat*: vii (sunt vederi, se desenează din model mereu); lista desenelor
   semnalează ce s-a schimbat față de ultima emitere, iar PDF-urile emise rămân în CDE ca înregistrare.
6. **Cum se numesc planșele?** *Recomandat*: numărul planșei e marca (`{mark}`), numele „{fel} {mark}”, toate într-un set
   „Desene de execuție” al browserului, pe feluri; regula le poate schimba.
7. **Ce e un ansamblu?** *Recomandat*: `IfcElementAssembly` (noduri metalice, rigle cu vute, panouri prefabricate, din
   IFC), instanțele de componente, piesele de mobilier, pereții pe cadre, piesele Studio cu mai multe corpuri; grupurile
   doar la cerere.
8. **Cadrul unei piese se poate întoarce?** *Recomandat*: da, pe marcă (ce latură e fața, ce e sus), ținut în registru,
   niciodată pe element (o referință IFC nu se scrie).
9. **În IFC?** *Recomandat*: marca în setul propriu `Lapin_Fabrication` (Mark, AssemblyMark) la export, iar legătura
   element → planșă ca `IfcDocumentReference` doar dacă e bifată la export.
10. **Cu ce fel începem?** *Recomandat*: S0–S3 cu barele și plăcile metalice (cererea lui Victor1) și regula generică
    pentru orice element, apoi ansamblurile metalice, apoi lemnul, prefabricatele și mobilierul.
