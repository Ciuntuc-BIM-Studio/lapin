# Visibility / Graphics as in Revit

Asked by a user and approved by the user (the developer of Lapin) on 07.11.2026: "Please expand the support for Visibility/Graphics like in Revit: with tabs for Annotative Objects, File Links, Imports, even Point Cloud (if possible). We need to be able to turn off Categories from links, like architects don't want to see rebars, engineers don't want to see wall and floor finishings, etc. The V/G is the most powerful tool in any BIM software."

Decisions 391– in `docs/decisions-pending.md`, one per stage. Stage A done (decision 391), stage B done (decision 393), stage C done (decision 394); stage D (point clouds) left for the user to decide.

## What there is today

- **The dialog** (View → Visibility VV, Graphics VG; `ui/src/modules/core/VisibilityDialog.tsx`) works on the view in front, 2D or 3D, with two tabs: *Model categories* (shown, line colour, line weight on the ISO series, fill, cut hatch) and *IFC classes* (shown). A view keeps its own graphics (`graphics` on the view, the 3D's in the project's `view3d`) or takes a named **graphics set** of the project's (`graphicsSets`), shared by every view that takes it (`GraphicsSets.cs`: `view.graphics`, `view.saveGraphicsSet`, `view.useGraphicsSet`, `view.removeGraphicsSet`, the queries `view.visibility` and `view.hidden3d`). Per element the same bucket holds line overrides (`elements`).
- **Categories** are the drawing's (`LineStyles.Categories`: walls, slabs, roofs, columns, beams, doors, windows, stairs, railings, spaces, furniture, topography, other, linework, voids, entourage), reached from an IFC class by `LineStyles.CategoryOf`. Object styles (Manage → Object styles) give each category its pens per role (cut, separation, projection, hidden, overhead, beyond); a view's category row overrides them. Rebar, formwork, scaffolds, room finishes, MEP and steel connections have no category: they can only be hidden by IFC class.
- **View styles** (`docs/view-styles.md`, decisions 137–139, 162) are Lapin's view templates: the layers drawn (all, without finishes, core only), joins, overhead, IFC 2D, voids, hatches, entourage. A view takes its style and overrides fields. The graphics sets are not tied to styles yet (view-styles.md, Rămase).
- **Layers in 3D**: every part of a layered element carries `hiddenBy` (`ViewStyles.HiddenBy`), the interface hides the parts by the view's layers mode (`viewport/layers3d.ts`). The 3D's category and class visibility comes from `view.hidden3d` as looks (`platform/looks.ts`: hidden, colour, opacity, and looks per part key).
- **Links** (`docs/links.md`, decision 132): a linked IFC or DXF is imported into the model and every entity carries `linkId`; Manage → Links shows/hides, halftones, toggles 3D (DXF), reloads, converts, opens separately, unlinks. These are project-wide: no view can hide a link alone, nor a category inside a link. A view's categories apply to linked elements as to the host's (Revit's "By host view").
- **Imported IFC** comes as groups (decision 204) of native elements or references (`StoredBodies`, shared bodies #166), with their IFC classes. **Fragments** and meshes the same.
- **DXF imports** (`docs/dxf-import.md`): native drafting on a view, on the project's drafting layers (`draftLayers`: shown, locked, colour, weight, line type), each import recorded in `dxfImports` and each shape carrying `dxfImport`; a view hides layers of its own (`hiddenLayers`); 3D underlays by import (`in3d`). PDFs and images traced over a view are `LapinViewImage` (decision 371).
- **Annotations** are marks the modules add to a view (`IViewAnnotations`, `ViewMark` with its owner): dimensions, texts, tags, grids, levels, section and elevation marks, room stamps, legends, reference planes, view boundaries. None can be hidden per view by kind; only the drafting layers and the elements they belong to.
- **Phases** (`IViewElementFilter`) and the 4D state at a date (`IViewPartFilter`) already hide or tint elements and parts per view, in the engine's drawing.
- Everything a 2D view shows is drawn by the engine (`Views.Gather`, `Views.Render`), so sheets, PDF and DXF follow; the 3D view follows `view.hidden3d`; the mesh exports follow the 3D view's graphics (`MeshScene`).

## The design

One resolver in the platform, `VisibilityGraphics` (`Lapin.Platform/VisibilityGraphics.cs`), built once per view and drawing, answers every question the drawing, the marks, the 3D and the exports ask: is this element hidden, is this part hidden, is this mark hidden, how is it drawn (colour, halftone, transparency, pens, patterns). Everything stays in the view's (or its set's) `graphics`, which only grows new keys, so an old project opens unchanged and needs no upgrade:

```json
{
  "categories": { "walls": { "hidden": false, "lineColor": "#…", "lineWeight": "0.5", "fillColor": "#…", "cutHatch": "…",
                             "halftone": true, "transparency": 50, "lines": { "cut": { "pen": 7, "pattern": "dashed" } },
                             "sub": { "finishes": { "hidden": true }, "insulation": { "halftone": true } } } },
  "classes": { "IfcWall": { "hidden": true } },
  "elements": { "<id>": { "cut": { "pen": 5 } } },
  "annotations": { "dimensions": { "hidden": true }, "tags": { "color": "#…", "halftone": true } },
  "imports": { "<dxfImport id>": { "hidden": false, "halftone": true, "layers": { "A-WALL": { "hidden": true } } },
               "<image id>": { "hidden": true } },
  "filters": [ { "id": "<filter id>", "hidden": false, "color": "#ff0000", "halftone": false, "transparency": 0, "lines": { … } } ],
  "links": { "<link id>": { "display": "host" | "custom", "hidden": false, "halftone": false,
                            "categories": { … }, "classes": { … }, "filters": [ … ] } },
  "detailLevel": "coarse" | "medium" | "fine"
}
```

- **The dialog** keeps its head (the view's own graphics or a set, Save as a set, hidden lines) and gets Revit's tabs: *Model categories*, *Annotation categories*, *Imported categories*, *Filters*, *Links*, and *IFC classes* (Lapin's own: what IFC calls the thing). Each tab is a table; the model categories unfold into their subcategories.
- **Templates**: a view style names a graphics set (`graphicsSet` on the style). A view without graphics of its own and without a set takes its style's set; so a Structural style can carry "no finishes, no furniture, rebar shown" and every structural view follows. The set stays editable in the dialog as now.

### Model categories and subcategories

The drawing's categories, plus those the modules lacked: **rebar** (`LapinRebar`, `LapinRebarRun`, `IfcReinforcingBar`, `IfcReinforcingMesh`), **coverings** (room finishes, skirtings, ceilings, `IfcCovering`), **formwork**, **scaffolds**, **mep** (pipes, ducts, cables, fittings, terminals, equipment, the exhaust fans), **steel connections** (`LapinSteelNode`, `IfcMechanicalFastener`), **transport** (elevators) and **shading** (sunshades). `IfcCovering` leaves *other* for *coverings*; a project that styled *other* keeps that style on its other elements.

Subcategories split an element by its parts, the way the view styles' layers already do:

| Subcategory | Parts |
|---|---|
| core | the layers marked core |
| finishes | layers in a finish material (`Materials.IsFinish`) |
| insulation | layers in an Insulation material |
| membranes | layers in a Membrane material |
| other layers | the remaining layers |
| reveals | door and window reveals (`reveal:*`) |

They exist for every category whose elements can have layers (walls, slabs, roofs, coverings, beams, columns, footings in *other*). Columns: Shown, Halftone (stage C), Projection/cut lines (colour, weight, pattern; stage C), Fill, Cut hatch, Transparency (3D, stage C), and *Detail level* for the whole view (stage C: coarse, medium, fine; by the scale when unset, as `PlanDetail` does now).

### Annotation categories

From a mark's owner: **dimensions** (IfcAnnotation DIMENSION), **texts** (TEXT), **tags** (TAG: door and window tags, build-up tags), **room tags** (a room's stamp; its fill stays with the *spaces* category), **grids**, **levels** (level heads in sections and elevations), **section marks**, **elevation marks**, **callouts** (insets), **reference planes**, **view boundaries**, **legends** (colour scheme legends). Shown in stage B; colour and halftone in stage C. Drafted lines stay in the model category *linework* (shown or hidden in 2D now too, as in 3D) and on the drafting layers.

### Imported categories

Per DXF import (and per linked DXF), by its file's name: shown, halftone, and each of its layers shown; per PDF or image traced over the view (`LapinViewImage`): shown, halftone. The project's drafting layers still turn a layer off everywhere (Draft → Layers); the view's `hiddenLayers` stay as they are.

### Filters

Rule-based, the project's (`viewFilters`), each a name, *all* or *any* of its rules, and rules `{field, op, value}`:

- fields: *IFC class*, *category*, *type* (the type's name), *name*, *material* (any of the element's materials), *storey*, *group*, *link* (its name; *none* for the host's), *phase created*, *predefined type*, a **parameter** (the user's or a property set's, `params`), or any stored attribute by name;
- operators: equals, does not equal, contains, does not contain, begins with, greater, less, has a value, has no value; numbers compared as numbers.

A view lists the filters it uses in order (`graphics.filters`); for each the same columns as a category (shown, colour, halftone, transparency, lines). An element a filter hides is hidden; where several filters match, the first in the list gives each field (Revit's rule). Manage → View filters edits them, the Filters tab adds, removes and orders them for the view.

### Links

The Links tab lists every link (IFC, Fragments imported as links, DXF) with:

- **Display**: *By host view* (the default: the view's categories, filters and classes reach the link's elements, as today) or *Custom*: the link has its own categories (with subcategories), classes and filters for this view, and the host's do not reach it. "Architect hides rebar in the structural link": the plan's Links tab → the structural IFC → Custom → Model categories → rebar off. "Engineer hides finishes": the architectural link → Custom → walls ▸ finishes off, coverings off.
- **Shown** and **Halftone** for this view alone, over the link's own (Manage → Links still hides a link everywhere).
- *By linked view* does not apply: a linked IFC or DXF has no views of its own. A linked Lapin project would bring them, and Lapin does not link projects (later, if ever).
- *Underlay* (a link drawn under the host's elements and not picked) and *display as references* are later.

### Where it is drawn

`Views.Gather` asks the resolver for each element and part; `Views.Render` drops the marks it hides and gives the overrides to the pens (`ElementColors`, the per-element line specs), so sheets, the PDF and DXF print what the view shows. The 3D asks `view.looks3d` for the view's looks (hidden, colour, opacity, and per part), which replace `view.hidden3d` in the interface; the mesh exports ask the same resolver.

### Analytical

Lapin has no analytical model of its own (the JG export writes one from the physical elements), so there is no Analytical tab. If an analytical model comes, it gets its categories in the same resolver.

## Stages

- **A. The dialog with tabs, model categories with subcategories, links per view.** The resolver; the new categories; subcategories by parts in plans, sections, elevations, sheets and 3D (looks per part) and the mesh exports; the Links tab (display by host / custom with the link's own categories, subcategories and classes, shown and halftone per view); a view style's graphics set. `view.graphics` takes `sub`, `link`; `view.visibility` returns subcategories and links; `view.looks3d`.
- **B. Annotation categories, imported categories, filters.** The mark filter by owner; DXF imports by file and layer, traced PDFs and images; the project's filters with their rules, Manage → View filters, the Filters tab; visibility only.
- **C. Graphic overrides.** Halftone, colour, transparency, cut and projection lines (pen, pattern) for categories, subcategories, filters, links and annotation categories, in drawings and in 3D; the view's detail level.
- **D. Point clouds** (below).

What stage C left: a subcategory's own graphics (colour, pattern; a subcategory is shown or hidden only), a filter's or a category's line weight as a pen for every role, overrides inside a custom link (it hides, shows and halftones), a traced image's halftone, the 3D underlays of a DXF following a 3D view's imported categories, a link drawn as an underlay.

Each stage: engine tests (`VisibilityGraphicsTests`), interface tests, the user guide (views.md, import-export.md, in the 8 translations), the dialog's scene, the strings in every language, an Ask Lapin entry, a decision row.

## D. Point clouds: what it would take

A point cloud is a link like any other, drawn in 3D and in plans and sections, with its own row on the Links tab (and later its classifications as subcategories).

- **Formats.** LAS 1.2–1.4 (point formats 0–10) and PLY (ASCII and binary) are simple and documented; a reader of Lapin's own is a few hundred lines with no dependency. LAZ needs a decoder: LASzip is LGPL 2.1 (not allowed for production), laz-perf (Apache 2.0, C++, also built for WebAssembly) or laz-rs (MIT/Apache 2.0, Rust) would be bundled as a native library like manifold: a few days, plus a build per platform and the browser. E57 is XML with paged binary sections and bit-packed compressed vectors; libE57Format is BSL-1.0 (permissive, C++); a reader of Lapin's own is about a week.
- **Size.** Scans have 10⁷–10⁹ points: they cannot be one entity in the model nor go through the session's diffs. A cloud stays in its own file beside the project (or in the `.lapin` as a part of its own), indexed once into an octree of tiles (Potree's scheme, BSD-2; written by Lapin, no dependency), each tile a few tens of thousands of points.
- **Display.** The 3D asks for the tiles in view by distance (a budget of, say, 3–5 million points), drawn as `THREE.Points`; the browser build the same, from the worker. Plans and sections draw the points inside the view's range as dots (decimated to the paper's resolution) on a pen of their own, so sheets print them.
- **Estimate.** LAS and PLY with the octree, the 3D display, the Links row and halftone, cut in plans: 2–3 weeks. LAZ: + 3–5 days (a native library per platform and a WebAssembly one). E57: + 1 week. Snapping to points and classification filters: + 1 week.

Point clouds are not part of this round: they need a tile store outside the model, a streaming path past the session's diffs, and for LAZ a native dependency built on every platform. They stay a later stage, for the user to decide.
